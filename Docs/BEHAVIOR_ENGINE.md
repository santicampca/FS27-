# Behavior Engine

`FootballDNA` + situación + memoria → los comportamientos que el jugador considera y el que elige. Puro, determinista y sin conocer a ningún jugador. **No ejecuta nada**: devuelve una decisión; convertirla en acción es trabajo de la IA de partido (aún no conectada).

```
Match Core (posiciones, balón, posesión)
   │  construye
   ▼
FootballContext ── FootballContextAnalyzer ──► ContextAnalysis { Flags, Pressure01, SpaceAhead01, DistanceToGoal, Zone, Phase }
   │                                                    │
   │      FootballDNA + PlayerAttributes + BehaviorMemory
   ▼                                                    ▼
                         BehaviorDecisionEngine.Decide(...) ──► BehaviorDecision { Candidates, Rejected, Chosen, Explanation }
                                                                     │
                       PlayerIntent (movimiento) ─► FootballActionResolver ─► ActionSelection ─► PlayerIntent con acción/estilo
```

## FootballContext (una vista, no un duplicado del Match Core)
Posición del jugador y del balón, orientación, `AttackSign` (+1/−1), posesión (propia/rival/suelta), si tiene el balón o le llega, compañeros y rivales. El host lo construye desde el Match Core; el motor no posee reglas ni cambia el partido. Los umbrales (`ContextThresholds`) son metros sobre el campo de 40×25: radio de presión 3,5, cono frontal 60°, caja 7 m, rango de tiro 15 m, banda 5 m...

Situaciones detectadas (`BehaviorContext`): tiene el balón, recibe, **frente a un defensor**, **espacio por delante**, dentro/cerca del área, **bajo presión**, zona ancha, **detrás de la línea defensiva** (el rival más profundo se toma por portero), tiro posible, tiro lejano, compañero/rival con balón, de espaldas a la portería.

## Puntuación de un comportamiento
Para cada comportamiento del catálogo (15):
1. **Puertas** (si falla, se rechaza *con motivo*): situaciones requeridas (las del comportamiento + las del jugador), prohibidas, enfriamiento (`BehaviorMemory`), umbral propio del jugador.
2. `PlayerAffinity` = cuánto es de los que lo hacen (impulso del DNA + peso de firma). `ContextMatch` = cuántas situaciones preferidas hay. `AttributeFit` = cuánto de lo que exige tiene el jugador.
3. `Score = (0,45·impulso + 0,40·firma + 0,15·situación) · (0,5 + 0,5·ajuste)`; luego **riesgo** (`max(0, riesgo − apetito) · (0,2 + 0,6·presión)` lo penaliza), **confianza** de la entrada y **secuencia** (si el comportamiento anterior reciente encadena con este).
4. `Utility = Score · (0,9 + 0,01·prioridad)`; se ordena por utilidad y luego por id. Por debajo de 0,15 → "juega normal".
5. Opcional: con una fuente aleatoria con semilla se elige entre los casi mejores (85 %) ponderando por utilidad: reproducible por semilla.

## De la decisión a la acción (`FootballActionResolver`)
- **IA sin acción explícita:** el comportamiento elegido pasa a ser `Action` + `MovementStyle` + `BehaviorId`.
- **Persona:** la acción pedida **nunca se sustituye**. El DNA solo da sabor dentro de la misma familia: tiro colocado, pase al hueco, finta en el regate. No se inventa ninguna acción para una persona.
- **No toca la mira** (`ShotIntent.Aim`, `PassIntent.Target`): `IntentAssist.SnapAim` y los ajustes de asistencia siguen siendo los dueños de eso.
- `PlayerIntent` es **el mismo struct** extendido (BehaviorId, Action, Style, Dribble/Shot/Pass); un intent solo de movimiento queda idéntico. Sin bool, sin sprint. `IntentMixer.Sanitize` (la tubería de entrada existente) ahora **conserva** esos campos: una prueba de extremo a extremo (IA → decisión → intent → `Sanitize` → `PlayerLocomotion`) comprueba que la velocidad del jugador es la misma con o sin acción.

## Estado honesto
Los 15 comportamientos siguen `Planned` (hay definición, prioridad, riesgo, enfriamiento y etiquetas de animación, pero ninguno se ejecuta en partido). Los valores de prioridad/riesgo/enfriamiento y los pesos son **valores de partida** pensados para calibrarse. La IA de partido no usa todavía este motor.
