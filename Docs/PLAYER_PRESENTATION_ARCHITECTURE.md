# FS27 — Arquitectura de presentación del jugador

**Diseño, no implementación.** Define cómo un jugador de datos acaba siendo un personaje 3D animado en pantalla,
sin que la lógica de juego sepa nada del aspecto, y sin que el aspecto pueda alterar la lógica.

## 1. La cadena

```
PlayerDefinition        qué ES el jugador (id, nombre, número, rol, atributos)     ← Core (Data Core, ya existe)
      │
      ▼
PlayerEntity            qué HACE en el partido (atributos → stats, estado)          ← Gameplay (ya existe)
      │   (solo lectura: estado y eventos)
      ▼
PlayerPresentation      cómo SE VE y SE OYE (decide modelo, kit, animación)         ← Presentación (futuro)
      │
      ▼
Model                   el personaje 3D (malla, LODs, esqueleto, materiales)        ← Arte
      │
      ▼
Animator                reproduce clips según parámetros                            ← Arte + Presentación
```

Regla de dependencias: **flecha hacia abajo = "conoce a"; nunca hacia arriba.**
`Core` no conoce Unity. `Gameplay` no conoce la presentación. `Presentation` lee de `Gameplay` y de `Core`, pero **nunca escribe** en el estado de juego.
Si se borra por completo la presentación, el partido se sigue jugando (con una cápsula, como hoy).

## 2. Qué existe hoy y dónde encaja

| Hoy | Papel en la arquitectura final |
|---|---|
| `PlayerDefinition` (Core/Data) | identidad y atributos. **No se modifica** para añadir aspecto |
| `PlayerEntity` (Gameplay) | une atributos, tuning, stats y `PlayerRuntimeState`; es la fuente de verdad que lee la presentación |
| Prefab `Player`: raíz con cuerpo cinemático + collider + lógica, y un hijo **`Visual`** | `Visual` es el **punto de montaje** del modelo. La raíz es "lógica"; `Visual` es "presentación" |
| `PlayerAnimation` (Gameplay) | primer *driver* mínimo del Animator (`Speed`, `Sprinting`). Será absorbido/sustituido por el driver de presentación |
| Cápsula + "morro" blanco | modelo **placeholder** (nivel 0); debe seguir funcionando como modelo válido |

## 3. Componentes (futuros)

| Componente | Capa | Responsabilidad (una) |
|---|---|---|
| `PlayerAppearance` (datos) | Core (puro) | descripción visual de un jugador: id de modelo, complexión, tono de piel, peinado, color de pelo, rostro, botas, accesorios |
| `PlayerAppearanceCatalog` (datos) | Core/Data | tabla **`playerId → PlayerAppearance`**. Vive **aparte** de `PlayerDefinition` |
| `KitDefinition` (datos) | Core/Data | uniforme: colores primario/secundario/acento (parte ya en `TeamColors`), estilo de camiseta, número visible |
| `PlayerModelDefinition` | Unity (asset) | un modelo concreto: prefab, LODs, tipo de rig, complexiones y calidad que soporta |
| `PlayerPresentationResolver` | Core (puro) | decide **qué modelo y apariencia** usar: `(PlayerDefinition, TeamDefinition, contexto) → PresentationSpec` |
| `PlayerPresentation` | Unity (MonoBehaviour) | ciclo de vida del modelo (toma/devuelve del *pool*), aplica apariencia y kit, enlaza el *driver* de animación |
| `PlayerAnimatorDriver` | Unity | traduce estado de gameplay → parámetros del Animator ([PLAYER_ANIMATION_SPEC.md](PLAYER_ANIMATION_SPEC.md)) |
| `AnimationParameterMapper` | Core (puro) | la lógica de esa traducción: umbrales, histéresis, suavizado de `Accel`/`TurnRate`. **Probable en tests sin Unity** |
| `PresentationEvent` | Core (puro) | señales de gameplay hacia presentación: `BallTouched`, `Kicked`, `Tackled`, `GoalScored`, `Exhausted`… |

Los marcados "Core (puro)" se pueden escribir y probar **ya** fuera de Unity cuando se decida; los de Unity se hacen cuando podamos validar en el editor.

## 4. Separación presentación / gameplay

**Gameplay → Presentación (permitido):**
1. Leer `PlayerRuntimeState`: `Speed`, `Heading`, `Stamina01`, `IsSprinting`, `IsExhausted`, `SprintIntensity`, y `PlayerStats.TopSpeed`.
2. Recibir `PresentationEvent` (flujo en un solo sentido; la presentación se suscribe, el gameplay no sabe quién escucha).
3. Leer la pose de la raíz (posición/rotación interpolada del cuerpo) para colocar `Visual`.

**Presentación → Gameplay (prohibido):**
- Escribir en `PlayerRuntimeState`, mover la raíz, o aplicar fuerzas.
- Leer el input. **No existe ninguna ruta de input hacia el aspecto**: el sprint se ve porque la velocidad lo es, no porque se pulse algo. No hay botón ni *flag* de sprint.
- Decidir resultados de juego (un `ControlFail` animado no decide que se falle el control; lo decide el gameplay y la presentación lo muestra).

El único acoplamiento temporal es el evento de animación `BallContact` (sincronizar el golpeo del pie con el balón). Se resuelve haciendo que el gameplay decida el golpeo y la animación se adapte
(o difiriendo el efecto visual), no al revés. El detalle se decide al implementar pase y tiro.

## 5. Un mismo `PlayerDefinition`, varios modelos

El aspecto **no está dentro** del `PlayerDefinition`. Se resuelve por catálogo:

```
PlayerDefinition (id "fs27-p-nico-valmar")
        │
        ├─ PlayerAppearanceCatalog["fs27-p-nico-valmar"] → PlayerAppearance (piel, pelo, rostro…)
        └─ contexto (Match / Showcase / Placeholder) + calidad
                 │
                 ▼
        PlayerPresentationResolver ──► PresentationSpec { Modelo, LOD profile, Appearance, Kit }
```

| Contexto | Modelo que se elige | Para qué |
|---|---|---|
| `Placeholder` | cápsula actual | desarrollo, pruebas de gameplay, respaldo si falta arte |
| `Match` | modelo de partido (LOD1–LOD3) | gameplay en cámara TV |
| `Showcase` | modelo de cercanas (LOD0 + expresiones + luces) | selección de jugador, repeticiones, celebraciones |

Consecuencias:
- Cambiar el modelo de un jugador = cambiar una **entrada de catálogo**, sin tocar `PlayerDefinition` ni código.
- Un jugador puede tener **varias apariencias** (p. ej. edición de temporada) y el mismo modelo puede vestir a **muchos jugadores** (variando piel, pelo y kit por parámetros).
- Añadir contenido (nuevo modelo, nuevo equipo) **no cambia sistemas existentes**: son datos nuevos.
- Se puede reemplazar el modelo placeholder por el real **sin reescribir `PlayerMovement`** (cumple el requisito de la Fase 1).

## 6. Flujo al empezar un partido (diseño)

```
MatchSetup (equipo local, rival)
   │  DataValidator.ValidateMatchTeams(...)             ← datos válidos (ya existe)
   ▼
por cada PlayerDefinition de cada equipo:
   1. se crea el jugador de lógica (PlayerEntity, movimiento, balón…) — sin modelo
   2. PlayerPresentationResolver → PresentationSpec
   3. PlayerPresentation toma un modelo del pool, lo cuelga de `Visual`
   4. aplica: piel, pelo, rostro, kit del equipo (máscara), número
   5. enlaza PlayerAnimatorDriver ← PlayerEntity.State
   ▼
durante el partido (cada frame, en Update/LateUpdate, no en FixedUpdate):
   driver lee estado → escribe parámetros del Animator; LODGroup decide la malla; sombra según calidad
```

Al terminar el partido, los modelos **vuelven al pool** (nada de `Instantiate`/`Destroy` durante el partido).

## 7. Reglas de implementación (para cuando se haga)

- Ensamblado nuevo **`FS27.Presentation`** (Unity) que referencia `Gameplay` y `Core`; `Gameplay` no lo referencia.
- Sin *singletons* ni búsquedas globales: la presentación recibe sus dependencias al crearse.
- Sin asignaciones por frame; parámetros del Animator por *hash*; sin LINQ.
- La presentación se actualiza **con la pose interpolada** del cuerpo cinemático para que la animación no vibre con la física a 60 Hz.
- El modelo no lleva lógica de juego ni *colliders*; cumple el contrato de [PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §11.
- Degradación de calidad: la presentación aplica el perfil de calidad (LOD bias, sombra de mancha, hoja de expresiones estática…) respetando los invariantes de legibilidad
  ([PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §9).

## 8. Qué se podrá probar sin Unity

Cuando se decida construirlos (aditivo, en Core): `PlayerAppearance` y su validación (ids existentes, rangos), `PlayerAppearanceCatalog`,
`PlayerPresentationResolver` (elección de modelo por contexto y calidad) y `AnimationParameterMapper` (por ejemplo, que `Speed` 0,25 / 0,65 / 1,0
produzca trote / carrera / sprint, que la histéresis evite parpadeos y que `Exhausted` active el cansancio). Lo que toca `Animator`, `LODGroup` o materiales exige Unity y se valida en el editor.

## 9. Decisiones abiertas

1. ¿Rig genérico o Humanoid? (por *profiling*; ver [PLAYER_ASSET_SPEC.md](PLAYER_ASSET_SPEC.md) §5).
2. ¿Número del jugador por atlas de dígitos o por textura? (por prototipo).
3. ¿El catálogo de apariencias vive en un único asset o en uno por equipo?
4. Mecanismo exacto del evento `BallContact` cuando existan pase y tiro.
