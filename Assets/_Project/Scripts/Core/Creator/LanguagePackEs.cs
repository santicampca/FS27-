using System;

namespace FS27.Core
{
    /// <summary>Spanish vocabulary. Data only: no logic, no meanings (those live in <see cref="DefaultConcepts"/>).</summary>
    public static class LanguagePackEs
    {
        private const SemanticDomain Vis = SemanticDomain.Visual;
        private const SemanticDomain Gam = SemanticDomain.Gameplay;

        public static LanguagePack Create()
        {
            var p = new LanguagePack { Id = "es" };

            // ---------- structure ----------
            p.Negator("no|sin|nunca|jamas|tampoco|nada de|ni siquiera");
            p.NegationContinuer("ni");
            p.Contrast("pero|aunque|sin embargo|salvo que|eso si|aunque sin");
            p.KeepWhileWords("que no pierda|que no pierdas|que no pierda nada de|no pierda|sin que pierda|que no sacrifique|que no le falte|sin perder|sin quitarle|sin quitar|sin sacrificar|sin dejar de|sin restarle");
            p.Conjunction("y|e|ademas|tambien|pero que|asi como");
            p.Create("crea|crea un|crea una|creame|genera|genera un|generame|haz un|haz una|hazme un|hazme una|quiero un|quiero una|necesito un|necesito una|dame un|dame una|disena|disena un|disena una|inventa un|nuevo jugador|un nuevo");
            p.Preserve("manten|mantene|mantiene|mantenga|conserva|conserve|respeta|no cambies|no toques|no modifiques|no le cambies|no le toques|deja igual|dejalo igual|sin cambiar|sin tocar");
            p.AllElse("todo lo demas|el resto|lo demas|lo otro|todo lo otro|todo igual|todo lo que no");
            p.Only("solo|solamente|unicamente|nada mas que");
            p.Undo("deshaz|deshacer|deshace|vuelve atras|volver atras|revierte|revertir|anula eso|olvida eso|cancela eso");

            p.Reference("el anterior|la anterior|como el anterior|como la anterior|el de antes|el previo|el ultimo|el jugador anterior", EntityReference.Previous);
            p.Reference("el actual|este jugador|este personaje", EntityReference.Current);

            // ---------- operators ----------
            p.Operator("mas|aun mas|todavia mas|incluso mas|aumenta*|sube|subele|incrementa*|mejora*|potencia*|refuerza*|acentua*|agranda*|alarga*|maximiza*", SemanticIntent.Increase);
            p.Operator("menos|reduce*|baja|bajale|disminuy*|resta|rebaja*|recorta*|achica*|acorta*|modera*|suaviza*|minimiza*", SemanticIntent.Decrease);
            p.Operator("quita*|elimina*|borra*|saca*|saca el|retira*|suprime*|descarta*|prescinde*|fuera", SemanticIntent.Remove);
            p.Operator("agrega*|anade*|anadele|pon|ponle|ponele|incluye*|incorpora*|dale|dale un|dale una|suma*|con", SemanticIntent.Add);
            p.Operator("cambia*|cambiale|reemplaza*|sustituye*|otro|otra|distinto|distinta|diferente|renueva*|varia*|rehace|remplaza*", SemanticIntent.Replace);
            p.Operator("resetea*|restablece*|reinicia*|vuelve a lo normal|vuelve al original|valores por defecto|como al principio|neutral", SemanticIntent.Reset);
            p.Operator("hazlo|hazla|hacelo|ponlo|ponla|dejalo|dejala|que sea|lo quiero|la quiero|quiero que sea|volvelo|vuelvelo|vuelvela|convierte lo en|convierte lo", SemanticIntent.Modify, makeIt: true);

            // ---------- amounts ----------
            p.Intensifier("un pelin|un pelito|apenas|casi nada|minimamente|imperceptiblemente", MagnitudeLevel.Minimal);
            p.Intensifier("ligeramente|levemente|un poquito|poquito|poquitin|un tanto|un tantito|lijeramente", MagnitudeLevel.Slight);
            p.Intensifier("un poco|algo|algo de|un chiquito|moderadamente|medianamente|relativamente", MagnitudeLevel.Little);
            p.Intensifier("tanto|tan", MagnitudeLevel.Moderate);
            p.Intensifier("bastante|considerablemente|notablemente|harto", MagnitudeLevel.Quite);
            p.Intensifier("muy|mucho|mucha|muchos|muchas|realmente|verdaderamente|bien|super|sumamente", MagnitudeLevel.Much);
            p.Intensifier("demasiado|demasiada|excesivamente|en exceso", MagnitudeLevel.Much, excess: true);
            p.Intensifier("muchisimo|muchisima|muchisimos|super mega|enormemente|tremendamente|hiper|ultra|altisimo|rapidisimo|fortisimo", MagnitudeLevel.VeryMuch);
            p.Intensifier("extremadamente|extremo de|increiblemente|exageradamente|brutalmente|absurdamente|descomunalmente|monstruosamente", MagnitudeLevel.Extreme);
            p.Intensifier("al maximo|lo maximo|maximo|maxima|al 100|a tope|todo lo posible|el mas|la mas|lo mas posible|lo mas|al limite", MagnitudeLevel.Maximum);
            p.Intensifier("poco|poca|pocos|pocas|escaso de|nada", MagnitudeLevel.Moderate, flip: true);
            p.Intensifier("muy poco|muy poca|muy pocos|pocho|casi nada de", MagnitudeLevel.Much, flip: true);

            // ---------- context cues ----------
            p.Cue("se vea|se ve|se veia|se vean|luzca|luzcan|parezca|parezcan|aspecto|apariencia|visualmente|a la vista|de aspecto|de cara|fisicamente|fisico|estetica|estetico|look|figura", Vis);
            p.Cue("juegue|juega|jugando|jugar|rinda|rendimiento|en el campo|en la cancha|en la cancha|en partido|en el partido|gameplay|de juego|en el juego|habilidad|a nivel de juego|dentro del campo", Gam);
            p.Cue("anim*", SemanticDomain.Animation);
            p.Cue("en los duelos|en duelos|en el duelo|en los choques|en el cuerpo a cuerpo|en los forcejeos|en los contactos|a la hora de pelear el balon|en las disputas|en disputas", Gam, SemanticPhase.Duels);
            p.Cue("con balon|con el balon|con la pelota|con pelota|llevando el balon|cuando tiene el balon|cuando tiene la pelota|con la bola|conduciendo", Gam, SemanticPhase.WithBall);
            p.Cue("sin balon|sin el balon|sin la pelota|sin pelota|cuando no tiene el balon|sin la bola", Gam, SemanticPhase.WithoutBall);
            p.Cue("al defender|defendiendo|en defensa|cuando defiende|a la hora de defender|en tareas defensivas", Gam, SemanticPhase.Defending);
            p.Cue("al atacar|atacando|en ataque|cuando ataca|a la hora de atacar|en tareas ofensivas|en ofensiva", Gam, SemanticPhase.Attacking);
            p.Cue("bajo presion|con presion|presionado|presionada|cuando lo presionan|cuando le presionan|bajo marca", Gam, SemanticPhase.Pressure);
            p.Cue("en sprint|al sprint|sprintando|en carrera|a toda velocidad|en velocidad maxima|corriendo a tope", Gam, SemanticPhase.Sprint);

            // ---------- fillers ----------
            p.Filler("el|la|los|las|un|una|unos|unas|lo|le|les|su|sus|se|me|te|que|de|del|al|a|en|por|para|es|ser|sea|sean|esta|este|esto|eso|ese|esa|quiero|quisiera|necesito|prefiero|podrias|puedes|por favor|favor|jugador|jugadora|personaje|futbolista|chico|tipo|pibe|muchacho|fue|era|tenga|tenia|tiene|como|mi|nuestro|nuestra|bien|ahora|luego|despues|primero|entonces|oye|vale|ok|a ver|digamos|osea|o sea|cuando|donde|donde sea|si|tambien|cosa|cosas|manera|forma|modo|un tipo de|tipo de|clase de|estilo de|le de|mas o menos|aproximadamente|casi|mismo|misma|propio|propia|ya|aun|todavia|siga|siga siendo|quede|queden|quedar|que quede");

            // ---------- body ----------
            p.Word("alto|alta|altos|altas|altura|estatura|talla alta|espigado|espigada|larguirucho", "height", 1, Vis);
            p.Word("bajo|baja|bajos|bajas|bajito|bajita|bajitos|chaparro|chaparra|petiso|petisa|enano|enana|pequeno de estatura|de baja estatura", "height", -1, Vis);
            p.Word("gordo|gorda|gordos|gordas|corpulento|corpulenta|pesado|pesada|rellenito|rellena|ancho de cuerpo", "mass", 1, Vis);
            p.Word("delgado|delgada|delgados|delgadas|flaco|flaca|flacos|flacas|esbelto|esbelta|ligero|ligera|liviano|liviana|fino|fina|escuálido|huesudo|esqueletico|ligerito", "mass", -1, Vis);
            p.Word("robusto|robusta|robustos|fornido|fornida|corpulenta|macizo|maciza|de complexion fuerte|complexion", "build", 1, Vis);
            p.Word("musculoso|musculosa|musculosos|musculosas|musculado|musculada|musculatura|muscular|marcado|marcada|cuadrado de espaldas|fibroso|fibrosa|trabajado|trabajada|definido|definida|cachas|cachudo|mazado|mazada|tonificado|tonificada", "muscularity", 1, Vis);
            p.Word("enclenque|debilucho|debilucha|enjuto|enjuta|canijo|canija", "muscularity", -1, Vis);
            p.Word("atletico|atletica|atleticos|atleticas|atleta|deportivo|deportiva|deportistas|con cuerpo de atleta", "athleticLook", 1, Vis);
            p.Word("caricaturesco|caricaturesca|cartoon|dibujo animado|animado|exagerado|exagerada|estilizado|estilizada|dibujado|comico|comica|de dibujos|tipo comic|de comic", "cartoon", 1, Vis);
            p.Word("realista|realistico|realistica|real|natural|fotorrealista|serio|seria|proporcionado|proporcionada", "realism", 1, Vis);
            p.Word("viejo|vieja|mayor|anciano|veterano|veterana|maduro|madura|de edad", "age", 1, Vis);
            p.Word("joven|jovencito|jovencita|juvenil|chavalito|menor|nino|nina|adolescente|de poca edad", "age", -1, Vis);
            p.Word("moreno|morena|oscuro|oscura|negro de piel|de piel oscura|piel oscura|tez oscura", "skinTone", 1, Vis);
            p.Word("palido|palida|blanco de piel|de piel clara|piel clara|tez clara|claro de piel|de piel blanca", "skinTone", -1, Vis);

            // ---------- generic modifiers (they need a thing) ----------
            p.Modifier("grande|grandes|enorme|enormes|gigante|gigantes|inmenso|inmensa|inmensos|voluminoso|voluminosa|abultado|abultada|prominente|prominentes|exagerado de tamano|desproporcionado|desproporcionada", "size", 1, "overallSize");
            p.Modifier("pequeno|pequena|pequenos|pequenas|chico|chica|chicos|chicas|diminuto|diminuta|minusculo|minuscula|reducido|reducida|chiquito|chiquita|discreto|discreta", "size", -1, "overallSize");
            p.Modifier("largo|larga|largos|largas|alargado|alargada|extenso|extensa|longo", "length", 1);
            p.Modifier("corto|corta|cortos|cortas|cortito|cortita|recortado|recortada|rapado|rapada", "length", -1);
            p.Modifier("ancho|ancha|anchos|anchas|amplio|amplia|amplios|amplias|abierto|abierta|espalda ancha", "width", 1);
            p.Modifier("estrecho|estrecha|estrechos|estrechas|angosto|angosta|angostos|angostas|apretado|apretada|cerrado|cerrada", "width", -1);
            p.Modifier("grueso|gruesa|gruesos|gruesas|poblado|poblada|pobladas|espeso|espesa|tupido|tupida", "thickness", 1);
            p.Modifier("fino|fina|finos|finas|ralo|rala|ralos|ralas|fina de", "thickness", -1);
            p.Modifier("fuerte|fuertes|marcada|marcadas|pronunciado|pronunciada|duro|dura|firme|cuadrado|cuadrada|angular|afilado|afilada", "strength", 1);
            p.Modifier("suave|suaves|redondeado|redondeada|blando|blanda|redondo|redonda", "strength", -1);

            // ---------- parts of the body (nouns) ----------
            p.Noun("cabeza|cabezas|craneo|testa", "headSize", "headSize", "face", "size", "headSize", "width", "headSize", "length", "headSize");
            p.Noun("ojo|ojos|mirada", "eyeSize", "eyeSize", "face", "size", "eyeSize", "width", "eyeSize");
            p.Noun("nariz|narices|napia|trompa", "noseSize", "noseSize", "face", "size", "noseSize", "width", "noseSize", "length", "noseSize");
            p.Noun("boca|labios|labio|sonrisa", "mouthWidth", "mouthWidth", "face", "size", "mouthWidth", "width", "mouthWidth");
            p.Noun("oreja|orejas|oido|oidos", "earSize", "earSize", "face", "size", "earSize", "length", "earSize");
            p.Noun("ceja|cejas", "eyebrowThickness", "eyebrowThickness", "face", "thickness", "eyebrowThickness", "size", "eyebrowThickness");
            p.Noun("mandibula|mandibulas|quijada|quijadas|mentón|menton", "jaw", "jaw", "face", "strength", "jaw", "size", "jaw", "width", "jaw");
            p.Noun("barbilla|menton|barbillas", "chin", "chin", "face", "strength", "chin", "size", "chin");
            p.Noun("pomulo|pomulos", "cheekbones", "cheekbones", "face", "strength", "cheekbones", "size", "cheekbones");
            p.Noun("frente|frentes", "forehead", "forehead", "face", "size", "forehead", "height", "forehead", "width", "forehead");
            p.Noun("cara|rostro|facciones|facial|rasgos", "face", "expressiveness", "face", "strength", "expressiveness");
            p.Noun("mano|manos|guantes", "handSize", "handSize", "body", "size", "handSize", "length", "handSize");
            p.Noun("pie|pies|botas|botines|zapatillas|pie grande", "footSize", "footSize", "body", "size", "footSize", "length", "footSize");
            p.Noun("pierna|piernas|zancos|zancada", "legLength", "legLength", "body", "length", "legLength", "size", "legLength");
            p.Noun("brazo|brazos", "armLength", "armLength", "body", "length", "armLength", "size", "armLength");
            p.Noun("hombro|hombros|espalda|espaldas", "shoulders", "shoulders", "body", "width", "shoulders", "size", "shoulders");
            p.Noun("cuerpo|complexion|fisico|figura|tronco|torso", "build", "mass", "body", "size", "mass", "width", "mass", "thickness", "mass");
            p.Noun("pelo|cabello|melena|pelos|cabellera|peinado|peinados|corte|corte de pelo|flequillo", "hairStyle", "hairLength", "hair", "length", "hairLength", "size", "hairVolume", "thickness", "hairVolume", "width", "hairVolume");
            p.Noun("barba|barbas|bigote|patillas|perilla|vello facial|barba de dias|barbita", "beard", "beard", "face");
            p.Noun("estilo|estetica|apariencia|look|estilo visual|vibra", "style", "cartoon", "style");
            p.Noun("expresion|gesto|cara de", "expression", "expressiveness", "face");
            p.Noun("camiseta|camisetas|equipo|uniforme|kit|jersey|casaca|equipacion|indumentaria", "kitColor", "kitColor", "kit");

            // ---------- hair, face and kit choices ----------
            p.Value("calvo|calva|calvos|pelon|pelona|sin cabello|cabeza afeitada|rapado total", "hairStyle", "", SemanticIntent.Remove);
            p.Value("rizado|rizada|rizados|rizos|chino|chinos|crespo|crespa|ensortijado", "hairStyle", "short_curly_07");
            p.Value("afro|afros", "hairStyle", "afro_08");
            p.Value("rapado|rapada|al ras|a cero|cero|pelado|pelada|buzz", "hairStyle", "buzz_02");
            p.Value("degradado|desvanecido|fade|degrade|con degradado", "hairStyle", "fade_06");
            p.Value("ondulado|ondulada|ondas|con ondas|wavy", "hairStyle", "medium_wavy_03");
            p.Value("coleta|cola de caballo|recogido|recogida|moño|mono|trenzado|trenza|trenzas|atado|atada", "hairStyle", "long_tied_05");
            p.Value("corte militar|casquete|corto texturizado|texturizado|texturizada", "hairStyle", "short_textured_04");
            p.Value("liso|lisa|lacio|lacia|planchado", "hairTexture", "straight");
            p.Value("crespo|apretado|coily|crespito", "hairTexture", "coily");
            p.Value("sin barba|afeitado|afeitada|limpio de cara|lampino", "beard", "none");
            p.Value("barba de tres dias|barba incipiente|barbudo|sin afeitar|sombra de barba|stubble", "beard", "stubble");
            p.Value("barba corta|barba recortada|barba cuidada", "beard", "short_beard");
            p.Value("sonriente|sonrisa grande|alegre|feliz|risueno|risueña", "expression", "smile");
            p.Value("determinado|determinada|decidido|decidida|concentrado|concentrada|con determinacion", "expression", "determined");
            p.Value("intenso|intensa|fiero|fiera|serio de cara|mirada intensa|amenazante", "expression", "intense");
            p.Value("picaro|picara|travieso|traviesa|socarron|guasón|guason", "expression", "cheeky");
            p.Value("neutro|neutra|inexpresivo|inexpresiva|tranquilo de cara", "expression", "neutral");

            // ---------- colours (hair/eyes/kit by the noun in the clause) ----------
            p.Color("negro|negra|negros|negras|azabache|oscuro de color", "#1A1A1A");
            p.Color("rubio|rubia|rubios|rubias|dorado|dorada|amarillo pelo|platinado|platinada", "#E3C77A");
            p.Color("castano|castana|castanos|castanas|marron|marrones|chocolate|cafe", "#5A3A22");
            p.Color("pelirrojo|pelirroja|rojizo|rojiza|colorado|colorada|zanahoria|cobrizo|cobriza", "#B5451B");
            p.Color("gris|grises|canoso|canosa|plateado|plateada|cano", "#9A9A9A");
            p.Color("blanco|blanca|blancos|blancas", "#F2F2F2");
            p.Color("rojo|roja|rojos|rojas|carmesi|escarlata", "#C62828");
            p.Color("azul|azules|celeste|marino", "#1E56C8");
            p.Color("verde|verdes|esmeralda", "#2E8B4A");
            p.Color("amarillo|amarilla|amarillos|amarillas", "#F2C500");
            p.Color("naranja|naranjas|anaranjado|anaranjada", "#EF7B10");
            p.Color("violeta|morado|morada|purpura|lila", "#7B3FA0");
            p.Color("rosa|rosado|rosada|fucsia", "#E85D9E");

            // ---------- gameplay: abilities and play ----------
            p.Word("rapido|rapida|rapidos|rapidas|veloz|velocidad|rapidez|velocista|ligero de piernas|corredor|corredora|vertiginoso|vertiginosa|fugaz|relampago|veloces|acelerado|acelerada", "speed", 1);
            p.Word("corr*|sprint*|vuele|vuela|volar|vuelan|vuelo|galopa*|trota*", "speed", 1, verb: true);
            p.Word("lento|lenta|lentos|lentas|pesadote|tardo|tarda|cansino|pausado|pausada|premioso|premiosa|parsimonioso", "speed", -1);
            p.Word("explosivo|explosiva|explosivos|explosivas|arranque|arrancada|aceleracion|de arranque|con arrancada|explosividad|resorte|impulso|reactivo", "acceleration", 1);
            p.Word("agil|agiles|agilidad|escurridizo|escurridiza|flexible|ligero de pies|gambeta agil|felino|felina|elastico|elastica", "agility", 1);
            p.Word("torpe|torpes|patoso|patosa|pesado de pies|rigido|rigida|poco agil|tosco|tosca", "agility", -1);
            p.Word("fuerte|fuertes|forzudo|forzuda|fortachon|fortachona|potente|potentes|fuerza|poderoso|poderosa|recio|recia|duro de roer|aguerrido|aguerrida|solido|solida|roca|vigoroso|vigorosa|bruto|bruta", "strength", 1, both: true);
            p.Word("debil|debiles|flojo|floja|fragil|fragiles|blandito|blandita|endeble|enclenque", "strength", -1, both: true);
            p.Word("resistencia|aguante|aguanta*|resistente|resistentes|incansable|infatigable|pulmon|pulmones|maraton|fondista|aguantador|aguantadora|con fondo|de fondo", "stamina", 1);
            p.Word("cansado|cansada|se canse|cansa|se cansa|agotado|agotada|poco aguante|sin fondo|fatiga|fatigoso|se fatiga|se fatigue", "stamina", -1);
            p.Word("tirador|tiradora|buen tiro|buena pegada|pegada|tiro potente|buen remate|chut|disparo certero|bota de oro|zurdazo|derechazo|puntería|punteria|punteria", "shootingSkill", 1);
            p.Word("tiros malos|mal tirador|mala punteria|mala pegada|falla los tiros|tiros flojos|tiro flojo", "shootingSkill", -1);
            p.Word("definicion|definidor|definidora|goleador|goleadora|killer|asesino del area|letal|letales|letalidad|certero|certera|rematador|rematadora|olfato goleador|cazagoles|clinico|clinica|frialdad", "finishing", 1);
            p.Word("pasador|pasadora|buen pase|buenos pases|visionario|visionaria|vision de juego|vision|pase|pases|distribuidor|distribuidora|precision de pase", "passingSkill", 1);
            p.Word("control|controla*|control de balon|buen control|primer toque|toque|asentar|orientado|orientada|domina*|dominio|dominador|dominadora|recibe bien", "control", 1);
            p.Word("tecnico|tecnica|tecnicos|tecnicas|refinado|refinada|elegante|elegantes|fino de pies|exquisito|exquisita|preciosista|virtuoso|virtuosa|habil|habiles|habilidoso|habilidosa|habilidades|zurdo fino", "technique", 1);
            p.Word("defensivo|defensiva|defensivos|defensivas|defensor|defensora|marcaje|marcador|marcadora|solido atras|rocoso|rocosa|sacrificado|sacrificada", "defense", 1);
            p.Word("driblador|driblador|driblo*|regateador|regateadora|gambeteador|gambeteadora|desborde|desequilibrante|escurridizo con balon|regate|regates|gambeta|gambetas|dribling|drible", "dribbling", 1, sense: "ability");
            p.Word("dribl*|regate*|gambete*|encara*|desborda*|supera*|eluda*|elude|eluden|se lleva a|se saca a|encaren|encare|1v1|uno contra uno|uno a uno", "dribbling", 1, sense: "tendency", verb: true);
            p.Word("tire|tira|tires|tirar|dispar*|chute|chuta|chutar|remate|remata|rematar|sacuda|patee|patea", "shooting", 1, verb: true);
            p.Word("pase|pasa|pases|pasar|distribuya|reparta|reparte|juegue en corto|toque corto|toca|toquen|asista|asiste|asistir", "passing", 1, verb: true);
            p.Word("arriesgado|arriesgada|arriesgados|arriesgadas|arriesgue|arriesga|temerario|temeraria|osado|osada|atrevido|atrevida|atrevimiento|riesgo|riesgoso|riesgosa|jugarsela|se la juegue|valiente|audaz|aventurero|aventurera", "risk", 1);
            p.Word("conservador|conservadora|cauto|cauta|prudente|seguro|segura|sin riesgo|sin riesgos|ordenado|ordenada|simple|sencillo|sencilla|conservadurismo|asegure|asegura|precavido|precavida", "risk", -1);
            p.Word("creativo|creativa|creativos|creativas|creatividad|imaginativo|imaginativa|inventivo|inventiva|ingenioso|ingeniosa|inspirado|inspirada|magico|magica|mago|artista|fantasista|sorprendente|impredecible|genio|ingenio|improvisador|improvisadora", "creativity", 1);
            p.Word("previsible|predecible|rutinario|rutinaria|mecanico|mecanica|esquematico|esquematica|plano|plana", "creativity", -1);
            p.Word("agresivo|agresiva|agresivos|agresivas|agresividad|aguerrido|combativo|combativa|guerrero|guerrera|garra|con garra|intenso de juego|fiero de juego|bravo|brava|mala leche|peleon|peleona|picante|bronco|bronca|aguerridas", "aggression", 1);
            p.Word("pacifico|pacifica|tranquilo|tranquila|calmado|calmada|sereno|serena|manso|mansa|suave de juego|educado|educada|limpio|limpia|caballeroso|caballerosa|sin agresividad", "aggression", -1);
            p.Word("presion*|presiona*|presionador|presionadora|pressing|acose*|acosa*|asfixia*|muerda|muerde|agobia*|ahogue|ahoga|robe|roba|recupere|recupera|recupere el balon", "pressing", 1, verb: true);
            p.Word("paciente|pacientes|paciencia|pausa|pausado de juego|espera|espere|espera el momento|esperar|frio|fria|calculador|calculadora|templado|templada", "patience", 1);
            p.Word("impaciente|impacientes|precipitado|precipitada|apurado|apurada|ansioso|ansiosa|atolondrado|atolondrada|visceral|a lo loco", "patience", -1);
            p.Word("directo|directa|directos|directas|vertical|verticales|verticalidad|a lo directo|al grano|sin rodeos|en vertical|profundo|profunda|profundidad|ir al frente|hacia delante|hacia adelante", "directness", 1);
            p.Word("amplitud|abierto de banda|pegado a la banda|pegado a la cal|por fuera|por la banda|por banda|en la banda|ancho de campo|abra el campo|abre el campo|abrir el campo|abran|abra|abre", "width", 1);
            p.Word("centra*|cruz*|centro|centros|cruce|cruces|pase al area|balones al area|colgar|cuelgue|cuelga|bombee|bombea|bombeo", "crossing", 1, verb: true);
            p.Word("apoyo|apoya*|ofrece*|ofrezca|se ofrezca|se ofrece|desmarque|desmarques|se desmarque|se desmarca|asoma*|asome|asoma|acompana*|acompanante|acompanar|llegada|llegadas|llega*|llegue", "supportRuns", 1, verb: true);

            // ---------- goalkeeper capabilities ----------
            p.Word("reflejos|buenos reflejos|reflejos de gato|reflejo|reactivo bajo palos|reaccion*", "gk.reflexes", 1, Gam);
            p.Word("manos seguras|seguro con las manos|blocaje|bloca bien|blocar|atrapa bien|buen blocaje|pocos rebotes|sin rebotes|buenas manos|manos de mantequilla", "gk.handling", 1, Gam);
            p.Word("colocacion|se coloca bien|buena colocacion|bien colocado|posicionamiento|lectura del juego|lee el juego|angulos|cierra los angulos|cierra angulos", "gk.positioning", 1, Gam);
            p.Word("estirada|estiradas|se estira|se tira|se lanza|se tira bien|vuelo bajo palos|paradas dificiles|elasticidad|salto lateral|plasticidad|ataja*|atajad*", "gk.diving", 1, Gam);
            p.Word("pegada de portero|saque largo|saques largos|golpeo largo|despeje largo|despeja largo|saca de puerta fuerte|buen saque de puerta|patada larga", "gk.kicking", 1, Gam);
            p.Word("juego con los pies|buen juego con los pies|sale jugando|salida de balon|sale con el balon|distribuye bien|saca jugando|saque corto|saca de mano|saque preciso|construye desde atras|inicia el juego", "gk.distribution", 1, Gam);
            p.Word("manda en el area|dominio del area|domina el area|comanda la defensa|organiza la defensa|lider|lider atras|sale a por los balones|sale bien|salidas|buenas salidas|voz de mando|comunica|comunicacion", "gk.command", 1, Gam);
            p.Word("se recupera rapido|recuperacion|se levanta rapido|se incorpora rapido|rearme|se rearma|rapida recuperacion|repone rapido", "gk.recovery", 1, Gam);

            // ---------- signature behaviours ----------
            p.Word("frena y arranca|frenada y arranque|frena y sigue|se frena y|stop and go|parada y arranque|se detiene y arranca|parar y arrancar|frenazo", "behavior.stopAndGo", 1, implied: SemanticIntent.Add);
            p.Word("finta|fintas|amague|amagues|amagar|amaga|amaga*|enganos|enganar|engana|enganche|enganches|bicicleta|bicicletas|elastico|tijera|cambio de ritmo falso|paso doble", "behavior.bodyFeint", 1, implied: SemanticIntent.Add);
            p.Word("salida explosiva|arrancada explosiva|sale disparado|sale como un rayo|acelera tras|despues del regate acelera", "behavior.explosiveExit", 1, implied: SemanticIntent.Add);
            p.Word("desmarque retrasado|llegada retrasada|carrera retrasada|carrera tardia|corre tarde|ataca tarde el espacio|carreras tardias", "behavior.delayedRun", 1, implied: SemanticIntent.Add);
            p.Word("a la espalda|a la espalda del defensor|por la espalda del defensor|carrera a la espalda|se cuela|se cuelan|colarse|se cuele|desmarque a la espalda|corre a la espalda|a espaldas", "behavior.blindSideRun", 1, implied: SemanticIntent.Add);
            p.Word("corte hacia adentro|recorte|recortes|recorta|corta hacia adentro|se mete hacia adentro|se cierra|cerrarse|cortar hacia dentro|cierre hacia dentro|pica hacia adentro|diagonal hacia adentro|a pierna cambiada|en diagonal", "behavior.insideCut", 1, implied: SemanticIntent.Add);
            p.Word("por fuera|se va por fuera|abierto por fuera|desborde por fuera|línea de fondo|linea de fondo|se va hacia la linea", "behavior.outsideCut", 1, implied: SemanticIntent.Add);
            p.Word("tiro de larga distancia|tiros de larga distancia|disparo lejano|disparos lejanos|tiro lejano|tiros lejanos|tiro de lejos|tiros de lejos|de media distancia|golpeo lejano|pegada de lejos|chutazo|chutazos|obus|cañonazo|canonazo|zapatazo|zapatazos", "behavior.longShot", 1, implied: SemanticIntent.Add);
            p.Word("remate de primera|remata de primera|remates de primera|a un toque|tiro de primera|tiros de primera|sin controlar|volea|voleas|de primeras|remata a la primera", "behavior.firstTimeFinish", 1, implied: SemanticIntent.Add);
            p.Word("pivote|de espaldas|aguanta el balon|aguantar el balon|proteger el balon|protege el balon|protege|escudo|con el cuerpo|pantalla|hacer pared de espaldas|poste|referencia", "behavior.holdUp", 1, implied: SemanticIntent.Add);
            p.Word("pared|paredes|pase y devolucion|uno dos|una dos|un toque|toques rapidos|combinacion|combinaciones|combina|combine|triangula|triangulacion|tabla|tablas", "behavior.oneTouch", 1, implied: SemanticIntent.Add);
            p.Word("pase inesperado|pases inesperados|pase de fantasia|pase magico|pases magicos|sorprende con pases|pases sorpresivos|pase no-look|pase sin mirar|taconazo|rabona|pase de tacon", "behavior.creativePass", 1, implied: SemanticIntent.Add);
            p.Word("pase filtrado|pases filtrados|filtra|filtre|pase en profundidad|pases en profundidad|pase al hueco|pase entre lineas|pase entre lineas|asistencia en profundidad|asistencias|asistidor|asistidora|pase al espacio", "behavior.throughBall", 1, implied: SemanticIntent.Add);
            p.Word("presion alta|presiona fuerte|presionador feroz|presion agresiva|cierra al rival|cierra al que tiene el balon|aprieta|aprieta arriba|aprieta al rival", "behavior.press", 1, implied: SemanticIntent.Add);
            p.Word("llegada al area|llega al area|llegada tardia al area|llega tarde al area|segunda linea|llegador|llegadora|aparece en el area|aparece por sorpresa en el area|se cuela al area|llegada desde segunda linea", "behavior.lateBoxArrival", 1, implied: SemanticIntent.Add);

            // ---------- roles ----------
            p.Word("extremo|extremos|ala|alero|aleros|banda|bandas|wing|wingers|extremo izquierdo|extremo derecho|punta|puntero|puntera", "role.winger", 1, implied: SemanticIntent.Add);
            p.Word("delantero|delantera|delanteros|goleador de area|nueve|nueve de area|centrodelantero|9|killer del area|rematador de area|atacante|atacantes|punta de lanza|ariete|referencia ofensiva|cazagoles", "role.striker", 1, implied: SemanticIntent.Add);
            p.Word("armador|armadora|creador de juego|mediapunta|enganche|organizador|organizadora|cerebro|director de juego|media punta|diez|10|playmaker|regista|conductor|conductora|pivote creativo", "role.playmaker", 1, implied: SemanticIntent.Add);
            p.Word("mediocampista|mediocampistas|centrocampista|centrocampistas|volante|volantes|medio|medios|interior|interiores|mediocentro|medio centro|contencion|box to box|motor|todocampista|todoterreno", "role.midfielder", 1, implied: SemanticIntent.Add);
            p.Word("defensa|defensas|defensor central|zaguero|zagueros|lateral|laterales|central|centrales|libero|stopper|marcador central|muro|pared defensiva|cierre|lateral derecho|lateral izquierdo|carrilero|carrilera", "role.defender", 1, implied: SemanticIntent.Add);
            p.Word("portero|porteros|arquero|arqueros|guardameta|guardametas|golero|goleros|cancerbero|cancerberos|meta|guardavallas|portera|arquera|guardian|el que ataja|el que para", "role.goalkeeper", 1, implied: SemanticIntent.Add);
            p.Word("pivote ofensivo|jugador referencia|hombre referencia|hombre objetivo|target man|torre|torre de area|hombre torre|punta de referencia|9 de referencia|centrodelantero de referencia|boya", "role.targetMan", 1, implied: SemanticIntent.Add);
            return p;
        }
    }
}
