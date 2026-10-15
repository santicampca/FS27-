# FS27 — Lista maestra de 1.000 requisitos: estado real (Fase 33)

Fuente: `FS27_lista_maestra_1000_requisitos.md` (1.000 identificadores `FS27-REQ-0001…1000` en 50 secciones). Estado por requisito: `Docs/requisitos_estado.csv` (id, sección, estado, evidencia, texto).

## Estado de los 1.000 (generado por `Tools/requisitos_audit.py`)
Cada fila de `Docs/requisitos_estado.csv` dice **cuánta confianza** tiene su estado:
- **requisito** (183 filas): comprobado uno a uno contra una prueba, una escena del banco, el código o la prueba del cliente.
- **seccion** (817 filas): el subsistema existe (o no) en el repositorio, pero el requisito **no se ha comprobado individualmente**. «Parcial» aquí significa «hay un subsistema con pruebas; falta verificar este comportamiento», no «medio hecho».

Totales: **confirmado 73**, parcial 858, pendiente 13, requiere humano 56. **No se han «integrado los 1.000»**: se han construido los grandes huecos que se podían construir (fases 33-35: cabezazos, saque y córner con fuerza y puntería, centro real, tiros libres y penaltis para la persona, ajustes, sonido sintetizado, vibración, práctica, perfil, estadísticas, registro de entradas, pausa) y el resto sigue como se indica.

Pendientes por sección (los más numerosos):
- Movimiento y locomoción: 5
- Balones aéreos y juego de cabeza: 3
- Centros y juego por las bandas: 2
- Interfaz y HUD: 2
- Multijugador futuro y límites de alcance: 1

Lo que **no se puede construir sin decisión, arte o red** y por eso sigue pendiente: arte 3D y modelos (no hay assets), música y archivos de audio, multijugador, cuenta/nube, volea, viento, y todo lo que solo una persona puede juzgar (sensación, diversión, legibilidad).

Resumen por sección:

| Sección | confirmado | parcial | pendiente | requiere humano |
|---|---|---|---|---|
| Identidad y estructura del partido | 2 | 16 | 0 | 2 |
| Movimiento y locomoción | 10 | 5 | 5 | 0 |
| Joystick, sprint y controles táctiles | 3 | 17 | 0 | 0 |
| Selección y cambio de jugador | 10 | 10 | 0 | 0 |
| Balón y física | 0 | 19 | 0 | 1 |
| Primer toque y recepción | 0 | 20 | 0 | 0 |
| Pases y combinaciones | 0 | 18 | 0 | 2 |
| Tiros y finalización | 0 | 20 | 0 | 0 |
| Centros y juego por las bandas | 6 | 12 | 2 | 0 |
| Balones aéreos y juego de cabeza | 5 | 12 | 3 | 0 |
| Regates y protección | 0 | 19 | 0 | 1 |
| Presión alta y defensa agresiva | 0 | 20 | 0 | 0 |
| Defensa posicional y coberturas | 0 | 20 | 0 | 0 |
| Entradas, presión individual y duelos | 0 | 20 | 0 | 0 |
| Porteros: colocación y reflejos | 0 | 20 | 0 | 0 |
| Porteros: salidas y distribución | 0 | 20 | 0 | 0 |
| Saques de banda | 0 | 19 | 0 | 1 |
| Córners | 0 | 20 | 0 | 0 |
| Tiros libres y puntos direccionales | 0 | 19 | 0 | 1 |
| Penaltis | 0 | 20 | 0 | 0 |
| Reanudaciones y flujo de partido | 1 | 19 | 0 | 0 |
| Reglas de Classic | 0 | 20 | 0 | 0 |
| Reglas de Arena | 0 | 18 | 0 | 2 |
| VAR y decisiones arbitrales | 0 | 18 | 0 | 2 |
| IA ofensiva sin balón | 0 | 20 | 0 | 0 |
| IA defensiva sin balón | 0 | 20 | 0 | 0 |
| Transiciones y contraataques | 0 | 20 | 0 | 0 |
| Personalidad táctica y entrenadores | 0 | 19 | 0 | 1 |
| Dificultad y adaptación | 0 | 20 | 0 | 0 |
| Atributos, roles y polivalencia | 0 | 20 | 0 | 0 |
| Equipos y plantillas | 0 | 19 | 0 | 1 |
| Cámara y legibilidad | 0 | 19 | 0 | 1 |
| Interfaz y HUD | 6 | 11 | 2 | 1 |
| Animaciones y presentación | 0 | 19 | 0 | 1 |
| Audio y respuesta sensorial | 0 | 20 | 0 | 0 |
| Rendimiento y estabilidad móvil | 1 | 19 | 0 | 0 |
| Guardado, perfiles y personalización | 0 | 20 | 0 | 0 |
| Economía, Zenit Points y progresión | 0 | 19 | 0 | 1 |
| Tutorial, práctica y accesibilidad | 0 | 20 | 0 | 0 |
| Calidad de IA y evaluación | 1 | 19 | 0 | 0 |
| Depuración y arquitectura | 0 | 20 | 0 | 0 |
| Seguridad, privacidad y entrega | 0 | 20 | 0 | 0 |
| Escenarios de partido y casos límite | 0 | 20 | 0 | 0 |
| Gestión del partido y estados | 0 | 20 | 0 | 0 |
| Multijugador futuro y límites de alcance | 16 | 1 | 1 | 2 |
| Balance y sensación futbolística | 0 | 0 | 0 | 20 |
| Dirección visual y modelos | 0 | 4 | 0 | 16 |
| Prueba humana y lanzamiento de prototipo | 0 | 20 | 0 | 0 |
| Córners, bandas y balón parado: control avanzado | 7 | 13 | 0 | 0 |
| Reglas de diseño y prioridades de implementación | 5 | 15 | 0 | 0 |

## Honestidad sobre el alcance
Mil requisitos no se implementan en un bloque. En la fase 33 se hizo lo siguiente: (1) se incorporaron las **cuatro ideas originales del usuario** hasta donde el Core lo permite; (2) se creó el registro de estado de los 1.000; (3) solo se han auditado con evidencia los requisitos que este bloque toca: **33 de 1.000** (4 confirmados, 20 parciales, 9 pendientes). **967 siguen «sin auditar»**: no se afirma nada de ellos.

## Las cuatro ideas originales
| Idea | Estado | Qué hay |
|---|---|---|
| 1. Presión alta agresiva | **Ya existía, no se ha tocado** | `PressingStyle.AggressivePress` (escala de presión ×1,5) desde los datos del entrenador (Fase 31) y la escena C1 del banco. Falta una prueba de coordinación de línea alta (REQ-0222) |
| 2. Balón aéreo jugable | **Implementado (parcial)** | `ActionTuning.HeadersEnabled`: un jugador de campo juega de cabeza un balón de hasta 2,2 m (alcance 0,9 m, no tiros): amortiguado hacia delante, despeje, o a puerta a ≤8 m; es un toque real del balón (último toque, evento, `ActionDetail.Headed`). Apagado por defecto en el Core; encendido en el juego y en el banco. **No hay**: volea, duelo aéreo entre dos jugadores, cálculo explícito del punto de caída, decisión del portero con balones aéreos |
| 3. Puntería compartida córner/tiro libre | **Implementado** | `FreeKickAiming.ResolveDestination` es la única función que traduce el gesto en destino (zona de portería, punto arrastrado o dirección) para tiros libres, córners y saques de banda; `MatchController.ExecuteSetPiece(FreeKickAim)` |
| 4. Potencia del saque de banda | **Implementado** | potencia = carga del botón (la del tiro libre) × `ThrowMaxPower` 0,62: más pulsación = más lejos, con tope; un saque largo es lanzamiento lofteado y uno corto va raso; barra de carga y flecha en el cliente; sin tocar nada, saca la IA a los 12 s |

## Requisitos de esta fase, por identificador
Confirmados: **0961**, **0965**, **0976**, **0979**. Parciales: 0001, 0004, 0005, 0011, 0107, 0109, 0111, 0147, 0182, 0183, 0199, 0221, 0964, 0966, 0971, 0972, 0973, 0985, 0993, 0997. Pendientes: 0108, 0146 (volea), 0175 (la acción `Cross` no existe), 0187, 0189, 0192, 0195 (duelos aéreos, portero), 0974, 0980. Evidencia en la columna del CSV.

## Medido
Partidos completos, 12 semillas pareadas, cabezazos apagados contra encendidos: ≈50 cabezazos por equipo y partido; goles 2,83/3,75 → 3,58/2,83 (suma 6,58 → 6,41, sin cambio), tiros 49 → 44, pases completados 0,59 → 0,60, córners 7,3 → 5,8. El balance de goles no se mueve; no es validación humana.

## Qué más agregaría (por impacto en cómo se siente jugar)
1. **Acción `Cross` real** (REQ-0161…0180): hoy los «centros» son pases largos; en el banco solo 12,7 de 54 llegan a un compañero y 0,6 acaban en tiro. Es lo que más cambia el juego por bandas. Requiere la acción, el remate de cabeza dirigido (ya existe el toque aéreo) y la IA de llegada al área.
2. **Duelo aéreo y portero**: dos jugadores que saltan al mismo balón (decide el atributo), y salida del portero a por balones altos (REQ-0187…0200).
3. **Interfaz de tiros libres y penaltis para la persona** en el cliente (el Core ya la tiene: `ExecuteFreeKick`, `FreeKickAim`): sin ella REQ-0361…0400 no son jugables.
4. **Cámara y legibilidad** (REQ-0621…0660): indicador de ataque y de posesión, aro del jugador, cámaras seleccionables; hoy solo hay una vista cenital.
5. **Sonido y respuesta háptica** (REQ-0681…0700): nada de audio todavía; un silbato y un golpe de balón cambian mucho la sensación.
6. **Tutorial de 2 minutos** (REQ-0761…0780) con práctica de pase y tiro, antes de las pruebas con gente nueva.
7. **Auditar los 967 requisitos restantes** con el banco y una comprobación por lote; es lo que convierte esta lista en un contrato verificable (REQ-0983-0985).
8. **Prueba humana real** (REQ-0941…0960): ninguna persona ha jugado; todo lo anterior es simulación.

## Conflictos y decisiones documentadas
- REQ-0010 (reglas iguales según dificultad) se respeta: los cabezazos, saques y córners no dependen de la dificultad; el atributo (`ControlQuality`) sí cambia la calidad del cabezazo, como en cualquier toque.
- REQ-0986/0993: no se finge nada: la «volea» y el «centro» siguen pendientes y no hay etiqueta ni efecto visual que los simule.
