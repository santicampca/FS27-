# FS27 — Identidad visual de los jugadores

Estado: **propuesta de dirección artística** (aún sin arte). Se valida cuando exista el primer personaje en Unity.
Las cifras marcadas como *objetivo inicial* son hipótesis, no requisitos rígidos.

> Nota de método: esta guía parte de la descripción escrita de la referencia (3D cartoon/arcade moderno, personajes
> deportivos expresivos). La imagen de referencia no está en el repositorio, así que conviene que quien la tenga
> contraste este documento con ella y corrija lo que no coincida. La referencia es **solo una guía de estilo**.

## 0. Originalidad (regla firme)

No se copian personajes, caras, modelos, texturas, uniformes, logos, nombres ni assets protegidos de ningún juego o marca.
Los jugadores son **ficticios y originales**; los nombres de equipo, escudos y patrocinadores son provisionales y se revisan
(marcas registradas) antes de publicar. Esta guía describe *principios de estilo*, no reproduce ningún diseño existente.

## 1. Filosofía visual

**"Fútbol arcade estilizado, moderno y legible."** Cinco pilares, en orden de prioridad:

1. **Legibilidad primero.** Lo primero que se lee en pantalla es: quién es mi equipo, quién tiene el balón, hacia dónde corre. Después, el detalle.
2. **Silueta reconocible.** Cada jugador se distingue por forma (pelo, cuerpo, botas), color y número, no por rasgos finos de la cara.
3. **Estilizado, no chibi ni hiperrealista.** Proporciones ligeramente exageradas en lo que comunica (cabeza, manos, pies, hombros); cuerpo atlético creíble.
4. **Personalidad en el movimiento.** La expresividad vive en la animación y las poses, que funcionan a cualquier distancia, más que en la geometría de la cara.
5. **Barato por diseño.** Formas limpias, pocos materiales, sombreado simple. Lo que se ve bien con poco, se ve bien en un móvil de gama media.

### Por qué la cámara TV manda (con números)

Con los perfiles de cámara actuales (`TV`, `TV Close`, `Wide`) y una pantalla de 1080 px de alto:

| Cámara | Jugador en pantalla | Cabeza | Balón | En un móvil de ~400 ppi |
|---|---|---|---|---|
| TV (por defecto) | ~110 px (10 % de la altura) | ~16 px | ~31 px | **~7 mm** |
| TV Close | ~163 px (15 %) | ~24 px | ~46 px | ~10 mm |
| Wide | ~64 px (6 %) | ~9 px | ~18 px | ~4 mm |

(Cálculo: distancia y FOV de `CameraProfileData`; la cámara mira hacia abajo ~39°, lo que acorta un 22 % las figuras verticales.)

Consecuencia de diseño: **en juego la cara no se ve**. Por eso la identidad se carga en silueta, pelo, color de piel, uniforme, botas y número;
y los rasgos finos (ojos, cejas, boca) se diseñan para las cámaras cercanas, donde sí importan. Si más adelante la cámara TV resulta demasiado lejana
y se acerca, mejor; el estilo no debe depender de ello.

## 2. Proporciones

- **~6,5 cabezas de alto** (el realismo ronda 7,5; el chibi 2–3). Altura de referencia 1,80 m (encaja con la cápsula actual de 1,8 m × 0,35 m de radio).
- **Cabeza ~+12 % más grande** que la proporcional: ayuda a leer la silueta y el pelo a 16 px.
- **Hombros anchos, cintura marcada, piernas fuertes.** Cuerpo atlético con volumen en muslos y gemelos (se leen al correr).
- **Manos ~+15 % y botas ~+15–20 %**: las extremidades terminales son lo que comunica el movimiento (golpeos, carrera).
- Anchura máxima de hombros ≲ 0,70 m (cabe en la cápsula de colisión); el modelo **no** debe sobresalir de ella de forma notable.
- Variantes de complexión (esbelto / atlético / fuerte): ajustes de escala y volumen sobre el **mismo esqueleto** (ver spec de assets).

## 3. Rostro

- Cabeza de forma simple y escultórica: mandíbula definida, pómulos suaves, nariz sencilla. Sin poros, arrugas ni detalle fotográfico.
- Planos limpios, sombreado suave; los rasgos se definen por **forma y color**, no por ruido de textura.
- Variación entre jugadores mediante un **kit modular de rasgos**: forma de cabeza (3–4), nariz (3), cejas (4), boca/sonrisa (3), vello facial (2–3).
- Mirada y emoción son lo más importante del rostro: ver §4 y §18.

## 4. Ojos

- **Grandes y expresivos pero no caricaturescos**: ~1,3× el tamaño realista; esclerótica clara, iris coloreado, brillo (*highlight*) fijo y legible.
- Párpados que permiten entrecerrar/abrir (expresión de esfuerzo, sorpresa, enfado).
- Recurso móvil recomendado: **ojos y boca como hoja de expresiones** (atlas de UV) en vez de *blendshapes* pesados; cambiar expresión = desplazar UV. Cumple "expresivo" con coste mínimo.
- En LOD lejanos los ojos se funden en una mancha oscura bien colocada: el diseño debe sobrevivir a eso (cejas y párpados marcados).

## 5. Cabello

- **Peinados como volúmenes esculpidos** (mechones agrupados con forma clara), no cabello fino por tarjetas translúcidas.
- Cada peinado debe leerse como **silueta** a 16 px (p. ej.: rizos altos, corte al ras con raya, trenzas, cola, cabeza rapada con barba, banda). Es el identificador principal #1 del jugador en TV.
- Paleta de color de pelo limitada y saturada, separada del color del césped.
- Movimiento secundario (cola, trenzas) con 2–3 huesos solo en LOD cercano; en los demás, pelo rígido.

## 6. Piel

- Paleta **estilizada** de 6 tonos base, de claro a oscuro, cálidos, con subtono controlado (sin gradientes fotorrealistas). Valores iniciales orientativos:
  `#F6D2B8`, `#E8B48F`, `#C98A5E`, `#A56A44`, `#7B4A2D`, `#4F2E1C`.
- Sombreado con 2–3 tonos (luz/medio/sombra) y un toque de **subsurface falso** (tinte cálido en las sombras) para que no se vea plástico.
- Todos los tonos deben conservar contraste con el uniforme y con el césped.

## 7. Manos

- Forma **tipo guante simplificado**: palma + pulgar + bloque de dedos (3 segmentos máx.). Silueta clara al correr y al celebrar.
- Portero: variante de manos con guantes más voluminosos y de color propio.
- Sin dedos individuales animados salvo en un posible primer plano (decisión futura, tras medir).

## 8. Pies / botas

- **Botas con silueta marcada** y color propio (acento de color, tachones sugeridos, no modelados). Es lo que más se mueve: debe leerse.
- Suela y tacos como forma simple; sin cordones modelados (van en la textura).
- Calcetines con bandas de color que enfaticen el movimiento de las piernas.

## 9. Uniforme

- **Camiseta, pantalón corto y medias** con buen acabado: costuras y cuellos como detalle de textura/normal sutil solo en LOD0; patrones **gráficos y simples** (franjas, bandas, bloques, degradados vectoriales) que sigan siendo legibles a 110 px.
- Tres zonas de color por equipo definidas por **máscara** (primario / secundario / acento), de modo que un mismo modelo sirva para cualquier equipo.
- **Número** grande en la espalda (legible en TV) y en el pantalón; nombre solo en cercanas.
- Equipación de portero **claramente distinta** de ambos equipos y del césped.
- Sin logos reales; escudos y patrocinios ficticios.

## 10. Materiales

- Un **shader de personaje propio** (toon suave + luz de borde), no PBR realista: 2–3 bandas de sombreado con transición suave, *rim light* sutil, brillo controlado en botas y balón.
- Telas: mate con micro-variación; botas/guantes: algo de brillo; piel: suave. Sin transparencias en el cuerpo.
- Una sola familia de shader con variantes por calidad (el rim y el detalle se apagan en gama baja; la silueta y el color no).
- Contornos: **sin malla de contorno** (costaría draw calls y overdraw); la separación visual se consigue con valor de color y *rim light*.

## 11. Iluminación

- **Gameplay (TV):** una luz direccional principal con sombra corta, ambiente plano, sombreado toon; el contraste jugador/césped se consigue por color y valor, no por luces extra.
- **Cercana / cinemática** (selección, repeticiones, celebraciones): equipo de luces explícito *solo en esas escenas*: luz clave cálida, relleno frío suave, contraluz (*rim*) de color del equipo; fondo desenfocado o con viñeta. Cuesta más y por eso solo se usa donde el jugador ocupa la pantalla.
- Las luces extra son por objeto y limitadas (1–2); nada de luces dinámicas en el campo durante el partido.

## 12. Colores

- **Alta saturación moderada** y fuerte **contraste de valor** entre uniforme, piel y césped. Los verdes del campo actual (≈ `#338538` / `#3D9440`) son una restricción: los uniformes de campo **evitan verdes** y el portero usa un color vivo distinto (p. ej. magenta, naranja, amarillo).
- Test de escala de grises: dos equipos deben distinguirse también en gris (por valor, no solo por tono).
- Acentos de color en botas y bandas para dar ritmo sin ensuciar la silueta.

## 13. Silueta (criterio de aceptación n.º 1)

Un jugador está bien diseñado si, **pintado en negro sobre fondo claro a ~110 px de alto**, se reconoce: pelo/cabeza, hombros, brazos en movimiento, botas y la postura (carrera, tiro, celebración).
Distinguir **equipo** (color/valor), **portero** (colores y manos), **balón** (forma y brillo) y **jugador controlado** (anillo bajo los pies, fuera del modelo) debe ser inmediato.

## 14. Nivel de detalle

| Zona | LOD0 (cerca) | LOD1 (TV) | LOD2 (lejos) |
|---|---|---|---|
| Cara | rasgos y expresión completos | cejas/ojos/boca en textura | mancha de color, ojos como forma oscura |
| Pelo | volúmenes esculpidos + 2–3 huesos opcionales | mismo volumen, rígido | silueta simplificada |
| Manos | palma + pulgar + dedos en bloque | palma + pulgar | manopla |
| Uniforme | costuras/cuello en textura, número y nombre | número y patrón | bloque de color + número |
| Botas | forma + acento | forma + acento | forma |

**Regla de oro:** al bajar de LOD se pierde detalle fino, nunca información de lectura (equipo, número, silueta, color de pelo y piel). Ver [PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §9.

## 15. Cámara TV (gameplay)

El diseño de personaje se evalúa **primero aquí**: ~110 px de alto.
Debe verse: silueta, número de espalda, color de equipo, balón, dirección de carrera (pelo y botas). No debe verse ni exigirse: rasgos faciales, costuras, texturas finas.
La animación debe exagerar las poses clave (zancada, tiro, giro) porque son lo que se percibe a esta escala.

## 16. Cámara cercana (selección, repeticiones, celebraciones)

Aquí el jugador ocupa buena parte de la pantalla: se usa LOD0, la hoja de expresiones, luces cinemáticas y materiales completos.
Es donde el personaje debe sentirse "muy bien acabado": cara reconocible, ojos expresivos, tela con detalle, pelo con carácter.
Estas escenas son pocas y de poca carga (un par de personajes), por lo que pueden permitirse el coste.

## 17. Una sola identidad, muchos niveles

El mismo diseño se declina en: **modelo de partido** (LOD1–3), **modelo de cercanas** (LOD0) y, si hace falta, **retrato 2D** para la UI.
Cualquier decisión de coste (menos polígonos, menos texturas, sin sombras) se evalúa contra esta pregunta: *¿sigue siendo reconocible desde la cámara TV?* Ver [PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §9.

## 18. Expresiones

Conjunto inicial (hoja de expresiones de ojos + boca + cejas; en cámara lejana se comunican con la **pose**, no con la cara):

| Expresión | Uso |
|---|---|
| Neutra / concentrada | por defecto |
| Esfuerzo | sprint, tiro potente (dientes apretados, ceño) |
| Cansancio | stamina agotada (boca abierta, ojos medio cerrados) |
| Alegría / grito | gol, celebración |
| Frustración / enfado | ocasión fallada, pérdida de balón |
| Sorpresa | rebote inesperado, parada |
| Dolor | golpe, pérdida de equilibrio |
| Desafío / confianza | previa de saque, repeticiones |

Reglas: máx. 8–10 expresiones; transición por *cross-fade* de 0,1–0,2 s; el estado de juego las elige (esfuerzo y cansancio salen del estado de stamina existente), nunca el input.
