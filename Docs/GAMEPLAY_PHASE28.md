# Fase 28 — Pases con física (primera entrega)

## Qué se hizo
- `PassPhysics` (Core/Decision): calcula la potencia de un pase a partir de la física real del balón (rodadura, rozamiento, vuelo del pase largo). Antes la IA golpeaba casi siempre demasiado fuerte: 15–24 % de los pases de Classic salían del campo y los pases largos llegaban ~3 %.
- `PassTo`, `ThroughTo` y el saque del portero usan esa potencia (`PassPhysicsEnabled`, activo).
- `PassRisk` (probabilidad de que un pase llegue según rivales en el trayecto) y su peso en `ReceiverScore`: implementado pero **desactivado por defecto** (`PassRiskDisabled`): con él Classic Profesional marcaba +75 % de goles (4,5 → 7,85) sin subir el porcentaje de pases completados.
- Diagnóstico `PassDiagnosticsTests` (`FS27_REPORT=ruta FS27_REPORT_MATCHES=12`): informe de pases por puesto.

## Resultado medido (batería rápida, 20 partidos por celda)
Goles Profesional: Arena 5,75 → 6,45; Classic 4,50 → 5,70. Pases completados sin cambio apreciable (≈0,40–0,42).

## Pendiente (no hecho)
- Saques de banda/portería/córner con lógica de IA (la mitad de los pases fallidos ocurren justo tras una reanudación con rival a ~2,5 m).
- Laterales: subir por banda, solapes, centros.
- Pasar con un rival pegado al pasador (proteger/regatear).
