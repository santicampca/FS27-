# FS27 — Diagnóstico e instrumentación de la IA (Fase 12)

Estado: **instrumentación de solo lectura y primeros datos.** Rama `phase-12-ai-diagnostics`, desde `00186bb` (2258/2258). Esta fase **no cambia gameplay ni balance**: observa, mide, documenta y recomienda.
Los datos completos de las 4 ejecuciones (Arena y Classic, F10 y F11, 8 partidos de 120 s cada una) están en [AI_DIAGNOSTICS_PHASE12_REPORT.txt](AI_DIAGNOSTICS_PHASE12_REPORT.txt).

## 1. Qué se puede medir ahora

| Qué | Dónde |
|---|---|
| Qué candidato eligió el portador, con qué puntuación, quién quedó segundo (de otro tipo) y el margen | `AiDiagnostics` (agregados) y `CarrierSummary` |
| Los términos de cada puntuación (tiro: cercanía, ángulo, línea, portero, presión; pase: avance, espacio, distancia, ángulo, línea, presión del receptor, movimiento, carrera, bono adelante, pase seguro, de espaldas; pase al espacio; regate; proteger; esperar) | `ActionCandidate.Parts`, `CandidatePartNames` |
| Si un tipo de opción no era candidato y **por qué** (fuera de alcance, ángulo, línea bloqueada, sin espacio, no avanza, portero no permitido, no hay corredor, no puede usarla, fuera del campo, sin ganancia, pierde la carrera, sin rival cerca, demasiada presión, fuera de la ventana de espera, sin camino libre) | `CandidateRejection` |
| El destino de cada pase (completado, interceptado, posesión perdida con otro, devuelto al pasador, perdido, fuera, invalidado, no observado) con el tiempo que tardó, la velocidad del pasador y del balón, la distancia, la potencia, la calidad y el rival más cercano | `PassOutcome`, `PassFeature` |
| Cada pase al espacio (pasador, corredor, destino, distancias, velocidades, rival más cercano, skill, resultado) | `SpaceAttempt` |
| El destino de cada tiro (gol, posesión rival, posesión propia, fuera, desconocido) | `ShotOutcome` |
| La posesión un segundo después de empezar un regate, una protección o una espera | `WindowOutcome` |
| Todas las acciones del Core pedidas por la IA: pedidas, aceptadas, rechazadas (y por qué), completadas, falladas, coste de stamina | `ActionRequested…` |
| Cada duelo por el balón y cada falta con todo lo que los rodea (tipo, quién, contra quién, distancia, velocidad relativa, stamina de ambos, si venía por detrás, `aggressive_tackle`, si el portador protegía, qué había decidido el portador, zona, calidad) | `ContactRecord` |
| Contadores por jugador (decisiones, pases, regates, protecciones, esperas, tiros, goles, posesiones, faltas cometidas y sufridas, contactos, entradas, intercepciones, pases al espacio) | `AiDiagnostics.PlayerAt` |
| Una traza opcional de un jugador y una ventana de pasos | `DiagnosticsConfig.TraceEnabled` |

**Cómo se usa:** `var d = new AiDiagnostics(); d.Attach(match); driver.Diagnostics = d;` … al terminar `d.Finish()`; `d.Report()` / `d.PlayersReport()`; varias ejecuciones se suman con `d.Merge(other)`. La ejecución de investigación es el test explícito `Phase12Data_RunTheHarness_AndWriteTheReports` (variable de entorno `DIAG_OUT` = carpeta de salida).

## 2. Garantías

- **Observador puro.** `AiDiagnostics` solo lee la vista, las decisiones, los resultados de acción y los eventos publicados. No llama a nada que cambie el partido (un test comprueba que el código no contiene esas llamadas) y no usa números aleatorios.
- **El partido es idéntico con o sin diagnóstico** (Arena y Classic, 120 s, también con la traza activada y con `Enabled = false`: misma huella de posiciones, balón, stamina, posesión, acciones, goles, faltas y contadores).
- **Las puntuaciones no cambian.** Para exponer los términos se nombraron las variables **sin tocar el orden de las operaciones**: las 32 huellas de partido de la Fase 10 y 11 (semillas 301–308 y 321–328, F10 y F11) son idénticas bit a bit a las de `00186bb`, y un test las fija.
- **Determinista:** misma semilla ⇒ mismo informe, mismo reparto por jugador. Otra semilla ⇒ otro informe.
- **Sin coste en juego normal:** sin observador, el motor solo añade a cada evaluación del portador el cálculo de los términos (ya estaban en el cálculo) y un puntero nulo; el recuento de rechazos está apagado por defecto. Con observador no se asignan objetos en el bucle de simulación (contadores en un solo array, registros en arrays de tamaño fijo, trazas acotadas).
- **Sin log masivo:** dos niveles (agregados siempre; traza opcional de un jugador/ventana, acotada).
- **No hizo falta una reserva de receptor:** el pase al espacio ya lleva al corredor en `PlayerDecision.TargetPlayerId` (informativo), con lo que se asocia el pase con el corredor que lo motivó sin tocar el Core.

### Definiciones (la semántica exacta, para no confundir cifras)
- **«Pase completado»** (nuevo, `PassOutcome.Completed`) = un compañero **distinto del pasador** consigue la posesión antes que un rival. En las fases 10 y 11 la cifra «pases» era **el número de patadas ejecutadas**, no pases completados: ver §4.1.
- **Interceptado** = un rival consigue la posesión **y** el Core publicó una intercepción/bloqueo suyo. **Posesión perdida con otro** = un rival consigue la posesión sin esa intercepción (balón suelto, el portero lo recoge, una entrada tras la recepción).
- **Devuelto al pasador** = el propio pasador vuelve a tener el balón. **No observado** = la simulación terminó antes de poder decirlo.
- **Contacto** = un duelo resuelto por el Core (entrada, entrada deslizante o disputa física) contra el portador. **Falta** = el Core la marcó (`ActionDetail.Foul`, mismo número que los eventos `FoulCommitted`).
- **Margen** = puntuación del ganador − la del mejor candidato de **otro tipo** (no el segundo receptor).
- **Retención** de un regate/protección/espera = su equipo conserva el balón 1 s después de empezar el episodio, excluidos los episodios en que él mismo patea (pase o tiro) antes.

## 3. Datos (8 partidos de 120 s por celda; Arena 301–308, Classic 321–328; equipos 301 % 12 vs (5·semilla+3) % 12)

### 3.1 Decisiones del portador (F11)
| Decisión | Disponible* | Elegida (Arena / Classic) | Puntuación media ganadora | Margen medio | Resultado |
|---|---|---|---|---|---|
| Pass | 37 % / 40 % | 232 / 185 | 2,43 / 2,31 | 0,77 / 0,67 | ver 3.2 |
| ForwardPass | 20 % / 23 % | 273 / 315 | 3,33 / 3,22 | 1,05 / 0,84 | ver 3.2 |
| ThroughPass | 1,1 % / 1,2 % | 23 / 26 | 3,72 / 3,71 | 1,08 / 1,01 | ver 3.3 |
| Shoot | 10 % / 8 % | 64 / 54 | 2,66 / 2,48 | 0,33 / 0,28 | 4 / 10 goles tras tiro seguido |
| Dribble | 100 % / 100 % | 1148 / 1353 | 2,41 / 2,36 | 0,73 / 0,75 | retención 39 % / 45 % (sin los que patean) |
| Protect | 71 % / 55 % | 403 / 225 | 1,59 / 1,47 | 0,77 / 0,66 | retención 46 % / 43 % |
| Hold | 29 % / 35 % | 176 / 169 | 2,13 / 1,96 | 0,59 / 0,57 | retención 56 % / 55 % |

\* «Disponible» = porcentaje de decisiones del portador en que existía al menos un candidato de ese tipo. Total de decisiones del portador: 2319 (Arena) y 2327 (Classic). Márgenes (Arena / Classic): media 0,76 / 0,72; por bandas < 0,10: 150 / 193; 0,10–0,25: 254 / 229; 0,25–0,50: 472 / 424; ≥ 0,50: 1360 / 1306; sin otro tipo de candidato: 83 / 175. Dribble y Hold son los que más a menudo compiten con margen pequeño.
Por qué algo **no** era candidato (recuentos por receptor/decisión, Arena / Classic): Pass — línea bloqueada 4974 / 4332, no avanza 1510 / 1837, portero no permitido 1493 / 1816, fuera de alcance 1304 / 1109, sin espacio 482 / 545; Shoot — fuera de alcance 1755 / 1967, ángulo 240 / 119, línea 38 / 12; ThroughPass — no hay corredor 5253 / 5139, sin ganancia 1835 / 1986, línea 764 / 717, pierde la carrera 635 / 634; Hold — demasiada presión 1062 / 735, fuera de la ventana 589 / 776.

### 3.2 Pases (todos, F10 → F11)
| | Arena F10 | Arena F11 | Classic F10 | Classic F11 |
|---|---|---|---|---|
| Patadas de pase | 848 | 675 | 798 | 669 |
| Completados (compañero distinto del pasador) | 286 (34 %) | 204 (30 %) | 247 (31 %) | 224 (33 %) |
| Interceptados (con evento) | 162 (19 %) | 104 (15 %) | 142 (18 %) | 95 (14 %) |
| Posesión perdida con otro | 210 (25 %) | 201 (30 %) | 272 (34 %) | 237 (35 %) |
| …de ellos, en manos del portero rival | 50 | 65 | 83 | 75 |
| Devuelto al pasador | 178 (21 %) | 156 (23 %) | 129 (16 %) | 111 (17 %) |
| …dentro de 0,1 s | 92 | 85 | 51 | 54 |
| Otros (perdidos, fuera, invalidados, no observados) | 12 | 10 | 8 | 2 |
| Decisiones de pase (jugadores de campo y portero) | 899 | 739 | 833 | 707 |

### 3.3 Pase al espacio (F11; 49 intentos)
| Resultado | Cantidad | Tasa |
|---|---|---|
| Completado (un compañero lo recoge) | 7 (3 Arena + 4 Classic) | 14 % |
| Interceptado | 7 (5 + 2) | 14 % |
| Posesión perdida con otro | 31 (12 + 19) | 63 % |
| …de ellos en manos del portero rival | 15 (11 + 4) | 31 % del total |
| Devuelto al pasador | 4 (3 + 1) | 8 % |
| Fallado / invalidado / no observado | 0 | 0 % |
Distancias medias al destino (Arena / Classic): corredor 4,9 / 4,6 m (completados) frente a 4,7 / 4,9 m (perdidos con otro); rival más cercano 8,5 / 8,2 m frente a 7,0 / 8,3 m. Ni la distancia del corredor ni la del rival separan los aciertos de los fallos con esta muestra.

### 3.4 Faltas en Classic (F10 → F11)
| Tipo de contacto | Contactos | Ganados / sueltos / fallados | Faltas | Tasa de falta |
|---|---|---|---|---|
| Entrada de pie | 84 → 80 | 11/24/49 → 11/14/55 | 1 → 1 | 1,2 % → 1,2 % |
| Entrada deslizante | 18 → 12 | 3/3/12 → 0/5/7 | 0 → 0 | 0 % |
| Disputa física | 184 → 212 | 60/56/68 → 43/64/105 | 6 → 17 | 3,3 % → 8,0 % |
| **Total** | 286 → 304 | | 7 → 18 | 2,4 % → 5,9 % |
Disputas físicas por detrás: contactos 125 → 141, falladas 44 → 77 (35 % → 55 %) y faltas 6 → 17. Portador que **protegía** (acción Shield): contactos 29 → 94, faltas 6 → 17 (tasa 21 % → 18 %); portador que no protegía: faltas 1 → 1 (en 257 → 210 contactos). Según lo que el portador había decidido: Protect 13 faltas en 76 contactos (17 %), Dribble 4/93. Defensor por posición (contactos/faltas, F11): delantero 12/121 (9,9 %), centrocampista 5/105 (4,8 %), defensa 1/78 (1,3 %); por arquetipo: Explosive 5/50, GoalHunter 4/33, Finisher 3/24, Architect 2/26, Creator 2/40, Engine 1/24, Wall 1/20; Guardian, Anchor, Builder, Winger, Target: 0. Entrada con `aggressive_tackle`: 0 faltas en 16 contactos (F11) y 0 en 9 (F10): **no concluyente** (muestra pequeña). Arena: 0 faltas (449 contactos F11, 363 F10), como debe.

### 3.5 Regate / protección / tiro (F11, Arena / Classic)
| Métrica | Arena | Classic |
|---|---|---|
| Decisiones Dribble (F10 → F11) | 1210 → 1145 | 1094 → 1350 |
| Decisiones con camino libre | 2104 de 2319 (91 %) | 2141 de 2327 (92 %) |
| Espacio medio / avance medio de los regates elegidos | 1,00 / 0,94 | 1,00 / 0,95 |
| Episodios / retenidos / perdidos / sueltos / soltó (patea) | 414 / 62 / 72 / 25 / 254 | 408 / 89 / 89 / 20 / 203 |
| Término `advanceSpace` del Dribble (media del mejor / ganador) | 2,54 / 3,00 | 2,68 / 3,01 |
| Decisiones Protect (F10 → F11) | 100 → 401 | 72 → 223 |
| Presión media cuando gana Protect | 0,74 | 0,69 |
| Protect gana frente a… (segundo) | Dribble 394 de 403 | Dribble 217 de 225 |
| Episodios Protect: retenidos / perdidos / sueltos / soltó | 24 / 18 / 10 / 78 | 18 / 22 / 2 / 47 |
| Decisiones con tiro disponible / elegido / acciones de tiro | 239 / 64 / 58 | 191 / 54 / 54 |
| Con tiro disponible, ganó Dribble (el tiro era el segundo) | 105 | 94 |
| Resultado de los tiros: gol / posesión rival / posesión propia / fuera / desconocido | 4 / 24 / 24 / 0 / 6 | 10 / 15 / 28 / 0 / 1 |
| Cercanía / ángulo / línea / portero / presión del mejor tiro (media) | 0,56 / 1,07 / 0,97 / 0,15 / −0,72 | 0,47 / 1,06 / 0,95 / 0,16 / −0,62 |

### 3.6 F10 → F11 (mismas semillas; totales de los 8 partidos)
| | Arena F10 → F11 | Classic F10 → F11 |
|---|---|---|
| Posesiones (cambios de equipo) | 581 → 461 | 567 → 500 |
| Recuperaciones (acciones defensivas que ganan el balón) | 403 → 301 | 365 → 253 |
| Goles / de ellos tras un tiro seguido | 18 / 11 → 6 / 4 | 10 / 7 → 12 / 10 |
| Contactos | 363 → 449 | 286 → 304 |
| Intercepciones (eventos) / bloqueos | 309 / 104 → 254 / 108 | 291 / 92 → 199 / 101 |
| Pases al espacio | 0 → 23 | 0 → 26 |
| Decisiones Hold | 0 → 172 | 0 → 169 |

## 4. Hallazgos (solo lo que los datos sostienen)

**4.1 «Pases» de las fases 10 y 11 eran patadas, no pases completados.** Solo el 30–34 % de los pases termina en un compañero; el 24–35 % acaba en un rival sin intercepción, el 14–19 % intercepta, y el 16–23 % vuelve al pasador. Esto es igual en F10 y F11: no es un efecto de la fase 11, pero cambia cómo hay que leer cualquier cifra de pases anterior.

**4.2 Por qué F11 pasa menos.** Pass (los que no ganan terreno) puntúa de media 1,66–1,76 cuando es el mejor de su tipo; Dribble puntúa 1,9–2,0 como mejor de su tipo y 2,4 (con un término de avance de 3,0) cuando gana. Los 251/295 casos (Arena/Classic) en que ganó Dribble teniendo un Pass como segundo, y los 97/125 con ForwardPass, son los pases «perdidos» frente al regate. Un pase solo supera al regate con camino libre si es ForwardPass (media 2,68–2,80, con +0,5 de bono) o corre con bono de carrera. En F10 cualquier receptor libre con avance ≥ 5 % se pasaba antes de considerar el regate. Además, un Pass existe solo en el 37–40 % de las decisiones (línea bloqueada en la mayoría de los receptores). **Conclusión sostenida por los datos:** menos pases porque el regate puntúa más que el pase sencillo cuando hay camino libre (que ocurre en el 91–92 % de las decisiones).

**4.3 Por qué F11 dribla/protege más.** Dribble: las decisiones **no** suben en Arena (1210 → 1145) y suben un 23 % en Classic (1094 → 1350). Lo que se multiplica es **Protect (×4 en Arena, ×3 en Classic)** y aparece **Hold** (7 % de las decisiones). Dribble gana porque su término `advanceSpace` se satura (el espacio medio de los regates elegidos es 1,00 y el avance 0,94 ⇒ ≈ 3,0) siempre que no haya un rival en un cono de 50° a < 3,5 m, y la penalización por presión (media 0,16–0,25) y por fatiga (media −0,3/−0,5) no lo bajan lo bastante. Protect gana casi solo contra Dribble (394 de 403 en Arena) y con presión alta (0,74): cuando un rival cierra el cono, el espacio del regate cae y Protect (0,4 + 1,2·presión) lo supera. **Hold** gana cuando no hay camino libre ni pase y no hay presión (0,05–0,09).

**4.4 Los portadores juegan casi sin stamina.** La stamina media del portador en el momento de decidir es 17–49 según lo que elige (regates 28–37). La penalización por fatiga del regate (< 35) apenas pesa (−0,3 a −0,5). Podría influir en todo (carreras, regates, entradas); no se investigó la causa del consumo.

**4.5 Pases al espacio.** Solo 14 % llegan a un compañero (7 de 49), 14 % se interceptan y 63 % acaban con un rival sin interceptación; **15 de esos 31 acaban en las manos del portero rival** (11 de 12 en Arena). Ni la distancia del corredor al destino (4,6–4,9 m) ni la del rival (7–8,5 m) distinguen aciertos de fallos con esta muestra. Causa probable de los de Arena: el destino cae en el área del portero; **no concluyente** para los 16 restantes.

**4.6 Pases devueltos al pasador.** Entre el 16 y el 23 % de los pases vuelve al pasador y **más de la mitad en menos de 0,1 s** (6 pasos); la IA decide cada 0,15 s por jugador, así que no es una decisión de «perseguir su pase» (solo el 21–34 % de esos pasadores había decidido ir al balón, frente al 54–80 % de los pases de otros resultados). No hay relación con la presión (rival más cercano al pasar: 2,1–3,6 m, como en los completados). Es un comportamiento del Core al recoger el balón recién pateado o una interacción pasador–balón. **Causa no concluyente.**

**4.7 Los pasadores persiguen su propio pase** en el 51–66 % de los pases (decisión `MoveToBall` antes de que el pase se resuelva): el más cercano al balón suelto (el propio pasador) va a por él. Es del motor de la Fase 8, ya existía en F10.

**4.8 Por qué suben las faltas en Classic.** Una falta es una disputa física fallada **por detrás** con margen bajo. En F11: 17 de 18 faltas son disputas físicas, 17 de 18 contra un portador que **protegía** (acción Shield). La **tasa de falta contra un portador que protege es la misma** (21 % F10, 18 % F11); lo que cambia es la **exposición**: los contactos contra portadores que protegen pasan de 29 a 94 (10 % a 31 % de los contactos) porque Protect se elige 3 veces más. Contra portadores que no protegen casi no hay faltas (1 de 257 / 1 de 210). Las disputas por detrás fallan más (35 % → 55 %), coherente con que el portador que protege añade bonificación defensiva en el duelo. Los que más faltas cometen son delanteros/atacantes que defienden (9,9 % de sus contactos frente a 1,3 % de los defensas). **Con los datos: el aumento viene de la exposición (más contactos con portadores que protegen), no de una probabilidad de falta mayor por contacto.** Entradas con `aggressive_tackle`: no concluyente.

**4.9 Protect y posesión.** Protect y Hold retienen más la posesión (43–56 %) que el regate (39–45 %), y las disputas físicas contra el equipo con F11 se ganan menos (25 de 312 en Arena frente a 73 de 226 en F10). Esto es **compatible con** las menos posesiones (461 vs 581) y recuperaciones (301 vs 403) de F11, pero los datos no demuestran la relación causal. **No se puede decir si es deseable.**

**4.10 Tiros.** El tiro es candidato en el 8–10 % de las decisiones, casi siempre por la distancia (fuera de los 11 m: 76–85 % de las decisiones). Cuando es candidato su puntuación media (2,0) es inferior a la del regate (2,4) porque el término de cercanía es pequeño (0,5 de 2,0) y la presión resta 0,6–0,7; Dribble gana con el tiro como segundo en 105/94 ocasiones (44–49 % de las decisiones con tiro). **El tiro no se elige porque está lejos y mal puntuado, no porque no se considere.**

**4.11 Qué no se puede responder aún:** si los pases al espacio fallan por la física del pase (error, velocidad) o por la geometría de la carrera; por qué el pasador recupera su pase en < 0,1 s; si las skills (`aggressive_tackle`) cambian la tasa de faltas (muestra mínima); resultado de regates/protecciones en F10 (esa capa no existe en el portador de la Fase 10, solo se miden decisiones).

## 5. Candidatos de balance para la Fase 13 (sin aplicar)
1. **Dribble sin tope práctico:** `advanceSpace` se satura en 3,0; con camino libre gana a cualquier pase que no avance. Estudiar un término de «utilidad» más bajo o una prima al ForwardPass/Pass seguro cuando hay un compañero libre.
2. **Protect contra Dribble:** Protect gana casi siempre solo a Dribble (394/403) y con presión 0,7: estudiar `ProtectBase`/`ProtectPressureWeight` frente a `DribblePressureWeight` y la saturación del espacio.
3. **Exposición a faltas:** estudiar si el bono defensivo del escudo (Core) + margen de falta de la disputa física (`StandingFoulMargin`) es lo deseado en Classic, o si los defensores deben usar entrada y no disputa física contra un portador que protege; y quién defiende (los delanteros cometen la mayoría).
4. **Pase al espacio:** el destino no debe caer en el área del portero rival (11 de 12 en Arena); añadir esa condición en la geometría (no en el Core). Medir la física del pase.
5. **Pases devueltos al pasador (< 0,1 s):** investigar en el Core la recogida del balón recién pateado (posiblemente un coste de la distancia inicial del balón o del radio de recogida).
6. **Alcance del tiro:** el 76–85 % de las decisiones no tienen tiro por distancia > 11 m; la cercanía pesa poco (0,5/2,0): estudiar `ShootRange` y `ShootDistanceWeight`.
7. **Stamina:** 17–49 de media en el portador; revisar el consumo antes de ajustar la penalización por fatiga del regate (`DribbleTiredStamina`, `DribbleTiredWeight`).
8. **Pasadores que persiguen su pase:** 51–66 % deciden `MoveToBall`: estudiar una regla «tras pasar, no perseguir» antes de seguir con el ataque.
9. **Medir con más muestra** las skills defensivas (aggressive_tackle) antes de tocar la probabilidad de falta.

## 6. Límites de esta fase
- Las ventanas (6 s para pases, 1 s para posesión, 3 s para tiros) son criterios de medición, no de juego.
- El resultado de un regate/protección/espera es la posesión 1 s después del inicio del episodio (los que patean antes se separan); no es «éxito del regate» en sentido estricto.
- Un pase «completado» exige que un compañero consiga la posesión: una recepción sin posesión (rebote) no se cuenta; un «tiro a puerta» no se distingue de un tiro fuera en Arena (rebota en las paredes): los tiros se clasifican por su destino, no por su calidad.
- Las faltas se explican por lo que registra el Core; el margen real del duelo no se expone (solo la calidad del informe, que se satura).
- Los jugadores por partido no se suman entre partidos (cada partido tiene sus jugadores); los totales por arquetipo/posición sí.
