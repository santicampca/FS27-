# Character Generation

De la especificación al personaje visible. **Lo que existe es una base real y honesta**: el motor calcula proporciones, genera una malla procedural de calidad **maniquí**, la exporta a GLB y la dibuja en PNG sin Unity ni GPU. **No existe arte final** (caras, pelo, ropa, botas reales) ni rig ni animación.

```
CharacterSpecification
   │ CatalogAppearanceResolver
   ▼
ResolvedCharacter ── CharacterAssemblyPlanner ──► CharacterAssemblyPlan  (DATOS: partes, huesos, materiales, colores, proporciones)
                                                      │
              ┌───────────────────────────────────────┼─────────────────────────────┐
              ▼ (Core, sin Unity)                     ▼ (Unity, sin verificar)      ▼ (futuro)
   ProceduralMeshBuilder → MeshData            UnityCharacterAssembler        partes tipo Asset
   ├─ GlbWriter (.glb, glTF 2.0)               UnityCharacterRenderer         (mallas reales por id)
   └─ SoftwareRenderer → PngWriter (.png)      UnityAnimatorAdapter
```

## 1. Apariencia 2.0
- **Parámetros** (`ParameterCatalog`): 51 de apariencia en 12 grupos (body, head, face, eyes, brows, nose, mouth, ears, hair, skin, facialHair, style), cada uno con rango, valor neutro y significado. Se guarda solo lo que difiere del neutro.
- **Piezas** (`AppearanceCatalog`): ranuras abiertas (pelo, textura, barba, nariz, boca, cejas, ojos, expresión, forma de cabeza, preset de cuerpo, camiseta, pantalón, medias, botas, guantes) con ids; hoy **solo ids**, sin assets detrás.
- **Coherencia** (`AppearanceNormalizer` + `CompatibilityRules`): una elección implica rangos (un rapado es corto; un peinado recogido pide pelo largo; una barba implica cobertura; un afro implica textura rizada). Rango **blando** → se **normaliza** y se dice; rango **duro** (un afro en una cabeza afeitada *explícita*) → se **rechaza** sin reescribir. Un valor que nadie fijó nunca es contradicción.
- **Estilos** (`StylePresets`): `FS27_CARTOON_SPORTS` (alias `FS27_CARTOON_SPORT`), `FS27_CARTOON_EXPRESSIVE`, `FS27_STYLIZED_ATHLETIC`, `FS27_REALISTIC_STYLIZED`: proporciones en cabezas (5,8 a 7,3), look inicial, eje cartoon↔realista, saturación, contorno. **Composición:** `StyleComposer.Blend` mezcla estilos con pesos; "más cartoon pero todavía deportivo" = mover el eje cartoon y mantener el atletismo. **Cambiar de estilo** (`StyleApplier.Restyle`) desplaza lo que el estilo posee por la *diferencia* entre estilos, conservando los retoques de la persona.
- **Variación con semilla** (`CharacterVariationGenerator`): un arquetipo + una semilla = un personaje distinto y reproducible; lo que el arquetipo eligió a propósito siempre gana.

## 2. Geometría (sin mallas en el JSON)
`AssemblyPart` = id, ranura, tipo (`Body/Head/Face/Hair/FacialHair/Clothing/Boot/Glove`), origen (`Procedural` o `Asset`), `AssetId` o definición procedural (`ellipsoid`, `segment`, `box` + números), `MaterialId`, hueso, transformación y parámetros. **Una malla nunca se guarda en datos**, solo su receta. Los materiales son nombres (`material.skin_toon`...) con color de ranura o rampa de piel; un renderizador los asigna a un shader.

`CharacterAssemblyPlanner` calcula: altura exacta pedida (`body.height`), cabeza según el estilo, hombros, cadera, brazos, piernas, grosor por masa y musculatura, ojos/nariz/boca/orejas/cejas, pelo por estilo y longitud (sin estilo elegido, la longitud decide), barba, guantes de portero, botas; esqueleto proxy de reposo; color de piel (rampa de 3 puntos + subtono), pelo, ojos, kit.

## 3. Salidas reales
- `ProceduralMeshBuilder`: ~35–40 partes → miles de triángulos (determinista).
- `GlbWriter`: glTF 2.0 binario válido (se comprueba cabecera, longitudes, vistas, accesores, rangos de índices). Ábrelo en Blender o en cualquier visor glTF.
- `SoftwareRenderer` + `PngWriter`: frente, perfil y espalda, con sombreado plano. PNG con zlib (`DeflateStream`).
- `CharacterPreviewData`: proporciones, partes, triángulos, colores y una nota explícita: *"Procedural mannequin ... not final art, not rigged and not animated."*

Muestras: [previews](previews) (`winger`, `goalkeeper`, `striker`).

## 4. Qué es realmente procedural
Sí: proporciones, ensamblaje, colores, formas simples (elipsoides, segmentos, cajas), pelo/barba/botas/guantes como formas básicas, variación por semilla. **No:** caras con carácter, peinados reales, ropa, texturas, blend shapes, rig, pesos, animación.

## 5. Capa de Unity (**no verificada**)
`Assets/_Project/Scripts/Gameplay/Creator/`: `UnityCharacterAssembler` (plan → `GameObject`, mallas del Core envueltas en `Mesh`), `UnityCharacterRenderer` (cámara → PNG, para herramientas de editor), `UnityAnimatorAdapter` (aplica el `AnimationPlan` y la personalidad a un `Animator`/hijo visual; **nunca** activa root motion ni mueve la raíz). **Escritos sin editor de Unity.** Solo se comprobó que compilan contra un stub escrito a mano (`dotnet build Tests/UnityLayerCheck`), lo cual demuestra ausencia de errores de tipeo/firma y **nada más**. Falta una primera compilación y una primera ejecución en Unity 6 / URP.

## 6. Qué necesita para llegar al juego
1. **Assets** (para pasar de maniquí a personaje): un modelo base con rig humanoide y proporciones ≈ 6,5 cabezas, piezas de pelo/ropa/botas, texturas, blend shapes de cara. Entran por `PartSource.Asset` + `ICharacterAssetLoader` sin tocar el resto.
2. **Unity**: compilar y ejecutar la capa `Gameplay/Creator`, URP, un shader toon.
3. **Medición en dispositivo** móvil (triángulos, llamadas de dibujo, memoria con 12 jugadores).
