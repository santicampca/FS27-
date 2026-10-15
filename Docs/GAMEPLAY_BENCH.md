# FS27 — GameplayBench (Fase 32)

Un banco **reproducible** de situaciones de ataque y defensa sobre el **Core real** (equipos y atributos reales, el `BasicDecisionDriver` real decidiendo por los doce jugadores, la física y las reglas del `MatchController`). Una escena solo coloca a los jugadores y el balón; a partir de ahí juega el Core. Un grabador mira los eventos y las acciones y escribe **las mismas métricas** para todas las escenas, de modo que un cambio se juzga por todo el cuadro y no solo por goles o pases completados.

Código: `Assets/_Project/Tests/EditMode/GameplayBench.cs` (configuraciones, grabador, ejecución, CSV, comparación pareada), `GameplayBenchScenes.cs` (las escenas), `GameplayBenchTests.cs` (el ejecutor explícito y las guardas).

## Cómo se usa

```
# banco completo, 12 semillas, referencia contra versión actual (≈ 4 min); escribe gameplay_bench.csv y gameplay_bench.md
GB_SEEDS=12 GB_OUT=/tmp/gb dotnet test Tests/Core.Tests --no-build --filter "Name=GameplayBench_Run"
# solo una familia o escenas: GB_ONLY=pressure,A1_BuildUp   (familias: attack, chances, pressure, transition, defence, keeper, human, match)
# cada cambio por separado (táctica, portero, ayuda de puntería, selección de jugador) contra la referencia: GB_ATTR=1
# un cambio de código o de FS27_EXP contra una ejecución guardada, pareja a pareja: GB_VS=/tmp/gb/gameplay_bench.csv
# ejes de variación (dificultad, parejas de equipos; GB_GAP=1 añade partidos entre niveles):
GB_SEEDS=8 GB_OUT=/tmp/gb dotnet test Tests/Core.Tests --no-build --filter "Name=GameplayBench_Axes"
# reescribir la línea base comprometida (a propósito, tras juzgar un cambio con el banco completo):
dotnet test Tests/Core.Tests --no-build --filter "Name=GameplayBench_WriteBaseline"
```

`FS27_EXP` (el mecanismo de exploración de las campañas anteriores, p. ej. `"*.Attack.ReceiverLaneWeight=1.6"`) funciona también en el banco.

## Configuraciones y pareo

- **ref**: sin táctica de equipo, el portero juega al instante, sin ayuda de puntería, selección de jugador de la Fase 8 (el comportamiento de las fases 28-30).
- **cur**: todo lo de las fases 31, 32 y 33 (cabezazos incluidos).
- Misma semilla = mismo arranque (la semilla desplaza lateralmente las posiciones de forma fija y siembra el ruido del controlador). La comparación es **pareada** (Δ por semilla, error estándar, veredicto con suelo `max(10 % de la referencia, 0,05)`).
- Las ejecuciones son deterministas: **las semillas no son muestras independientes** de una población, y solo hay 12 equipos distintos. Un veredicto «mejor / peor» es evidencia de que el cambio mueve esa métrica en esas escenas, no una estimación de la liga.

## Escenas

| Id | Familia | Qué se mira |
|---|---|---|
| A1_BuildUp | ataque | salida desde el campo propio contra bloque medio: progreso, pases, pérdidas, xG |
| A2_WingPlay | ataque | extremo con balón en banda: centros, centros que llegan, tiros desde ellos |
| A3_Combination | ataque | portador con compañero cerca y defensor encima: pared/pérdida |
| A4_RunsOffTheBall | ataque | portador con dos desmarques posibles a la espalda de una línea de cinco |
| A5_FinalThird | ocasiones | portador al borde del área: ¿crea y toma una buena ocasión? |
| B1_UnderPressure | presión | portador cerrado a 1,5 m y 3,8 m con salidas atrás y al lado |
| B2_TransitionDefence | transición | el rival acaba de robar con el equipo local adelantado: recuperación y ocasiones concedidas |
| B3_TransitionAttack | transición | se recupera con el rival adelantado y dos corredores ya por detrás de su línea |
| C1_PressingBuildUp | defensa | el rival sale desde su portería contra un equipo que presiona |
| C2_CompactDefence | defensa | tres atacantes contra cuatro defensas junto a su área |
| C3_WingDefence | defensa | extremo rival por banda con corredor al segundo palo |
| C4_CoverSecond | defensa | portador contra primer defensor y cobertura |
| K_S2_CoveredReceiver, K_S4_Rival7m, K_GoalKick | portero | las escenas del ScenarioBench del portero y un saque de puerta real |
| H1_AssistPasses, H2_AssistShots | humano | una «persona» con error de puntería sembrado, con y sin ayuda de puntería |
| H3_AutoSelection | humano | una persona que no toca nada 5 min: ¿controla al jugador con balón o al más cercano?; cambios y parpadeos del marcador |
| H4_ManualSwitch | humano | el botón de cambiar de jugador con el rival con balón: ¿el elegido sigue siendo suyo? |
| M1_FullMatch | partido | partidos Classic completos entre parejas reales de equipos (flujo: saques, paradas, fuera de juego del balón…) |

## Métricas (por equipo local `h_` y visitante `a_`)

Pases (completados, perdidos, a ningún sitio, al receptor previsto, hacia delante, en el último tercio), centros (aproximados por geometría: pase desde banda hacia dentro en los últimos 12 m, y centros que llegan a compañero y acaban en tiro), tiros, **xG** (el `ShotQualityEvaluator` calibrado), ocasiones claras, goles, intercepciones, entradas, faltas, recuperaciones, paredes (A→B→A en menos de 3,5 s), **progreso** hacia la portería rival, posesión, profundidad defensiva, distancia del defensor más cercano al portador, tiempo hasta la primera pérdida y hasta recuperar, tiempo hasta el primer tiro, flujo de saques (banda, córner, saque de puerta, falta: cuántos y cuántos se pierden en 6 s), de dónde sale el balón fuera (tras pase, tiro, parada…), paradas y blocajes del portero, **estrechez de la línea de pase** de cada pase (cuatro bandas por distancia del rival más cercano al segmento) y anomalías (balón suelto quieto, portador parado sin rival cerca). Estabilidad del jugador controlado por una persona (ver H3 y H4).

## Qué se encontró

Resultados completos de la ejecución comprometida (20 escenas × 12 semillas pareadas): `Docs/GAMEPLAY_BENCH_RESULTS.md` y `Docs/gameplay_bench_run.csv`. Resumen y decisiones: `Docs/AI_PHASE32.md`. Línea base de las guardas: `Docs/gameplay_bench_baseline.csv` (se reescribe a propósito con `GameplayBench_WriteBaseline`).
