# FS27 — DATA LOCK (Fase 5)

Cierra la fuente de verdad de los datos: jugadores, identidades, skills, equipos, formaciones, coaches, árbitros y dificultad. **No hay IA, tácticas de gameplay, árbitro funcional, Unity ni mecánicas nuevas.** Todo es dato y validadores; los contratos existentes se mantienen. Detalle auditable: [DATA_LOCK_IDENTITIES.md](DATA_LOCK_IDENTITIES.md) y [DATA_LOCK_SKILLS.md](DATA_LOCK_SKILLS.md).

## 1. Fuente de verdad
`GameDatabaseLoader.Load(readSource)` (carpeta `Core/DataLock/`) construye **la base cerrada** (`GameDatabase`). Core no lee archivos: el host entrega el texto de cada fuente lógica (`GameDatabaseSources`).

| Dato | Fuente | Quién la lee |
|---|---|---|
| Jugadores (160 = 24 porteros + 136 de campo) | Bases de authoring `Docs/authoring/FS27_*_DATABASE_V*.md` + correcciones (sin cambios) | `AuthoredPlayerLibrary` (importadores existentes) |
| Identidades publicadas (185) | `FS27_IDENTITIES_V1.json` | `IdentityJson` / `IdentityApplier` |
| Skills (catálogo) | `FS27_SKILLS_V1.json` | `SkillCatalogJson` |
| Skills de cada jugador | se **calculan** al cargar (etiquetas de firma + atributos + roles) | `SkillAssigner` |
| Equipos (12) y su formación | `FS27_TEAMS_V1.json` (ahora con `formationId`) | `TeamCatalogJson` |
| Coaches (20) y sus variantes | `FS27_COACHES_V1.json` | `CoachJson` |
| Árbitros (5) | `FS27_REFEREES_V1.json` | `RefereeJson` |
| Formaciones base (4) | `DefaultFormations.CreateExtended()` | `FormationLibrary` |
| Dificultad (5 niveles) | `DefaultDifficulties` | `DifficultyLibrary` |

Los archivos de investigación (`*_DATABASE_*.md`, `*_RECORDS_*.json`) **no se tocan**: son el histórico. Los nombres reales siguen ahí; el juego publica los de `FS27_IDENTITIES_V1.json`.

### Discrepancias con la Auditoría Maestra (el repositorio real manda)
| Auditoría | Realidad | Qué se hizo |
|---|---|---|
| 60 variantes de formación de coach (20 × 3) | **53** (14 coaches × 3, 5 × 2, 1 × 1): ya siguen la regla por grado | Nada que cambiar; un test lo fija |
| 46 jugadores con pie débil exactamente 50, "puede ser un default" | **Ninguno es un 50 escrito**: los 46 son valores ausentes (24 porteros + 22 defensas de campo) que el importador rellenó | Marcados como neutrales no verificados |
| 258 etiquetas distintas | Cierto, pero ya existía una capa que las normaliza a **160** etiquetas conceptuales por base de datos | Se reutilizó esa capa; no se creó otra |
| `FormationLibrary.CreateDefault()` = formaciones base | Solo trae 3 (falta `3-1-1`) | La base cerrada usa `CreateExtended()` (4) |

## 2. Estructura de un jugador
`PlayerDefinition` (id, nombre, `ShortName`, dorsal, rol grueso, 12 atributos, pie, pie débil, tipo de cuerpo, edad, altura, peso, nacionalidad) **no cambia**. Lo que se añade vive al lado, por id: `PlayerPlayingProfile` (zonas, roles oficiales, riesgo, creatividad, agresividad), `GoalkeeperProfile` (porteros), `PlayerSkillSet` (jugadores de campo) y `PlayerDataProvenance` (qué es real y qué es neutral). Los 12 roles oficiales no cambian (Wall, Guardian, Anchor, Builder, Creator, Architect, Engine, Winger, Explosive, Finisher, GoalHunter, Target); no se reactivó ninguno retirado.

## 3. Nombres (política definitiva)
- FS27 **publica solo identidades ficticias**. Los nombres reales de futbolistas, entrenadores y árbitros quedan únicamente en los archivos de investigación y en los ids internos.
- **Estrategia** (`IdentityGenerator`, determinista, sin azar de tiempo): la nacionalidad elige una de 16 culturas de nombre (`NamePools`); un generador sembrado con el id (FNV-1a) toma nombre y apellido de esa cultura y repite el sorteo hasta que (a) el apellido no se use en toda la base, (b) el nombre completo no sea el original, (c) ninguna palabra de un nombre de la investigación se use como apellido y (d) el nombre de pila no se repita más de dos veces. Español y latinoamericano llevan segundo apellido en el nombre completo; los árbitros se muestran por apellido (como ya hacían sus datos) con código de 3 letras.
- **Editable**: cada entrada de `FS27_IDENTITIES_V1.json` es texto. Una entrada con `"edited": true` el generador no la toca y su nombre queda reservado. Un test comprueba que el archivo es exactamente lo que genera el generador (salvo editadas).
- **Ids**: no cambian (`cb-...`, `coach-...`, `referee-...`). Son internos y **nunca se muestran**. Ver pendientes.
- **Relación con el original**: la tabla completa (id, nombre de investigación, nombre final) está en [DATA_LOCK_IDENTITIES.md](DATA_LOCK_IDENTITIES.md). Es solo referencia de authoring.

## 4. ShortName
`ShortNameRule` (determinista, Unicode seguro: cuenta y corta caracteres visibles, no unidades UTF-16):
1. Una sola palabra → esa palabra. 2. Nombre compuesto → el apellido (última palabra más partículas previas: "van der Berg", "de la Cruz"); si no cabe en 12, solo la última palabra; si no cabe, se corta. 3. Si otra persona ya lo tiene (sin distinguir mayúsculas ni acentos): inicial ("T. Berg"), dos letras, número. Nunca vacío, máximo 12 caracteres, único entre jugadores y entre coaches. Los árbitros conservan su convención de 3 letras mayúsculas, únicas.
Resultado real: 0 jugadores con `ShortName` vacío (había 86) y 0 repetidos. Si una identidad llegara con el corto vacío, el cargador lo deriva con la misma regla.

## 5. Pie débil y procedencia
`PlayerDataProvenance` (construido de lo que los importadores tuvieron que rellenar, no de suposiciones):
- **Authored**: la fuente lo escribe. 114 jugadores de campo.
- **NeutralUnverified**: la fuente no lo trae; se conserva el neutro 50, **sin inventar precisión**. 22 jugadores de campo (todos los defensas zurdos y dos laterales). Para resolverlos basta añadir el dato a la tabla de correcciones documentadas (`AuthoringCorrections` ya admite `weakFootQuality`).
- **NotApplicable**: los 24 porteros. Conservan su lógica de pie dominante; el proceso no les toca ningún valor.
Otros valores neutrales detectados (informativo):

- **weakFootQuality**: 22 valores neutros no verificados — `cb-alessandro-bastoni`, `cb-aymeric-laporte`, `cb-calvin-bassey`, `cb-castello-lukeba`, `cb-daley-blind`, `cb-david-hancko`, `cb-dean-huijsen`, `cb-gabriel-magalhaes`, `cb-gerard-martin`, `cb-josko-gvardiol`, `cb-lisandro-martinez`, `cb-lucas-hernandez`, `cb-marc-guehi`, `cb-micky-van-de-ven`, `cb-nico-schlotterbeck`, `cb-piero-hincapie`, `cb-renato-veiga`, `cb-riccardo-calafiori`, `cb-sven-botman`, `cb-willian-pacho`, `fb-andy-robertson`, `fb-marcos-llorente`.
- **shirtNumber**: 23 valores neutros no verificados — `cb-alessandro-bastoni`, `cb-aymeric-laporte`, `cb-calvin-bassey`, `cb-castello-lukeba`, `cb-daley-blind`, `cb-david-hancko`, `cb-dean-huijsen`, `cb-gabriel-magalhaes`, `cb-gerard-martin`, `cb-josko-gvardiol`, `cb-lisandro-martinez`, `cb-lucas-hernandez`, `cb-marc-guehi`, `cb-micky-van-de-ven`, `cb-nico-schlotterbeck`, `cb-piero-hincapie`, `cb-renato-veiga`, `cb-riccardo-calafiori`, `cb-sven-botman`, `cb-willian-pacho`, `dc-karim-adeyemi`, `dc-pierre-emerick-aubameyang`, `dc-romelu-lukaku`.
- **heightCm**: 4 valores neutros no verificados — `dc-karim-adeyemi`, `sd-michael-olise`, `wg-antoine-semenyo`, `wg-giuliano-simeone`.
- **weightKg**: 42 valores neutros no verificados.

## 6. Atributos (sin rebalanceo)
`AttributeAuditor` revisa a los 136 jugadores de campo (los porteros quedan fuera a propósito):

| Atributo | Mín | Máx | Rango | Media | Desv. típica |
|---|---|---|---|---|---|
| Speed | 67 | 97 | 30 | 81.4 | 5.6 |
| Acceleration | 65 | 97 | 32 | 82.1 | 5.9 |
| Agility | 63 | 94 | 31 | 82.2 | 5.8 |
| Strength | 54 | 94 | 40 | 77.8 | 8.4 |
| Stamina | 73 | 95 | 22 | 85.9 | 4.2 |
| Shooting | 45 | 94 | 49 | 71.3 | 12.9 |
| Finishing | 49 | 96 | 47 | 70.1 | 11.3 |
| Passing | 72 | 95 | 23 | 82.5 | 5.1 |
| Control | 74 | 93 | 19 | 84.4 | 4.4 |
| Dribbling | 65 | 95 | 30 | 81.7 | 6.4 |
| Technique | 73 | 93 | 20 | 84.8 | 4.9 |
| Defense | 31 | 90 | 59 | 70.9 | 14.7 |

- **Authoring real**: ningún jugador de campo tiene los 12 atributos por defecto, ni dos tienen el mismo vector, ni valores fuera de 1–99, ni 7 o más atributos con el mismo valor.
- **Comprimidos** (rango < 30, **solo informativo**): Stamina 73–95, Passing 72–95, Control 74–93, Technique 73–93 (Speed 67–97 queda justo en 30). Es asunto de la fase de balance; no se tocó ningún valor.
- **Porteros**: sus 12 atributos de campo son el neutro por diseño (70) y su calidad es su `GoalkeeperProfile`. `AttributeAuditor.HasAuthoredFieldAttributes(player)` es el contrato para que ningún consumidor (estadísticas, balance, calidad de roster) lea esos 70 como datos reales; el validador falla si un portero tuviera atributos de campo "escritos".

## 7. Skills
Sistema existente (`SkillDefinition` / `PlayerSkillSet`); se **extendió** de forma aditiva (familia, tipo, acción que desbloquea, roles, requisitos, etiquetas de origen). `PlayerDefinition` sigue sin llevar skills: se consultan por id (`IPlayerSkillLookup`).
- **Tres familias** y los tipos del contrato anterior (Dribble, SpecialPass, SpecialShot = técnicas especiales) más los nuevos (Passing, Shooting, FirstTouch, Technique | Acceleration, Agility, BallCarrying, OffBall | Interception, Tackle, Block, Pressure, Recovery, Aerial). Portería: sistema propio, ninguna capacidad de portero es skill.
- **Catálogo**: 37 skills (17 de balón, 10 de movimiento, 10 defensivas) que agrupan las 160 etiquetas. 2 etiquetas se difieren a propósito porque dependen de reglas que no existen (fuera de juego, faltas).
- **Acciones**: solo 3 skills-tipo desbloquean acciones existentes: `SkillMove` (regates), `SkillLobPass` (cambio de orientación y pase creativo), `CurvedShot` (disparo con efecto). El resto son capacidades pasivas que leerá la IA. `GameDatabase.UnlockedActions(playerId)` es el permiso (`SkillGranted`) que piden las acciones; un test lo integra con `MatchController`.
- **Asignación** (`SkillAssigner`): (1) las etiquetas del jugador, en el orden de su ficha, se convierten en skills; (2) solo se da si cumple **todos** los mínimos de atributo **y** (si la skill nombra roles) su perfil tiene alguno; el Overall no interviene; (3) máximo 4; (4) si se queda sin ninguna, recibe **una** derivada: la que supera con mayor margen. Todo rechazo queda registrado. Resultado: 136 jugadores de campo con set; reparto 1 skill = 12, 2 = 12, 3 = 36, 4 = 76 jugadores; 447 asignadas, 1 rechazada por atributo, 39 recortadas por el tope, 1 derivada.
- `Reaction` no es skill ni requisito (el lector lo rechaza).

## 8. Equipos y formaciones
- Cada equipo tiene `formationId`: la variante de su coach (id de variante) o, sin coach, una formación base. La regla (`TeamFormationAssigner`, sin nombres, solo ids y niveles): Elite y Strong juegan la **primera variante** de su coach (su seña); Balanced y Modest, la de **menor riesgo** (empates: línea defensiva más baja, luego la primera). Un equipo sin coach toma `2-1-2`. El número de variantes por coach ya lo fija su grado en los datos (A+/A/A- = 3, B+/B/B- = 2, C+/C = 1) y `CoachValidator` lo exige.
- "Mi Equipo": Balanced, nunca Elite, **sin coach**, formación `2-1-2` válida; se puede cambiar a cualquier otra base o variante (`TeamFormationResolver`).
- **Identidad táctica inicial** (`TeamTacticalIdentity`, solo lectura): mentalidad, construcción, presión, transición, ancho, tempo, riesgo, línea defensiva y tipo de línea, leídos de la variante (o neutros sin coach). Es dato; ninguna IA lo usa todavía.

| Equipo | Tipo | Nivel | Coach (id) | Formación (id) | Base | Mentalidad | Construcción | Presión | Transición | Ancho | Tempo | Riesgo | Línea def. |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Solaris Meridian (`team-solaris-meridian`) | Official | Elite | `coach-pep-guardiola` | `coach-pep-guardiola-control-posicional` | 2-1-2 | Attacking | Positional | HighPress | PossessionRecovery | 70 | 45 | 55 | 80 |
| Obsidian Bastion (`team-obsidian-bastion`) | Official | Elite | `coach-diego-simeone` | `coach-diego-simeone-bloque-bajo-de-tres` | 3-1-1 | Defensive | Direct | LowBlock | Counter | 40 | 60 | 25 | 25 |
| Vortex Rapids (`team-vortex-rapids`) | Official | Strong | `coach-hansi-flick` | `coach-hansi-flick-linea-alta-y-presion` | 2-1-2 | Attacking | FastVertical | AggressivePress | Fast | 65 | 80 | 68 | 90 |
| Emerald Compass (`team-emerald-compass`) | Official | Strong | `coach-roberto-mancini` | `coach-roberto-mancini-interiores-tecnicos` | 1-2-2 | Attacking | Short | HighPress | PossessionRecovery | 60 | 55 | 55 | 70 |
| Gilded Falcons (`team-gilded-falcons`) | Official | Strong | `coach-zinedine-zidane` | `coach-zinedine-zidane-mediocampo-de-calidad` | 2-2-1 | Balanced | Short | MidBlock | Controlled | 55 | 45 | 45 | 50 |
| Northwind Union (`team-northwind-union`) | Official | Balanced | `coach-lionel-scaloni` | `coach-lionel-scaloni-bloque-colectivo` | 2-2-1 | Balanced | Balanced | MidBlock | Controlled | 50 | 50 | 42 | 55 |
| Cobalt Syndicate (`team-cobalt-syndicate`) | Official | Balanced | `coach-mikel-arteta` | `coach-mikel-arteta-bloque-medio-compacto` | 2-2-1 | Balanced | Balanced | MidBlock | Controlled | 45 | 45 | 35 | 60 |
| Ashen Foundry (`team-ashen-foundry`) | Official | Balanced | `coach-massimiliano-allegri` | `coach-massimiliano-allegri-bloque-bajo-italiano` | 2-2-1 | Defensive | Direct | LowBlock | Counter | 40 | 45 | 25 | 25 |
| Tidal Phoenix (`team-tidal-phoenix`) | Official | Balanced | `coach-thomas-tuchel` | `coach-thomas-tuchel-tres-atras-flexible` | 3-1-1 | Balanced | Balanced | HighPress | Controlled | 50 | 60 | 45 | 60 |
| Ember Rovers (`team-ember-rovers`) | Official | Modest | `coach-mauricio-pochettino` | `coach-mauricio-pochettino-doble-pivote-atletico` | 2-2-1 | Balanced | Direct | Selective | Fast | 50 | 65 | 50 | 60 |
| Silver Thistle (`team-silver-thistle`) | Official | Modest | `coach-manuel-pellegrini` | `coach-manuel-pellegrini-futbol-ofensivo-tecnico` | 2-2-1 | Attacking | Short | MidBlock | Controlled | 55 | 55 | 62 | 60 |
| Mi Equipo (`team-user`) | User | Balanced | — | `2-1-2` | 2-1-2 | Balanced | Balanced | MidBlock | Controlled | 50 | 50 | 50 | 50 |

## 9. Coaches y Coach Boost
| Coach (id) | Nombre publicado | Grado | Variantes | Ids de variante |
|---|---|---|---|---|
| `coach-pep-guardiola` | Efrén Espinosa | APlus | 3 | `coach-pep-guardiola-control-posicional`, `coach-pep-guardiola-superioridad-interior`, `coach-pep-guardiola-falso-nueve-y-pivote` |
| `coach-hansi-flick` | Matthias Vogelsang | A | 3 | `coach-hansi-flick-linea-alta-y-presion`, `coach-hansi-flick-doble-pivote-vertical`, `coach-hansi-flick-presion-total` |
| `coach-mikel-arteta` | Octavio Galindo | A | 3 | `coach-mikel-arteta-estructura-de-posesion`, `coach-mikel-arteta-lateral-interior`, `coach-mikel-arteta-bloque-medio-compacto` |
| `coach-carlo-ancelotti` | Alberto Tedesco | APlus | 3 | `coach-carlo-ancelotti-equilibrio-fluido`, `coach-carlo-ancelotti-libertad-controlada`, `coach-carlo-ancelotti-contraataque-adaptado` |
| `coach-lionel-scaloni` | Facundo Peralta | A | 3 | `coach-lionel-scaloni-bloque-colectivo`, `coach-lionel-scaloni-medio-campo-intenso`, `coach-lionel-scaloni-campeon-compacto` |
| `coach-diego-simeone` | Baltasar Yrigoyen | APlus | 3 | `coach-diego-simeone-bloque-bajo-de-tres`, `coach-diego-simeone-bloque-compacto-4-4-2`, `coach-diego-simeone-contra-intenso` |
| `coach-thomas-tuchel` | Ansgar Eberhard | AMinus | 3 | `coach-thomas-tuchel-tres-atras-flexible`, `coach-thomas-tuchel-doble-pivote-vertical`, `coach-thomas-tuchel-salida-adaptable` |
| `coach-jose-mourinho` | Salvador Camarinha | A | 3 | `coach-jose-mourinho-muro-de-tres`, `coach-jose-mourinho-bloque-medio-de-transicion`, `coach-jose-mourinho-contragolpe-eficiente` |
| `coach-luis-de-la-fuente` | Hilario Montalvo | AMinus | 3 | `coach-luis-de-la-fuente-extremos-abiertos`, `coach-luis-de-la-fuente-interiores-y-extremos`, `coach-luis-de-la-fuente-control-equilibrado` |
| `coach-zinedine-zidane` | Oscar Gaillard | A | 3 | `coach-zinedine-zidane-mediocampo-de-calidad`, `coach-zinedine-zidane-libertad-para-las-estrellas`, `coach-zinedine-zidane-equilibrio-de-plantilla` |
| `coach-cesc-fabregas` | Pelayo Olmedo | B | 2 | `coach-cesc-fabregas-juego-de-posicion-joven`, `coach-cesc-fabregas-salida-corta` |
| `coach-luis-enrique` | Néstor Delgado | APlus | 3 | `coach-luis-enrique-presion-tras-perdida`, `coach-luis-enrique-rotacion-en-el-medio`, `coach-luis-enrique-presion-y-posesion` |
| `coach-vincent-kompany` | Pascal Pelletier | BPlus | 2 | `coach-vincent-kompany-linea-alta-agresiva`, `coach-vincent-kompany-presion-hombre-a-hombre` |
| `coach-yaya-toure` | Famara Abena | CPlus | 1 | `coach-yaya-toure-mediocampo-solido` |
| `coach-hajime-moriyasu` | Ren Hayashida | BPlus | 2 | `coach-hajime-moriyasu-tres-atras-con-alas`, `coach-hajime-moriyasu-4-2-3-1-intenso` |
| `coach-massimiliano-allegri` | Tullio Landolfi | AMinus | 3 | `coach-massimiliano-allegri-3-5-2-pragmatico`, `coach-massimiliano-allegri-bloque-bajo-italiano`, `coach-massimiliano-allegri-contraataque-medido` |
| `coach-roberto-mancini` | Massimo Ruggeri | AMinus | 3 | `coach-roberto-mancini-interiores-tecnicos`, `coach-roberto-mancini-juego-asociativo-equilibrado`, `coach-roberto-mancini-presion-y-verticalidad` |
| `coach-louis-van-gaal` | Freek Hendriks | A | 3 | `coach-louis-van-gaal-sistema-de-tres`, `coach-louis-van-gaal-posesion-estructurada`, `coach-louis-van-gaal-triangulos-de-ajax` |
| `coach-manuel-pellegrini` | Pablo Oyarzún | BPlus | 2 | `coach-manuel-pellegrini-futbol-ofensivo-tecnico`, `coach-manuel-pellegrini-interiores-ofensivos` |
| `coach-mauricio-pochettino` | Abel Insúa | BPlus | 2 | `coach-mauricio-pochettino-presion-fisica`, `coach-mauricio-pochettino-doble-pivote-atletico` |

Coach Boost (regla de la arquitectura, **sin cambios**): compatibilidad ≥ 80 → +1, < 80 → +0, nunca −1, máximo +1. `CoachBoostRules` es el único sitio donde viven esos números; `EffectiveOverall = BaseOverall + CoachBoost` (tope 99) se calcula **a petición** (`EffectiveRating`) y nunca se guarda: `BaseOverall` no cambia y el boost no es un atributo permanente. Tests: ningún código de Match, Actions, Ball, Locomotion, Ai, Difficulty ni Teams lo usa.
**Quién podrá consumir `EffectiveOverall` en el futuro**: la fase de balance (fuerza de equipo en simulaciones masivas), la IA de decisión **si** se decide que la calidad percibida lo usa, la competición y la UI (fichas). La simulación de partido no debe leerlo hasta que se decida explícitamente.

## 10. Dificultad (5 niveles, 8 grupos)
Nombres: Novato, Amateur, Profesional, Experto, Élite (no cambian). Cada nivel tiene 8 grupos de parámetros de **comportamiento**, no multiplicadores de estadísticas (un test impide campos "bonus"/"multiplier"). `DifficultyValidator.ValidateSet` exige rangos, límites humanos y orden entre niveles.

| Nivel | Id | Decisión (calidad) | Reacción base (s) | Posicionamiento (calidad) | Anticipación (calidad) | Presión (intensidad) | Ejecución (calidad) | Coordinación | Portero |
|---|---|---|---|---|---|---|---|---|---|
| Novato (Novice) | `novice` | 0.35 | 0.5 | 0.3 | 0.2 | 0.5 | 0.35 | 5 valores | 7 valores |
| Amateur (Amateur) | `amateur` | 0.5 | 0.4 | 0.5 | 0.4 | 0.5 | 0.5 | 5 valores | 7 valores |
| Profesional (Professional) | `professional` | 0.65 | 0.32 | 0.65 | 0.6 | 0.55 | 0.65 | 5 valores | 7 valores |
| Experto (Expert) | `expert` | 0.8 | 0.26 | 0.8 | 0.78 | 0.6 | 0.8 | 5 valores | 7 valores |
| Élite (Elite) | `elite` | 0.92 | 0.21 | 0.92 | 0.9 | 0.6 | 0.9 | 5 valores | 7 valores |

Mapping previsto grupo → futura IA (documentado, no implementado):

| Grupo | Lo consumirá | Modelo ya existente |
|---|---|---|
| Decisión | motor de decisión de ataque y defensa (fases 10–11) | `AiSkillResolver` |
| Reacción | percepción y dirección (fase 8) | `ReactionModel`, `ObservationDelayBuffer` |
| Posicionamiento | formaciones y posicionamiento (fase 9) | `PositioningModel` |
| Anticipación | percepción (fase 8) y defensa (fase 11) | `AnticipationModel` |
| Presión | IA defensiva (fase 11) | `PressureModel` |
| Ejecución | error de las acciones (fase 14): **un solo** modelo de error, hoy hay dos (`ActionModel.KickError` y `AiErrorModel`) | `AiErrorModel` |
| Coordinación | tácticas de equipo (fase 13) | — |
| Portero | IA de portero (fase 12) | `GoalkeeperSkillModel` |

Propuesta de etiquetas de menú (pendiente de decidir): Fácil = Amateur, Normal = Profesional, Difícil = Experto; Novato y Élite como extremos.

## 11. Árbitros
| Árbitro (id) | Nombre | Corto | Estilo | Rigor | Tarjetas | Ventaja | Control | Penaltis | Consistencia | Comunicación |
|---|---|---|---|---|---|---|---|---|---|---|
| `referee-szymon-marciniak` | Andrei Balog | BAL | BalancedControl | 55 | 50 | 80 | 95 | 55 | 90 | 90 |
| `referee-michael-oliver` | Monty Calloway | CAL | PermissiveFlow | 45 | 45 | 90 | 88 | 50 | 88 | 85 |
| `referee-clement-turpin` | Damien Huet | HUE | TechnicalBalanced | 55 | 55 | 75 | 92 | 55 | 92 | 88 |
| `referee-danny-makkelie` | Teun Wester | WES | DisciplinaryControl | 70 | 70 | 60 | 92 | 55 | 88 | 85 |
| `referee-istvan-kovacs` | Dragan Lazarević | LAZ | AuthorityIntensity | 68 | 65 | 60 | 92 | 60 | 88 | 85 |

Contrato listo para la fase Classic: `RefereeDatabase` (ids, estilos, perfil de 7 valores 0–100, descripción y notas). Solo el validador de datos (`DataLock`) los lee; ningún código de partido los conoce (un test lo vigila).

## 12. Rosters
Sin cambios de arquitectura. Verificado para 12 semillas: 12 equipos, 10 por roster (6 activos + 4 suplentes), 2 porteros (1 activo + 1 suplente), 8 de campo (5 + 3), ningún jugador repetido en un sorteo, **24 porteros usados** (los 24 disponibles), **96 de campo usados** (quedan 40 disponibles), máximo 3 sustituciones, mismo sorteo con la misma semilla.

## 13. Validadores
`DataLockValidator.Validate(db)` → `DataLockReport` (errores y avisos con área, código, sujeto y mensaje). Compone los validadores existentes (jugador, formación, equipo, coach, dificultad, sorteo) y añade los de la Fase 5. Es imposible pasarlo con: jugador sin id; nombre o `ShortName` vacío, repetido o demasiado largo; atributo fuera de 1–99; jugador de campo con atributos por defecto o idénticos a otro; portero sin perfil o con atributos de campo escritos; rol inválido; skill inexistente, repetida, fuera de los mínimos o del rol; jugador de campo sin skills (o con más de 4); etiqueta de firma sin skill ni diferimiento; skill sin etiquetas, con requisito inválido o que desbloquee una acción que no es de skill; equipo sin formación o con formación no jugable 6v6; roster o sorteo inválido; coach inválido; árbitro con estilo inexistente, rating fuera de rango o corto repetido; dificultad incompleta; persona sin identidad publicada.
**Avisos** (no invalidan): atributos comprimidos (4) y skills sin dueño.

## 14. Legacy (no se elimina nada aquí; todo sale en el cierre, fase 19)
| Sistema | Dónde | Estado y quién lo usa hoy | Debe desaparecer cuando |
|---|---|---|---|
| `PlayerLocomotion`, `MovementTuning`, `InputCurve` | `Core/` | Solo el sandbox (`PlayerMovement`, `TuningProfile`, `PlayerEntity`) y sus tests | La escena de juego use `LocomotionPresenter` |
| `StaminaSystem` (+ `PlayerRuntimeState.Stamina`) | `Core/` | Sandbox; `MatchPlayer.Runtime` aún recupera ese segundo stamina en cada intervalo: **duplicado** de `Locomotion.CurrentStamina` | Se retire `Runtime` del partido |
| `PlayerMovement`, `BallInteractor` | `Gameplay/` | Sandbox (el puente ignora el toque antiguo si el Core tiene jugadores) | Unity migre la escena |
| `DribbleTouchModel` | `Core/Ball/` | 0 usos; sustituido por la conducción de la Fase 4 | Cierre |
| `MatchController.Tick` | `Core/Match/` | `Advance` lo llama por dentro; tests antiguos lo usan | Se haga privado |
| `PlayerDefinition.TeamId` | `Core/Data/` | `[Obsolete]`; el sorteo no lo lee | Cierre |
| `PlayerAttributes.Reaction` | `Core/` | `[Obsolete]`; solo `ReactionModel`, `AiErrorModel` y la dificultad | Se migre la IA |
| **Duplicados nuevos encontrados** | | | |
| `PlayerDefinition.ResolveShortName()` | `Core/Data/` | Derivación antigua (última palabra recortada): superada por `ShortNameRule` | Delegue en `ShortNameRule` |
| `TeamDefinition.PlayerIds` y su constructor | `Core/Data/` | Alineación antigua de 6; los equipos nuevos la dejan vacía y el validador lo exige | Cierre |
| `PlayerIntent`, `IIntentSource`, `IntentRouter`, `VirtualStickModel` | `Core/`, `Core/Input/` | Contrato de input del sandbox, paralelo a `ActionInput` + `SetPlayerInput` | La capa táctil use `ActionInput` |
| Dos modelos de error de acción | `ActionModel.KickError` / `AiErrorModel` | Se unificarán con la dificultad (fase 14) | Fase 14 |

## 15. Decisiones tomadas
1. Identidades ficticias en un archivo de datos propio; los ids no cambian; los archivos de investigación no se editan.
2. Estrategia de nombres por cultura de nacionalidad con generador sembrado por id; una entrada editada a mano manda.
3. Regla de `ShortName` (apellido con partículas; inicial en colisión; Unicode seguro; máx. 12).
4. Pie débil: nunca se inventa; 22 neutrales no verificados; porteros "no aplica".
5. Procedencia de datos por jugador (`PlayerDataProvenance`) en vez de un campo nuevo en `PlayerDefinition`.
6. Porteros: atributos de campo neutros por diseño, validados y fuera de toda estadística.
7. Skills: se parte de las etiquetas conceptuales que ya normalizaban los importadores; 37 skills; mínimos de atributo y roles son filtros de cordura, no bonificaciones; máx. 4; una derivada si no hay ninguna.
8. Dos etiquetas diferidas con motivo (fuera de juego, faltas). Las etiquetas "CentreBack…" y "PhysicalFriction" son de delanteros que dominan a centrales: van a `hold_up_play`; "DefendingFromFront" es presión alta.
9. Formación por equipo: primera variante para Elite/Strong, menor riesgo para Balanced/Modest, `2-1-2` sin coach.
10. Los validadores nuevos viven en `DataLock`; la guarda "los árbitros no los usa otro código" ahora permite esa capa de datos y sigue prohibiéndolos en gameplay.
11. El cargador no rellena formaciones: el archivo es la fuente y el validador falla si falta.
12. `TeamCatalogJson` añade `formationId` (opcional al leer, para archivos antiguos).

## 16. Decisiones pendientes y datos que no se pudieron resolver sin inventar
- **Ids con nombres reales** (`dc-…`, `coach-…`, `referee-…`): internos, nunca mostrados; ¿se sustituyen por ids neutros en el cierre? Cambiarlos toca tests y datos.
- **Textos de authoring** (filosofía y notas de coaches, descripciones y notas de árbitros, notas de jugadores) pueden nombrar personas reales. No forman parte de la identidad publicada y la interfaz no debe mostrarlos; falta decidir si se limpian.
- **Coincidencias**: un nombre ficticio puede coincidir por azar con una persona real. Revisar la lista antes de publicar; se corrige editando el archivo (`"edited": true`).
- **22 pies débiles** sin dato: hace falta la fuente; mientras tanto neutro 50.
- **Dorsales (23), alturas (4), pesos (42)** rellenados con neutros por el importador, sin verificar.
- **Catálogo de skills**: mínimos, roles y agrupaciones son criterio mío; conviene que lo valides. Hay 2 etiquetas diferidas.
- **Compresión de atributos** (Stamina, Passing, Control, Technique): para la fase de balance.
- **Porteros**: los 24 se usan en cada sorteo (24 de 24): solo varía a qué equipo van.
- **Mapping de dificultad a Fácil/Normal/Difícil**: propuesta arriba, sin cerrar.
- Los **9 coaches** sin equipo quedan disponibles para el usuario; no hay lógica de selección todavía.

## 17. Qué queda preparado para la fase siguiente
Base cerrada y validada; skills por jugador y acciones que desbloquean; formación e identidad táctica por equipo; dificultad accesible por contrato; procedencia de datos; ledger de legacy. Sin implementar: percepción, decisión, posicionamiento, tácticas, árbitro, competición.
