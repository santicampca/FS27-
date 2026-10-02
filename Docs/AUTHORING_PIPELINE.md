# Authoring Pipeline

La **autoría** es todo lo que ocurre mientras se crea contenido (en desarrollo o en una herramienta): prompts, edición, modelos de lenguaje, investigación, validación, vistas previas. **El juego no hace nada de esto en partido.** La autoría produce datos; el runtime solo recibe datos compactos ([RUNTIME_PIPELINE](RUNTIME_PIPELINE.md)).

## 1. Una pasada: `CreatorPipeline`

`pipeline.Run(CreatorRequest)` → `CreatorResult`. Etapas (`result.Stages`): interpretar → validar el modelo semántico → compilar a parche → aplicar → normalizar apariencia → validar la especificación → DNA y comportamientos → plan de ensamblaje, movimiento y animación → vista previa (opcional) → exportar.

`CreatorRequest`: texto, borrador actual (`null` = crear), borrador anterior (para "como el anterior"), semilla, id y estilo del personaje nuevo, enriquecer DNA, pedir PNG/GLB y `Transactional`.

`CreatorResult`: `Success`, `Specification`, `Draft` (para el turno siguiente), `Errors`, `Warnings`, `Conflicts`, `Clarifications`, `Unsupported`, `AttributeHints`, `ProfileHints`, `Goalkeeper`, `BehaviorPreview`, `Interpretation`, `Patch`, `GenerationPlan`, `Preview`, `RuntimeData`, `AuthoringData`, `DebugReport`.

**`Success` nunca es `true` si algo es inválido, contradictorio o está esperando respuesta.** Con `Transactional` (por defecto), si un conflicto no resuelto bloquea parte de la petición **no se aplica nada** y se pregunta; con `false` se aplica la parte segura y el resultado sigue sin ser éxito. Una petición que solo conserva (`mantén la cara`) es un éxito que no cambia nada y lo dice.

## 2. Conversación multi-turno: `AuthoringSession`
Cada turno se interpreta, se compila a parche y se aplica **de forma incremental**: lo que no se menciona se conserva exactamente. Recuerda el personaje anterior (`como el anterior`), acumula los deseos que viven fuera de la especificación (atributos, perfil, roles, capacidades de portero) en el `AuthoringDraft` y permite `Undo()` (el parche inverso se calcula comparando el antes y el después, así deshace también cambios de estilo y comportamientos).

Prueba de referencia: `Crea un extremo` → `Hazlo más rápido` → `Más pequeño` → `Cámbiale el pelo` → `Déjalo menos agresivo`: cada turno cambia solo lo suyo; el rol, la velocidad y el pelo sobreviven a los turnos posteriores.

## 3. Parches (`CharacterSpecificationPatch`)
Operaciones `Add / Remove / Replace / Modify / Increment / Decrement / Reset / Preserve` sobre parámetros de apariencia y de DNA, piezas, colores, comportamientos, el eje cartoon, el estilo, atributos, perfil y portero. Con `Reason`, `Confidence` y marca de **efecto colateral**. `PatchApplier` no toca la entrada, limita y avisa de lo que se sale de rango, rechaza lo desconocido y respeta los `Preserve`. `PatchJson` lo lee/escribe con validación estricta: **es la puerta para un editor visual** (un slider genera un parche, igual que un prompt).

## 4. Informes
`InterpretationReport` (entendido, aplicado, asumido, no entendido, bloqueado, conflictos, no soportado, preguntas, confianza) y `CreatorDebugReport` (toda la pasada, sin reloj ni azar: dos ejecuciones iguales dan el mismo texto). `CreatorContentValidator` valida el **contenido** del motor (catálogos, vocabularios, enlaces entre ellos) antes de que llegue a un jugador.

## 5. Modelos de lenguaje
Un modelo es un intérprete más (`LlmSemanticInterpreter : ISemanticInterpreter`). Todo vive en `Core/Creator/Providers` y **ninguna clase abre red, guarda claves ni ejecuta nada**:

```
texto ─► PromptTemplates ─► LlmRequest ─► ILlmWireFormat ─► WireRequest ─► ILlmTransport (lo pone el HOST) ─► WireResponse
                                                                                   │
        SemanticProgram ◄─ cadena de validación ◄─ texto del modelo ◄─ ILlmWireFormat.Parse
```

- **Esquema JSON estricto** para la respuesta (`SemanticProgramJson.Schema`): todos los objetos con `additionalProperties:false` y todas las propiedades `required`, enumeraciones cerradas (cada concepto es un objetivo permitido), sin límites numéricos ni recursión. Una prueba comprueba que lo que produce el parser **cumple** ese esquema.
- **Cadena de validación** de una respuesta (no confiable): *esquema* (forma, nombres de enum, tamaños) → *semántica* (ids, objetivos, relaciones, rangos) → *dominio* (los valores existen: piezas, colores `#RRGGBB`) → *aplicación* (una simulación debe compilar y aplicarse sin saltos).
- **Reparación acotada** (2 intentos por defecto): `InvalidOutput → RepairRequest (la respuesta del modelo + los errores) → proveedor → ValidatedOutput`. Una negativa, un corte por `max_tokens` o una caída **no** se reintentan.
- **Si no hay forma:** se usa el parser offline (`UsedFallback`, con aviso) o se devuelve un programa vacío que pide reformular. Nunca se usa algo inválido.
- **Plantillas:** `CharacterCreationPrompt`, `CharacterModificationPrompt` (incluye el estado actual y pide un solo cambio), `FootballDNAExtractionPrompt`, `ResearchAnalysisPrompt`; se construyen desde datos (catálogo de conceptos, ejemplos generados por el propio parser, validados en las pruebas) y no contienen secretos ni nombres reales.
- **Formatos de red:** `ClaudeWireFormat` (Messages API con `output_config.format = json_schema`, sin `thinking`, sin cabecera de credencial; modelo por defecto `claude-opus-5-5`), y `OpenAiWireFormat`, `GeminiWireFormat`, `LocalModelWireFormat`. `ExercisedAgainstRealService` es `false` para todos.
- **Transportes:** `OfflineTransport` (por defecto: dice con claridad que no hay red) y `ScriptedTransport` (pruebas). Un transporte real lo implementaría el host en una herramienta de autoría; **no existe y no debe vivir en Core ni en el juego**. Las claves, si algún día se usan, viven en el almacén de secretos del host.

⚠️ **Estado:** el formato de Claude sigue la documentación oficial de salida estructurada, pero **nunca se ha enviado ni una petición** a ningún servicio; los de OpenAI/Gemini/local están **NO VERIFICADOS** (escritos de memoria). Lo que sí está probado es todo lo que no depende de un servicio: esquema, validación, reparación, límites, contención de fallos de transporte.
