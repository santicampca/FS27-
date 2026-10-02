# Creator Engine: hoja de ruta

Sin fechas ni horas: solo orden y dependencias. Esta hoja sustituye a [CREATOR_ENGINE_ROADMAP](CREATOR_ENGINE_ROADMAP.md) (la del primer 50 %), que queda como historia.

## Hecho (verificado con `dotnet test`, fuera de Unity)
Capa semántica ES/EN con negación, magnitud, contraste, contexto, referencias y preservación · compilador a parches con conflictos, no soportado y preguntas · sesión multi-turno con deshacer · `PatchJson` para editores · FootballDNA 2.0 con migración · motor de contexto y de decisión de comportamientos · resolutor de acciones y `PlayerIntent` compatible · observaciones, agregación, composición de DNA y análisis estadístico offline · apariencia 2.0, coherencia, 4 estilos y mezcla · ids de contenido estables, materiales y animaciones como catálogos · plan de ensamblaje, malla procedural, GLB, vista previa PNG · variación con semilla (5000 personajes) · selección de animación y personalidad de movimiento · `CreatorPipeline` con registros de runtime y de autoría · contratos de modelos de lenguaje (esquema, validación, reparación, formatos, transporte fuera de Core) · capa de Unity escrita (sin verificar).

## Orden del trabajo que queda

| Paso | Contenido | Depende de | Necesita |
|---|---|---|---|
| **1. Verificar la capa de Unity** | Abrir el proyecto en Unity 6/URP, compilar `Gameplay/Creator`, ensamblar un personaje y compararlo con `Docs/previews`; arreglar lo que salga | — | Unity |
| **2. Primer modelo base real** | Elegir y comprobar un modelo base (proporciones ≈ 6,5 cabezas, rig humanoide, materiales por zona, licencia) y registrarlo como `BaseModelDefinition`; cargarlo con `ICharacterAssetLoader` | 1 | un asset |
| **3. Cuerpo y cara reales** | Aplicar `body.*`/`head.*`/`face.*` (escalas por hueso, blend shapes o piezas) y sustituir el maniquí; mantener el mismo plan de ensamblaje | 2 | Unity + piezas |
| **4. Pelo, ropa, botas** | Piezas por id (`hair.*`, `shirt.*`, `boots.*`), colores por ranura, dorsal | 2 | piezas 3D / texturas |
| **5. Animación de locomoción** | Primeros clips (`idle`, `walk`, `jog`, `run`, `sprint`), `Animator Controller` leyendo `MovementState`, retargeting | 2 | animaciones |
| **6. Acciones de fútbol** | Pase, tiro, control, entrada, cabezazo, portero + eventos de contacto con el Ball Core | 5 + sistemas de pase/tiro | animaciones + juego |
| **7. Conectar el motor de comportamiento a la IA de partido** | La IA construye `FootballContext`, llama a `BehaviorDecisionEngine`, escribe el `PlayerIntent`; marcar comportamientos como `Implemented` uno a uno | 6, IA de jugador | IA de partido |
| **8. Calibración** | Pesos de composición, umbrales de contexto, prioridades/riesgos/enfriamientos, escala de magnitud, referencias z | 7 | partidas reales |
| **9. Transporte real de modelo (opcional)** | Un `ILlmTransport` en una herramienta de autoría, con la clave en el almacén de secretos del host; probar el formato de Claude de verdad y decidir qué hacer con los otros tres | — | clave + red (solo autoría) |
| **10. Interfaz visual (Character Studio)** | Crear/editar/guardar/cargar con sliders sobre `PatchJson`; vista previa 3D; deshacer; mostrar `InterpretationReport` | 3, 4 | UI en Unity |
| **11. Optimización móvil** | Triángulos, llamadas de dibujo, LOD, memoria con 12 jugadores | 3–5 | dispositivo |
| **12. Pipeline de contenido** | Prompt → investigación → DNA → apariencia → validación → revisión → exportación → paquete | 9, 10 | herramientas de autoría |

## Qué se puede avanzar **sin** Unity ni assets
- Ampliar vocabularios (nuevos idiomas = nuevo `LanguagePack`), conceptos y comportamientos: son filas de datos y las pruebas de contenido los vigilan (`CreatorContentValidator`).
- Más parámetros de apariencia y reglas de coherencia.
- Calibrar con simulaciones puras sobre `FootballContext`.
- Más estilos y mezclas.

## Riesgos conocidos
- **El primer modelo base condiciona todo** (pasos 2–5): si no trae blend shapes o materiales por zona, la cara y la ropa necesitan piezas adicionales. Verificarlo antes de congelar el esquema.
- **El maniquí no es arte**; si se decide que el estilo final es muy distinto, `StylePreset`/`AssemblyPart` ya aíslan el cambio, pero no lo evitan.
- **Rendimiento móvil sin medir.**
- **Animaciones de fútbol:** no se conocen bibliotecas gratuitas; habría que conseguirlas o crearlas.
- **Un modelo de lenguaje real puede devolver respuestas válidas pero poco acertadas.** La cadena de validación evita datos inválidos, no ideas pobres: por eso la autoría revisa el `InterpretationReport`.
- **El parser entiende lo que sus paquetes conocen.** Un usuario real escribirá cosas fuera de vocabulario; el motor lo dice (`Unparsed`, preguntas) en vez de adivinar, pero hará falta ampliar paquetes con uso real.

## Siguiente paso recomendado
Paso 1: abrir el proyecto en Unity y comprobar la capa `Gameplay/Creator` (es lo único escrito sin ejecutar). Todo lo demás de esta fase está probado fuera de Unity.
