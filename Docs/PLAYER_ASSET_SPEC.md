# FS27 — Especificación técnica del primer jugador 3D

## 1. Cómo leer las cifras

**Todas las cifras de este documento son objetivos iniciales, no requisitos.** Se han estimado sin poder medir en Unity ni en un teléfono.
Se confirman o corrigen con *profiling* real (ver §12) sobre un dispositivo de gama media. Si una cifra choca con el estilo
([VISUAL_STYLE.md](VISUAL_STYLE.md)) o con el rendimiento medido, **se cambia la cifra, no se fuerza el arte**.

Contexto de partida: 5 vs 5 = **10 jugadores en pantalla** (+ balón), cámara TV con el jugador a ~110 px de alto,
objetivo 60 FPS en gama media-alta y 30 FPS estables en gama baja.

## 2. Presupuesto de polígonos y LOD

| LOD | Uso | Triángulos (objetivo) | Altura en pantalla* | Materiales | Huesos |
|---|---|---|---|---|---|
| **LOD0** | cercanas: selección, repeticiones, celebraciones | 6.000 – 8.000 | ≥ ~30 % | hasta 3 | ~50 |
| **LOD1** | **gameplay por defecto** (TV y TV Close) | 2.500 – 3.500 | ~8 – 30 % | 2 | ~32 |
| **LOD2** | cámara Wide, jugadores lejanos | 1.000 – 1.500 | ~3,5 – 8 % | 1 | ~20 |
| **LOD3** | muy lejos / respaldo en gama baja | 300 – 500 | ~1 – 3,5 % | 1 | ~12 o rígido |

\* Porcentaje de la altura de pantalla que ocupa el jugador; con los perfiles actuales: TV ≈ 10 %, TV Close ≈ 15 %, Wide ≈ 6 %.
Los umbrales de cambio de LOD son un punto de partida y se ajustan viendo el modelo en cada cámara.

Presupuesto de escena (solo jugadores, 10 en pantalla, casi todos en LOD1): **~25.000–35.000 triángulos**. Es una referencia holgada para gama media;
si el *profiling* muestra margen, LOD1 puede subir; si no, baja.

Reglas de reducción entre LOD:
- Se conserva **la silueta** (cabeza + pelo, hombros, brazos, piernas, botas) y se quita primero el detalle interior (dedos, orejas, costuras modeladas, bolsillos).
- LOD1 es el **"modelo de diseño"**: es el que más se verá; se revisa con más cuidado que LOD0.
- Sin *cross-fade* por dithering entre LODs (cuesta en móvil): el cambio es limpio y los modelos deben parecerse lo suficiente para que no salte.

## 3. Texturas y atlas

| Elemento | LOD0 | LOD1 | LOD2 / LOD3 |
|---|---|---|---|
| Atlas de cuerpo + uniforme (color base) | 1024 × 1024 | 512 × 512 | 256 × 256 |
| Máscara de equipo/color (RGB) | 512 | 256 | 128 |
| Hoja de expresiones (ojos/boca/cejas) | 512 × 512 | estática en el atlas | – |
| Atlas de números | 256 × 256 compartido por todos los jugadores | idem | idem |

- **Resolución máxima recomendada: 1024 px por atlas de personaje** (solo LOD0). El gameplay vive en 512 o menos: a 110 px de alto una textura de 512 ya está sobremuestreada.
- Compresión **ASTC** (6×6 como punto de partida; probar 5×5/8×8) en iOS y Android moderno; **ETC2** como respaldo en Android antiguo. Mipmaps activados; *streaming* de mipmaps según proyecto.
- Objetivo de memoria por personaje único: **≲ 1,5 MB de texturas comprimidas** (todos los LOD). Con 10 jugadores y variantes compartidas, bajo unos pocos MB en total.
- **Un atlas por personaje** (cuerpo, uniforme, botas, accesorios) y no una textura por pieza. La piel y el color de pelo/equipo se parametrizan, no se hornean.
- **Color de equipo por máscara**: R = primario, G = secundario, B = acento. Un solo modelo y atlas sirven a todos los equipos.
- El **número** se resuelve con un atlas compartido de dígitos (desplazamiento de UV), no con una textura única por jugador. *(Decisión pendiente de prototipo.)*

## 4. Materiales

| LOD | Máx. materiales | Contenido |
|---|---|---|
| LOD0 | 3 | cuerpo/uniforme · cara (hoja de expresiones) · pelo |
| LOD1 | 2 | cuerpo/uniforme/pelo · cara estática o integrada |
| LOD2–3 | 1 | todo en el atlas |

- **Un shader de personaje propio** (toon suave + *rim light* + máscara de equipo), una sola familia con variantes por calidad.
- Compatible con el **SRP Batcher** (URP): preferir **instancias de material** (pocas combinaciones equipo × tono de piel × pelo) frente a `MaterialPropertyBlock`, que rompe el batching.
- Opaco en todo el cuerpo. **Sin transparencia** (ni pelo con tarjetas translúcidas, ni accesorios translúcidos).
- Mapas: color + máscara. **Normal map solo en LOD0** (y opcional). Sin mapa metálico/rugosidad: valores constantes por zona.

## 5. Rig y esqueleto

- **Un único esqueleto para todos los jugadores** (misma jerarquía y nombres). Variación de complexión/altura por escala y mallas sobre ese esqueleto, no por esqueletos distintos. Así comparten animaciones.
- Huesos aproximados: **LOD0 ~50 · LOD1 ~32 · LOD2 ~20 · LOD3 ~12**. Recuento orientativo:
  - tronco y cabeza: caderas, 3 de columna, cuello, cabeza (6)
  - brazos: 2 × (clavícula, brazo, antebrazo, mano) (8) + manos simplificadas (palma + pulgar + bloque de dedos: 3 × 2 = 6)
  - piernas: 2 × (muslo, pierna, pie, punta) (8)
  - cara (solo LOD0): mandíbula, 2 ojos, 2 párpados (opcional) (~5) · pelo (solo LOD0, si lo hay): 2–3 por cola/trenza
  - resto: huesos de ayuda (torsión de antebrazo/muslo) y *sockets* (ver §11)
- Orientación estándar: **Y arriba, +Z adelante, pivote en los pies**, escala 1 unidad = 1 m.
- **Rig genérico por defecto**; Humanoid solo si hace falta reutilizar animaciones externas. Decisión por *profiling* (coste del Animator con 10 personajes).
- Sin huesos con escala no uniforme animada; sin *constraints* ni IK en tiempo de ejecución en el prototipo (el IK de pies/mirada, si se quiere, es una mejora posterior medida).
- Animaciones **sin *root motion***: el movimiento lo decide el gameplay (`PlayerMovement`); el clip se mueve "en el sitio" y se sincroniza por velocidad.

## 6. Skinning

- **Máx. 4 influencias por vértice** en LOD0–LOD1; **2** en LOD2–LOD3. La calidad de *skin weights* es un ajuste global de Unity por nivel de calidad: se coordina con los perfiles de calidad.
- Pesos normalizados; limpiar influencias residuales < ~0,05. Sin vértices sin pesos.
- Deformación cuidada en hombros, caderas y rodillas (son lo más visible al correr y patear).
- **GPU skinning** activado; sin *blendshapes* en gameplay (las expresiones van por hoja de UV). Si LOD0 usara *blendshapes* en cercanas, solo en la cabeza y solo en esas escenas.

## 7. Cabello y accesorios

**Cabello**
- Esculpido como **malla sólida** con volúmenes claros. **Sin tarjetas alfa** en el gameplay (overdraw y ordenación); cualquier uso en LOD0 se mide antes.
- Pelo incluido en el atlas principal (o 1 material propio solo en LOD0). Color por parámetro.
- Movimiento secundario: **2–3 huesos como máximo** por cola/trenza y solo en LOD0–LOD1; sin simulación física en el prototipo.
- Cada peinado debe leerse como silueta a ~16 px de cabeza.

**Accesorios** (cinta de pelo, muñequeras, brazalete de capitán, guantes de portero, protecciones)
- Máx. **2 accesorios** visibles por jugador en LOD0; se **fusionan en la malla y el atlas** en LOD1+. Sin materiales ni draw calls propios.
- Sin accesorios translúcidos ni mallas sueltas; si se mueven, van enlazados a un hueso existente.
- El balón es un objeto aparte (ya existe como esfera física); su apariencia se define en otro documento.

## 8. Sombras

- Gameplay: **una luz direccional** con sombras suaves *opcionales* y de corta distancia (el campo mide ~40 m), una cascada.
- Los jugadores **proyectan** sombra pero **no la reciben** (sin autosombra) en gameplay.
- La sombra proyectada puede usar el **LOD2** como malla de sombra (*shadow proxy*) si el *profiling* lo justifica.
- Respaldo para gama baja: **sombra de mancha** (un cuadrado con textura bajo el jugador), que también ayuda a leer la posición. Misma lectura de "dónde está pisando".
- Cercanas/cinemáticas: sombras de mayor calidad **solo en esas escenas**.

## 9. Relación con mobile: la apariencia sobrevive a la reducción

Principio: **al degradar calidad se pierde detalle, nunca lectura.** Orden en que se recorta (de primero a último) en gama baja:

| Paso | Recorte | Qué NO se toca |
|---|---|---|
| 1 | Sombra real → sombra de mancha; se apaga el *rim light* | silueta, colores |
| 2 | Resolución de texturas (mip) y desactivar normal map | color de piel/pelo/uniforme, número |
| 3 | LOD más agresivo (sesgo de LOD) y hoja de expresiones estática | proporciones, forma del pelo |
| 4 | Fusión de materiales (3 → 2 → 1) y menos huesos | contraste de equipo, portero distinto |
| 5 | 2 influencias de piel, menos frecuencia de animación lejos | poses clave de la carrera |
| 6 | Resolución de render dinámica (solo 3D; el HUD no baja) | legibilidad del HUD |

**Invariantes de legibilidad** (no se sacrifican en ningún perfil): relación cabeza/cuerpo, forma del peinado, tono de piel, colores de uniforme y portero,
número de espalda, silueta de botas, contraste jugador/césped.
Comprobación: el **test de silueta a ~110 px** ([VISUAL_STYLE.md](VISUAL_STYLE.md) §13) debe pasarse en **todos** los perfiles de calidad.

## 10. Optimización (resumen)

- **LOD Group** con umbrales por altura en pantalla; `LOD bias` por perfil de calidad.
- **Animator**: *culling* total fuera de pantalla; sin capas innecesarias (máx. 2); parámetros por *hash*, sin cadenas por frame.
- **Pooling** de modelos de jugador: nada de `Instantiate`/`Destroy` durante el partido.
- Mallas: lectura/escritura desactivada, compresión de malla, índices de 16 bits, sin tangentes/UV2/colores de vértice que no se usen.
- Pocas combinaciones de material (SRP Batcher); atlas compartidos; sin *overdraw* (nada translúcido).
- Animaciones comprimidas (reducción de claves), autoría a 30 FPS y muestreo a la velocidad del juego.
- Sin asignaciones por frame en la capa de presentación (se aplica la misma regla que al resto del proyecto).

## 11. Contrato del modelo (para que cualquier modelo encaje)

Un modelo de jugador debe cumplir, con independencia de quién lo haya hecho:

- Raíz en los **pies**, +Z adelante, Y arriba, 1 unidad = 1 m, altura ~1,80 m (con pelo), encaja en la cápsula de colisión actual (≈ 0,35 m de radio).
- Esqueleto con la **jerarquía y nombres estándar** del proyecto.
- *Sockets* (huesos vacíos) nombrados: `Socket_Head` (marcadores), `Socket_FootL`, `Socket_FootR` (polvo, sombra), `Socket_Chest` (efectos), `Socket_Hips`.
- `LODGroup` con 3–4 niveles según §2; materiales según §4; sin escalas negativas.
- Se coloca bajo el hijo **`Visual`** del prefab `Player` actual; no añade lógica de juego ni colliders propios.

## 12. Qué hay que medir en Unity (para confirmar o corregir este documento)

1. Coste de GPU/CPU con 10 personajes LOD1 en un dispositivo de gama media y otro de gama baja (milisegundos por frame).
2. Draw calls y *batches* con el shader propio y las instancias de material por equipo.
3. Coste del Animator (rig genérico frente a Humanoid) y de los *skin weights* 4 vs 2.
4. Coste real de sombras (real frente a mancha) y de las tres calidades.
5. Memoria de texturas y mallas por personaje.
6. Que LOD1 se vea bien en TV y TV Close, y LOD2 en Wide (revisión visual, con el test de silueta).

Los resultados se anotan aquí y los números de §2–§8 se actualizan.
