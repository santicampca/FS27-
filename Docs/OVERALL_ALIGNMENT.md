# FS27 — Overalls que superaban la tolerancia: atributos ajustados, objetivos intactos

> Revisados los **136 jugadores de campo** (20 porteros aparte). 14 tenían un Overall calculado que superaba en más de 5 puntos el Target OVR de la investigación; **ya no queda ninguno** (0 avisos `TargetOvrDiffers`). Los **objetivos (las medias de la investigación) no se tocaron ni se subieron**. Sin push, sin PR.

## Qué se hizo

El Overall oficial lo calcula `PlayerRatingCalculator` desde los atributos y el perfil. En estos 14 jugadores los atributos investigados salían más altos que su propio objetivo. En lugar de subir el objetivo, se **bajaron los 12 atributos del jugador la misma cantidad** (mínimo 1 por atributo) hasta que el Overall calculado quedó **a +3 del objetivo** (dentro de la tolerancia de 5 y como el resto de la biblioteca). Así se conserva la forma del perfil (quién es rápido, quién pasa mejor...) y solo baja el nivel.

Cada cambio es una corrección documentada (`attr.<Atributo>`, clase *Design*, antes → después y razón) en el `..._CORRECTIONS_V1.md` de su base: 168 filas en total. No se tocó ningún rol ni afinidad, ni la tolerancia (sigue en 5), ni los documentos fuente. Los registros finales (`..._RECORDS_V1.json`) se regeneraron y un test comprueba que son fuente + correcciones.

## Los 14 jugadores

| Jugador | Objetivo (intacto) | Overall antes | Bajada por atributo | Overall ahora |
|---|---|---|---|---|
| Calvin Bassey (cb) | 76 | 82 | −3 | 79 |
| Alphonso Davies (lateral) | 81 | 87 | −3 | 84 |
| João Cancelo (lateral) | 81 | 87 | −3 | 84 |
| Luka Modrić (MC) | 81 | 87 | −3 | 84 |
| Isco (MCO) | 80 | 86 | −3 | 83 |
| Kevin De Bruyne (MCO) | 82 | 88 | −3 | 85 |
| Florian Wirtz (MCO) | 81 | 87 | −3 | 84 |
| Estêvão (extremo) | 78 | 86 | −5 | 81 |
| Kaoru Mitoma (extremo) | 77 | 87 | −7 | 80 |
| Lionel Messi (SD) | 83 | 90 | −4 | 86 |
| Neymar (SD) | 80 | 89 | −6 | 83 |
| Donyell Malen (DC) | 79 | 85 | −3 | 82 |
| Pierre-Emerick Aubameyang (DC) | 80 | 86 | −3 | 83 |
| Karim Adeyemi (DC) | 80 | 87 | −4 | 83 |

Efecto secundario: los valores de 95+ de Davies (velocidad 98 → 95), Messi, Neymar y Adeyemi bajaron; ya solo Mbappé y Haaland tienen atributos de 95 o más entre estos jugadores.

## Otras revisiones

- **Porteros:** usan su propio GRL; las diferencias frente al objetivo del archivo son de 1-2 puntos (documentadas), dentro de la tolerancia. No se tocaron.
- **Sin objetivo:** Andy Robertson y Marcos Llorente (autoría propia) no tienen Target OVR; su Overall calculado es 80.
- **Si prefieres otro nivel** (p. ej. que el Overall quede exactamente en el objetivo, o a +2), es cambiar un número por jugador y regenerar.

## Cómo se verifica

Los tests de cada base comprueban que ningún jugador supera la tolerancia, que no hay `TargetOvrDiffers`, que el objetivo sigue siendo el investigado, que solo estos 14 tuvieron atributos bajados y que la tolerancia sigue en 5. Suite completa: 1555 tests, 0 fallos.
