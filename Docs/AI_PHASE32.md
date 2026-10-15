# FS27 — Fase 32: sistema de evaluación de gameplay y primeras correcciones

## Entregado
- **GameplayBench** (`Docs/GAMEPLAY_BENCH.md`): 20 escenas (ataque, ocasiones, presión, transiciones, defensa, portero, humano, partido completo) sobre el Core real, mismas métricas en todas, comparación pareada referencia/actual, ejes de dificultad y de equipos, guardas automáticas y línea base comprometida.
- **Corrección 1 (humano): estabilidad del jugador controlado** (`Decision/SelectionStability.cs`). La selección automática se ejecutaba en cada paso y **anulaba el botón ⇄ en menos de un fotograma** cuando el equipo no tenía el balón, y movía el marcador entre dos defensas cada 1–2 s. Ahora: retención de 3 s tras un cambio manual, tiempo mínimo de 0,8 s entre cambios automáticos y margen de 1,5 m sin balón. Medido (12 semillas pareadas): cambio manual que se mantiene **8 % → 100 %**; parpadeo A→B→A **10,0 → 0,08 por minuto**; cambios sin balón **32,6 → 14,8 por minuto**; jugador más cercano al defender 0,90 → 0,93; portador seleccionado 0,95 → 0,93 (plano).
- **Cliente jugable**: archivo único `FS27-jugable.html` (`Client/pack-offline.mjs`), menú con instrucciones y «ayuda de puntería», formulario de valoración al final. `Docs/PRUEBA_HUMANA.md`.

## Hallazgos medidos (no corregidos)
| Hallazgo | Dato | Por qué no se tocó |
|---|---|---|
| Los **centros no existen** como acción del Core (`Cross` no soportado); la métrica de centros es geométrica | 54 «centros» por equipo y partido, solo 12,7 llegan a un compañero y 0,6 acaban en tiro | Es una función nueva (acción, remate de cabeza, IA), no un defecto puntual. **Siguiente bloque de mayor impacto** |
| **Pases por carriles estrechos**: 31 % de los pases con un rival a <1,5 m del segmento se pierden el 59 %; 34 % a 1,5–3 m se pierden el 39 %; a >5 m, 2 % | por partido y equipo | Endurecer el carril o su peso sube la compleción (0,63 → 0,67–0,75) pero **recorta pases un 13–35 % y el xG un 0–10 %**: un compromiso, no una mejora clara |
| **Rechaces del portero**: ~50 % de las paradas rechazadas salen del campo (≈38 saques por partido, casi todos de banda) | 75 rechaces vs 27 blocajes | Rechaces más cortos bajan los saques de 55 a 18 pero **suben los goles de 3,3 a 6,9 por equipo**: depende de la decisión pendiente de la tasa de goles de Classic |
| Pase al pie del rival en el último tercio | trazado en A5 | Requiere modelo de riesgo; `PassRisk` activado sube goles +14 % y la compleción solo +4 pts |
| Dificultades | Elite vence a Novice 5,6–2,3 y Novice a Elite 2,2–5,1; xG, tiros y compleción (0,68 vs 0,55) se ordenan | Sin defecto |
| Fase 31 contra la referencia | sin regresión significativa en defensa ni transiciones; portero S2 mejora; asistencia mejora pases al receptor (0,05→0,16) y goles de tiros asistidos | — |

## Regresiones y artefactos señalados por el banco (honestidad)
- A1: pérdidas de pase 0,44 → 0,51 (+0,07 ± 0,03) y A2: centros que llegan 0,50 → 0,17 (−0,33 ± 0,14): vienen de la táctica de equipo de la Fase 31 (más verticalidad); xG sin cambio significativo. Siguen en el registro.
- K_S4: «pases completados» 0,58 → 0,00 es un **artefacto de la métrica** (con distribución activa el portero despeja en vez de pasar; no hay gol en contra: 0,08 → 0).
- Las semillas son deterministas y no independientes; 12 equipos distintos. Los veredictos son evidencia de efecto en estas escenas, no estimaciones de la liga.

## Validación
Suite completa 2612/2612 (9 min 9 s); guardas del banco (determinismo, completitud, ids únicos, sentido de la presión, línea base); benchmark de 12 semillas; `Client/test/offline.mjs` sobre `file://` con cero peticiones de red. **No hay validación humana**: ninguna persona ha jugado todavía.
