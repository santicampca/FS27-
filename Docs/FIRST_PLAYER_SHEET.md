# FS27 — Ficha del primer jugador de prueba

> **Personaje ficticio y original.** Nombre, rasgos y equipo inventados para el prototipo; no representan a ninguna persona real ni
> imitan a un futbolista concreto. Los nombres son provisionales y se revisarán (parecidos con personas o marcas) antes de publicar.

## Identidad

| | |
|---|---|
| **Nombre** | Nico Valmar |
| **Id estable** | `fs27-p-nico-valmar` |
| **Número** | 10 |
| **Posición / rol** | Delantero (`Forward`) — mediapunta/extremo rápido |
| **Equipo de prueba** | "Cobalto" (nombre provisional) — camiseta azul cobalto con bandas naranja |
| **Altura aproximada** | 1,76 m (algo por debajo de la referencia de 1,80 m: ágil y de centro de gravedad bajo) |
| **Complexión** | Atlética esbelta: hombros anchos pero delgado de cintura, piernas potentes |

## Aspecto

| | |
|---|---|
| **Tono de piel** | Marrón medio cálido (tono 3–4 de la paleta: ≈ `#C98A5E`–`#A56A44`) |
| **Cabello** | Rizos cortos y altos con la parte lateral rapada; color negro azulado. **Silueta única**: copete alto y redondeado que se reconoce a 16 px de cabeza |
| **Rostro** | Mandíbula definida, pómulos marcados, sonrisa torcida, ojos grandes y vivos (iris ámbar), cejas expresivas |
| **Vello facial** | Ninguno |
| **Accesorios** | Cinta fina naranja en la muñeca derecha. (Máx. 2 accesorios, ver spec de assets) |
| **Botas** | Naranja intenso con suela clara (acento de color que se lee al correr) |
| **Uniforme** | Camiseta azul cobalto con bandas naranja en los hombros, pantalón azul oscuro, medias azules con banda naranja; número 10 grande en la espalda |
| **Personalidad visual** | Confiado, juguetón, enérgico. Poses: carrera con brazos amplios y torso muy inclinado; celebra con un salto y el puño; cuando se cansa se nota mucho (manos a las rodillas) |

Test de silueta: **copete + brazos amplios + botas naranja** deben bastar para reconocerlo a ~110 px ([VISUAL_STYLE.md](VISUAL_STYLE.md) §13).

## Atributos (1–99)

| Atributo | Valor | Lectura |
|---|---|---|
| Speed | 88 | muy rápido |
| Acceleration | 90 | arranque explosivo |
| Stamina | 66 | aguanta poco: ~8 s de sprint continuo |
| BallControl | 84 | muy buen regate |
| Passing | 70 | pase correcto |
| Shooting | 76 | buen disparo |
| Defense | 38 | casi no defiende |
| Strength | 52 | flojo en el cuerpo a cuerpo |
| Reaction | 78 | reacciona rápido |

Valores de juego resultantes con el tuning por defecto actual (calculados con el código existente):
**velocidad máxima ≈ 9,1 m/s** (trote 2,3 · carrera 5,9), **aceleración ≈ 10,4 m/s²**, **tanque de sprint ≈ 8,0 s**, **recuperación ≈ 10 s** de vacío a lleno.
Es, por diseño, más rápido y explosivo que el jugador de referencia (atributos 70: 8,5 m/s) y se cansa antes: ideal para **notar el contraste de velocidad y stamina** al probar.

## Estilo de juego

Regateador explosivo de pocos segundos: arranca fuerte, gana metros en carrera y se agota si abusa del sprint. Fuerte con balón y de cara a portería;
débil defendiendo y en el choque. Premia el **uso inteligente del joystick** (sprint en el borde solo cuando compensa) frente a correr siempre al máximo.

## Validación

Esta ficha **pasa `DataValidator.ValidatePlayer`** (id, nombre, número 1–99, rol y los nueve atributos en 1–99). Equivale a este `PlayerDefinition`
(solo datos, aún sin crear en el repositorio):

| id | name | number | role | speed | accel | stamina | ballControl | passing | shooting | defense | strength | reaction |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `fs27-p-nico-valmar` | Nico Valmar | 10 | Forward | 88 | 90 | 66 | 84 | 70 | 76 | 38 | 52 | 78 |

Pendiente para cuando haya arte: crear su `PlayerAppearance` (id de modelo, piel, peinado de rizos altos, rostro, botas naranjas)
en el catálogo de apariencias ([PLAYER_PRESENTATION_ARCHITECTURE.md](PLAYER_PRESENTATION_ARCHITECTURE.md) §5).
