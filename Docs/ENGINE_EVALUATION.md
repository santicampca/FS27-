# Evaluación del motor para el cliente 3D de FS27

**Estado: evaluación sobre el papel. No se ha instalado ningún motor, SDK, controlador ni herramienta pesada.** Lo que sigue distingue: *hecho del repositorio* (comprobado aquí), *dato externo* (de búsquedas web, **sin verificar en documentación oficial**) y *decisión que necesita tu información*.

## 1. Hechos del repositorio que condicionan la elección
| Hecho | Consecuencia |
|---|---|
| El Core es C# puro: `noEngineReferences`, `LangVersion 9`, se compila con `net8.0` (`Tests/Core.Tests`) y ya corre en **WebAssembly** (`Client/Fs27.Wasm`, .NET 8) | Cualquier cliente que ejecute .NET 8 puede usar el Core sin cambios. Un motor sin .NET obliga a reescribirlo o a hablar con él por un puente |
| Existe un cliente web jugable (Canvas 2D) que ya consume el Core por `GameBridge` y funciona sin conexión | El cliente 3D puede nacer **dentro de ese mismo puente**, sin migrar nada |
| El Core ya genera personajes procedurales y los exporta a **GLB** sin Blender (`ProceduralCharacterGenerator`, fase Creator) | Hay un camino de arte de partida sin herramientas externas |
| La capa Unity del repositorio (`Gameplay`, `Cameras`, `Input`, `UI`, `Infrastructure`) es fina y **nunca se verificó en Unity** (así lo dicen sus documentos) | Descartar Unity no pierde trabajo verificado. El Core y los tests no dependen de Unity |
| Contrato de renderizado neutro nuevo (`RenderFeed`/`RenderFrame`, fase 40) | Los candidatos se comparan por lo fácil que es conectarlos a él |

## 2. Candidatos
Se evalúan Godot, Unreal y Unity (descartado por tu error de GPU; se deja en la tabla solo como referencia) y un cuarto, **3D web sobre el puente actual** (three.js o Babylon.js, bibliotecas JavaScript), porque encaja con lo que ya existe.

| Criterio | Godot 4 | Unreal 5 | Unity (descartado) | 3D web (three.js / Babylon.js) |
|---|---|---|---|---|
| Usar el Core C# tal cual | **Parcial**: Godot con .NET 8 sí; **la exportación web de proyectos C# no está soportada** (dato externo) | Solo con puente nativo o reescritura (el motor usa C++/Blueprints) | Sí (era el plan original) | **Sí**: el Core ya corre en WASM y se llama por `GameBridge` |
| Móvil (Android) | Sí (exportación nativa; C# en Android soportado según la documentación de Godot 4, a verificar) | Sí, pero pesado para un juego low-poly | Sí | Por navegador (PWA); sin instalación; rendimiento limitado por WebGL |
| Coste de licencia | Gratuito, MIT | Gratuito hasta 1 M$ de ingresos, luego regalía (dato externo) | Cuota por plaza (dato externo) | Gratuito (MIT) |
| Riesgo de GPU/controlador | Medio-bajo: renderizador *Compatibility* (OpenGL) con poca exigencia; **sin probar en tu equipo** | **Alto**: exige GPU y controladores recientes | Real: ya te falló | Bajo: WebGL del navegador; el navegador aísla fallos de GPU |
| Tamaño/tiempo de instalación | Pequeño (≈ cientos de MB) | Muy grande (decenas de GB) | Grande | Nada que instalar para probar; una biblioteca JS |
| Herramientas para animación/importación GLB | Importa GLB; editor de animación | Muy completo, excesivo | Completo | GLB con `GLTFLoader`; animaciones a mano o por código |
| Encaja con "sin pagar nada" | Sí | Sí hasta cierto ingreso | Parcial | Sí |
| Riesgo principal | La exportación web con C# ; un cambio de motor entero | Peso, curva, GPU | Ya descartado | Rendimiento en móviles modestos; límite de memoria del navegador (el HTML de 12 MB ya existe) |

*Los datos externos vienen de comparativas web de 2026 que **no coinciden entre sí** en versiones y tamaños; hay que contrastarlos con la documentación oficial antes de decidir.*

## 3. Recomendación (condicionada)
1. **Primer prototipo: 3D web sobre el puente existente.** No instala nada pesado, reutiliza el Core ya probado en WASM, aísla el riesgo de GPU en el navegador y permite validar cámaras, escala del campo, legibilidad del balón y estilos **antes** de elegir motor.
2. **Candidato a cliente definitivo: Godot 4 (.NET)** para Android/escritorio, si la prueba anterior confirma que hace falta un motor. Decisión final tras comprobar tres cosas que solo tú o una prueba pueden confirmar (§4).
3. **Unreal queda fuera** para este juego (peso y exigencia de GPU desproporcionados para low-poly 6 contra 6). **Unity queda fuera** por tu decisión.

## 4. Lo que necesito saber antes de instalar nada (no bloquea el resto)
- Modelo de tu GPU, sistema operativo y versión del controlador, y **el mensaje exacto del error de Unity** (para saber si Godot en modo Compatibility lo evitaría).
- Si el objetivo es solo móvil Android, solo navegador, o ambos.
- Tu presupuesto de rendimiento: dispositivo móvil más modesto que quieres soportar.

## 5. Pruebas baratas que no instalan nada pesado (cuando lo autorices)
1. 3D web con campo plano, 12 capsulas y un balón alimentado por `RenderFeed` (ver `Docs/CORE_RENDER_INTERFACE.md`): mide fotogramas en tu aparato.
2. Descargar solo el ejecutable de Godot (un archivo, sin instalador) para abrir un proyecto vacío con el renderizador *Compatibility* y comprobar que tu GPU no falla. **Sin tocar controladores.**
3. Solo si ambas salen bien: referenciar el Core en un proyecto Godot .NET.

## 6. Lo que se decide y lo que no
Decidido: no se migra ni se sustituye la arquitectura; el Core sigue siendo la única fuente de verdad. **No decidido** (necesita tu respuesta del §4): el motor definitivo.
