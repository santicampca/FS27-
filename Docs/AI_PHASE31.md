# FS27 — IA, campaña de la Fase 31

Qué hace la IA hoy, qué se añadió en esta campaña, con qué evidencia, y qué queda. El registro de oportunidades está en `Docs/AI_OPPORTUNITIES_PHASE31.md`.

## 1. El flujo real (verificado en el código)

```
MatchState ─► WorldState/WorldView ─► BasicDecisionDriver.Step (cada jugador a su ritmo: IsDue, retardo de percepción, retardo de contienda)
                                        └► BasicDecisionEngine.Decide(view, id)
                                             ├ cierra: no vivo / bloqueado / penalti  ⇒ Idle
                                             ├ portero ⇒ Keeper (con balón: reanudación o KeeperPlayBall; sin balón: reacción, salida, posición)
                                             ├ con balón ⇒ CarrierDecision: candidatos puntuados en UNA escala + sesgo (dificultad ⊕ táctica)
                                             └ sin balón ⇒ saque (ofrecerse) · balón suelto (interceptar, receptor, el más cercano) ·
                                                           rival con balón (primario / secundario / resto a su sitio táctico) ·
                                                           compañero con balón (trabajos de ataque: apoyo, ruptura, amplitud, hueco)
                                        ─► PlayerDecision ─► MovementIntent (prioridad: persona > IA) / ActionIntent
                                        ─► MatchController.Perform ─► eventos ─► la siguiente decisión lee el mundo
```
Quién manda: el motor decide; la dificultad (`DecisionProfile`) y el plan táctico (`TeamTactic`) **solo suman sesgos** a las opciones o mueven dónde está el jugador, nunca ordenan una acción distinta ni cambian atributos. Vetos: la generación de candidatos (carril, alcance, pase mínimo), la disciplina (tarjetas) y el estado de la partida. Conflictos de movimiento: `MovementIntent.Priority` (la persona gana a la IA). La ayuda de puntería solo modifica la petición de la propia persona.

Huecos del flujo: no hay realimentación del resultado de una acción hacia la decisión (solo vía posesión/mundo); `TeamStyleDefinition`, `PressureModel` y `AnticipationModel` existen sin conectar; `PassRisk` está desactivado por defecto; `ShotQualityEvaluator` solo diagnostica; ningún sistema de decisión usa `BallPredictor`.

## 2. Qué se añadió

### 2.1 Táctica de equipo (`TeamTactic`, `Core/Decision/TeamTactic.cs`)
Las 12 identidades de equipo ya estaban escritas por entrenador (`TeamTacticalIdentity`: línea defensiva, ancho, presión, riesgo, juego directo, transición, mentalidad) y **no influían en el partido**. Ahora el plan (a) mueve la línea y el ancho de la posición base de cada jugador, (b) escala a qué distancia empieza a cerrar el defensor primario, (c) sesga las opciones del portador (pase adelante/seguro, tiro, regate, aguantar) y (d) se empuja con el marcador y el reloj (`Urgency`, ventana de 150 s). Una identidad neutral con 0-0 no cambia nada: comprobado sobre más de 300 decisiones reales de partidos reales. Números en `TeamTacticDefaults` (con gemelos en `TeamTacticTuning`).
Medido con partidos completos emparejados (Professional contra Professional, mismos pares y semillas, 36 en Classic y 24 en Arena):
- Los totales no se mueven (goles, tiros, posesión): en Classic goles −0,15±0,30, tiros −1,8±2,1 por equipo y partido.
- La identidad se nota: correlación entre el dial y la conducta medida, sin plan → con plan. Línea→profundidad sin balón 0,39→0,86 (Arena 0,50→0,84); ancho→anchura 0,34→0,54 (0,47→0,78); presión→recuperaciones altas 0,16→0,51 (0,37→0,66); tempo→pases −0,18→−0,57 (−0,19→−0,52).
- **No se nota**: el dial de riesgo apenas cambia el reparto de pases hacia delante (0,33→0,35 en Classic, 0,18→0,05 en Arena). Triplicar los pesos lo subió a 0,44 pero con más faltas: no se hizo.
- Aviso estadístico: hay 12 equipos distintos; las correlaciones tienen pocos grados de libertad reales y las partidas son deterministas, no muestras independientes.
Activo en el cliente del navegador; en las baterías y tests antiguos sigue apagado (por defecto no hay plan), para que sus resultados no cambien.

### 2.2 Ayuda de puntería para la persona (`HumanAssist`, `Core/Intents/HumanAssist.cs`)
Apagada en el Core y encendida en el cliente. Un pase empujado «más o menos hacia» un compañero pasa a ser un pase **a él** (receptor mejor alineado dentro de 40°, con espacio, sin rival en el carril), con la potencia que necesita el balón (cercano a ras de suelo, lejano en alto); el pase al hueco apunta delante del corredor; el tiro empujado hacia la portería va al poste del lado señalado (o al contrario del portero si apuntó al centro). Si nadie está en el cono, no cambia nada.
Medido con una persona simulada que apunta con error aleatorio sembrado (12 partidos de 300 s por configuración): con 0° de error, pase al receptor previsto 15,9 %→21,5 %; con 20°, 8,7 %→18,0 %; con 40°, 4,1 %→18,6 %, y los pases perdidos bajan de 85,6 % a 63,3 %. Limitación: es un guion, no una persona; y las pérdidas siguen siendo altas porque el guion pasa en espacios estrechos.

### 2.3 Portero: despeje real y apuro predictivo (`GoalkeeperDistribution`)
El banco de escenarios descubrió un defecto grave: sin receptor, la «patada larga a nadie» era el lanzamiento de menor potencia (aterrizaba a unos 12 m) y, tras esperar hasta que el rival estuviera a menos de 5 m, el rival la interceptaba junto a la portería: 17 goles en contra en 40 repeticiones (1 si jugaba de inmediato). Ahora (a) el despeje aterriza a 20 m en el canal sin rival y (b) el portero no espera si algún rival puede alcanzarlo en menos de 1,1 s. Resultado en el banco (40 semillas emparejadas): goles en contra S4 17→0, S3 3→0; retención con receptor cubierto sin cambio (40/40). La batería de partidos completos queda dentro de bandas (Classic Professional 7,65→6,35 goles por partido en 20 partidos; es una dirección, no una prueba). El número 20 m y 24° se eligieron mirando tres escenarios: otras combinaciones mueven la retención de forma no suave (ver el registro, ítem 101).

### 2.4 Herramientas
`DecisionExplainer` (por qué decidió, sacado del propio registro del motor), `Phase31Campaign` (campaña táctica emparejada, con `TC_TUNE`), `HumanAssistCampaign`, `Perf_Budget_Prints`, `OneVsOneBench`, `SCEN_TUNE` en el banco del portero. Todas son explícitas (no entran en la suite) salvo las guardas, que sí.

## 3. Presupuesto de CPU y memoria (medido)
Nativo (esta máquina): 0,12 ms por paso de simulación con IA de los dos equipos (7 ms por segundo de juego, 0,7 % de un núcleo a 60 Hz), 29 bytes por paso, ninguna recolección de basura de generación 0 en 20.000 pasos; el plan táctico añade ≈6 % al coste del conductor. Navegador (WebAssembly, intérprete): 0,3-0,5 ms por fotograma. No medido: móvil real.

## 4. Experimentos descartados (con evidencia)
- Riesgo de pase solo en la zona propia (`PassRiskOwnZoneOnly`): batería de 6 celdas, pases completados y goles sin cambio medible; revertido.
- Que los compañeros del sacador busquen hueco en lugar de esperar: informe de conducta de 16 partidos, saque de banda 62→60 % perdido, saque de puerta 64→76 %, córner peor; revertido.

## 5. Qué sigue (por impacto)
1. Apoyo al poseedor humano y desmarques según su orientación (ítems 81-82): hoy los compañeros lo tratan como un portador de IA.
2. Personalidades por jugador (73-74): `PlayingProfile` sigue sin conectar.
3. Banco de escenarios de ataque y de defensa (91-92) y métrica real de paredes (31-32).
4. Usar el xG en el tiro (14, 113) y el dial de riesgo (110).
5. Portero: centros y balones aéreos (67), rechazo vs. blocaje (68).
