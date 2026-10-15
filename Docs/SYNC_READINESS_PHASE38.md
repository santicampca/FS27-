# Fase 38 — Preparación para multijugador futuro (sin red)

No hay código de red ni se afirma que exista juego online (REQ-0887, 0899). Esto fija las bases para no impedirlo después.

## Qué existe
- **Estado autoritativo único** (REQ-0884, 0889, 0897): `MatchController.State` (reloj, marcador, balón, jugadores) es la única fuente de verdad; el cliente solo envía entradas y lee. Un futuro cliente online sería un espejo que aplica comandos, nunca una segunda simulación con otra verdad.
- **Comandos discretos** (REQ-0888): toda acción táctil es un `NormalizedInput` (palo + un botón + gesto + dirección + potencia + curva), saneado. `InputCommandLog` los registra por paso de simulación y los guarda como texto (una línea por comando, floats ida y vuelta).
- **Reproducción** (REQ-0882): con la misma semilla, los mismos equipos y los mismos comandos, el partido es idéntico bit a bit (`Phase38Tests.TheSameCommands_Reproduce...`: 1.500 pasos, mismo checksum en cada control, también tras escribir y leer el registro).
- **Checksum** (REQ-0890, 0896 parcial): `MatchChecksum.Of` (FNV-1a de reloj, fase, marcador, balón, jugadores). Un comando distinto cambia el valor (`ADifferentCommand_ChangesTheChecksum`). Registro corrupto: se rechaza entero.
- **Identificadores estables** (REQ-0883): los ids de jugador vienen de los datos del equipo, no de contadores ni de la semilla (prueba con dos semillas).
- **Offline sin cuentas ni servidor** (REQ-0892, 0893, 0895): el HTML funciona sin conexión (0 peticiones de red en `offline.mjs`) y los jugadores son datos ficticios locales.
- **Menú** (REQ-0885, 0886, 0891): un botón «ONLINE · próximamente» atenuado que solo explica que necesita conexión y que no existe; no hay otra función online mostrada como disponible.

## Qué NO existe (y por qué)
- Latencia y entradas retrasadas (REQ-0896): no se pueden probar sin red; solo queda la base (comandos con paso). Pendiente para cuando haya transporte.
- Privacidad y consentimiento online (REQ-0894), compras online (REQ-0895 solo en el sentido de que nada bloquea el modo local): decisiones de producto futuras.
- Diferencias entre plataformas de coma flotante: el Core usa `float` IEEE y no usa aleatoriedad ni hora; la igualdad bit a bit entre arquitecturas (p. ej. WASM vs. Unity IL2CPP) **no está probada**: requiere ejecutar el mismo registro en ambos y comparar checksums.
