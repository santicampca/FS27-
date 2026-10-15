# FS27 — 117 oportunidades de mejora de la IA (Fase 31)

Estados: **EX** ya existe (lo verifiqué leyendo o buscando el código en esta sesión) · **IM** implementada y validada en esta campaña · **IP** presente o implementada con validación parcial · **PE** pendiente · **DE** descartada con evidencia · **BL** bloqueada (ninguna).

Prioridad: A alta, M media, B baja, - nada que hacer. «Dep.» = ítems de los que depende o que dependen de él. Los números de ítem 1-100 siguen la lista del encargo; 101-117 son hallazgos propios.

| # | Frente | Estado | Evidencia / acción | Prio | Dep. |
|---|---|---|---|---|---|
| 1 | Percepción contextual | EX | `WorldView`/`PlayerObservation` (`view.Observe`): balón, compañeros y rivales, leídos solo del mundo. | - | - |
| 2 | Distancias y tiempos de llegada | EX+IM | Intercepción (`TryIntercept`), `PassRisk.RivalMargin`, `GoalkeeperExit`; nuevo: apuro del portero por tiempo de llegada del rival (`GoalkeeperDistribution.Hurried`). | A | - |
| 3 | Predicción de trayectorias | IP | Existe `Ball/BallPredictor` pero las decisiones no lo usan (la intercepción muestrea linealmente). Sin defecto medido que lo justifique. | B | 3 |
| 4 | Líneas de pase | EX | `IsPassLaneClear`, `PassRisk.LaneMargin`. | - | - |
| 5 | Espacios libres | EX | `FindBestOpenPoint`, `DribbleSpace`. | - | - |
| 6 | Bloqueadores de trayectoria | EX+IM | Carril libre en candidatos; la ayuda de puntería humana penaliza receptores con rival en el carril. | M | 85 |
| 7 | Presión rival | EX | Presión del portador en `CarrierSummary.Pressure`, `PressureRadius`. | - | - |
| 8 | Peligro de perder el balón | IP | `PassRisk` existe pero está desactivado por defecto (+75 % de goles medido en Fase 28). Variante solo en zona propia: probada y descartada (sin mejora). | M | 103 |
| 9 | Coberturas defensivas | EX | Defensor secundario (`DefenseRoles`). | - | - |
| 10 | Información limitada y realista | IP | Portero con visibilidad limitada; jugadores de campo ven todo el `WorldView`. Pendiente decidir si compensa limitarla. | B | - |
| 11 | Evaluación contextual de pases | EX | `ReceiverScore` + candidatos del portador (Fase 11). | - | - |
| 12 | Pases cortos y largos | EX | `PassPhysics` (potencia según llegada), umbral `LongPassDistance`. | - | - |
| 13 | Pases filtrados | EX | `ThroughPass` hacia el hueco del corredor. | - | - |
| 14 | Disparos contextuales | IP | Candidato `Shoot` con rango; `ShotQualityEvaluator` (tipo xG) solo se usa en diagnósticos, no decide. | A | 14 |
| 15 | Disparar/pasar/conducir | EX | Una sola escala de puntuación (`BallCarrierEvaluator`). | - | - |
| 16 | Regate | EX | `DribbleSpace` y movimientos de habilidad. | - | - |
| 17 | Protección del balón | EX | Candidato `Protect`/`Hold`. | - | - |
| 18 | Progresar vs conservar | EX+IP | Sesgo de táctica de equipo (riesgo/verticalidad) sobre las opciones del portador; efecto medido en el reparto de pases hacia delante: débil (r 0,33→0,35). | M | 110 |
| 19 | Riesgo según la zona | IP | `PassRiskOwnZone` existe (desactivado); ver 8. | M | 8 |
| 20 | Decisiones repetitivas | IP | Entropía del portador medida en tests de simulación; no hay guarda automática en campañas. | M | 96 |
| 21 | Desmarques de apoyo | EX | `AttackJob.Support`. | - | - |
| 22 | Rupturas | EX | `AttackJob.Forward`. | - | - |
| 23 | Movimientos diagonales | PE | Emergen de `FindBestOpenPoint`; no medidos. | B | 91 |
| 24 | Ocupación de carriles | IP | `Width` y ahora el ancho del plan táctico (r 0,34→0,54 en Classic; 0,47→0,78 en Arena). | M | - |
| 25 | Amplitud por bandas | EX+IM | `AttackJob.Width` + `WidthSpan` del plan. | - | - |
| 26 | Llegadas desde segunda línea | PE | No verificado que los defensas/pivotes lleguen; solape de Fase 28 muy limitado por stamina. | M | - |
| 27 | Apoyo al poseedor | EX | `AttackJob.Support`. | - | - |
| 28 | Liberar a otros compañeros | PE | Sin señuelos ni movimientos de arrastre. | B | 31 |
| 29 | Evitar aglomeraciones | EX | Separación física + límite de apoyos (`SupportCount`). | - | - |
| 30 | Recuperación de posición | EX | `TransitionReturn`, `FarFromHome`. | - | - |
| 31 | Coordinación de combinaciones | IP | No hay jugadas pactadas, pero A→B→A (cada toque en menos de 3,5 s) ocurre 60 veces por equipo y partido (36 partidos Classic): la ida y vuelta ya emerge. | M | 32 |
| 32 | Paredes | IP | `WallPass` existe como acción humana; la IA no la usa a propósito. La métrica A→B→A no distingue una pared con carrera de un simple intercambio: hace falta una métrica mejor antes de diseñar nada. | M | 31 |
| 33 | Triángulos de pase | PE | Sin estructura explícita. | M | 31 |
| 34 | Cambios de orientación | PE | Sin candidato de cambio de banda. | M | - |
| 35 | Sobrecargas en zonas útiles | IP | Solo un solapador (Fase 28). | M | 26 |
| 36 | Superioridades locales | PE | Sin medida ni objetivo. | B | - |
| 37 | Ataques por dentro y por fuera | IP | Ancho/verticalidad por plan; sin medida de reparto. | M | 110 |
| 38 | Sincronía pase–desmarque | IP | El receptor previsto corre al balón (`IntendedReceiver`); pase al hueco existe; sin métrica. | M | - |
| 39 | Transiciones rápidas | IP | Estilo de transición (contraataque/posesión) sesga hacia delante 4 s tras ganar el balón; efecto aislado sin medir. | M | 110 |
| 40 | Velocidad de ataque | IM | Dial de tempo: más lento ⇒ más pases (r −0,18→−0,57 Classic, −0,19→−0,52 Arena). | - | - |
| 41 | Presión coordinada | EX+IM | Primario/secundario + escala de presión por estilo. | - | - |
| 42 | Momento de presionar | IM | `ApproachDistance`×escala del plan (bloque bajo 0,7 … presión agresiva 1,5) y urgencia; r 0,16→0,51 en recuperaciones altas. | - | - |
| 43 | Cobertura del compañero que sale | EX | Secundario (`Secondary`). | - | - |
| 44 | Protección del espacio central | PE | No hay ocupación explícita del carril central. | M | - |
| 45 | Defensa de las bandas | PE | Sin reglas específicas. | M | - |
| 46 | Seguimiento contextual | PE | Sin marcaje al hombre. | B | - |
| 47 | Interceptación de líneas | EX | `TryIntercept`. | - | - |
| 48 | Repliegue organizado | EX | `TransitionReturn`. | - | - |
| 49 | Recuperar la forma defensiva | EX+IM | Vuelta a casa + línea defensiva del plan (r 0,39→0,86). | - | - |
| 50 | Evitar persecuciones peligrosas | PE | No medido. | B | - |
| 51 | Estructura ofensiva | IM | `TeamTactic`: ancho, línea, verticalidad, tempo desde la identidad del entrenador. | - | - |
| 52 | Estructura defensiva | IM | Línea y presión del plan. | - | - |
| 53 | Ajuste por zona del balón | EX | Casa atraída hacia el balón (`HomeTarget`). | - | - |
| 54 | Adaptación al marcador | IM | Urgencia del plan mueve línea y presión; la del portador ya existía por dificultad. | - | - |
| 55 | Gestión del tiempo | IM | Ventana de 150 s por periodo. | - | - |
| 56 | Control vs verticalidad | IM | `Directness` y `Tempo`. | - | - |
| 57 | Riesgo en ventaja | IM | Urgencia negativa: línea más baja y presión menor. | - | - |
| 58 | Respuesta si se va perdiendo | IM | Urgencia positiva: línea y presión más altas. | - | - |
| 59 | Adaptación al estilo rival | PE | No hay modelo del rival. | B | - |
| 60 | Cambios tácticos con diferencias observables | IM | 12 equipos con identidades distintas, medido en 36 (Classic) y 24 (Arena) partidos emparejados; ojo: solo 12 equipos distintos. | - | - |
| 61 | Posicionamiento del portero | EX | Fase 16. | - | - |
| 62 | Ángulos de tiro | EX | `GoalkeeperPositioning`. | - | - |
| 63 | Anticipación de disparos | EX | `GoalkeeperReaction`. | - | - |
| 64 | Reacción contextual | EX | Visibilidad y retardo. | - | - |
| 65 | Uno contra uno | EX | Medido: 0 goles en 80 uno-contra-uno (8 y 14 m, 40 semillas). No usa `GoalkeeperRush`. | B | - |
| 66 | Salidas ante balones peligrosos | EX | `GoalkeeperExit`, solo balón suelto a ≤11 m. | M | - |
| 67 | Centros y balones aéreos | PE | Sin lógica específica. | M | - |
| 68 | Blocaje vs despeje | PE | Solo existe la parada. | M | - |
| 69 | Rebotes y segundas jugadas | EX | Espera por `ReboundResponse`. | - | - |
| 70 | Distribución segura y útil | IM | Etapa 1 + despeje real + apuro predictivo: goles en contra S4 17/40→0/40. | - | - |
| 71 | Influencia de atributos en acciones | EX | `ActionProfile` en sesgos y ejecución. | - | - |
| 72 | Inteligencia vs capacidad técnica | EX | `DecisionProfile` (dificultad) separado de `ActionProfile` (atributos). | - | - |
| 73 | Personalidades reconocibles | PE | `PlayingProfile`/`TeamStyleDefinition` siguen sin conectar a individuos. | A | 74 |
| 74 | Preferencias individuales | PE | Sin hábitos por jugador. | A | 73 |
| 75 | Fatiga y deterioro | EX | `EffortCap`, stamina en decisiones. | - | - |
| 76 | Diferencias por rol | EX | Posiciones de casa por rol/zona. | - | - |
| 77 | Dificultad por calidad de decisión | EX | `DecisionProfile`. | - | - |
| 78 | Dificultad por tiempos de reacción | EX | Retardo de percepción e intervalo de decisión. | - | - |
| 79 | Sin ventajas injustas | EX | La IA solo lee `WorldView`; tests prohíben tocar el estado. | - | - |
| 80 | Variedad sin aleatoriedad absurda | EX | `DecisionDice` determinista. | - | - |
| 81 | Apoyo contextual al poseedor humano | PE | Los compañeros lo tratan como a cualquier portador. | A | 82 |
| 82 | Desmarques según su orientación | DE | Probado: las rupturas siguen el rumbo del portador humano (60 %). Con la persona simulada, pase al receptor previsto 18,6 %→20,0 % y pérdidas 68,4 %→66,6 %: dentro del ruido; revertido. | A | 81 |
| 83 | Líneas de pase visibles | PE | Pista visual en el cliente (receptor sugerido). | A | 85 |
| 84 | Respuesta a pases manuales | IM | Con ayuda, el pase fija receptor previsto y el compañero corre al balón. | - | - |
| 85 | Asistencia de puntería configurable | IM | `HumanAssistTuning`; con 40° de error: pase al receptor previsto 4,1 %→18,6 %, pérdidas 85,6 %→63,3 %. | - | - |
| 86 | Cambios de jugador confusos | IP | `AutoSelection`; sin medida con persona. | M | - |
| 87 | Coordinación en transiciones del humano | PE | Sin diseño. | M | - |
| 88 | Respuesta a la pérdida de posesión | EX | `TransitionReturn`. | - | - |
| 89 | Consistencia intención–resultado | IM | Potencia y receptor del pase/tiro asistido. | - | - |
| 90 | Reducir estorbos al usuario | PE | Sin medida. | M | 81 |
| 91 | Escenarios de ataque reproducibles | IP | Informe de conducta (Fase 28) y banco del portero; falta banco de ataque. | A | - |
| 92 | Escenarios de defensa | PE | Sin banco. | A | 91 |
| 93 | Pruebas de decisiones del portero | EX+IM | `ScenarioBench` (6 escenarios) + 1v1 + guarda de regresión. | - | - |
| 94 | Comparaciones con semillas emparejadas | EX+IM | Batería rápida, banco emparejado, campaña táctica emparejada. | - | - |
| 95 | Detección automática de regresiones | EX | `compare_battery.py` (bandas) + guardas. | - | - |
| 96 | Métricas de variedad | PE | Entropía del portador no entra en las campañas. | M | 20 |
| 97 | Calidad de posesión y ocasiones | IP | Columnas de la batería y xG diagnóstico; sin resumen por partido. | M | 14 |
| 98 | Presupuesto de CPU y memoria | IM | Nativo: 0,12 ms por paso (7 ms por segundo de juego), 29 B/paso, 0 recolecciones en 20.000 pasos; navegador: 0,3-0,5 ms por fotograma. | - | - |
| 99 | Explicar por qué decidió la IA | IM | `DecisionExplainer` (registro propio de la decisión; no cambia nada). | - | - |
| 100 | Evaluación integral y priorización | IP | Campañas y este registro; la priorización automática no existe. | M | - |
| 101 | Despeje real del portero | IM | Sin receptor: 20 m de aterrizaje por el canal sin rival (antes ~12 m). | - | 70 |
| 102 | Apuro predictivo del portero | IM | Tiempo de llegada del rival < 1,1 s ⇒ no espera. | - | 70 |
| 103 | Riesgo de pase solo en zona propia | DE | Batería de 6 celdas: pases completados y goles sin cambio medible; revertido. | - | 8 |
| 104 | Ofrecerse a buscar hueco en saques | DE | Informe de conducta, 16 partidos: saque de banda 62→60 % perdido, saque de puerta 64→76 %: descartado y revertido. | - | - |
| 105 | Tácticas y ayuda activas en el cliente | IM | `GameBridge.NewMatch`: planes de ambos equipos y asistencia del humano. | - | - |
| 106 | Determinismo nativo = WebAssembly con la IA nueva | IM | Mismo checksum (`d2f9c2eb0440aa44`, 60.400 pasos). | - | - |
| 107 | `TeamStyleDefinition` sin conectar | IP | El plan lee la identidad (`TeamTacticalIdentity`); `TeamStyleDefinition` sigue sin uso: limpiar o fusionar. | B | - |
| 108 | `PressureModel`/`AnticipationModel` sin uso | PE | Sin referencias fuera de `Ai/`; revisar o eliminar. | B | - |
| 109 | Activar `PassRisk` globalmente | DE | +75 % de goles sin mejorar el pase (Fase 28). | - | 8 |
| 110 | Dial de riesgo con efecto débil | PE | Subir los pesos ×3 mejoró r a 0,44 pero con más faltas (+0,65): requiere diseño, no más peso. | M | 18 |
| 111 | Saques de banda perdidos (62 %) | PE | Sin mejora demostrada; la idea de buscar hueco falló (104). | M | - |
| 112 | Métrica «centros llegan 335 %» | PE | La columna del informe no es un porcentaje válido: corregir. | B | - |
| 113 | Usar xG en el disparo | PE | `ShotQualityEvaluator` existe y solo diagnostica. | A | 14 |
| 114 | Validación de tácticas en Arena | IM | 24 partidos emparejados: agregados sin cambio; identidad→conducta 0,5→0,84 (línea). | - | - |
| 115 | Saques de puerta | IP | Perdido en 4 s: ~85 %→60-64 % (n pequeño, otra medida). | M | - |
| 116 | Potencia del pase humano por deslizamiento | PE | El cliente manda 0,55-1; la ayuda la ignora al fijar receptor. | B | 85 |
| 117 | Mapa de dependencias del flujo de decisión | IM | `Docs/AI_PHASE31.md`. | - | - |

Recuento: DE 3, EX 37, EX+IM 7, EX+IP 1, IM 22, IP 17, PE 30 (total 117).
