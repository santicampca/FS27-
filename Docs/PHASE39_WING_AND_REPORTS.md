# Fase 39 — Apoyo exterior, informes de saques, menú y plan visual

Rama `phase-39-visual-plan` (apilada sobre la fase 38).

## Apoyo exterior (REQ-0177)
`WingSupport`: cuando el extremo conduce **hacia dentro** (velocidad lateral hacia el centro ≥ 0,8 m/s, en la mitad atacante), el jugador ancho de esa banda que está **detrás** de él (el lateral, o el centrocampista ancho en la forma de 6 contra 6, donde casi no hay laterales) corre por fuera y un poco por delante (solapamiento). Solo va el más cercano. Bandera `BasicDecisionDriver.OutsideSupport` (apagada por defecto en el Core, encendida en juego y banco). Prueba: `TheWidePlayerBehind_OffersTheOutside...` (con el extremo yendo por la línea no ofrece nada).
- Efecto global (72 semillas nuevas, 100-171, partido completo, sin → con el juego aéreo + apoyo): goles totales −0,12 ± 0,41, xG total 12,3 → 11,0, tiros del rival −16 % (las cifras de tiros y xG bajan por los cabezazos, ya declarado en la fase 33). **Los +10 % de goles vistos con 36 semillas eran ruido.**
- El apoyo exterior solo no mueve tiros ni xG (36 semillas: 5,67 → 5,64).

## Informes de saques (REQ-0980)
`MatchController.SetPieceReports`: por cada saque de banda, córner o tiro libre que saca una **persona**: objetivo (compañero o punto), potencia aplicada, acción y resultado. Prueba: `EveryRestartATakenByAPerson_LeavesAReport...`.

## Cliente
- Menú: conserva equipo, rival, modo y dificultad al volver (REQ-0659); botones A/B/C/⇄ con etiqueta y pista (REQ-0658); botón «ONLINE · próximamente» (fase 38). `offline.mjs` lo comprueba.
- El tiro libre ya tiene la opción «Pase» (pasar rápido, REQ-0974).

## Centros con el pie malo (REQ-0169)
Prueba nueva: el pie débil cruza con menos calidad que el bueno (la lógica ya existía desde la fase 35).

## Plan visual
`Docs/VISUAL_PARALLEL_PLAN.md`: qué puede empezar ya, qué espera una decisión y qué depende de sistemas del Core. No hay arte.

## Honestidad
- La escena `A2_WingPlay` pone a un central (`cb-cristian-romero`) como «extremo» porque ocupa el índice 0: sus pocos centros no representan a un extremo real. La prueba que exigía centros en A2 ahora usa A2 y A6 (24 semillas).
- Viento (REQ-0968): no existe; el requisito solo lo permite si fuese regla visible y medible. Sin cambios.
