# Análisis de blender-skills para FS27

Repositorio: `arjun988/blender-skills` (público, licencia **MIT**). **Limitación de este análisis:** solo he podido leer la página principal y el README (no está entre tus repositorios accesibles y no lo he clonado ni instalado); no he abierto las carpetas de cada skill. Los números (94 skills, 175+ ficheros de referencia) son los que declara el README.

## Qué es
Un paquete de *skills* (instrucciones en Markdown) para agentes (Claude Code, Cursor, Kiro, Codex) que controlan **Blender** a través de **BlenderMCP**, un complemento de Blender que abre un puente entre el agente y Blender en ejecución. Se instala copiando `.claude/skills` y configurando BlenderMCP (Blender, `uv` y el complemento).

## Requisitos y coste
- Necesita **Blender instalado y abierto** y el complemento BlenderMCP, más `uv`. Es software gratuito, pero **es una instalación pesada** que, según tu regla, **no se instala todavía**.
- El README no menciona servicios de pago ni claves. *Del complemento BlenderMCP en sí (otro proyecto) recuerdo integraciones opcionales con generadores en línea y bibliotecas de activos, algunas con límites o de pago: hay que comprobarlo antes de activarlas; yo no las usaría.*

## Qué se puede aprovechar sin servicios de pago
| Parte | Uso en FS27 | Notas |
|---|---|---|
| `lowpoly-style`, `stylized-style`, `cartoon` | Guía del estilo de modelo | Solo texto; sirve como lista de comprobación del arte (siluetas, planos, paleta) |
| `asset-optimization`, `lod-pipeline`, `collision-proxy` | Presupuestos de polígonos, LOD, colisión | Texto; los valores concretos los fija `Docs/GRAPHICS_CLIENT_SPEC.md` |
| `export-pipeline`, `godot-export` | Exportar GLB para el cliente | Útil si se elige Godot; para web basta glTF/GLB estándar |
| `rigging`, `animation`, `retopology`, `uv-workflow`, `materials` | Flujo de personaje con Blender | Requiere Blender abierto |
| `racing-sports` (única de deportes) | Referencia visual de juego deportivo | Marginal; no es fútbol |
| `character-artist`, `blender-director` | Orquestación de un personaje | Requiere Blender + MCP |
| Estilos de terror, mundos, géneros (≈ 60) | **Nada** | No aplican a un juego de fútbol |

## Lo que FS27 ya hace sin Blender
El Core genera un personaje procedural por piezas y lo exporta a **GLB** (`ProceduralCharacterGenerator`, `CharacterSpecification`, `AssemblyPlan`, `Docs/CHARACTER_GENERATION.md`), con un rig común `fs27_humanoid_v1` y un catálogo de animaciones por acción. Blender aporta lo que el Core no hace: modelado a mano, retopología, rig y animación con clips reales.

## Recomendación
1. **No instalar Blender ni BlenderMCP todavía.** Primero el prototipo web con el maniquí procedural.
2. Si se decide modelar a mano: usar solo las skills de estilo/optimización/exportación como **guía de texto** y Blender de forma manual o con MCP local, sin generadores en línea.
3. Antes de copiar nada: revisar el contenido de cada skill elegida (instrucciones de terceros) y la licencia de los 175 ficheros de referencia.
