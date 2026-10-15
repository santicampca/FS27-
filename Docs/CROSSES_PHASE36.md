# FS27 — Fase 36: el centro como mecánica de fútbol

Base: `phase-35-complete-client` (PR #8). Esta rama (`phase-36-crosses-mechanic`) añade solo la fase 36; el PR se abre contra la rama de la fase 35 para no duplicar commits.

## Qué hace (Core)
- **Intención (`CrossPlay`)**: un portador en banda (a 6 m o menos de la línea de banda) y en el tercio de ataque (a 16 m o menos de la línea de fondo) es un centro por venir. Los compañeros lo leen y **corren a sitios distintos del área**: centro (punto de penalti), segundo palo, primer palo (el de la banda del balón) y borde del área (rechace y pase atrás). El más retrasado se queda cubriendo. El reparto es voraz por puntuación: papel del jugador (Finisher/GoalHunter/Target al centro y primer palo, Winger/Explosive al segundo palo, Creator/Engine al borde) menos metros hasta el punto; nadie repite punto (separación mínima 2,2 m, comprobada en test).
- **El portador espera a los corredores**: lleva el balón al trote por la banda hasta 6 m de la línea de fondo y, si los corredores no han llegado y no le presionan, lo protege (`WaitForRunners`); si le presionan, decide como antes. Con marcaje, un receptor en el área necesita 0,9 m de espacio (no 2,0), y desde la línea de fondo el pase atrás al borde del área cuenta como centro.
- **Tres centros** (`CrossStyle`): **alto** (arco 0,8, sobre los defensas), **tenso** (arco 0,28, rápido, a la altura del pecho), **raso** (por el suelo, como un pase duro). La IA elige: raso al borde del área con carril libre, tenso al primer palo o punto de penalti si está cerca y el carril es abierto, alto en el resto (`CrossPlay.StyleFor`).
- **La persona centra sin botones nuevos**: con el balón en banda cerca de la portería rival, el deslizamiento hacia arriba de A (que era una vaselina) es un centro; el botón A pasa a decir **CENTRO**. La longitud del deslizamiento da el estilo (hasta 82 px raso, hasta 112 px tenso, más alto) y el círculo elige el lugar del área; si hay un compañero cerca del lugar, es el destino.
- **Defensa**: ante un portador rival en banda cerca de su portería, cada defensor libre **marca** a un atacante del área (el más peligroso primero, por detrás de él hacia la portería, a 1,3 m) y uno **cubre el primer palo**. Los cabezazos defensivos ya despejaban.
- Todo apagado por defecto en el Core (`BasicDecisionDriver.CrossMechanics`, `MatchController.HumanCrossEnabled`); encendido en el juego y en el banco.

## Medido
**Partidos completos, 36 semillas nuevas (13-48), referencia contra fase 36** (las 12 primeras semillas se usaron para afinar; estas 36 no):

| por equipo y partido | referencia (sin cabezazos ni centros) | **fase 36** |
|---|---|---|
| centros reales | 0 | **10,1** (altos y tensos; rasos 0,5) |
| tiros / tiros a puerta | 47,7 / 43,2 | 47,6 / 42,1 |
| xG | 5,81 (rival 6,32) | 5,57 (rival 5,84) |
| goles (local / visitante) | 3,25 / 2,86 | 3,53 / 3,61 |
| cabezazos tras centro | 0 | 2,4 |
| centros que acaban en tiro (proxy geométrico) | 5,7 | 4,7 |
| pases completados / córners | 0,61 / 6,9 | 0,60 / 7,0 |

**Tres versiones, 12 semillas (1-12):** referencia xG 6,04 y goles 2,83/3,75; fase 35 (centro real sin juego de equipo) xG 5,34, tiros 44,6, goles 3,00/3,33; fase 36 xG 5,16, tiros 44,8, goles 2,42/3,42, 11,8 centros (6,8 altos, 4,5 tensos, 0,4 rasos).

**Lectura honesta**: los centros ahora **existen como mecánica** (corredores distintos, marcaje, tres estilos, la persona los ejecuta) y se usan ~10 veces por equipo y partido, pero **no producen más ocasiones que antes**: los tiros a puerta y los remates tras centro no suben y el xG baja un 4 % (diferencia dentro del ruido de 36 semillas deterministas). Goles totales sin cambio apreciable. Una variante intermedia que midió +18 % de tiros con 12 semillas **no se confirmó** con otras 36: era ruido. Los cabezazos no se han desactivado. Lo que falta para que centrar rinda es calidad de decisión (cuándo centrar, a quién), no la mecánica: siguiente bloque.

Escenas nuevas del banco (`A6_CrossBox`, `D1_BoxDefence`, una sola jugada de 14 s): 0,42 centros reales frente a 0,08 en la fase 35 en A6, centros que llegan 0,5 frente a 0,08; cifras pequeñas, solo indicativas.

## Regresión conocida en una escena
`A2_WingPlay` (un extremo en banda, 20 s, 24 semillas): tiros 0,88 → 0,12 y xG 0,19 → 0,01 con el juego de equipo (la referencia daba 1,25 tiros). El portador ahora lleva el balón por la banda hacia la línea de fondo y espera a los corredores en lugar de recortar hacia dentro y tirar; en el partido completo los tiros totales no bajan, pero en jugadas de banda aisladas sí. Si en la prueba humana se nota que los extremos «no tiran nunca», el parámetro es `CrossPlayTuning.MinRunnersToWait` (0 desactiva la espera) o `BasicDecisionEngine.CrossMechanics`. En `A6_CrossBox` los pases perdidos suben de 0,48 a 0,71 (un centro que no llega cuenta como pase perdido).

## Verificación
Suite completa, benchmark de 12 semillas y la prueba del HTML con toques reales (`Client/test/offline.mjs`): en un partido real, la persona en banda con el balón ve **CENTRO** en A y los tres deslizamientos (corto, medio, largo) dan un centro raso (altura máxima 0,2 m), tenso (0,86 m) y alto (2,69 m). La prueba usa `GameBridge.TestSetup("cross")` (solo para tests: coloca al jugador en banda con los rivales lejos).

## No resuelto
- El lateral no ofrece apoyo exterior cuando el extremo conduce hacia dentro (REQ-0177); sin diferencia medida entre pie bueno y débil (0169); sin voleas; el portero sigue con su lógica de siempre ante balones altos.
- La persona elige el estilo por la longitud del deslizamiento, no manteniendo pulsado; en pantallas pequeñas puede costar acertar el estilo: lo dirá la prueba humana.
- Nadie ha jugado todavía.
