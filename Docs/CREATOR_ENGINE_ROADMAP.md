# Creator Engine: hoja de ruta del 50 % restante

Sin fechas ni horas: solo orden y dependencias. Cada fase necesita **Unity y/o assets** salvo donde se indica. Lo hecho (datos, validación, intérprete determinista, DNA, contratos) es la base de todas.

| Fase | Contenido | Depende de | Necesita |
|---|---|---|---|
| **A. Base 3D** | Elegir y verificar un modelo base (proporciones ≈ 6,5 cabezas, materiales por zona, rig humanoide, licencia) y registrarlo como `BaseModelDefinition` | — | un asset; investigación y prueba en Unity |
| **B. Cuerpo procedural** | Aplicar `body.*` (escalas por hueso, tipos de cuerpo) | A | Unity |
| **C. Cara** | Aplicar `head.*`/`face.*` (blend shapes si la base los tiene; si no, piezas de cabeza) | A | Unity + posiblemente piezas hechas en Blender |
| **D. Pelo** | Piezas de pelo por id y color | A | piezas 3D |
| **E. Ropa** | Camiseta, pantalón, medias, botas, guantes por zonas de color y dorsal | A | piezas/texturas |
| **F. Rig** | Rig común, esqueleto y pesos del modelo base | A | Unity/Blender |
| **G. Retargeting de animación** | Biblioteca de animaciones compartida para el rig; `IAnimationResolver` | F | animaciones (locomoción primero) |
| **H. Runtime de movimiento** | Animator leyendo `MovementState`; `MovementSignature` aplicada; sin cambiar la simulación | F, G | Unity |
| **I. Acciones de fútbol** | Pase, tiro, control, entrada, cabezazo, portero + eventos de contacto con Ball Core | G, H y los sistemas de pase/tiro | animaciones + sistemas de juego |
| **J. FootballDNA → comportamiento de IA** | `BehaviorResolver` → decisión → `PlayerIntent`; marcar comportamientos como `Implemented` | I, IA de jugador | IA |
| **K. Proveedor real de prompts** | Adaptador (opcional) que pide un `PromptResult` JSON a un modelo, lo valida y lo devuelve | núcleo ya hecho | una clave y red (solo en autoría) |
| **L. Character Studio visual** | Crear / editar / guardar / cargar / importar / exportar apariencia con sliders sobre estos datos | A–E | UI en Unity |
| **M. Optimización móvil** | Medir triángulos, blend shapes, llamadas de dibujo, LOD, memoria con 12 jugadores | A–H | dispositivo real |
| **N. Pipeline de contenido** | Prompt → investigación → DNA → apariencia → validación → revisión → exportación → paquete de runtime | K, L | herramientas de autoría |

## Qué se puede avanzar sin Unity

- **K** (el adaptador, sin ejecutar nada de red en tests) y **N** (herramientas de exportación) son en gran parte datos y pueden hacerse con `dotnet test`.
- Ampliar el léxico, el catálogo de comportamientos y los parámetros: filas de datos.
- Calibrar `MagnitudeScale`, los pesos del `BehaviorResolver` y la `MovementSignature` cuando haya algo con lo que comparar.

## Riesgos conocidos

- **Elegir la base (A) condiciona B–E y F:** si no tiene blend shapes o materiales por zona, la cara y la ropa necesitan piezas adicionales. Hay que verificarla antes de fijar el esquema final.
- Rendimiento móvil sin medir (M).
- Las animaciones de fútbol (I) no existen en bibliotecas gratuitas conocidas; habría que conseguirlas o crearlas.
- Un modelo de lenguaje real (K) puede devolver resultados válidos pero poco acertados: por eso pasan por los mismos validadores y se revisan en la fase de autoría.
