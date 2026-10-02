# Authoring vs. Runtime

El juego **no usa internet ni IA durante la partida**. Todo lo que necesita Internet o un modelo ocurre al **crear** contenido; el juego solo recibe datos ya compactos.

```
AUTORÍA (desarrollo)                                              RUNTIME (el juego)
Claude / otro proveedor / investigación / edición manual          datos compactos + piezas compartidas
   ↓ prompt, análisis                                                ↓
FootballDNA + Appearance (borrador) → validación → revisión → exportación → paquete de runtime
```

| | Autoría | Runtime |
|---|---|---|
| Prompt original, notas, generador, fecha | ✅ (`AuthoringData`) | ❌ |
| `ReferenceProfile` (descripción de una fuente, confianza, procedencia) | ✅ | ❌ |
| `CharacterSpecification` (apariencia + DNA) | ✅ | ✅ (sin `authoring`) |
| `PlayerDefinition`, `PlayerPlayingProfile` | ✅ | ✅ |
| Interpretador de prompts, proveedores de IA, `PromptDebugReport` | ✅ | ❌ |
| Catálogos (parámetros, piezas, estilos, comportamientos) | ✅ | ✅ (pequeños) |
| Vídeos, imágenes de referencia, páginas web, bases de datos de investigación | ❌ nunca en el repo del juego | ❌ |

`CharacterSpecification.ForRuntime()` quita todo lo de autoría; `ToJson(spec)` lo omite por defecto. Un test comprueba que el prompt y la referencia no aparecen en el JSON de runtime.

## Presupuesto de tamaño

Objetivo de planificación: **650–820 MB** para una primera versión completa (muy por debajo de 2 GB). El Creator Engine solo aporta **datos**:

| Dato | Tamaño medido o acotado |
|---|---|
| Personaje típico (runtime, JSON compacto) | ≈ 0,7 KB (732 bytes en el ejemplo 58) |
| Personaje recién creado | ≈ 0,4 KB |
| Peor caso (todo ajustado) | < 6 KB (test) |
| 1.000 personajes | ≈ 0,7 MB |

**Política:** no almacenar vídeos, imágenes de referencia, fuentes web, texto innecesario ni datos duplicados. Compartir materiales, shaders, mallas, rigs y animaciones. La variedad debe salir de **datos + reutilización + composición**. El peso real del juego lo decidirán los assets 3D, texturas y audio, que **todavía no existen y no están medidos**.

## Reglas

- Ninguna dependencia de internet, claves ni servicios de pago en el núcleo. Todo lo que dependa de un servicio externo es **opcional** y vive fuera de `Core`.
- Los jugadores reales, si algún día existen como contenido, son **datos** (nunca un `if` por nombre o id; un test escanea las fuentes).
- Los comportamientos surgen de DNA + atributos + contexto + IA.
