# Muestras generadas por el Creator Engine

Maniquí procedural: **proporciones y colores correctos, formas simples; no es arte final, no tiene rig y no está animado.**

Los `.debug.txt` (informe de toda la pasada) y los `.runtime.json` (registro compacto que viajaría en el paquete) están versionados. Los `.png` (frente / perfil / espalda) y los `.glb` (glTF 2.0, ábrelos en Blender o en cualquier visor) **no se suben** porque el repositorio guarda los binarios en Git LFS; se regeneran, idénticos, con:

```
dotnet run --project Tools/PreviewWriter -- Docs/previews
```
