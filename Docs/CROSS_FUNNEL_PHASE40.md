# Fase 40 — Por qué los centros y los duelos aéreos no daban más ocasiones

Herramienta: `Phase40Tool_CrossFunnel` (Explicit; `FN_SEEDS`, `FN_ONLY`, `FS27_AER="Campo=valor"`). Sigue **cada centro real** de partidos completos (Classic, 16 min, pares reales de equipos, semillas 1–36) y anota quién juega el balón primero, cómo, y si hay un disparo o un cabezazo a puerta del mismo equipo en los 6 s siguientes. No cambia el partido.

**Límites de la medición.** Las semillas son deterministas, no muestras independientes. Las cifras son de **los dos equipos juntos** por partido. «Disparo en 6 s» no prueba causalidad: un equipo que centra ya está en ataque. Por eso se compara con un **control**: los pases (no centros) de un equipo en el sexto de campo atacante. Un cabezazo «a puerta» (≤ 8 m, `HeaderShotSpeed`) **no es un disparo** en el Core (decisión de la fase 33, para no tocar el balance) y por eso no entraba en las métricas de disparos del banco.

## 1. Resultados (36 partidos por configuración)
| | sin centro en equipo (fase 35) | fase 36 + 37 + 39 (actual) |
|---|---|---|
| centros por partido (ambos equipos) | 23,1 | 22,9 |
| el centro llega a un compañero | 68,6 % | **73,1 %** |
| lo juega primero un rival | 28,9 % | **22,2 %** |
| primer toque del compañero | cabeza 43 %, control 25 % | **control 54 %**, cabeza 19 % |
| disparo del equipo en 6 s | 31,5 % | **53,6 %** |
| cabezazo a puerta en 6 s | 34,3 % | 17,9 % |
| control: disparo en 6 s tras un pase normal en el último sexto | 21,5 % | 26,2 % |
| disparos / goles por partido | 84,0 / 7,0 | 95,0 / 8,3 |

## 2. Lectura
1. **Cada centro vale más que antes**: llegan más, los intercepta menos el rival y acaban en disparo ~el doble de veces que un pase normal del último sexto (54 % frente a 26 %). La mecánica de las fases 36–37 funciona.
2. **Pero se centra igual de poco.** ≈ 23 por partido entre los dos equipos (≈ 11 por equipo) en las tres configuraciones: la decisión de centrar sigue siendo la de la fase 28 (`CrossToBox`). Mejorar cada centro sin centrar más no sube el total de ocasiones: los disparos por partido apenas se mueven por esto.
3. **El receptor controla antes de rematar.** Con centros rasos y tensos (la mitad de los centros ya no son altos), el primer toque del compañero es un **control** el 54 % de las veces y un cabezazo solo el 19 %. Entre control y disparo pasa una decisión de la IA (0,15 s) y llega el defensa. Ese retraso es la fuga principal.
4. **Los cabezazos a puerta no se contaban.** Con la mecánica antigua (todos altos) había 8,8 cabezazos a puerta por partido; ahora 5,1. Las métricas de «disparos» no los incluían, así que el descenso de ocasiones por cabezazo no se veía y parecía que «no había ocasiones». El informe del banco debe sumarlos como intentos aparte (no como disparos).
5. **El duelo aéreo mejoró la elección, no la cantidad**: la escena A7 muestra que los defensas, con mejor `AerialQuality`, ganan más balones aéreos. Eso es lo esperable y no genera más ocasiones atacantes.

## 3. Arreglo probado: rematar al primer toque
`AerialPlay` + `ArmKick`: el jugador que ve venir el pase de un compañero delante de la portería decide golpearlo sin controlarlo; el Core hace el disparo con el toque que habría sido la recepción (una primera versión que decidía solo cuando el balón ya estaba a su alcance **no se disparó nunca** en 36 partidos —resultado idéntico—, porque los jugadores deciden cada 0,15 s y el Core recibe el balón antes). Prueba: `ThePlayerInFrontOfGoal_StrikesTheTeamMatesPassFirstTime...`.
- Con alcance de 6,5 m (36 partidos): el 19 % de los primeros toques de los centros pasa a ser disparo o volea, el disparo en 6 s sube del 54 % al **68 %**, y los disparos por partido de 95,0 a 99,2.
- Con 9 m: disparos 125,7 (+32 %), goles 9,0: demasiado, **descartado**.
- Verificación con **72 semillas nuevas** (100–171), alcance 6,5 m frente a apagado: disparos del rival +6,4 (±1,5), xG del rival +1,15 (±0,22); en el equipo local ≈ sin cambio; **goles totales +0,72 ± 0,40**. En conjunto +7 % de disparos y +12 % de xG.

## 4. Decisión que NO se ha tomado por ti
Por ese efecto en el equilibrio (que se suma a la decisión pendiente sobre el ritmo de goles de Classic), **el rematador al primer toque queda apagado por defecto** (`AerialPlayDefaults.FirstTimeRange = 0`). Está probado, medido y se enciende con un número (`FirstTimeRange = 6,5`). Dime si lo quieres encendido, con otro alcance o solo en Arena.
No se reescribió ninguna línea base del banco en esta fase: el comportamiento por defecto no cambia (la guarda de línea base pasa sin tocarla).

## 5. Siguiente
1. Que la IA **centre más cuando corresponde**: ajustar `CrossToBox` con el embudo como medida (frecuencia por zona, no por partido).
2. Sumar los cabezazos a puerta como «intento» en el banco y en `AiDiagnostics`.
3. Un estado de salto en el Core para que el duelo aéreo tenga momento (REQ-0185/0195).
