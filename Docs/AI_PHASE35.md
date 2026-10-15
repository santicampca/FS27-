# FS27 — Fase 35: cliente completo y centro real

## Construido
- **Acción `Cross` real en el Core** (`ActionDefaults.Cross*`): arco más alto que un pase largo (loft 0,8 frente a 0,55), con las velocidades escaladas para aterrizar donde aterrizaría un pase largo de la misma potencia (`Phase35Tests`). La IA la usa en las decisiones `CrossToBox` de la fase 28 cuando `BasicDecisionDriver.CrossActions` está activo (apagado por defecto en el Core; encendido en el juego y en el banco). Medido: unos 12 centros reales por equipo y partido.
- **Duelo aéreo y bloqueo de rebote de cabezazos**: gana el balón aéreo el mejor (calidad de control, resistencia, cercanía y algo de azar con semilla), y tras un cabezazo nadie vuelve a cabecear durante 0,4 s. Sin ese bloqueo el banco medía 43 cabezazos por equipo y partido por un ping-pong entre dos jugadores; con él, 15.
- **Cliente**: ajustes (controles, cámara, audio, accesibilidad), sonido sintetizado y vibración, práctica de 5 pasos, perfil y estadísticas finales, tiros libres y penaltis para la persona, indicadores (posesión, resistencia, jugador que elegiría la IA), pausa con registro de entradas, pérdida de foco, botón ⇄ al más cercano al balón.

## Medido (12 semillas pareadas, partidos completos, referencia contra actual)
Goles 2,83/3,75 → 3,17/3,75 (sin cambio); **tiros 49,2 → 42,8 y xG 6,04 → 5,15 (−15 %)**; córners 7,3 → 5,2; pases completados 0,59 → 0,59; centros reales 0 → 12,3 por equipo, pero **los centros que acaban en tiro no suben (0,42 → 0,42)**: la decisión de centrar es la de la fase 28.

## No resuelto (honesto)
- Los centros existen pero **no producen más ocasiones**: falta que los compañeros ataquen el área esperando el centro y que el rematador decida el cabezazo a puerta. Siguiente bloque de mayor impacto.
- Menos tiros por los cabezazos (los balones aéreos ya no llegan al pie): es el coste de la idea 2 del usuario; si no gusta, `MatchController.HeadersEnabled = false`.
- La persona no puede centrar (ningún botón pide `Cross`), ni hay centro raso o tenso.
- No hay arte 3D, archivos de audio, música, multijugador ni volea; nadie ha jugado todavía.
