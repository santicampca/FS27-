# Research Pipeline

Cómo pasar de "este jugador de referencia juega así" a un `FootballDNA`, sin meter vídeos, páginas ni datos reales en el juego.

```
Fuentes externas ─► IResearchProvider ─► ResearchFinding[] ─► IFootballAnalysisProvider ─► FootballObservation[]
 (hoja de cálculo,    (adaptador,          (material bruto,       (estadísticas offline |            │ ObservationValidator
  persona, web,        contrato)            solo autoría)          modelo de lenguaje)               ▼ (lo no válido se descarta)
  modelo con búsqueda)                                                             ObservationAggregator ─► AggregatedPattern[]
                                                                                                          │
                                       atributos + perfil + preferencias manuales ─► FootballDnaComposer ─► FootballDNA + explicación
```

## Reglas
- **Ficticio y sin nombres reales:** el sujeto es un id de referencia **opaco**. Las plantillas de modelo lo dicen expresamente ("nunca nombres ni identifiques a una persona real"). Una prueba escanea las plantillas.
- **Solo autoría:** hallazgos y observaciones nunca viajan en el paquete de runtime (el registro de runtime no tiene dónde guardarlos).
- **Lo que viene de fuera no es de fiar:** todo lo que devuelve un analizador pasa por `ObservationValidator` (el patrón existe, valores y confianza en 0..1, fuente conocida...). Lo inválido se descarta y se cuenta. Un modelo nunca es más fiable de lo que dice su tipo de fuente.
- **Offline por defecto:** `InlineResearchProvider` (hallazgos ya en mano) y `StatLineAnalyzer` funcionan sin red ni modelo. Un proveedor que falla es un error informado; un analizador que falla es un aviso; **nunca se inventan datos**.

## Observación (`FootballObservation`)
Sujeto opaco, tipo (`Tendency` | `Behavior`), patrón (id de parámetro de DNA o de comportamiento), valor 0..1, **confianza** 0..1, **tipo de fuente** (`Manual, Statistics, VideoAnalysis, Scouting, Prompt, Model`), **situación** (`BehaviorContext`), marca de tiempo (texto ISO; el motor no lee el reloj) y **número de eventos** de respaldo. JSON: `FS27.Observations.v1` (`ObservationJson`).

## Agregación (`ObservationAggregator`)
Peso = `confianza × confianza de la fuente × √eventos`. Confianzas de fuente (valores de partida): Manual/Estadística 1,0; Vídeo 0,9; Scouting 0,7; Prompt 0,6; Modelo 0,5. Resultado por patrón: valor ponderado, **confianza** (varias fuentes independientes que coinciden la suben; el desacuerdo la baja), eventos totales, nº de fuentes, **desacuerdo** (desviación) y la situación compartida por más de la mitad del peso. Independiente del orden de llegada (prueba).

## Análisis estadístico real (`StatLineAnalyzer`)
Convierte estadísticas por 90 minutos en tendencias comparando cada una con una media y una desviación de referencia (puntuación z): `dribbles_attempted_p90`, `shots_p90`, `shots_outside_box_share`, `first_time_shot_share`, `through_balls_p90`, `crosses_p90`, `progressive_passes_p90`, `short_pass_share`, `pressures_p90`, `tackles_p90`, `interceptions_p90`, `touches_in_box_p90`, `back_to_goal_touch_share`. `nivel = 0,5 + 0,18 · z`. La confianza sube con los minutos (900 min = muestra plena) y una estadística ≥ 1 desviación por encima sugiere el comportamiento de firma asociado. **Las medias y desviaciones son valores de partida, no una afirmación sobre ninguna liga real.**

## Con un modelo de lenguaje
`LlmAnalysisProvider` pide observaciones con un esquema estricto (`PromptTemplates.ObservationSchema`) y pasa por el mismo validador. Contrato probado con transporte falso; ⚠️ nunca ejecutado contra un servicio real.

## Qué falta
Adaptadores reales de fuentes (hojas de cálculo, web) en una herramienta de autoría; estadísticas por zona/situación más finas; calibración de las referencias z con datos propios.
