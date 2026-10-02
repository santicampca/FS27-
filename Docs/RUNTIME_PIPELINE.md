# Runtime Pipeline

Lo que el **juego** recibe: datos pequeños que se resuelven contra catálogos compartidos. Sin internet, sin IA, sin intérpretes, sin autoría.

```
AUTORÍA                                              RUNTIME
CharacterSpecification (con prompt, notas)  ──ForRuntime()──►  CharacterSpecification (sin AuthoringData)
        │                                                              │
        └─ RuntimeCharacterJson.ToJson ─► FS27.RuntimeCharacter.v1 ──► TryFromJson + ContentIndex ──► CharacterSpecification
                (ids de contenido estables)                                              │
                                                                       CatalogAppearanceResolver → CharacterAssemblyPlan → (Unity) personaje
```

## Autoría vs runtime
| | Autoría | Runtime |
|---|---|---|
| Prompt, notas, generador, informe de interpretación, `ReferenceProfile` | ✅ | ❌ |
| Observaciones y datos de investigación | ✅ | ❌ |
| `CharacterSpecification` (apariencia + DNA 2.0 + semillas) | ✅ | ✅ (sin `authoring`) |
| Catálogos (parámetros, piezas, estilos, comportamientos, materiales, animaciones) | ✅ | ✅ (pequeños, compartidos) |
| Parser, compilador, proveedores de modelos, transportes | ✅ | ❌ |
| Vídeos, imágenes de referencia, páginas web | ❌ nunca en el repo | ❌ |

## Registro compacto (`FS27.RuntimeCharacter.v1`)
Las piezas y los comportamientos se nombran por **id de contenido estable**: `hair.short_curly_07`, `behavior.stop_and_go`, `style.cartoon_sports`, `material.skin_toon`, `anim.run_loop`. Regla: `categoría.nombre`, minúsculas con guiones bajos, un solo punto. Los ids antiguos (`StopAndGo`, `FS27_CARTOON_SPORTS`) siguen funcionando y cada uno tiene su id de contenido derivado (`ContentIndex`); **no se renombró nada existente**. Un id desconocido, o de otra clase (un comportamiento donde va una pieza), se rechaza con `ContentUnresolved`.

Solo se guarda lo que difiere del neutro; los comportamientos simples son `["behavior.stop_and_go", 0.8]` y los ricos objetos con sus ajustes. Ejemplo medido (extremo del propio motor): **~1,1 KB**; es menor que el JSON de autoría y no contiene el prompt (prueba).

## Escala (100 – 5000 jugadores)
`CharacterVariationGenerator.Crowd`: un arquetipo + una semilla = un personaje, reproducible; el jugador se puede **regenerar desde tres números** (`GenerationSeed`, `AppearanceSeed`, `BehaviorSeed`) en vez de almacenarse. Medido en la prueba de 5000 personajes (este contenedor, un hilo): **~0,55 s** para generarlos y ~**1,7 KB** de JSON de especificación de runtime por personaje (~8,5 MB en total, texto sin comprimir; el registro compacto es menor). Todos válidos, coherentes y distintos. El peso real de un paquete lo dominarán los assets, no estos datos. **No se ha medido en un dispositivo móvil.**

## Migración y versiones
`FS27.CharacterSpecification.v2` (carga v1), `FS27.FootballDNA.v2`, `FS27.RuntimeCharacter.v1`, `FS27.SemanticProgram.v1`, `FS27.CharacterSpecificationPatch.v1`, `FS27.CharacterAssemblyPlan.v1`, `FS27.Observations.v1`. Cada cambio incompatible sube la versión y registra un paso de migración; una versión más nueva se rechaza.
