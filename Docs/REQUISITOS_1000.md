# FS27 — Lista maestra de 1.000 requisitos: estado real (Fase 33)

Fuente: `FS27_lista_maestra_1000_requisitos.md` (1.000 identificadores `FS27-REQ-0001…1000` en 50 secciones). Estado por requisito: `Docs/requisitos_estado.csv` (id, sección, estado, evidencia, texto).

## Honestidad sobre el alcance
Mil requisitos no se implementan en un bloque. En esta fase se hizo lo siguiente: (1) se incorporaron las **cuatro ideas originales del usuario** hasta donde el Core lo permite; (2) se creó el registro de estado de los 1.000; (3) solo se han auditado con evidencia los requisitos que este bloque toca: **33 de 1.000** (4 confirmados, 20 parciales, 9 pendientes). **967 siguen «sin auditar»**: no se afirma nada de ellos.

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
