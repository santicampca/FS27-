# Fase 23 — Penales, tandas y VAR (Classic) + menú web

Estado por capa (no se mezclan "hecho" y "pendiente"):

| Pieza | Core | Conectado a la simulación | Probado | Verificado visualmente |
|---|---|---|---|---|
| Penales Classic (física real) | sí | sí (falta en el área → penal) | sí | no (sin cliente Unity) |
| Penales Arena (resolutor arcade) | sí | sí, solo para tandas | sí | no |
| Tanda de penales (orden, elegibilidad, muerte súbita) | sí | sí | sí | no |
| Segundo toque / rebotes | sí | sí | sí | no |
| VAR Classic + coste ZP | sí | sí (fase `VarReview`) | sí | no |
| Cartera ZP real, cliente de VAR | **no** (solo contrato `IZenitWallet`) | — | contrato probado con cartera de prueba | — |
| Menú web | n/a (separado del Core) | no aplica | `npm run check` | sí (Playwright, escritorio y móvil) |

## Archivos
`Core/Match/Penalties.cs`, `MatchController.Penalties.cs`, `Core/Ball/GoalFrameCollider.cs`, `Core/Match/Var.cs`, `MatchController.Var.cs`; tests `PenaltyTests`, `ShootoutTests`, `VarTests`. Todos los números en `PenaltyDefaults/PenaltyTuning` y `VarDefaults/VarTuning`.

## Separación Arena / Classic
El motor pregunta a `IGameModeRules` (`PlaysPenaltiesPhysically`, `HasVar`); no hay `GameMode.X` en `Match/`. Arena: sin faltas, sin penal en juego, sin VAR; tanda arcade solo si la competición exige ganador y hay empate. Classic: árbitro, tarjetas, tiros libres, penales físicos y VAR.

## Penales
- **Classic**: tirador elegible (`PenaltyTakerCandidates`, recomendado; sin expulsados/rivales/sustituidos), dirección/precisión + potencia + altura; el portero se lanza con retardo y velocidad según atributos; el marco (`GoalFrameCollider`: postes y larguero, solo activo durante penales) devuelve rebotes. Interpretación propia, **no** se afirma copia de los controles de PES 2011 Wii.
- **Arena**: zona + potencia + altura/efecto; el portero elige lado; atributos influyen (`ArenaPenaltyResolver`).
- **Segundo toque** (en juego): el lanzador no puede tocar el rebote de poste/larguero antes que otro jugador; sí tras toque del portero. Un segundo toque ilegal se sanciona (tiro libre indirecto). Un solo gol/evento por penal.
- **Tanda**: un disparo por turno y termina en su primer resultado. Cada equipo fija su orden antes de empezar (`SetShootoutOrder`), se respeta y se guarda. Si el siguiente del orden deja de ser elegible (expulsado, sustituido) se salta al siguiente elegible, de forma determinista y la vista usa siempre `NextShootoutTaker`. Cuándo hay tanda lo decide `CompetitionResolver` (no si se permite empate; prórroga solo si está configurada); N tiros configurables (5) con final anticipado y muerte súbita. `ShootoutExecutionMode`: `Recorded` (legado, por defecto) o `Simulated`.

## VAR (solo Classic)
- Categorías: gol / roja / penal (dado o no dado), con criterio (`MinSeverity`: no se revisa cualquier contacto).
- Flujo: incidente registrado → petición (`RequestVarReview(team, incidentId, requestId)`) → validación → cobro → `MatchPhase.VarReview` (reloj parado, acciones bloqueadas, `ReviewSeconds`) → decisión final aplicada por el Core al estado real (el VAR no cambia el marcador por sí mismo) → reanudación.
- Resultados: `Confirmed`, `Corrected`, `NotReviewable`, `Rejected`, `Cancelled`.
- **Cobro de ZP**: una petición válida cobra `CostOf(categoría)` (por defecto 1, **marcador de posición**) **siempre**, cambie o no la decisión (`Confirmed` cobra igual). Se cobra una sola vez por `requestId` (doble clic/reintento no duplica). No se cobra si se rechaza antes de empezar (`NoWallet`, `InsufficientBalance`, `Duplicate`, `NotReviewable`, `Expired`, `LimitReached`, `NotEligible` —un rival no puede pedirla a su favor—, `NotAuthorized`, `ReviewInProgress`, `NotAvailable`, `MatchNotActive`) ni en `NotReviewable`. Se registra saldo antes/después, solicitante, motivo y resultado.
- Política: `RequestsPerTeam` (2), un solo pedido por incidente, `RequireHumanRequester` (la IA nunca paga; `VarAuthority` permite ampliar). Intervención automática (gratis) separada y apagada por defecto.
- Contrato de cliente: `GetVarView()` (indicador, tipo, solicitante, coste, cobro confirmado, estado, decisión original/final, mensaje corto).

## Limitaciones honestas
- Marco de portería solo existe durante penales; en jugada normal el gol sigue siendo el modelo previo.
- La economía ZP real y el cliente (UI/Unity) del VAR no están conectados; solo el contrato.
- Sin datos de repetición (replay).
- Controles de penal de Classic: interpretación propia sin validar con jugadores.
- El impacto de balance de los penales en Classic no se ha ajustado (ver batería abajo).

## Web
`Web/menu` (ver su README). Sitio estático separado del Core; sin endpoints.

## Batería rápida (20 partidos por celda, semillas 5001-5020, Fase 22 `d2fc33b` vs Fase 23)
| Celda | goles/partido F22 | F23 |
|---|---|---|
| Arena Elite | 4.80 | 4.80 |
| Arena Professional | 5.75 | 5.75 |
| Arena Novice | 9.55 | 9.55 |
| Classic Elite | 4.15 | 3.80 |
| Classic Professional | 4.50 | 5.05 |
| Classic Novice | 8.05 | 9.05 |

Arena: métricas agregadas idénticas (los CSV difieren solo porque se añadió la columna de penales). Classic: cambio dentro del ruido de n=20 y en direcciones mixtas; no se ha ajustado ningún balance. (La referencia `fast_reference.json` es de la Fase 17 y ya quedaba fuera de banda por fases anteriores; no se usa aquí.)

## Tests
Suite completa: 2539 pasan, 0 fallan (7 min 41 s). Web: `npm run build` OK.
