# Cómo abrir y probar el proyecto (Windows / Mac)

Requisitos: Unity Hub + Unity 6 LTS (con módulo **Android Build Support**: OpenJDK + Android SDK & NDK), GitHub Desktop.

## 1. Traer el código
GitHub Desktop → File → Clone repository → pestaña URL → `santicampca/FS27-` → elige una carpeta (ej. `C:\Dev\FS27`) → Clone.
Current Branch → `phase-1-movement`.

## 2. Aportar la base del proyecto Unity (una sola vez)
El repositorio contiene nuestro código pero no los archivos base que genera Unity (URP, paquetes, ajustes):
1. Unity Hub → Projects → New project → **Universal 3D** (Unity 6) → nombre `FS27_base`, ubicación `C:\Dev` → Create project. Espera a que abra y ciérralo.
2. Copia de `C:\Dev\FS27_base` a `C:\Dev\FS27` (la carpeta clonada): las carpetas `Packages` y `ProjectSettings`, y todo lo que hay dentro de `Assets`
   **excepto** `Scenes`, `TutorialInfo` y `Readme.asset` (con sus `.meta`). Si pregunta por reemplazar, elige reemplazar.
3. GitHub Desktop mostrará muchos cambios: escribe el resumen `Añade base Unity 6 URP` → Commit to phase-1-movement → Push origin.

## 3. Abrir y construir la escena
1. Unity Hub → Add → Add project from disk → `C:\Dev\FS27` → ábrelo (la primera vez tarda).
2. Edit → Project Settings → Player → Other Settings → **Active Input Handling** = *Input System Package (New)* (o *Both*). Reinicia si lo pide.
3. Menú superior **FS27 → 1. Build Sandbox Scene**. Se crea y guarda `Assets/_Project/Scenes/Sandbox.unity`.
4. Pulsa **Play**. WASD mueve, **Shift** sprinta, **Space** trota.
5. GitHub Desktop → commit de lo nuevo (escena, prefabs, materiales, assets de tuning, `.meta`) y Push.

Si Unity muestra errores rojos en la Console, cópialos tal cual y pásamelos.

## 4. Probar en un teléfono Android
1. En el teléfono: Ajustes → Acerca del teléfono → toca 7 veces "Número de compilación" → Opciones de desarrollador → activa **Depuración USB**. Conéctalo al PC por USB y acepta el aviso.
2. Unity: **FS27 → 2. Apply Mobile Settings (Android)**.
3. File → Build Profiles → Android → **Switch Platform** (si ya tiene la escena `Sandbox`, perfecto; si no, Add Open Scenes).
4. Con el teléfono conectado, elígelo en *Run Device* → **Build And Run**.

## Lista de pruebas de la Fase 1
Ver `Docs/PHASE1.md` para ajustar. En el Game view: 1 abre proyecto · 2 abre Sandbox · 3 aparece la cápsula azul con el "morro" blanco ·
4 WASD la mueve · 5 acelera poco a poco · 6 frena poco a poco · 7 el balón rueda, rebota y para solo · 8 correr hacia el balón lo empuja ·
9 el joystick (mouse en el editor, dedo en el teléfono) mueve · 10 más lejos del centro = más rápido · 11 la barra baja al sprintar ·
12 sube al soltar · 13 con la barra en rojo ya no llega a la velocidad máxima · 14 la cámara sigue · 15 build en el teléfono.
