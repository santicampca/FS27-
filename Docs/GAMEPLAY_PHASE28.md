# Fase 28 — Pases con física, reanudaciones y juego por bandas

## Qué cambió en el comportamiento
1. **Potencia de pase por física** (`PassPhysics`): el pase sale con la fuerza que el balón necesita para llegar al receptor a una velocidad de llegada fija (`GroundArrivalSpeed` 9 m/s, 9,5 con rival cerca del receptor). Antes se golpeaba de más y 15–24 % de los pases de Classic salían del campo. El barrido 6,5 / 8 / 9 / 10 / 11,5 / 14 m/s mostró que 9 es el punto de equilibrio (llegada ~42 % → ~56 %, controles limpios 84 % → 77 %); más rápido rompe el primer toque.
2. **Reanudaciones (saque de banda, de puerta, córner)** — solo donde el modo las tiene (Classic):
   - El balón espera en el punto, en posesión del sacador, hasta que lo juega (`SetPieceState`, `MatchController.SetPieces.cs`). Antes el balón "controlado" se arrastraba por encima de la línea y el 31 % de los saques de banda volvían a salir en 0,2 s.
   - Los rivales se mantienen fuera de un círculo (2,5 m saque de banda, 3,5 m córner, 5 m saque de puerta) hasta que se juega; máximo 6 s.
   - Al preparar la reanudación los compañeros se colocan como opciones de pase (3 puntos por tipo, buscando huecos sin rivales en el punto y en el carril), el más retrasado se queda de cobertura.
   - La IA del sacador (`TakeSetPiece`) valora cada compañero como valor × probabilidad de llegada (`PassRisk`); juega a la mejor opción si llega con ≥ 50 %, espera 0,5 s a que se abra una, acepta ≥ 30 %, y si no despeja. En el córner lofts al área.
3. **Jugador presionado**: un pase que un rival que ya corre hacia el pasador puede tocar en los primeros metros vale menos (`PassRisk.ReleaseMargin`, peso moderado); el resultado es pequeño (≈ +1 punto de llegada). El modelo de riesgo de pase completo (`PassRiskDisabled`) sigue **desactivado por defecto**: disparaba tiros y goles sin mejorar la llegada.
4. **Bandas**: un defensor cuyo sitio está en la mitad trasera sube por la banda del balón cuando el equipo lleva posesión, el balón está cerca o más allá del medio campo y queda al menos otro jugador de cobertura detrás del balón (`AttackSupport.ChooseOverlapper`); solo uno a la vez. Centro lofteado al área (`IsCross`) desde la banda en el último tercio a un compañero libre en el área.

## Reglas
Arena no cambia: no tiene saques de banda, de puerta ni córners (`IGameModeRules`); nada de lo anterior se ejecuta allí. El pin bit a bit de la Fase 11 se mantiene con los interruptores de la Fase 28 apagados.

## Medición (batería rápida, 20 partidos completos por celda, semillas 5001-5020; referencia F22)
| Celda | pases completados F22 → ahora | goles por partido F22 → ahora |
|---|---|---|
| Classic Profesional | 0,406 → 0,596 | 4,50 → 8,05 |
| Classic Élite | 0,403 → 0,576 | 4,15 → 4,90 |
| Classic Novato | 0,435 → 0,652 | 8,05 → 10,75 |
| Arena Profesional | 0,425 → 0,465 | 5,75 → 7,65 |
| Arena Élite | 0,408 → 0,421 | 4,80 → 4,10 |
| Arena Novato | 0,458 → 0,537 | 9,55 → 11,85 |

## Abierto
- Classic Profesional marca +79 % de goles (más tiros: 40 → 49-52 por equipo, mejor calidad y conversión). Es un desequilibrio a revisar; no se ha tocado el Core de tiros ni el portero.
- La llegada de los pases largos sigue baja (~20-30 %).
- El desmarque de ruptura y la pared (jugador presionado) no están implementados; el defensor en campo rival apenas cambia (12-13 %), porque la subida se activa solo ~9 % de las decisiones.
