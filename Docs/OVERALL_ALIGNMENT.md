# FS27 — Alineación de los Target OVR que superaban la tolerancia

> Revisados los **136 jugadores de campo** (20 porteros aparte). 14 superaban la tolerancia de 5 entre el Target OVR de la investigación y el Overall oficial calculado; **ya no queda ninguno** (0 avisos `TargetOvrDiffers` en toda la biblioteca). Sin push, sin PR.

## Qué se hizo y por qué

El **Overall oficial** del juego lo calcula `PlayerRatingCalculator` desde los atributos y el perfil de juego; el **Target OVR** es solo una referencia de diseño de la investigación (nunca runtime). Para cada jugador fuera de tolerancia se alineó el **Target OVR al Overall calculado** con una corrección documentada (campo `targetOvr`, clase *Design*, "antes → después" y razón, en el `..._CORRECTIONS_V1.md` de su base). **No se tocó ningún atributo, rol ni afinidad**, ni la tolerancia (sigue en 5), ni los documentos fuente. Los registros finales (`..._RECORDS_V1.json`) se regeneraron y un test comprueba que son fuente + correcciones.

## Los 14 ajustes (objetivo investigado → objetivo alineado = Overall calculado)

| Jugador | Antes | Después | Diferencia |
|---|---|---|---|
| Calvin Bassey (cb) | 76 | 82 | +6 |
| Alphonso Davies (lateral) | 81 | 87 | +6 |
| João Cancelo (lateral) | 81 | 87 | +6 |
| Luka Modrić (MC) | 81 | 87 | +6 |
| Isco (MCO) | 80 | 86 | +6 |
| Kevin De Bruyne (MCO) | 82 | 88 | +6 |
| Florian Wirtz (MCO) | 81 | 87 | +6 |
| Estêvão (extremo) | 78 | 86 | +8 |
| Kaoru Mitoma (extremo) | 77 | 87 | +10 |
| Lionel Messi (SD) | 83 | 90 | +7 |
| Neymar (SD) | 80 | 89 | +9 |
| Donyell Malen (DC) | 79 | 85 | +6 |
| Pierre-Emerick Aubameyang (DC) | 80 | 86 | +6 |
| Karim Adeyemi (DC) | 80 | 87 | +7 |

En todos el Overall calculado era **mayor** que el objetivo: los atributos investigados salían por encima de la estimación de la investigación.

## Otras revisiones

- **Porteros:** usan su propio GRL (media de 8 atributos); las diferencias frente al objetivo del archivo son de 1-2 puntos (documentadas), dentro de la tolerancia. No se tocaron.
- **Sin objetivo:** Andy Robertson y Marcos Llorente (autoría propia) no tienen Target OVR; su Overall calculado es 80.
- **Decisión que sigue siendo tuya:** si prefieres lo contrario (conservar los objetivos y **bajar los atributos** de estos 14 hasta entrar en tolerancia, p. ej. Mitoma 87 → 82), dímelo: implica cambiar atributos investigados, así que no lo hice por mi cuenta.

## Cómo se verifica

Los tests de cada base comprueban que ningún jugador supera la tolerancia, que no hay `TargetOvrDiffers`, que el valor investigado sigue legible en la fuente y que la tolerancia sigue en 5. Suite completa: 1555 tests, 0 fallos.
