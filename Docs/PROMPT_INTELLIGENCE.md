> ⚠️ **Documento histórico (primer 50 %).** El intérprete de este documento (`DeterministicPromptInterpreter`, basado en léxico) **sigue existiendo y probándose**, pero el camino principal ahora es la capa semántica: ver [SEMANTIC_PROMPT_ENGINE](SEMANTIC_PROMPT_ENGINE.md).

# Prompt Intelligence

Convierte lenguaje natural en **datos estructurados**. Nunca en código y nunca directamente en un objeto 3D:

```
Prompt → interpretación semántica → CharacterSpecification (borrador) → validación → runtime
```

Eso permite cambiar de intérprete sin tocar el motor, y que una misma `CharacterSpecification` venga de Claude, otro modelo, un editor, un JSON o una importación.

## 1. El contrato

```
IAICharacterInterpreter.Interpret(PromptRequest) → PromptResult
```

`PromptRequest`: texto + (opcional) el personaje existente a modificar + id para uno nuevo. El intérprete **nunca modifica** el personaje existente.

`PromptResult` (todo lo que se pide en la especificación):

| Campo | Contenido |
|---|---|
| `Intent` | `CreateCharacter`, `ModifyCharacter`, `ModifyAppearance/Body/Face/Hair/Clothing/Style`, `ModifyFootballDna`, `AddBehavior`, `RemoveBehavior`, `ChangePlayStyle`, `Unknown` (el contrato admite todos; el intérprete actual produce los que su vocabulario permite) |
| `Context` | `Gameplay`, `Visual`, `Animation` o sin especificar |
| `Draft` | el personaje con los cambios aplicados (**sin validar** contra los catálogos aún); nulo si no se entendió nada |
| `Changes` | lista de `SpecChange`: tipo, objetivo, dirección, magnitud, nivel, valor, confianza, contexto y **las palabras de origen** |
| `AttributeHints` | deseos sobre los 12 atributos (solo sugerencias; no pertenecen al personaje) |
| `ProfileHints` | deseos sobre `PlayerPlayingProfile` (riesgo, creatividad, agresividad, zona, roles) |
| `Warnings` | avisos (supuestos, valores limitados, elecciones duplicadas) |
| `Conflicts` | `Contradiction` (bloquea lo afectado) o `Tension` (se aplica y se avisa) |
| `Unsupported` | lo que el motor no puede hacer, con motivo (`RequiresAsset`, `RequiresFutureRuntime`, `NotUnderstood`) |
| `Unresolved` | frases que no se reconocieron |
| `Confidence` | 0–1; la más baja de los cambios, reducida por lo no resuelto y los conflictos |

`CanApplyAutomatically` es falso si hay una contradicción.

## 2. Magnitud semántica

No son cadenas ("alto"): cada cambio lleva **dirección, magnitud, objetivo y confianza**. Ejemplo real: `ScalarDelta body.height dir=+1 mag=0.18 conf=0.8`.

| Palabra | Cambio relativo ("más…") | Intensidad de un descriptor absoluto |
|---|---|---|
| ligeramente | 0,05 | ×0,35 |
| un poco | 0,10 | ×0,55 |
| (sin palabra) | 0,18 | ×1,0 |
| bastante | 0,28 | ×1,15 |
| mucho / muy | 0,40 | ×1,3 |
| extremadamente | 0,55 | ×1,5 |

Son fracciones del rango del parámetro y están en `MagnitudeScale` (configurable). **Estos valores son estimaciones iniciales**, no calibrados con usuarios.

Distinciones que el intérprete hace:
- **Comparativo vs. absoluto:** "más delgado" mueve; "delgado" fija. "Cambia el pelo a corto" fija, no es relativo.
- **"No tan X"** = un paso hacia abajo, no el opuesto. **"No X"** = el opuesto (`no musculoso` → poco musculoso).
- **"Más creativo y arriesgado":** el segundo adjetivo hereda el "más".
- **Descriptores ligados a un sujeto:** "pelo corto rizado", "cabeza ligeramente grande", "camiseta roja" (en cualquier género y número).

## 3. Contexto

La misma palabra significa cosas distintas y no se mezclan:

| "rápido" en… | Resultado |
|---|---|
| gameplay (por defecto) | pista sobre Speed y Acceleration (+ aviso: "interpretado como gameplay") |
| visual ("que parezca rápido") | piernas más largas y menos masa; **no** cambia Speed |
| animación ("animación más rápida") | `Unsupported / RequiresFutureRuntime`: la velocidad de animación sale del movimiento real, no es un ajuste del personaje |

Igual con "agresivo": jugar agresivo (perfil) frente a *parecer* agresivo (mandíbula, cejas, expresión).

## 4. Conflictos

- **Contradicción:** "muy alto pero bajito" → `body.height`: **no se aplica nada** a lo contradicho (tampoco el preset `tall`/`compact` que vino de esas palabras), se informa con las dos frases y `CanApplyAutomatically = false`. El resto de la petición sigue entendiéndose.
- **Tensión:** "muy musculoso pero extremadamente ligero" → ambos se aplican y se avisa de que se oponen. Una futura UI pedirá resolverlo.
- Deseos opuestos sobre el mismo atributo ("rápido pero lento") → contradicción; no se sugiere nada para ese atributo.
- Dos peticiones para la misma ranura ("camiseta roja y camiseta azul") → se queda con la primera y avisa.

## 5. No soportado y no entendido

- "Créame una animación completamente nueva de chilena" → `Unsupported (RequiresAsset)`, **sin borrador** (no se inventa un personaje por defecto).
- Pedir un parecido con una persona real → `NotUnderstood` con la explicación de que los personajes son originales.
- Una frase donde no se reconoce nada → `Unresolved`, y baja la confianza.

## 6. El intérprete determinista (hoy)

`DeterministicPromptInterpreter` no usa red ni modelo: lee el **léxico** (`PromptLexicon`, datos, español y algo de inglés) con normalización (mayúsculas, tildes, puntuación), división en cláusulas, modificadores de magnitud, comparativos, negación, sujeto+descriptor, herencia de modificadores y detección de contexto. Sirve para construir y probar todo el pipeline sin gastar llamadas a ninguna API.

**Es un parser con léxico, no comprensión semántica.** Límites conocidos:
- Solo entiende las palabras del léxico. Una cláusula sin ninguna se devuelve como `Unresolved`; **palabras desconocidas dentro de una cláusula que sí tiene algo reconocido se ignoran** (no distingue relleno de contenido; test lo documenta).
- Ambigüedades léxicas simples ("alto" = estatura; no distingue "alta aceleración").
- El contexto es global por petición, no por frase.
- No hace correferencia compleja ni preguntas.

Un intérprete basado en un modelo de lenguaje cubriría esos huecos detrás de la **misma interfaz**: debe devolver el mismo `PromptResult`, que pasa por los **mismos validadores** (hay un test con un proveedor falso que prueba la sustitución).

## 7. Proveedores de IA

Claude puede ser el primer proveedor de referencia, pero **no es el motor**. Un adaptador de proveedor (futuro, opcional, fuera del núcleo): recibe el `PromptRequest` y el catálogo (ids y rangos válidos), pide al modelo un `PromptResult` en JSON, lo valida y lo devuelve. No hay claves, ni dependencia de red, ni proveedor concreto en `Core` (un test lo escanea).

## 8. Seguridad

La IA genera **datos**. `PromptSpecificationValidator` rechaza objetivos desconocidos, magnitudes y confianzas fuera de 0–1, piezas y colores inexistentes y resultados incoherentes. Los fuentes de `Creator` no contienen `Reflection.Emit`, `Assembly.Load`, `Process`, red ni `Activator.CreateInstance` (test). Un texto que parezca una orden de código ("ejecuta … y borra todo") queda como `Unresolved`: no se ejecuta ni se interpreta.

## 9. Ejemplos probados

| # | Petición | Resultado clave |
|---|---|---|
| 1 | "Crea un extremo pequeño, explosivo, atlético y creativo." | zona Wing; altura baja; `accelerationTendency` alta; comportamiento `ExplosiveExit`; Creatividad → perfil |
| 2 | "Crea un delantero alto y fuerte que sea muy bueno definiendo." | altura y musculatura altas; pista de Finishing ≈ 1; `FirstTimeFinish` |
| 3 | "Hazlo un poco más delgado." | solo masa y musculatura bajan, en 0,10 |
| 4 | "Hazle el pelo más corto." | solo `hair.length` baja |
| 5 | "Quiero que sea más creativo y arriesgado." | creatividad y riesgo suben (relativo); riesgo de regate y pase arriesgado suben |
| 6 | "Quiero que ataque mucho el espacio." | `spaceSeeking`, `movement.aggression`, `attackingRuns` altos |
| 7 | "…especializado en cambios de ritmo y uno contra uno." | `changeOfPace`, `takeOn`; `StopAndGo`, `ExplosiveExit`, `BodyFeint` |
| 8 | "…baje a recibir y después ataque el área." | `dropping` y `boxPresence`/`attackingRuns` |
| 9 | "Hazlo más cartoon pero no infantil." | desplazamiento de estilo con la guarda (la cabeza crece menos que sin guarda) |
| 10 | "Quiero un jugador rápido pero no musculoso." | Speed/Acceleration altas (gameplay) y musculatura baja; sin conflicto |

Y los tres ejemplos extremo a extremo de la especificación (extremo explosivo 1v1, delantero de referencia, modificación "más pequeño, menos musculoso y mucho más explosivo" sin resetear pelo, cara, ropa ni DNA no mencionado) están cubiertos por tests.

## 10. Depuración

`PromptDebugReport.Build(...)` imprime las etapas: **Prompt → Interpretación → Especificación (JSON) → Validación → FootballDNA → Candidatos de comportamiento** (para un contexto dado). Texto, determinista y sin efectos; sirve para logs, tests y una futura ventana de depuración.
