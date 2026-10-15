#!/usr/bin/env python3
"""Fase 34: classifies the FS27-REQ register (Docs/requisitos_estado.csv) from what the repository really contains.
Honest by construction: every row says HOW SURE the classification is.
  confianza = requisito : the requirement itself was checked against a test, a bench scene or a built file (kept from earlier phases or listed in VERIFIED below)
  confianza = seccion   : the subsystem the requirement belongs to exists (or does not) in the repository; the requirement was NOT checked one by one
Statuses: confirmado, parcial, pendiente, requiere-humano (a person has to judge it), sin auditar.
Usage: python3 Tools/requisitos_audit.py   (rewrites Docs/requisitos_estado.csv and Docs/requisitos_resumen.csv)
"""
import csv, os, re, sys
from collections import Counter, defaultdict
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CSV = os.path.join(ROOT, 'Docs', 'requisitos_estado.csv')

def exists(*parts): return os.path.exists(os.path.join(ROOT, *parts))
def nfiles(d, ext=None):
    n = 0
    for r, _, fs in os.walk(os.path.join(ROOT, d)):
        n += sum(1 for f in fs if not f.endswith('.meta') and f != '.gitkeep' and (ext is None or f.endswith(ext)))
    return n

# ---- facts read from the repository (not typed in)
FACT = {
    'audio_assets': nfiles('Assets/_Project/Audio'),
    'art_assets': nfiles('Assets/_Project/Art'),
    'camera_modes': exists('Assets/_Project/Scripts/Core/Camera/CameraModeId.cs'),
    'var': exists('Assets/_Project/Scripts/Core/Match/Var.cs'),
    'penalties': exists('Assets/_Project/Scripts/Core/Match/Penalties.cs'),
    'free_kicks': exists('Assets/_Project/Scripts/Core/Match/FreeKicks.cs'),
    'economy': exists('Assets/_Project/Scripts/Core/Economy/ZenitLedger.cs'),
    'cross_not_supported': 'NotSupported' in open(os.path.join(ROOT, 'Assets/_Project/Scripts/Core/Match/MatchController.Actions.cs'), encoding='utf-8').read(),
    'offline_test': exists('Client/test/offline.mjs'),
    'bench': exists('Assets/_Project/Tests/EditMode/GameplayBench.cs'),
    'tutorial': any('tutorial' in open(os.path.join(ROOT, p), encoding='utf-8', errors='ignore').read().lower() for p in ['Client/web/game.js', 'Client/web/index.html']),
}

# section -> (default status, evidence).  Default for a subsystem that exists: parcial (it exists and the suite exercises it; each requirement is not checked).
S = {}
def sec(name, status, ev): S[name] = (status, ev)
sec('Identidad y estructura del partido', 'parcial', 'Core 6v6 determinista, datos ficticios editables (Docs/authoring); cliente web con HUD mínimo')
sec('Movimiento y locomoción', 'parcial', 'Locomotion/StaminaSystem con pruebas (Docs/LOCOMOTION_STAMINA.md)')
sec('Joystick, sprint y controles táctiles', 'parcial', 'VirtualJoystick (Unity) y stick + A/B/C del cliente web; sprint por intensidad (InputCurve)')
sec('Selección y cambio de jugador', 'parcial', 'AutoSelection + SelectionStability (fase 32; escenas H3/H4 del banco)')
sec('Balón y física', 'parcial', 'BallSimulator y pruebas (Docs/ACTIONS_AND_BALL.md)')
sec('Primer toque y recepción', 'parcial', 'FirstTouch con puntuación y umbrales; pruebas de acciones')
sec('Pases y combinaciones', 'parcial', 'PassPhysics, BallCarrierEvaluator, escenas A1/A3/A4/B1 del banco; sin paredes intencionadas (Docs/AI_OPPORTUNITIES_PHASE31.md)')
sec('Tiros y finalización', 'parcial', 'Shot/CurvedShot/Chip, ShotQualityEvaluator (xG), escenas A5/H2')
sec('Centros y juego por las bandas', 'parcial', 'fase 35: la acción Cross es real (arco más alto que un pase largo, aterriza donde caería un pase largo de igual potencia) y la IA la usa unas 10 veces por equipo y partido; pero solo 0,75 centros por partido acaban en tiro: la decisión de centrar es la de la fase 28, sin mejorar')
sec('Balones aéreos y juego de cabeza', 'pendiente', 'solo cabezazo básico (fase 33); sin volea, duelo aéreo ni portero con balones altos')
sec('Regates y protección', 'parcial', 'DribbleSpace, Shield, Protect en el motor de decisión')
sec('Presión alta y defensa agresiva', 'parcial', 'PressingStyle.AggressivePress y escala de presión (TeamTactic, fase 31); escena C1')
sec('Defensa posicional y coberturas', 'parcial', 'DefenseRoles (primario/secundario), HomeTarget; escenas C2-C4')
sec('Entradas, presión individual y duelos', 'parcial', 'Tackle/PhysicalChallenge/faltas en el Core; tackles raros en el banco (16 por partido)')
sec('Porteros: colocación y reflejos', 'parcial', 'GoalkeeperSystem/GoalkeeperAi con pruebas (Docs/GOALKEEPER_SYSTEM.md)')
sec('Porteros: salidas y distribución', 'parcial', 'GoalkeeperDistribution, despeje y apuro (fases 29 y 31); banco del portero')
sec('Saques de banda', 'parcial', 'Fase 28 (procedimiento) y fase 33 (potencia por pulsación para la persona)')
sec('Córners', 'parcial', 'Fase 28 (procedimiento) y fase 33 (puntería compartida con los tiros libres)')
sec('Tiros libres y puntos direccionales', 'parcial', 'Core completo (FreeKicks, FreeKickAim) y fase 35: la persona lo saca en el cliente (pase o tiro a zona, carga por pulsación); sin barrera interactiva ni indicador de altura')
sec('Penaltis', 'parcial', 'Core (Penalties, tanda) con pruebas y fase 35: la persona lo tira en el cliente (zona y fuerza); sin portero manual ni tanda interactiva')
sec('Reanudaciones y flujo de partido', 'parcial', 'RestartRequest/PrepareRestart/ConfirmRestart; el banco mide el flujo (saques, fuera de juego del balón)')
sec('Reglas de Classic', 'parcial', 'GameModeRules/MatchSettings.CreateClassic con pruebas')
sec('Reglas de Arena', 'parcial', 'MatchSettings.CreateArena con pruebas; el banco de la fase 32 solo mide Classic')
sec('VAR y decisiones arbitrales', 'parcial', 'Var.cs, RefereeDecisionSystem, Docs/PENALTIES_VAR_PHASE23.md; sin interfaz de VAR en el cliente jugable')
sec('IA ofensiva sin balón', 'parcial', 'AttackSupport/AttackPositioning (Docs/ATTACK_SUPPORT_RUNS.md); sin paredes ni triangulaciones intencionadas')
sec('IA defensiva sin balón', 'parcial', 'DefenseRoles, TransitionReturn, TeamTactic')
sec('Transiciones y contraataques', 'parcial', 'TransitionStyle en TeamTactic (efecto aislado sin medir: Docs/AI_OPPORTUNITIES_PHASE31.md); escenas B2/B3')
sec('Personalidad táctica y entrenadores', 'parcial', 'TeamTacticalIdentity desde los datos de 20 entrenadores (fase 31); nombres de entrenadores pendientes de edición')
sec('Dificultad y adaptación', 'parcial', 'DecisionProfile; el banco ordena Elite > Novice en partidos completos; sin adaptación dinámica')
sec('Atributos, roles y polivalencia', 'parcial', 'PlayerAttributes/Roles/RolePositionAssigner con validadores (DataLock)')
sec('Equipos y plantillas', 'parcial', '12 equipos y plantillas con DataLock; sin editor para la persona')
sec('Cámara y legibilidad', 'parcial', 'Core/Camera con 6 modos y CameraManager (Unity); el cliente web tiene una sola vista cenital')
sec('Interfaz y HUD', 'parcial', 'cliente web: marcador, reloj, posesión y dirección de ataque, resistencia con aviso de sprint, avisos de reanudación, pausa, ajustes, estadísticas finales, formulario; sin menús de competición')
sec('Animaciones y presentación', 'parcial', 'arquitectura de animación y generador de personajes (Docs/ANIMATION_ARCHITECTURE.md); sin arte final')
sec('Audio y respuesta sensorial', 'parcial', 'fase 35: sonidos sintetizados por el navegador (patada, silbato, gol, cabezazo, falta, parada, murmullo) con volumen y silencio, y vibración en el móvil; sin archivos de audio (Assets/_Project/Audio vacío), sin música ni sonido por superficie')
sec('Rendimiento y estabilidad móvil', 'parcial', 'presupuesto de rendimiento medido (fase 31) y 60 pasos/s en Chromium sin cabeza; sin móvil real')
sec('Guardado, perfiles y personalización', 'parcial', 'fase 35: ajustes y perfil (partidos, victorias, goles) guardados en este aparato con localStorage; sin cuenta, nube ni exportar perfil; ZenitLedger sin integrar')
sec('Economía, Zenit Points y progresión', 'parcial', 'ZenitLedger (Core) con pruebas y prototipo web de tienda; sin integración en el cliente jugable')
sec('Tutorial, práctica y accesibilidad', 'parcial', 'fase 35: práctica guiada de cinco pasos, tamaño de texto, colores para daltonismo y reducción de movimiento; sin subtítulos (no hay audio con voz) ni lector de pantalla')
sec('Calidad de IA y evaluación', 'parcial', 'GameplayBench (20 escenas), campañas, DecisionExplainer, línea base comprometida')
sec('Depuración y arquitectura', 'parcial', 'AiDiagnostics, DecisionExplainer, DebugState, Docs/ARCHITECTURE.md; pruebas de arquitectura (sin literales sueltos, separación Arena/Classic)')
sec('Seguridad, privacidad y entrega', 'parcial', 'el HTML no envía nada; el formulario no incluye datos personales; ningún despliegue sin permiso')
sec('Escenarios de partido y casos límite', 'parcial', 'pruebas de casos límite del Core y escenas del banco; sin catálogo completo de los casos de la lista')
sec('Gestión del partido y estados', 'parcial', 'MatchPhase/MatchState, pausa, descanso, prórroga, tanda')
sec('Multijugador futuro y límites de alcance', 'pendiente', 'fuera del alcance actual: no hay red ni modo para dos personas')
sec('Balance y sensación futbolística', 'requiere-humano', 'el banco mide goles, tiros, xG, pases y flujo, pero la sensación solo la juzga una persona')
sec('Dirección visual y modelos', 'pendiente', 'Assets/_Project/Art vacío; el cliente dibuja círculos')
sec('Prueba humana y lanzamiento de prototipo', 'parcial', 'HTML offline, guía no técnica y formulario; ninguna persona ha jugado todavía')
sec('Córners, bandas y balón parado: control avanzado', 'parcial', 'fase 33: carga de potencia, puntería compartida, ejecución única; sin indicador de altura/zona ni viento')
sec('Reglas de diseño y prioridades de implementación', 'parcial', 'este registro y Docs/REQUISITOS_1000.md')

# requirement-level overrides, each with the evidence that backs it (confianza = requisito)
V = {}
def ver(ids, status, ev):
    for i in ids: V[i] = (status, ev)
ver([11], 'confirmado', 'Client/test/offline.mjs: partido completo desde file:// con 0 peticiones de red')
ver([420, 702, 781], 'confirmado', 'GameplayBench_IsDeterministic_AndComplete y escenas con semilla; mismo CSV al repetir')
ver([10], 'parcial', 'la dificultad solo cambia DecisionProfile (sesgos y reacción), no la física ni las reglas (Docs/DIFFICULTY_SYSTEM.md); DifficultyGuardTests')
ver([981, 982, 984], 'confirmado', 'Docs/requisitos_estado.csv: id estable, sección y estado por requisito')
ver([983], 'parcial', 'cada fila lleva evidencia, pero solo los requisitos con confianza «requisito» tienen un criterio observable comprobado')
ver([996], 'confirmado', 'los informes y el PR distinguen simulación automática de prueba humana')
ver([998], 'parcial', 'Docs/AI_OPPORTUNITIES_PHASE31.md y Docs/AI_PHASE32.md conservan los experimentos descartados')
ver([999], 'confirmado', 'sin push, despliegue ni cambio de permisos sin autorización explícita en las fases 28-34')
ver([1000], 'parcial', 'criterio rector; no es comprobable automáticamente')

# ---- batch checked one by one in phase 34: selection of the controlled player (61-80)
ver([187], 'parcial', 'Phase35Tests.AnAerialBall_WithTwoPlayersInReach...: gana un solo jugador, según calidad de control, resistencia y cercanía; sin salto ni atributo de juego aéreo')
ver([161], 'confirmado', 'Phase35Tests.ACross_IsARealAction_WithAHigherArc... y TheAI_CrossesWithTheRealAction: la acción existe en el Core y la IA la usa')
ver([162], 'pendiente', 'la persona no puede centrar: ningún botón del cliente pide la acción Cross (solo la IA)')
ver([163, 164], 'pendiente', 'un solo tipo de centro (lofteado); sin centro raso ni tenso')
ver([61, 62, 65], 'confirmado', 'Phase32SelectionTests.ASwitchByHand_WhileDefending_IsKept_ForTheHold y escena H4 (manualKept 8 % -> 100 %)')
ver([63, 74], 'confirmado', 'Phase32SelectionTests.TheMarker_DoesNotFlickerBetweenTwoPlayers y escena H3 (parpadeo 10,0 -> 0,08 por minuto)')
ver([77], 'confirmado', 'escenas H3 (ataque y defensa) y H4 del banco, con guardas de línea base')
ver([67], 'confirmado', 'AutoSelection.Choose excluye al portero (AiDecisionSimulationTests.AutoSelection_WithoutTheBall_TheNearestFieldPlayer)')
ver([78], 'confirmado', 'SelectionStability: retención tras cambio manual, tiempo mínimo entre cambios y margen de distancia (Phase32SelectionTests)')
ver([64, 69], 'parcial', 'sin balón elige al defensor primario de DefenseRoles contra el portador; no usa la trayectoria del balón ni al receptor rival (REQ-0069 pendiente en la parte del receptor)')
ver([66], 'confirmado', 'MatchController.SelectNearestToBall y Phase32SelectionTests.TheSwitchButton_GoesToTheNearestToTheBall (el botón ⇄ del cliente lo usa)')
ver([68], 'pendiente', 'sin cálculo del punto de caída en la selección (REQ-0181 tampoco)')
ver([70], 'parcial', 'con el balón en el aire la selección automática va al más cercano al balón, no al receptor previsto (IntendedReceiverId existe pero no se usa)')
ver([71, 76], 'parcial', 'CycleSelection incluye al portero y no depende de la fase, sin prueba específica')
ver([73, 72], 'parcial', 'el cliente web mueve el aro con el jugador seleccionado; sin prueba durante giros')
ver([79], 'parcial', 'a igual distancia gana el primero de la plantilla (comparación estricta), sin prueba dedicada')
ver([75], 'parcial', 'la retención manual de 3 s lo permite; no hay bloqueo prolongado')
ver([80], 'pendiente', 'el cliente no muestra qué jugador movería la IA sin intervención')

# ---- batch checked one by one in phase 34: controls (41-60) and HUD (641-660) of the web client
ver([41], 'confirmado', 'Client/web/game.js: zona muerta del stick de 0,08 (stickMove)')
ver([53], 'confirmado', 'Client/web/game.js releaseAll (blur y visibilitychange) y Client/test/offline.mjs: perder el foco pausa y suelta todo')
ver([649, 650], 'confirmado', 'botón de pausa del cliente: el Core no avanza y se reanuda sin perder estado (Client/test/offline.mjs)')
ver([641], 'confirmado', 'captura del cliente en Chromium: equipos, goles y reloj en el marcador (Client/test/offline.mjs)')
ver([652], 'confirmado', 'el formulario es opcional: OTRO PARTIDO siempre está disponible (Client/test/offline.mjs)')
ver([43], 'parcial', 'el Core usa la magnitud del stick (InputCurve) y el cliente la envía; sin escena que mida el sprint por intensidad')
ver([49, 645], 'parcial', 'los botones A/B/C cambian de estilo al pulsarse (clase on); sin prueba')
ver([45, 56], 'parcial', 'botones de 60-74 px y safe-area en CSS; sin prueba en móviles estrechos con recorte')
ver([51], 'parcial', 'pointerup/cancel devuelven el stick a cero en el código; sin prueba táctil dedicada')
ver([52], 'parcial', 'eventos de puntero independientes para stick y botones (smoke.mjs mueve y actúa); sin prueba de dos dedos simultáneos')
ver([651], 'confirmado', 'la pantalla final muestra resultado y estadísticas (posesión, tiros, pases, entradas, faltas, córners, saques, cabezazos): Client/test/offline.mjs')
ver([5], 'confirmado', 'HUD con quién tiene el balón y hacia dónde atacas (Client/test/offline.mjs)')
ver([14], 'parcial', 'resultado y estadísticas; sin goleadores')
ver([80], 'parcial', 'aro discontinuo en el jugador que la IA elegiría (SuggestedIndex); sin prueba visual')
ver([644], 'parcial', 'el HUD avisa SPRINT / CANSADO según la intensidad del stick y la resistencia; sin prueba')
ver([57], 'confirmado', 'Client/test/offline.mjs: la práctica avanza paso a paso al hacer de verdad cada gesto')
ver([654], 'confirmado', 'pantalla de ajustes agrupada en controles, cámara, audio y accesibilidad (Client/test/offline.mjs la abre y la usa)')
ver([44, 54], 'parcial', 'tamaño del stick y de los botones y modo zurdo (Client/test/offline.mjs); sin mover libremente el stick ni invertir ejes')
ver([655], 'parcial', 'el volumen cambia el volumen maestro al instante; sin prueba con audio real')
ver([658, 659], 'pendiente', 'sin etiquetas en los iconos ni conservación de opciones por menú')
ver([60, 809, 898], 'parcial', 'el cliente registra stick, pulsaciones, cambios y saques (hasta 3.000 entradas con la semilla) y se copia desde la pausa; sin reproductor que las rejuegue')

# keyword overrides: a requirement about something the repository does not have is pendiente whatever its section says
ABSENT = [
    (r'\b(multijugador|online|en línea|en linea|matchmaking|dos personas|cooperativ)', 'sin red ni multijugador (fuera del alcance actual)'),
    (r'\b(volea)', 'la acción volea no existe en el Core'),
    (r'\b(viento)', 'no hay viento en el Core'),
    (r'\b(modelo 3d|modelos 3d|textura|shader|polígonos|poligonos|rig|lod)\b', 'sin arte 3D en el repositorio (Assets/_Project/Art vacío)'),
]
HUMAN = re.compile(r'(sensaci|divertid|natural\b|creíble|creible|justo|injust|frustr|agradable|satisfactori|legible|claramente|atractiv|intuitiv|cómod|comod|fluid)', re.I)
REP = re.compile(r'\b(informe|prueba|test|benchmark|pruebas|métric|metric|registr|medir|verificar)\w*', re.I)

def classify(r):
    i = int(r['id'][-4:]); text = r['requisito'].lower(); s = r['seccion']
    if i in V: return V[i][0], V[i][1], 'requisito'
    if r['confianza'] == 'requisito' and r['estado'] != 'sin auditar':
        return r['estado'], r['evidencia'], 'requisito'
    base, ev = S[s]
    for pat, why in ABSENT:
        if re.search(pat, text) and base != 'pendiente':
            return 'pendiente', why, 'seccion'
    if base == 'parcial' and HUMAN.search(text):
        return 'requiere-humano', 'depende de cómo lo percibe una persona (' + ev + ')', 'seccion'
    return base, ev, 'seccion'

def main():
    rows = list(csv.DictReader(open(CSV, encoding='utf-8')))
    for r in rows:
        r.setdefault('confianza', 'requisito' if r['estado'] != 'sin auditar' else '')
        if not r.get('confianza') and r['estado'] != 'sin auditar': r['confianza'] = 'requisito'
        r['estado'], r['evidencia'], r['confianza'] = classify(r)
    with open(CSV, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f); w.writerow(['id', 'seccion', 'estado', 'confianza', 'evidencia', 'requisito'])
        for r in rows: w.writerow([r['id'], r['seccion'], r['estado'], r['confianza'], r['evidencia'], r['requisito']])
    by = defaultdict(Counter)
    for r in rows: by[r['seccion']][r['estado']] += 1
    with open(os.path.join(ROOT, 'Docs', 'requisitos_resumen.csv'), 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f); w.writerow(['seccion', 'confirmado', 'parcial', 'pendiente', 'requiere-humano', 'sin auditar'])
        for s, c in by.items(): w.writerow([s, c['confirmado'], c['parcial'], c['pendiente'], c['requiere-humano'], c['sin auditar']])
    tot = Counter(r['estado'] for r in rows); conf = Counter(r['confianza'] for r in rows)
    print('estados', dict(tot)); print('confianza', dict(conf)); print('hechos', FACT)

def write_doc():
    rows = list(csv.DictReader(open(CSV, encoding='utf-8'))); res = list(csv.DictReader(open(os.path.join(ROOT, 'Docs', 'requisitos_resumen.csv'), encoding='utf-8')))
    st = Counter(r['estado'] for r in rows); cf = Counter(r['confianza'] for r in rows)
    t = "| Sección | confirmado | parcial | pendiente | requiere humano |\n|---|---|---|---|---|\n" + "".join(f"| {r['seccion']} | {r['confirmado']} | {r['parcial']} | {r['pendiente']} | {r['requiere-humano']} |\n" for r in res)
    pend = [r for r in rows if r['estado'] == 'pendiente']
    bysec = Counter(r['seccion'] for r in pend)
    pl = "\n".join(f"- {k}: {v}" for k, v in bysec.most_common(12))
    new = f"""## Estado de los 1.000 (generado por `Tools/requisitos_audit.py`)
Cada fila de `Docs/requisitos_estado.csv` dice **cuánta confianza** tiene su estado:
- **requisito** ({cf['requisito']} filas): comprobado uno a uno contra una prueba, una escena del banco, el código o la prueba del cliente.
- **seccion** ({cf['seccion']} filas): el subsistema existe (o no) en el repositorio, pero el requisito **no se ha comprobado individualmente**. «Parcial» aquí significa «hay un subsistema con pruebas; falta verificar este comportamiento», no «medio hecho».

Totales: **confirmado {st['confirmado']}**, parcial {st['parcial']}, pendiente {st['pendiente']}, requiere humano {st['requiere-humano']}. **No se han «integrado los 1.000»**: se han construido los grandes huecos que se podían construir (fases 33-35: cabezazos, saque y córner con fuerza y puntería, centro real, tiros libres y penaltis para la persona, ajustes, sonido sintetizado, vibración, práctica, perfil, estadísticas, registro de entradas, pausa) y el resto sigue como se indica.

Pendientes por sección (los más numerosos):
{pl}

Lo que **no se puede construir sin decisión, arte o red** y por eso sigue pendiente: arte 3D y modelos (no hay assets), música y archivos de audio, multijugador, cuenta/nube, volea, viento, y todo lo que solo una persona puede juzgar (sensación, diversión, legibilidad).

Resumen por sección:

{t}
"""
    d = open(os.path.join(ROOT, 'Docs', 'REQUISITOS_1000.md'), encoding='utf-8').read()
    a = d.index('## Fase 34') if '## Fase 34' in d else d.index('## Estado de los 1.000'); b = d.index('## Honestidad sobre el alcance')
    open(os.path.join(ROOT, 'Docs', 'REQUISITOS_1000.md'), 'w', encoding='utf-8').write(d[:a] + new + d[b:])

if __name__ == '__main__':
    main()
    if '--doc' in sys.argv: write_doc()
