# FS27 — ENTRENADORES V1 (authoring)

Fuente de los datos: `FS27_COACHES_V1.json` (se carga con `CoachJson`; este documento es su explicación). Los 20 entrenadores iniciales, su grado, notas de investigación y formaciones.

## Cómo leer las notas

- **FACT**: información documentada (trayectoria, títulos, sistemas usados), con la investigación hecha en esta fase (búsquedas web de 2026 para el cargo actual y los sistemas de tres defensas).
- **INFERENCE**: traducción de esos hechos a FS27 (roles, zonas, atributos, formaciones de 6v6). **Toda formación FS27 es una inferencia**: FS27 tiene 5 jugadores de campo y ningún entrenador juega así en la vida real.
- **AUTHORING**: decisión de diseño (grado, valoraciones de apoyo, números de compatibilidad). Los grados son un juicio global de autoría sobre la trayectoria **como entrenador**, no un promedio de las ocho valoraciones.

El cargo actual de cada entrenador **no se usa en ningún cálculo**: solo aparece en las notas como contexto de la fecha de investigación.

## Resumen

| # | Entrenador | Grado | Formaciones | Línea de 3 | Jugadores con boost (de 136 de campo) |
|---|---|---|---|---|---|
| 1 | Pep Guardiola | A+ | 3 | no | 40 |
| 2 | Hansi Flick | A | 3 | no | 35 |
| 3 | Mikel Arteta | A | 3 | no | 21 |
| 4 | Carlo Ancelotti | A+ | 3 | no | 17 |
| 5 | Lionel Scaloni | A | 3 | no | 30 |
| 6 | Diego Simeone | A+ | 3 | sí | 38 |
| 7 | Thomas Tuchel | A- | 3 | sí | 12 |
| 8 | José Mourinho | A | 3 | sí | 19 |
| 9 | Luis de la Fuente | A- | 3 | no | 26 |
| 10 | Zinedine Zidane | A | 3 | no | 40 |
| 11 | Cesc Fàbregas | B | 2 | no | 42 |
| 12 | Luis Enrique | A+ | 3 | no | 39 |
| 13 | Vincent Kompany | B+ | 2 | no | 20 |
| 14 | Yaya Touré | C+ | 1 | no | 9 |
| 15 | Hajime Moriyasu | B+ | 2 | sí | 35 |
| 16 | Massimiliano Allegri | A- | 3 | sí | 16 |
| 17 | Roberto Mancini | A- | 3 | no | 31 |
| 18 | Louis van Gaal | A | 3 | sí | 25 |
| 19 | Manuel Pellegrini | B+ | 2 | no | 32 |
| 20 | Mauricio Pochettino | B+ | 2 | no | 28 |

## 1. Pep Guardiola — A+

**Filosofía:** Juego de posición: control del balón, superioridades por posición y presión inmediata tras pérdida.  
**Identidad táctica:** Posesión y control territorial; el equipo se organiza para que el balón llegue limpio a los espacios entre líneas.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 98 · Attack 96 · Defense 86 · Transitions 88 · Adaptability 90 · PlayerDevelopment 90 · Management 88 · Identity 99

**Compatibilidad:** roles [Architect 100, Creator 85, Builder 85, Guardian 50, Winger 45, Engine 45, Finisher 35, Anchor 30] · zonas [Midfield 100, Wing 60, Defense 50, Attack 50] · atributos [Passing 3, BallControl 3, Technique 3, Dribbling 1.5, Agility 1, Stamina 1] · riesgo 60 (peso 40) · creatividad 85 (peso 70) · agresividad 55 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Control Posicional** — Dominar el balón desde atrás con un pivote organizador y un extremo creador. | 2-1-2 | Two | Defense/Guardian · Defense/Builder · Midfield/Architect · Wing/Creator · Attack/Finisher | Attacking · Positional · HighPress · PossessionRecovery | 70 · 45 · 55 · 80 |
| **Superioridad Interior** — Un solo defensor y dos interiores para ganar la zona central y liberar al extremo. | 1-2-2 | One | Defense/Anchor · Midfield/Architect · Midfield/Creator · Wing/Winger · Attack/Finisher | VeryAttacking · Positional · HighPress · PossessionRecovery | 55 · 50 · 62 · 85 |
| **Falso Nueve y Pivote** — Un delantero que baja a jugar y un doble pivote que sostiene el control. | 2-2-1 | Two | Defense/Guardian · Defense/Builder · Midfield/Architect · Midfield/Engine · Attack/Creator | Attacking · Positional · Selective · Controlled | 45 · 40 · 50 · 75 |

- **FACT:** Juego de posición y presión inmediata tras pérdida como señas de identidad en Barcelona, Bayern y Manchester City.
- **FACT:** Usó delanteros que bajan a asociarse (falso nueve) en Barcelona y laterales que interiorizan en el City.
- **INFERENCE:** FS27 traduce la superioridad por posición en roles Architect / Creator / Builder y en el atributo de pase y control; es una interpretación, no un dato histórico.
- **AUTHORING:** Grado A+ y valoraciones de apoyo: juicio de autoría por trayectoria como entrenador.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Martin Ødegaard 88→89 (95); Pedri 88→89 (94); Frenkie de Jong 87→88 (93); Luka Modrić 84→85 (93)

## 2. Hansi Flick — A

**Filosofía:** Presión alta y línea defensiva adelantada: recuperar rápido y atacar en vertical con intensidad.  
**Identidad táctica:** Intensidad, verticalidad y riesgo controlado con la línea alta.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 92 · Attack 93 · Defense 84 · Transitions 92 · Adaptability 86 · PlayerDevelopment 86 · Management 88 · Identity 90

**Compatibilidad:** roles [Engine 100, Explosive 90, Winger 85, Finisher 75, Guardian 70, Creator 65, Builder 55] · zonas [Midfield 85, Wing 80, Attack 75, Defense 65] · atributos [Stamina 3, Speed 2, Acceleration 2, Passing 1.5, Defense 1, Finishing 1] · riesgo 70 (peso 50) · creatividad 60 (peso 20) · agresividad 75 (peso 70)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Línea Alta y Presión** — Defensas rápidos con línea adelantada, un motor en el medio y un extremo explosivo. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Wing/Explosive · Attack/GoalHunter | Attacking · FastVertical · AggressivePress · Fast | 65 · 80 · 68 · 90 |
| **Doble Pivote Vertical** — Dos interiores que presionan y progresan rápido hacia un solo punta. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Midfield/Builder · Attack/Finisher | Attacking · FastVertical · HighPress · Fast | 55 · 75 · 62 · 85 |
| **Presión Total** — Un solo defensor, interiores agresivos y un extremo: todo el equipo presiona arriba. | 1-2-2 | One | Defense/Guardian · Midfield/Engine · Midfield/Creator · Wing/Winger · Attack/Finisher | VeryAttacking · FastVertical · AggressivePress · Fast | 60 · 85 · 72 · 92 |

- **FACT:** Presión alta y línea defensiva adelantada en el Bayern del triplete 2020 y en el Barcelona 2024-25.
- **INFERENCE:** FS27 lo traduce en preferencia por Engine / Explosive / Winger y en velocidad, aceleración y resistencia.
- **AUTHORING:** Grado A: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Federico Valverde 85→86 (89); Achraf Hakimi 87→88 (87); Nicolò Barella 86→87 (87); Dani Muñoz 83→84 (86)

## 3. Mikel Arteta — A

**Filosofía:** Estructura de posesión con laterales interiores, presión organizada y cuidado de las acciones a balón parado.  
**Identidad táctica:** Orden posicional y solidez: control con balón y defensa muy estructurada.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 90 · Attack 86 · Defense 92 · Transitions 84 · Adaptability 84 · PlayerDevelopment 86 · Management 86 · Identity 88

**Compatibilidad:** roles [Anchor 90, Architect 90, Guardian 85, Builder 85, Engine 70, Wall 70, Winger 70, Target 60] · zonas [Midfield 90, Defense 80, Wing 60, Attack 50] · atributos [Passing 2.5, Defense 2, BallControl 2, Strength 1.5, Technique 1.5, Stamina 1] · riesgo 45 (peso 50) · creatividad 65 (peso 20) · agresividad 65 (peso 40)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Estructura de Posesión** — Dos centrales sólidos, un pivote que ordena y un extremo para estirar. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Architect · Wing/Winger · Attack/Finisher | Balanced · Positional · Selective · Controlled | 60 · 45 · 40 · 70 |
| **Lateral Interior** — Un defensor que se suma al medio y dos interiores para sostener la posesión. | 1-2-2 | One | Defense/Anchor · Midfield/Architect · Midfield/Engine · Wing/Creator · Attack/Finisher | Attacking · Positional · HighPress · PossessionRecovery | 50 · 50 · 50 · 80 |
| **Bloque Medio Compacto** — Bloque equilibrado con doble pivote y un delantero referencia para el juego directo y el balón parado. | 2-2-1 | Two | Defense/Wall · Defense/Guardian · Midfield/Anchor · Midfield/Builder · Attack/Target | Balanced · Balanced · MidBlock · Controlled | 45 · 45 · 35 · 60 |

- **FACT:** Arsenal con juego de posición, laterales que interiorizan y fuerte trabajo a balón parado; subcampeón de la Premier League en 2022-23, 2023-24 y 2024-25.
- **INFERENCE:** FS27 lo traduce en Anchor / Architect / Guardian / Builder y en defensa y pase.
- **AUTHORING:** Grado A: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Rodri 87→88 (88); Virgil van Dijk 86→87 (85); William Saliba 88→89 (85); Declan Rice 85→86 (84)

## 4. Carlo Ancelotti — A+

**Filosofía:** Adaptación a los jugadores: equilibrio, libertad controlada para los de más calidad y gestión excelente del grupo.  
**Identidad táctica:** No impone un sistema fijo: ajusta la estructura al talento que tiene. Equilibrio entre ataque y defensa.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 92 · Attack 88 · Defense 86 · Transitions 86 · Adaptability 98 · PlayerDevelopment 86 · Management 98 · Identity 84

**Compatibilidad:** roles [Creator 95, Builder 85, Anchor 85, Engine 80, Architect 80, Finisher 80, Guardian 80, Winger 75, Explosive 75, GoalHunter 70, Target 65, Wall 65] · zonas [Midfield 90, Wing 80, Attack 80, Defense 75] · atributos [Passing 1.5, BallControl 1.5, Technique 1.5, Stamina 1, Strength 1, Defense 1, Shooting 1] · riesgo 50 (peso 30) · creatividad 65 (peso 40) · agresividad 55 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Equilibrio Fluido** — Estructura equilibrada que deja libertad a los creadores del medio. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Builder · Midfield/Creator · Attack/Finisher | Balanced · Balanced · MidBlock · Controlled | 55 · 50 · 48 · 55 |
| **Libertad Controlada** — Interiores libres y extremo explosivo con un defensa que garantiza el equilibrio. | 1-2-2 | One | Defense/Anchor · Midfield/Engine · Midfield/Architect · Wing/Explosive · Attack/GoalHunter | Attacking · Balanced · Selective · Fast | 60 · 55 · 55 · 60 |
| **Contraataque Adaptado** — Bloque que cede el balón cuando conviene y sale rápido con extremo y delantero. | 2-1-2 | Two | Defense/Wall · Defense/Guardian · Midfield/Engine · Wing/Winger · Attack/Finisher | Balanced · Direct · LowBlock · Counter | 60 · 60 · 45 · 40 |

- **FACT:** Cinco Champions League como entrenador (Milan 2003 y 2007, Real Madrid 2014, 2022 y 2024); es entrenador de la selección de Brasil.
- **FACT:** Cambia de estructura según la plantilla (rombo, 4-3-3, 4-2-3-1) y destaca en la gestión de vestuario.
- **INFERENCE:** Su perfil de compatibilidad es AMPLIO a propósito (acepta muchos roles); el boost lo recibirán los jugadores más completos.
- **AUTHORING:** Grado A+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Rodri 87→88 (85); Pedri 88→89 (83); Martin Ødegaard 88→89 (83); Federico Dimarco 84→85 (82)

## 5. Lionel Scaloni — A

**Filosofía:** Equipo colectivo, intenso y equilibrado que confía en un grupo cohesionado y se adapta al rival.  
**Identidad táctica:** Compacidad, intensidad en el medio y un jugador libre entre líneas.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 86 · Attack 84 · Defense 86 · Transitions 84 · Adaptability 92 · PlayerDevelopment 86 · Management 94 · Identity 88

**Compatibilidad:** roles [Engine 95, Creator 85, Anchor 85, Guardian 85, Builder 80, Winger 70, Finisher 70, Wall 60] · zonas [Midfield 95, Defense 75, Wing 65, Attack 65] · atributos [Stamina 3, Defense 1.5, Passing 1.5, Strength 1, BallControl 1] · riesgo 45 (peso 40) · creatividad 55 (peso 15) · agresividad 70 (peso 50)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Bloque Colectivo** — Dos líneas juntas y un creador libre arriba. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Midfield/Builder · Attack/Creator | Balanced · Balanced · MidBlock · Controlled | 50 · 50 · 42 · 55 |
| **Medio Campo Intenso** — Un defensa y dos interiores incansables para ganar el centro. | 1-2-2 | One | Defense/Anchor · Midfield/Engine · Midfield/Creator · Wing/Winger · Attack/Finisher | Attacking · Balanced · Selective · Fast | 60 · 60 · 52 · 60 |
| **Campeón Compacto** — Defensa sólida, pivote que ordena y salida rápida por el extremo. | 2-1-2 | Two | Defense/Guardian · Defense/Wall · Midfield/Engine · Wing/Explosive · Attack/Finisher | Balanced · Balanced · MidBlock · Counter | 55 · 60 · 45 · 45 |

- **FACT:** Campeón del Mundo 2022, de la Copa América 2021 y 2024 y de la Finalissima 2022 con Argentina.
- **FACT:** Destaca por el equilibrio colectivo y la cohesión del grupo.
- **INFERENCE:** FS27 lo traduce en preferencia por Engine / Anchor / Guardian y resistencia.
- **AUTHORING:** Grado A: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Declan Rice 85→86 (88); Rodri 87→88 (87); Federico Valverde 85→86 (86); Sandro Tonali 85→86 (86)

## 6. Diego Simeone — A+

**Filosofía:** Bloque compacto, intensidad competitiva, disciplina defensiva y transiciones rápidas.  
**Identidad táctica:** Solidez y agresividad: el equipo defiende en bloque y golpea en la transición.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 90 · Attack 72 · Defense 98 · Transitions 92 · Adaptability 84 · PlayerDevelopment 84 · Management 92 · Identity 99

**Compatibilidad:** roles [Wall 100, Guardian 95, Anchor 95, Engine 80, Target 70, GoalHunter 65] · zonas [Defense 100, Midfield 80, Attack 55, Wing 55] · atributos [Defense 3, Strength 2.5, Stamina 2.5, Speed 0.5] · riesgo 30 (peso 50) · creatividad 35 (peso 20) · agresividad 85 (peso 70) · cuerpo Strong, Tall

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Bloque Bajo de Tres** — Línea de tres defensas con un mediocentro de contención y un delantero para golpear en la transición. | 3-1-1 | Three | Defense/Wall · Defense/Guardian · Defense/Anchor · Midfield/Engine · Attack/GoalHunter | Defensive · Direct · LowBlock · Counter | 40 · 60 · 25 · 25 |
| **Bloque Compacto 4-4-2** — Dos líneas muy juntas con doble pivote y un delantero referencia. | 2-2-1 | Two | Defense/Guardian · Defense/Wall · Midfield/Engine · Midfield/Anchor · Attack/Target | Defensive · Direct · MidBlock · Counter | 45 · 60 · 30 · 35 |
| **Contra Intenso** — Defensa firme y salida explosiva por un extremo y un goleador. | 2-1-2 | Two | Defense/Wall · Defense/Guardian · Midfield/Anchor · Wing/Explosive · Attack/GoalHunter | Balanced · Direct · MidBlock · Counter | 55 · 70 · 40 · 40 |

- **FACT:** Entrenador del Atlético de Madrid desde diciembre de 2011; campeón de La Liga 2013-14 y 2020-21.
- **FACT:** Análisis tácticos recientes describen un sistema híbrido: 5-3-2 sin balón y 4-4-2 con balón, con variantes de tres centrales (3-5-2).
- **INFERENCE:** La línea de 3 se justifica por esa evidencia; FS27 la traduce en 3-1-1 con roles defensivos y Engine delante de ellos.
- **AUTHORING:** Grado A+: juicio de autoría. La preferencia por cuerpos Strong/Tall es una inferencia.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Virgil van Dijk 86→87 (91); Gabriel Magalhães 85→86 (90); Jonathan Tah 81→82 (89); William Saliba 88→89 (89)

## 7. Thomas Tuchel — A-

**Filosofía:** Flexibilidad táctica: cambia de estructura según el rival, con intensidad y detalle en cada fase.  
**Identidad táctica:** Estructuras que se transforman (tres atrás con balón, 4-2-3-1) y presión intensa con mucho trabajo táctico.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 94 · Attack 84 · Defense 88 · Transitions 86 · Adaptability 92 · PlayerDevelopment 80 · Management 78 · Identity 88

**Compatibilidad:** roles [Guardian 85, Anchor 85, Engine 85, Builder 80, Creator 75, Finisher 75, Winger 70, Architect 65] · zonas [Midfield 90, Defense 80, Wing 60, Attack 60] · atributos [Stamina 2, Passing 1.5, Defense 1.5, Speed 1, Strength 1, BallControl 1] · riesgo 55 (peso 30) · creatividad 55 (peso 20) · agresividad 70 (peso 40)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Tres Atrás Flexible** — Tres defensas que construyen, un motor en el medio y un delantero. | 3-1-1 | Three | Defense/Guardian · Defense/Anchor · Defense/Builder · Midfield/Engine · Attack/Finisher | Balanced · Balanced · HighPress · Controlled | 50 · 60 · 45 · 60 |
| **Doble Pivote Vertical** — Doble pivote que equilibra y un creador entre líneas con un delantero solo. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Builder · Midfield/Creator · Attack/Finisher | Balanced · Balanced · Selective · Fast | 55 · 60 · 50 · 55 |
| **Salida Adaptable** — Estructura de salida cambiante: un defensa, dos interiores y un extremo. | 1-2-2 | One | Defense/Anchor · Midfield/Architect · Midfield/Engine · Wing/Winger · Attack/Target | Attacking · Direct · HighPress · Direct | 60 · 60 · 58 · 65 |

- **FACT:** Con el Chelsea usó línea de tres en 55 de sus 63 partidos de Premier League y ganó la Champions League 2021.
- **FACT:** Con Inglaterra ha mezclado un 4-2-3-1 con salidas en 3-2-5, según análisis de 2026.
- **INFERENCE:** FS27 le da una variante de tres atrás justificada por el Chelsea y deja dos variantes de cuatro por su flexibilidad.
- **AUTHORING:** Grado A-: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Rodri 87→88 (85); William Saliba 88→89 (83); Declan Rice 85→86 (83); Joško Gvardiol 87→88 (81)

## 8. José Mourinho — A

**Filosofía:** Estructura disciplinada, control del riesgo, eficacia en las transiciones y mentalidad competitiva.  
**Identidad táctica:** Orden, bloque y golpe eficiente: lo importante es no fallar y aprovechar el error del rival.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 92 · Attack 76 · Defense 96 · Transitions 92 · Adaptability 88 · PlayerDevelopment 76 · Management 92 · Identity 92

**Compatibilidad:** roles [Wall 100, Anchor 95, Guardian 90, Engine 80, Target 70, Explosive 70, GoalHunter 70] · zonas [Defense 95, Midfield 80, Attack 60, Wing 60] · atributos [Defense 3, Strength 2, Stamina 2, Speed 1, Finishing 1] · riesgo 25 (peso 60) · creatividad 40 (peso 20) · agresividad 70 (peso 50)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Muro de Tres** — Tres defensas contundentes, un medio de equilibrio y un delantero referencia. | 3-1-1 | Three | Defense/Wall · Defense/Guardian · Defense/Anchor · Midfield/Engine · Attack/Target | Defensive · Direct · LowBlock · Counter | 40 · 55 · 22 · 25 |
| **Bloque Medio de Transición** — Doble pivote duro y un punta que recibe en profundidad. | 2-2-1 | Two | Defense/Guardian · Defense/Wall · Midfield/Anchor · Midfield/Engine · Attack/GoalHunter | Balanced · Direct · MidBlock · Counter | 45 · 60 · 30 · 35 |
| **Contragolpe Eficiente** — Defensa firme y un extremo y un delantero para golpear con precisión. | 2-1-2 | Two | Defense/Wall · Defense/Guardian · Midfield/Anchor · Wing/Explosive · Attack/Finisher | Balanced · Direct · LowBlock · Counter | 50 · 65 · 35 · 30 |

- **FACT:** Campeón de la Champions League con Porto (2004) e Inter (2010); entrenador del Real Madrid desde julio de 2026.
- **INFERENCE:** La línea de tres se apoya en estructuras de tres centrales de sus etapas italianas; no se ha reverificado en esta pasada y se marca como INFERENCIA.
- **AUTHORING:** Grado A: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Virgil van Dijk 86→87 (88); Gabriel Magalhães 85→86 (87); Jonathan Tah 81→82 (84); William Saliba 88→89 (84)

## 9. Luis de la Fuente — A-

**Filosofía:** Extremos abiertos con mucho desborde, equilibrio colectivo y un medio de control.  
**Identidad táctica:** Amplitud y desborde por las bandas apoyados en un mediocampo que ordena.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 84 · Attack 84 · Defense 82 · Transitions 84 · Adaptability 86 · PlayerDevelopment 88 · Management 88 · Identity 84

**Compatibilidad:** roles [Winger 95, Explosive 85, Builder 75, Creator 75, Anchor 65, Architect 65, Finisher 60] · zonas [Wing 100, Midfield 75, Attack 60, Defense 50] · atributos [Dribbling 2, Speed 1.5, Passing 1.5, BallControl 1.5, Technique 1.5, Stamina 1] · riesgo 60 (peso 30) · creatividad 70 (peso 40) · agresividad 50 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Extremos Abiertos** — Un pivote que reparte y un extremo muy abierto para el uno contra uno. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Builder · Wing/Winger · Attack/Finisher | Attacking · Balanced · Selective · Controlled | 85 · 55 · 58 · 60 |
| **Interiores y Extremos** — Interiores que combinan y un extremo explosivo. | 1-2-2 | One | Defense/Anchor · Midfield/Builder · Midfield/Creator · Wing/Explosive · Attack/GoalHunter | Attacking · Short · Selective · Controlled | 80 · 60 · 60 · 65 |
| **Control Equilibrado** — Doble pivote técnico con un delantero que termina. | 2-2-1 | Two | Defense/Guardian · Defense/Builder · Midfield/Anchor · Midfield/Architect · Attack/Finisher | Balanced · Positional · MidBlock · Controlled | 60 · 45 · 45 · 55 |

- **FACT:** Campeón de Europa 2024 con España, con dos extremos muy abiertos y centrocampistas de control.
- **INFERENCE:** FS27 lo traduce en preferencia por Winger / Explosive y en regate y velocidad.
- **AUTHORING:** Grado A-: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Achraf Hakimi 87→88 (88); Nuno Mendes 87→88 (87); Bukayo Saka 86→87 (87); Vinícius Júnior 91→92 (87)

## 10. Zinedine Zidane — A

**Filosofía:** Gestión de plantilla y de estrellas, flexibilidad y libertad para los jugadores de más calidad.  
**Identidad táctica:** Equipo adaptable que protege el equilibrio y deja decidir a los técnicos.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 84 · Attack 84 · Defense 80 · Transitions 82 · Adaptability 92 · PlayerDevelopment 84 · Management 98 · Identity 82

**Compatibilidad:** roles [Creator 95, Architect 90, Builder 80, Finisher 70, Winger 65, Anchor 65] · zonas [Midfield 95, Attack 65, Wing 60, Defense 50] · atributos [BallControl 2.5, Technique 2.5, Passing 2, Dribbling 1.5, Shooting 1] · riesgo 55 (peso 30) · creatividad 80 (peso 50) · agresividad 45 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Mediocampo de Calidad** — Dos centrales sólidos y un doble pivote técnico que alimenta al delantero. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Architect · Midfield/Creator · Attack/Finisher | Balanced · Short · MidBlock · Controlled | 55 · 45 · 45 · 50 |
| **Libertad para las Estrellas** — Interiores y extremo con libertad de movimientos. | 1-2-2 | One | Defense/Anchor · Midfield/Engine · Midfield/Creator · Wing/Winger · Attack/GoalHunter | Attacking · Balanced · Selective · Fast | 65 · 55 · 58 · 55 |
| **Equilibrio de Plantilla** — Estructura segura que aprovecha a quien está en forma. | 2-1-2 | Two | Defense/Guardian · Defense/Wall · Midfield/Builder · Wing/Winger · Attack/Finisher | Balanced · Balanced · MidBlock · Controlled | 55 · 50 · 42 · 45 |

- **FACT:** Tres Champions League seguidas (2016-2018) con el Real Madrid; seleccionador de Francia desde el 1 de septiembre de 2026.
- **INFERENCE:** FS27 lo traduce en preferencia por Creator / Architect / Builder y en control y técnica, con libertad para los más técnicos.
- **AUTHORING:** Grado A: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Pedri 88→89 (91); Cole Palmer 86→87 (89); Martin Ødegaard 88→89 (89); Jamal Musiala 87→88 (88)

## 11. Cesc Fàbregas — B

**Filosofía:** Posesión y juego de posición con jugadores jóvenes y técnicos.  
**Identidad táctica:** Control por pases cortos y ataque posicional.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 82 · Attack 82 · Defense 70 · Transitions 74 · Adaptability 76 · PlayerDevelopment 84 · Management 76 · Identity 82

**Compatibilidad:** roles [Architect 100, Creator 90, Builder 85, Winger 60, Anchor 45] · zonas [Midfield 100, Wing 60, Attack 50, Defense 40] · atributos [Passing 3, BallControl 2.5, Technique 2.5, Dribbling 1, Agility 0.5] · riesgo 65 (peso 40) · creatividad 85 (peso 60) · agresividad 40 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Juego de Posición Joven** — Un defensor que construye y dos interiores técnicos para sostener la posesión. | 1-2-2 | One | Defense/Builder · Midfield/Architect · Midfield/Creator · Wing/Winger · Attack/GoalHunter | Attacking · Positional · HighPress · PossessionRecovery | 60 · 50 · 62 · 75 |
| **Salida Corta** — Salida limpia desde atrás con un pivote creador. | 2-1-2 | Two | Defense/Guardian · Defense/Builder · Midfield/Architect · Wing/Creator · Attack/Finisher | Balanced · Short · Selective · Controlled | 55 · 45 · 52 · 65 |

- **FACT:** Entrenador del Como en la Serie A, con un estilo de posesión y juego de posición.
- **INFERENCE:** FS27 lo traduce en Architect / Creator / Builder y pase y técnica.
- **AUTHORING:** Grado B: trayectoria corta como entrenador; juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Martin Ødegaard 88→89 (95); Pedri 88→89 (94); Luka Modrić 84→85 (94); Frenkie de Jong 87→88 (93)

## 12. Luis Enrique — A+

**Filosofía:** Presión intensa tras pérdida, posesión con rotaciones y un sistema muy trabajado que se adapta a las piezas.  
**Identidad táctica:** Intensidad y orden: presionar arriba, rotar en el medio y atacar con muchos hombres.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 94 · Attack 92 · Defense 86 · Transitions 88 · Adaptability 90 · PlayerDevelopment 88 · Management 88 · Identity 92

**Compatibilidad:** roles [Engine 95, Architect 90, Builder 80, Explosive 80, Creator 75, Winger 70, Guardian 60] · zonas [Midfield 95, Wing 70, Attack 60, Defense 50] · atributos [Stamina 2.5, Passing 2, BallControl 2, Speed 1.5, Agility 1] · riesgo 65 (peso 40) · creatividad 70 (peso 30) · agresividad 70 (peso 50)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Presión Tras Pérdida** — Pivote motor y un extremo explosivo para presionar y salir. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Wing/Explosive · Attack/GoalHunter | Attacking · FastVertical · HighPress · Fast | 70 · 65 · 62 · 75 |
| **Rotación en el Medio** — Interiores que rotan, un extremo y un delantero que remata. | 1-2-2 | One | Defense/Anchor · Midfield/Architect · Midfield/Engine · Wing/Winger · Attack/Finisher | VeryAttacking · Positional · HighPress · PossessionRecovery | 60 · 55 · 65 · 80 |
| **Presión y Posesión** — Doble pivote que presiona y construye, con un delantero que baja a jugar. | 2-2-1 | Two | Defense/Guardian · Defense/Builder · Midfield/Architect · Midfield/Engine · Attack/Creator | Attacking · Short · HighPress · PossessionRecovery | 55 · 55 · 58 · 75 |

- **FACT:** Triplete con el Barcelona (2015) y Champions League 2025 con el PSG.
- **FACT:** Estilo de presión intensa y posesión con rotaciones; no se le documenta línea de tres habitual.
- **INFERENCE:** Sin variante de tres defensas porque no hay evidencia en esta pasada; FS27 lo traduce en Engine / Architect y resistencia.
- **AUTHORING:** Grado A+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Nicolò Barella 86→87 (89); Martin Ødegaard 88→89 (89); Federico Valverde 85→86 (87); Frenkie de Jong 87→88 (87)

## 13. Vincent Kompany — B+

**Filosofía:** Presión muy alta, línea defensiva adelantada y duelos individuales para dominar al rival.  
**Identidad táctica:** Valentía y agresividad: línea alta, marca al hombre y presión constante.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 82 · Attack 84 · Defense 76 · Transitions 78 · Adaptability 78 · PlayerDevelopment 84 · Management 84 · Identity 84

**Compatibilidad:** roles [Guardian 90, Engine 90, Explosive 80, Anchor 80, Builder 75, Finisher 75, Target 70] · zonas [Midfield 85, Defense 80, Attack 70, Wing 60] · atributos [Stamina 2.5, Defense 2, Strength 1.5, Speed 1.5, Acceleration 1] · riesgo 70 (peso 50) · creatividad 50 (peso 15) · agresividad 80 (peso 60)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Línea Alta Agresiva** — Defensas rápidos con línea adelantada, un motor y un extremo explosivo. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Wing/Explosive · Attack/Finisher | Attacking · FastVertical · AggressivePress · Fast | 60 · 70 · 70 · 90 |
| **Presión Hombre a Hombre** — Marcas individuales por todo el campo con un delantero referencia. | 2-2-1 | Two | Defense/Guardian · Defense/Wall · Midfield/Engine · Midfield/Builder · Attack/Target | Attacking · Direct · AggressivePress · Direct | 50 · 65 · 65 · 85 |

- **FACT:** Ascenso con el Burnley en 2023 y entrenador del Bayern desde 2024, con presión alta y línea adelantada.
- **INFERENCE:** FS27 lo traduce en Guardian / Engine / Explosive y en defensa, resistencia y agresividad.
- **AUTHORING:** Grado B+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Micky van de Ven 85→86 (87); Federico Valverde 85→86 (85); Joško Gvardiol 87→88 (84); Dayot Upamecano 85→86 (83)

## 14. Yaya Touré — C+

**Filosofía:** Estructura sólida con mediocentros potentes y mucha carga física (modelo de autoría: la trayectoria como entrenador es corta).  
**Identidad táctica:** Equipo ordenado con mediocampo fuerte y llegada desde segunda línea.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 66 · Attack 68 · Defense 66 · Transitions 64 · Adaptability 66 · PlayerDevelopment 64 · Management 72 · Identity 60

**Compatibilidad:** roles [Engine 90, Anchor 85, Builder 80, Explosive 75, Finisher 65] · zonas [Midfield 95, Defense 70, Attack 60, Wing 55] · atributos [Strength 2, Stamina 2, Passing 1.5, BallControl 1.5, Shooting 1, Dribbling 1] · riesgo 50 (peso 30) · creatividad 55 (peso 15) · agresividad 60 (peso 30)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Mediocampo Sólido** — Doble pivote potente y un delantero que remata, con dos centrales firmes. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Midfield/Builder · Attack/Finisher | Balanced · Balanced · MidBlock · Controlled | 50 · 50 · 45 · 50 |

- **FACT:** Fue ayudante en varios clubes y en la selección de Arabia Saudí y en junio de 2026 asumió su primer puesto de primer entrenador, en el Slovan Bratislava, al que clasificó para la Champions League.
- **AUTHORING:** La valoración corresponde a su trayectoria como ENTRENADOR, no a su carrera como jugador. La formación y el perfil de compatibilidad son un modelo de AUTORÍA (hay poca evidencia táctica propia); grado C+.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Rodri 87→88 (85); Declan Rice 85→86 (83); Federico Valverde 85→86 (82); Sandro Tonali 85→86 (82)

## 15. Hajime Moriyasu — B+

**Filosofía:** Intensidad y energía con un sistema flexible: estructura de tres atrás o 4-2-3-1 según el rival, muchos cambios desde el banquillo.  
**Identidad táctica:** Camaleón táctico: presión intensa, transiciones rápidas y extremos altos y abiertos.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 82 · Attack 80 · Defense 82 · Transitions 86 · Adaptability 84 · PlayerDevelopment 80 · Management 84 · Identity 86

**Compatibilidad:** roles [Engine 95, Winger 85, Explosive 80, Creator 70, Guardian 70, Builder 70] · zonas [Midfield 90, Wing 85, Attack 65, Defense 65] · atributos [Stamina 3, Speed 1.5, Acceleration 1.5, Agility 1.5, Passing 1] · riesgo 50 (peso 30) · creatividad 60 (peso 20) · agresividad 65 (peso 40)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Tres Atrás con Alas** — Tres defensas con un medio que se suma a la línea y un creador arriba, inspirado en el 3-4-2-1. | 3-1-1 | Three | Defense/Guardian · Defense/Anchor · Defense/Builder · Midfield/Engine · Attack/Creator | Balanced · Balanced · Selective · Fast | 65 · 65 · 48 · 60 |
| **4-2-3-1 Intenso** — Doble línea de cuatro adaptada: pivote, extremo veloz y un delantero. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Wing/Winger · Attack/Finisher | Balanced · FastVertical · HighPress · Fast | 65 · 70 · 52 · 70 |

- **FACT:** Seleccionador de Japón desde 2018; análisis de 2026 describen un 4-2-3-1 base con cambio a 3-4-2-1 según el rival, presión intensa y la estrategia del "súper suplente".
- **INFERENCE:** FS27 lo traduce en una variante de tres defensas (justificada por el 3-4-2-1) y en Engine / Winger / Explosive con mucha resistencia.
- **AUTHORING:** Grado B+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Achraf Hakimi 87→88 (87); Federico Valverde 85→86 (87); Nicolò Barella 86→87 (87); Dani Muñoz 83→84 (85)

## 16. Massimiliano Allegri — A-

**Filosofía:** Pragmatismo: solidez defensiva, control del riesgo y adaptación durante el partido.  
**Identidad táctica:** Equipo ordenado que se protege bien y decide los partidos con eficacia.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 88 · Attack 76 · Defense 94 · Transitions 84 · Adaptability 90 · PlayerDevelopment 80 · Management 88 · Identity 88

**Compatibilidad:** roles [Anchor 95, Wall 90, Guardian 90, Builder 80, Finisher 75, Target 65] · zonas [Defense 95, Midfield 80, Attack 60, Wing 50] · atributos [Defense 2.5, Passing 1.5, Strength 1.5, Technique 1, BallControl 1] · riesgo 25 (peso 60) · creatividad 50 (peso 20) · agresividad 55 (peso 30)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **3-5-2 Pragmático** — Tres defensas, un mediocentro constructor y un delantero. | 3-1-1 | Three | Defense/Wall · Defense/Guardian · Defense/Anchor · Midfield/Builder · Attack/Finisher | Defensive · Balanced · LowBlock · Counter | 45 · 45 · 28 · 30 |
| **Bloque Bajo Italiano** — Cuatro jugadores muy juntos delante de los centrales y un punta de referencia. | 2-2-1 | Two | Defense/Guardian · Defense/Wall · Midfield/Anchor · Midfield/Engine · Attack/Target | Defensive · Direct · LowBlock · Counter | 40 · 45 · 25 · 25 |
| **Contraataque Medido** — Equilibrio y salida medida con extremo y goleador. | 2-1-2 | Two | Defense/Wall · Defense/Anchor · Midfield/Builder · Wing/Winger · Attack/GoalHunter | Balanced · Balanced · MidBlock · Counter | 50 · 50 · 35 · 35 |

- **FACT:** Cinco Scudetti seguidos con la Juventus; con el Milan usó el 3-5-2 como sistema base y, según 2026, pasó al Nápoles en julio de ese año.
- **FACT:** Con el Milan el 3-5-2 funcionaba como un 5-4-1 en bloque bajo sin balón y un 3-2-4-1 con balón.
- **INFERENCE:** Línea de tres justificada por esa evidencia; FS27 lo traduce en Anchor / Wall / Guardian y defensa.
- **AUTHORING:** Grado A-: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Virgil van Dijk 86→87 (89); William Saliba 88→89 (88); Rodri 87→88 (85); Rúben Dias 83→84 (84)

## 17. Roberto Mancini — A-

**Filosofía:** Fútbol técnico y equilibrado con presión alta, interiores que asocian y extremos de calidad.  
**Identidad táctica:** Equilibrio con posesión y verticalidad, muy apoyado en la técnica de los interiores.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 86 · Attack 84 · Defense 80 · Transitions 80 · Adaptability 86 · PlayerDevelopment 82 · Management 84 · Identity 82

**Compatibilidad:** roles [Builder 90, Creator 80, Architect 80, Anchor 70, Winger 70, Finisher 65] · zonas [Midfield 95, Wing 65, Attack 60, Defense 50] · atributos [Passing 2, Technique 2, BallControl 2, Dribbling 1, Stamina 1] · riesgo 55 (peso 30) · creatividad 70 (peso 40) · agresividad 50 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Interiores Técnicos** — Interiores que combinan y un extremo que desequilibra. | 1-2-2 | One | Defense/Anchor · Midfield/Builder · Midfield/Architect · Wing/Winger · Attack/Finisher | Attacking · Short · HighPress · PossessionRecovery | 60 · 55 · 55 · 70 |
| **Juego Asociativo Equilibrado** — Doble pivote técnico y un creador cerca del delantero. | 2-2-1 | Two | Defense/Guardian · Defense/Anchor · Midfield/Builder · Midfield/Creator · Attack/GoalHunter | Balanced · Positional · Selective · Controlled | 50 · 50 · 45 · 55 |
| **Presión y Verticalidad** — Pivote motor y un extremo que ataca el espacio. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Engine · Wing/Winger · Attack/Finisher | Attacking · Balanced · HighPress · Fast | 65 · 65 · 58 · 70 |

- **FACT:** Campeón de Europa 2021 con Italia, Premier League 2012 con el Manchester City; vuelve como seleccionador de Italia en julio de 2026.
- **INFERENCE:** FS27 lo traduce en Builder / Creator / Architect y técnica.
- **AUTHORING:** Grado A-: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Frenkie de Jong 87→88 (89); Pedri 88→89 (89); Vitinha 86→87 (88); Rodri 87→88 (87)

## 18. Louis van Gaal — A

**Filosofía:** Sistema estricto de posesión con triángulos, estructura clara y disciplina táctica.  
**Identidad táctica:** Posesión controlada y roles muy definidos; el sistema por encima de las individualidades.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 94 · Attack 80 · Defense 86 · Transitions 80 · Adaptability 76 · PlayerDevelopment 92 · Management 86 · Identity 96

**Compatibilidad:** roles [Architect 95, Builder 90, Anchor 85, Guardian 80, Target 70, Winger 70] · zonas [Midfield 95, Defense 75, Wing 65, Attack 60] · atributos [Passing 2.5, BallControl 2, Technique 2, Strength 1] · riesgo 40 (peso 50) · creatividad 60 (peso 30) · agresividad 55 (peso 20) · cuerpo Tall

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Sistema de Tres** — Tres defensas que construyen, un medio que ordena y un punta de referencia. | 3-1-1 | Three | Defense/Wall · Defense/Guardian · Defense/Builder · Midfield/Architect · Attack/Target | Balanced · Positional · Selective · Controlled | 50 · 45 · 38 · 50 |
| **Posesión Estructurada** — Posesión con pivote organizador y extremo abierto. | 2-1-2 | Two | Defense/Guardian · Defense/Anchor · Midfield/Architect · Wing/Winger · Attack/Target | Balanced · Positional · MidBlock · PossessionRecovery | 60 · 40 · 38 · 55 |
| **Triángulos de Ajax** — Interiores y extremo en triángulos de posesión. | 1-2-2 | One | Defense/Anchor · Midfield/Architect · Midfield/Builder · Wing/Winger · Attack/GoalHunter | Attacking · Positional · Selective · PossessionRecovery | 55 · 45 · 45 · 65 |

- **FACT:** Champions League 1995 con el Ajax; también Barcelona y Bayern; con Países Bajos fue tercero en el Mundial 2014 usando una línea de tres.
- **INFERENCE:** Línea de tres justificada por esa evidencia; FS27 lo traduce en Architect / Builder / Anchor.
- **AUTHORING:** Grado A: juicio de autoría. La preferencia por cuerpos Tall es una inferencia.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Martin Ødegaard 88→89 (87); Rodri 87→88 (86); Enzo Fernández 86→87 (86); Frenkie de Jong 87→88 (85)

## 19. Manuel Pellegrini — B+

**Filosofía:** Fútbol ofensivo y técnico con posesión y mucha creación.  
**Identidad táctica:** Ataque asociativo y generoso con interiores creativos.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 86 · Attack 88 · Defense 74 · Transitions 80 · Adaptability 82 · PlayerDevelopment 84 · Management 84 · Identity 84

**Compatibilidad:** roles [Creator 90, Architect 80, Builder 80, Winger 70, Finisher 65] · zonas [Midfield 95, Wing 65, Attack 65, Defense 45] · atributos [Passing 2, Technique 2, BallControl 2, Dribbling 1.5, Shooting 1] · riesgo 65 (peso 40) · creatividad 75 (peso 50) · agresividad 40 (peso 20)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Fútbol Ofensivo Técnico** — Doble pivote técnico, creador y delantero. | 2-2-1 | Two | Defense/Guardian · Defense/Builder · Midfield/Architect · Midfield/Creator · Attack/Finisher | Attacking · Short · MidBlock · Controlled | 55 · 55 · 62 · 60 |
| **Interiores Ofensivos** — Un defensa, interiores creativos y un extremo. | 1-2-2 | One | Defense/Anchor · Midfield/Builder · Midfield/Creator · Wing/Winger · Attack/GoalHunter | VeryAttacking · Short · Selective · Controlled | 65 · 55 · 68 · 65 |

- **FACT:** Premier League 2014 con el Manchester City; también Villarreal, Real Madrid y Betis.
- **INFERENCE:** FS27 lo traduce en Creator / Architect / Builder y técnica.
- **AUTHORING:** Grado B+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Pedri 88→89 (90); Cole Palmer 86→87 (87); Martin Ødegaard 88→89 (87); Jamal Musiala 87→88 (86)

## 20. Mauricio Pochettino — B+

**Filosofía:** Presión alta física, intensidad atlética y desarrollo de jóvenes.  
**Identidad táctica:** Equipo atlético, vertical y muy intenso sin balón.

Valoraciones de apoyo (no calculan el grado): TacticalIQ 84 · Attack 82 · Defense 80 · Transitions 80 · Adaptability 86 · PlayerDevelopment 88 · Management 82 · Identity 86

**Compatibilidad:** roles [Engine 95, Explosive 85, Anchor 80, Guardian 80, Winger 75, Finisher 70] · zonas [Midfield 90, Wing 75, Defense 75, Attack 65] · atributos [Stamina 3, Speed 1.5, Strength 1.5, Acceleration 1.5, Defense 1] · riesgo 60 (peso 30) · creatividad 50 (peso 15) · agresividad 80 (peso 60)

| Formación | Base | Línea | Slots (zona/rol) | Mentalidad · Construcción · Presión · Transición | Ancho · Ritmo · Riesgo · Línea |
|---|---|---|---|---|---|
| **Presión Física** — Defensas atléticos, un motor y un extremo explosivo. | 2-1-2 | Two | Defense/Wall · Defense/Anchor · Midfield/Engine · Wing/Explosive · Attack/GoalHunter | Attacking · FastVertical · HighPress · Fast | 60 · 70 · 60 · 75 |
| **Doble Pivote Atlético** — Doble pivote potente y un delantero que remata. | 2-2-1 | Two | Defense/Guardian · Defense/Wall · Midfield/Engine · Midfield/Anchor · Attack/Finisher | Balanced · Direct · Selective · Fast | 50 · 65 · 50 · 60 |

- **FACT:** Final de la Champions League 2019 con el Tottenham; también PSG y Chelsea; seleccionador de Estados Unidos.
- **INFERENCE:** FS27 lo traduce en Engine / Explosive y resistencia, fuerza y velocidad.
- **AUTHORING:** Grado B+: juicio de autoría.

*Ejemplos de Coach Boost (+1) con la biblioteca actual:* Federico Valverde 85→86 (88); Declan Rice 85→86 (86); Sandro Tonali 85→86 (86); Nicolò Barella 86→87 (86)

