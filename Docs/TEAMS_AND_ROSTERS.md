# FS27 — Equipos, Player Library y sorteo (Fase 2)

Datos y contratos. No hay movimiento, IA, formaciones, skills, gráficos, economía ni online. El Match Engine de la Fase 1 no se tocó.

## Flujo

```
PlayerLibrary  (global: 156 jugadores, sin club permanente)
      ↓
RosterGenerator (seed, library, teams)      ← sorteo determinista
      ↓
TeamRoster      (qué 10 jugadores tiene cada equipo, para ESE sorteo)  ← única autoridad
      ↓
TeamDefinition  (identidad y configuración: nombre, nivel, coach, colores…)
      ↓   (más adelante)
Match Setup → MatchState → Match Engine
```

El Match Engine **no** genera plantillas, no modifica atributos, no decide quién pertenece a quién, no ejecuta el sorteo y no usa `PlayerDefinition.TeamId` (un test lo vigila). `TeamRoster.TryToMatchSquad` es la única conexión: convierte un roster en el `MatchSquad` que el motor ya consume.

## Decisión de plantilla (conflicto resuelto con el usuario)
La especificación decía a la vez "exactamente 1 portero entre los 10" y "al menos 1 portero entre los 4 suplentes" (regla de la Fase 0/1: el portero suplente cubre una roja al portero), y pedía hasta 24 porteros con solo 20 existentes. El usuario decidió **2 porteros por plantilla**:

| | Jugadores |
|---|---|
| Activos (6) | 1 portero (primero) + 5 de campo |
| Suplentes (4) | 1 portero suplente (primero) + 3 de campo |
| Total (10) | **2 porteros + 8 de campo** |

Las constantes viven en `RosterRules`. La regla de la Fase 1 (`MatchSquad`: 1 portero titular, ≥1 suplente) **no se cambió**.

### ⚠ Consecuencia: la biblioteca real NO se puede sortear todavía
12 plantillas × 2 porteros = **24 porteros**; hay **20**. `RosterGenerator.CheckFeasibility` lo detecta antes de generar y devuelve `Goalkeeper shortage: 24 needed (2 per team), 20 available`; `Generate` no crea ningún roster (un test lo fija). Los jugadores de campo sobran (96 necesarios, 136 disponibles).
Para poder sortear con datos reales hay que **autorar 4 porteros más** en la base de datos de porteros (o, si se prefiere, cambiar la regla de plantilla: ver la sección de alternativas). Los tests del sorteo usan la biblioteca real + **4 porteros de prueba** creados en el propio test (copias de números de porteros reales con otro id); no forman parte de los datos del juego.
Alternativas si no se quieren autorar porteros: 8 plantillas con 2 porteros y 4 con 1 (rompe "exactamente"), o 1 portero por plantilla (exige relajar la regla de banquillo de la Fase 1).

## Player Library
- `PlayerLibrary` ya era el registro global (y único) de jugadores; no se creó otra. `AuthoredPlayerLibrary.Load(readText)` construye la biblioteca completa desde las bases de datos con los mismos lectores, correcciones e importadores de siempre. Core no lee archivos ni conoce nombres de archivo: el anfitrión entrega el texto por nombre lógico (`AuthoredPlayerLibrary.Sources`).
- Números reales: **156 jugadores = 20 porteros + 136 de campo** (52 defensas, 42 mediocampistas, 42 delanteros).
- `PlayerDefinition.TeamId`: **marcado `[Obsolete]` (legacy/deprecated), no eliminado**: lo usan las cartas de jugador, `RosterValidator`/`DataValidator` y ~57 usos en tests existentes. Ningún sistema de la Fase 2 lo lee (test sobre el código fuente); los jugadores importados lo tienen vacío; el sorteo no lo escribe. Eliminarlo es una migración futura (junto con `PlayerCardData.TeamId` y `PlayerLibrary.GetByTeam/TryAssignToTeam`, también marcados `[Obsolete]`).
- `TeamDefinition.PlayerIds` (alineación de 6 del núcleo de datos original) queda como **legacy**: los equipos de la Fase 2 la dejan vacía y el validador lo exige.
- `PlayerAttributes.Reaction`: marcado `[Obsolete]` como valor legado/interno/derivado; sigue existiendo solo porque la IA lo lee; no es el 13.º atributo (los 12 oficiales no cambian).
- El `TeamRoster` estático que existía (constructor de equipos a partir de ids) pasó a llamarse `TeamRosterBuilder` para liberar el nombre `TeamRoster` para el contrato de datos nuevo (cambio mecánico en 4 archivos).

## Contratos
| Tipo | Rol |
|---|---|
| `TeamKind` (Official, User), `TeamLevel` (Elite, Strong, Balanced, Modest) | Etiquetas del equipo |
| `TeamDefinition` (ampliada) | Id, nombre, short name, tipo, nivel, `CoachId`, `BadgeRef`, tema, descripción, colores. `IsUserTeam`, `IsElite`. Solo datos |
| `TeamCatalog`, `TeamCatalogValidator`, `TeamCatalogJson` | Los 12 equipos, su validación y el archivo `Docs/authoring/FS27_TEAMS_V1.json` (esquema `FS27.Teams.v1`) |
| `RosterRules` | Forma de la plantilla (10 = 6 + 4; 2 porteros + 8 de campo) y mínimos por línea |
| `TeamRoster` | **Autoridad** de la plantilla: `ActiveIds` (portero primero) y `BenchIds` (portero suplente primero), seed |
| `RosterGenerator` (+ `RosterDrawSettings`, `DrawFeasibility`, `RosterGeneration`) | El sorteo |
| `RosterGenerationValidator` (+ `RosterReport`) | Valida el resultado |
| `RosterQualityModel` | Métrica derivada de calidad (solo para el sorteo) |
| `SeedMixer` | Semilla → estado del generador (usa el `SeededRandom` existente) |
| `AuthoredPlayerLibrary` | Biblioteca global desde las bases de datos |
| `SkillDefinition`, `PlayerSkillSet` | **Solo contratos**; nadie los crea; ningún jugador tiene skills |

## Los 12 equipos (datos en `FS27_TEAMS_V1.json`)
Todos ficticios; sin escudos ni equipaciones finales (solo colores y una referencia `BadgeRef`). Cada equipo oficial tiene un entrenador distinto del sistema de 20 entrenadores existente, elegido por afinidad con su identidad (referencia de datos: **no** se aplica Coach Boost ni se recalcula nada).

| Equipo | Short | Nivel | Tema | CoachId |
|---|---|---|---|---|
| Solaris Meridian | SMR | **Elite** | Dominio por posición | coach-pep-guardiola |
| Obsidian Bastion | OBS | **Elite** | Bloque y transición | coach-diego-simeone |
| Vortex Rapids | VXR | Strong | Presión alta vertical | coach-hansi-flick |
| Emerald Compass | EMC | Strong | Técnica equilibrada | coach-roberto-mancini |
| Gilded Falcons | GLF | Strong | Libertad para el talento | coach-zinedine-zidane |
| Northwind Union | NWU | Balanced | Colectivo intenso | coach-lionel-scaloni |
| Cobalt Syndicate | CBS | Balanced | Posesión estructurada | coach-mikel-arteta |
| Ashen Foundry | ASF | Balanced | Pragmatismo | coach-massimiliano-allegri |
| Tidal Phoenix | TDP | Balanced | Flexibilidad táctica | coach-thomas-tuchel |
| Ember Rovers | EMR | Modest | Energía física | coach-mauricio-pochettino |
| Silver Thistle | SVT | Modest | Ofensivo y técnico | coach-manuel-pellegrini |
| **Mi Equipo** (User Team) | USR | Balanced (configurable) | Equipo del usuario | — (sin entrenador asignado) |

Exactamente 11 oficiales + 1 User Team; exactamente 2 oficiales Elite; el User Team no es Elite (su nivel es un dato, no consecuencia del nombre; si se pone Elite, el validador lo rechaza).

## El sorteo
Determinista: `seed + library + teams (+ settings) → rosters`. Sin `DateTime`, GUID ni random global (un test lo vigila). La semilla se guarda en `RosterGeneration.Seed` y en cada `TeamRoster.Seed`.
1. **Factibilidad** antes de sortear: porteros (2 por equipo), jugadores de campo (8 por equipo) y mínimos por línea. Si no es posible no se genera nada y se informa de la carencia.
2. **Etapa A — Elite primero:** los dos Elite reciben sus 2 porteros y 8 jugadores de campo de un *pool* barajado (con la semilla) de los mejores (1,25 × lo necesario), respetando los mínimos por línea. Así son fuertes, pero no copia espejo.
3. **Etapa B — el resto** (oficiales restantes + User Team) en orden aleatorio con la semilla, con lo que queda: primero 2 porteros, luego los mínimos por línea (2 defensas, 2 medios, 1 delantero) y luego los huecos libres. Cada elección es aleatoria ponderada con un sesgo según el nivel (Strong favorece a los mejores, Modest a los peores, Balanced neutro); **no hay igualación** de ningún tipo.
4. **Etapa C — activos/suplentes:** el mejor portero y los mejores 5 de campo cubriendo las tres líneas son los 6 activos; el otro portero y los 3 restantes, los suplentes.
5. **Etapa D — validación;** si falla (p. ej. un Elite no es claramente superior) se prueba con una semilla derivada (mismo orden de intentos para la misma semilla; hasta 16). Si todos fallan se informa.

Sin duplicados: cada jugador sale de una lista de restantes al ser elegido, y el validador lo comprueba (ningún `PlayerId` ni portero en dos equipos). Entre sorteos con otra semilla, un jugador sí puede cambiar de equipo.

### Métrica de calidad (solo para el sorteo)
Derivada, determinista, sin guardarse: calidad de un jugador de campo = su **Overall** oficial (`PlayerRatingCalculator`); de un portero = su **GRL** oficial (media de sus 8 capacidades). **Fuerza de plantilla** = 0,75 × media de los 6 activos + 0,25 × media de los 4 suplentes. No hay `EliteScore` en el jugador.

### Garantía Elite
Cada Elite debe superar a **la mejor plantilla restante en ≥ 1,0** y a **la media de las demás en ≥ 3,0** puntos de fuerza. Los jugadores autorados están muy juntos (todos los de campo entre 79 y 92 de Overall, media 83,8; los 16 mejores promedian 88,4), así que las diferencias entre plantillas son de pocos puntos: medido con 60 semillas, el Elite más débil supera a la mejor plantilla restante en 2,8 de media (nunca menos de 1,7) y a la media en 4,0 (nunca menos de 3,6). Ejemplo con la semilla 12345: Elite 88,1 y 87,1; el resto entre 82,3 y 84,4. Ningún atributo se modifica (un test compara todos los atributos, capacidades de portero, `Reaction` y `TeamId` antes y después de 10 sorteos).

## Lo que NO se hizo (a propósito)
Movimiento, IA, formaciones, skills (solo contratos), acciones, gameplay 6v6, animaciones, gráficos finales, menús, economía/mercado, restricciones por nacionalidad/edad/club, química, pantalla de creación del equipo del usuario, editor de plantillas, `Match Setup` (solo existe `TeamRoster.TryToMatchSquad`).
