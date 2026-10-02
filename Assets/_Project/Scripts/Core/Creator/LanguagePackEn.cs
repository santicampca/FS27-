using System;

namespace FS27.Core
{
    /// <summary>English vocabulary. Data only. Same concepts as Spanish: the meanings are shared, only the words change.</summary>
    public static class LanguagePackEn
    {
        private const SemanticDomain Vis = SemanticDomain.Visual;
        private const SemanticDomain Gam = SemanticDomain.Gameplay;

        public static LanguagePack Create()
        {
            var p = new LanguagePack { Id = "en" };

            p.Negator("no|not|without|never|don't|dont|doesn't|doesnt|isn't|isnt|shouldn't|shouldnt|won't|nor|neither|none|cannot|can't|cant");
            p.NegationContinuer("nor|or");
            p.Contrast("but|though|although|however|yet|except|while");
            p.KeepWhileWords("without him losing|without her losing|without it losing|that doesn't lose|that doesnt lose|who doesn't lose|don't let him lose|dont let him lose|shouldn't lose|shouldnt lose|without losing|without sacrificing|without giving up|without reducing|without hurting");
            p.Conjunction("and|also|plus|as well as");
            p.Create("create|create a|create an|generate|generate a|make me a|make me an|i want a|i want an|i need a|i need an|give me a|give me an|design|design a|invent a|new player|a new");
            p.Preserve("keep|preserve|maintain|retain|don't change|dont change|do not change|leave alone|leave as is|leave the same|don't touch|dont touch|stay the same|unchanged");
            p.AllElse("everything else|the rest|all the rest|the others|all else|everything");
            p.Only("only|just|solely|nothing but");
            p.Undo("undo|revert|go back|take that back|cancel that|rollback");

            p.Reference("the previous one|the previous|previous one|like the previous|like the last|the last one|the one before", EntityReference.Previous);
            p.Reference("the current one|this player|this character", EntityReference.Current);

            p.Operator("more|even more|still more|increase*|raise*|boost*|improve*|enhance*|strengthen*|enlarge*|lengthen*|maximize*", SemanticIntent.Increase);
            p.Operator("less|reduce*|lower*|decrease*|drop|shrink*|shorten*|tone down|soften*|minimize*", SemanticIntent.Decrease);
            p.Operator("remove*|delete*|erase*|take out|get rid of|drop the|strip*|cut", SemanticIntent.Remove);
            p.Operator("add*|include*|give|give him|give her|give it|put|with|incorporate*", SemanticIntent.Add);
            p.Operator("change*|replace*|swap*|switch*|different|another|redo|vary|varies", SemanticIntent.Replace);
            p.Operator("reset*|restore*|back to normal|back to default|default values|neutral", SemanticIntent.Reset);
            p.Operator("make him|make her|make it|make them|let him be|let her be|i want him|i want her|i want it|i want him to be|i want her to be|i want it to be|have him|have her|turn him|turn her|turn it|should be|that he is|that she is|that is|to be", SemanticIntent.Modify, makeIt: true);

            p.Intensifier("barely|a hair|hardly|a smidge|imperceptibly|minimally", MagnitudeLevel.Minimal);
            p.Intensifier("slightly|a tiny bit|a touch|marginally|ever so slightly|a tad", MagnitudeLevel.Slight);
            p.Intensifier("a little|a bit|somewhat|kind of|sort of|moderately|a little bit|rather a bit", MagnitudeLevel.Little);
            p.Intensifier("so|that|as much", MagnitudeLevel.Moderate);
            p.Intensifier("quite|fairly|rather|pretty|considerably|notably|decently", MagnitudeLevel.Quite);
            p.Intensifier("very|really|much|a lot|lots|truly|highly|way|super", MagnitudeLevel.Much);
            p.Intensifier("too|too much|excessively|overly|overwhelmingly", MagnitudeLevel.Much, excess: true);
            p.Intensifier("hugely|massively|incredibly|tremendously|insanely|ultra|a great deal|enormously|immensely", MagnitudeLevel.VeryMuch);
            p.Intensifier("extremely|exceedingly|ridiculously|absurdly|outrageously|monstrously", MagnitudeLevel.Extreme);
            p.Intensifier("maximum|max|to the max|as much as possible|to the limit|fully|at the maximum|the most|100 percent|all the way", MagnitudeLevel.Maximum);
            p.Intensifier("little|few|barely any|hardly any|low on", MagnitudeLevel.Moderate, flip: true);
            p.Intensifier("very little|very few|extremely little", MagnitudeLevel.Much, flip: true);

            p.Cue("looks|look|looking|looks like|appearance|visually|visual|appear|appears|aesthetic|aesthetics|looking like|in appearance|physically|physique", Vis);
            p.Cue("plays|play|playing|gameplay|in the game|in games|on the pitch|on the field|in matches|in a match|performance|performs|perform|skill", Gam);
            p.Cue("anim*", SemanticDomain.Animation);
            p.Cue("in duels|in the duels|in challenges|in tackles|in physical battles|in physical duels|when challenging|in contact|in battles", Gam, SemanticPhase.Duels);
            p.Cue("with the ball|on the ball|when he has the ball|when she has the ball|while carrying|when carrying the ball|in possession|on the dribble|while dribbling", Gam, SemanticPhase.WithBall);
            p.Cue("without the ball|off the ball|when he doesn't have the ball|out of possession", Gam, SemanticPhase.WithoutBall);
            p.Cue("when defending|while defending|in defense|in defence|defensively|on defense", Gam, SemanticPhase.Defending);
            p.Cue("when attacking|while attacking|in attack|on attack|offensively|going forward", Gam, SemanticPhase.Attacking);
            p.Cue("under pressure|when pressed|when pressured|when closed down|when marked|being pressed", Gam, SemanticPhase.Pressure);
            p.Cue("when sprinting|while sprinting|at full speed|in a sprint|at top speed|in the sprint|when running flat out", Gam, SemanticPhase.Sprint);

            p.Filler("the|a|an|he|she|it|his|her|its|him|them|they|their|that|to|of|in|on|at|for|with a|be|is|are|was|were|am|i|me|my|we|us|our|you|your|want|wants|would|like|please|player|character|footballer|guy|girl|kid|dude|should|could|can|will|shall|so that|so|this|these|those|as|than|then|now|just|ok|okay|well|let|lets|let's|got|has|have|having|looks|thing|things|type|kind|style of|sort|overall|really|actually|basically|still|keep being|stays|stay|remains|remain|become|becomes|get|gets|getting|whose|who|which|what|how|if|when|where|there|here|one|ones|also|too");

            p.Word("tall|taller|tallest|height|lanky|towering|statuesque", "height", 1, Vis);
            p.Word("short|shorter|shortest|small in height|petite|tiny|midget|low in height|squat|stubby|dwarf", "height", -1, Vis);
            p.Word("fat|chubby|heavy|heavier|heavyset|stocky|plump|bulky|big bodied|large bodied|thick bodied|overweight|round", "mass", 1, Vis);
            p.Word("thin|slim|skinny|lean|slender|light|lighter|lightweight|scrawny|wiry|gaunt|slight", "mass", -1, Vis);
            p.Word("sturdy|burly|brawny|beefy|solid build|well built|well-built|hefty|barrel chested|broad", "build", 1, Vis);
            p.Word("muscular|muscle|muscles|muscled|ripped|buff|jacked|toned|defined|swole|muscly|chiseled|chiselled|shredded|built|hench", "muscularity", 1, Vis);
            p.Word("weedy|frail|puny|feeble|flabby", "muscularity", -1, Vis);
            p.Word("athletic|athlete|sporty|sporting|athletically built|fit|in shape", "athleticLook", 1, Vis);
            p.Word("cartoon|cartoony|cartoonish|animated|exaggerated|stylized|stylised|comic|comical|caricature|drawn|toon|comic book", "cartoon", 1, Vis);
            p.Word("realistic|realism|lifelike|natural|photorealistic|serious|proportionate|proportioned|true to life", "realism", 1, Vis);
            p.Word("old|older|elderly|aged|mature|veteran|senior|aging|ageing", "age", 1, Vis);
            p.Word("young|younger|youthful|juvenile|boyish|girlish|teen|teenage|teenager|childlike|kid-like", "age", -1, Vis);
            p.Word("dark skinned|dark-skinned|dark skin|tanned|brown skinned|brown-skinned|darker skin", "skinTone", 1, Vis);
            p.Word("pale|fair skinned|fair-skinned|light skinned|light-skinned|fair skin|light skin|pasty|ashen", "skinTone", -1, Vis);

            p.Modifier("big|bigger|large|larger|huge|giant|enormous|massive|oversized|prominent|bulging|voluminous", "size", 1, "overallSize");
            p.Modifier("small|smaller|little|tiny|miniature|compact|reduced|discreet|mini|minute", "size", -1, "overallSize");
            p.Modifier("long|longer|elongated|extended|lengthy", "length", 1);
            p.Modifier("short|shorter|cropped|clipped|trimmed|shaved", "length", -1);
            p.Modifier("wide|wider|broad|broader|spacious|open", "width", 1);
            p.Modifier("narrow|narrower|tight|thin sided|closed", "width", -1);
            p.Modifier("thick|thicker|bushy|dense|heavy set|full|fuller", "thickness", 1);
            p.Modifier("thin|thinner|sparse|wispy|faint|fine|slender", "thickness", -1);
            p.Modifier("strong|strong looking|pronounced|square|angular|sharp|defined|firm|hard|prominent", "strength", 1);
            p.Modifier("soft|rounded|gentle|weak|mild|flat", "strength", -1);

            p.Noun("head|skull|noggin", "headSize", "headSize", "face", "size", "headSize", "width", "headSize", "length", "headSize");
            p.Noun("eye|eyes|gaze", "eyeSize", "eyeSize", "face", "size", "eyeSize", "width", "eyeSize");
            p.Noun("nose|nostrils", "noseSize", "noseSize", "face", "size", "noseSize", "width", "noseSize", "length", "noseSize");
            p.Noun("mouth|lips|lip|smile", "mouthWidth", "mouthWidth", "face", "size", "mouthWidth", "width", "mouthWidth");
            p.Noun("ear|ears", "earSize", "earSize", "face", "size", "earSize", "length", "earSize");
            p.Noun("eyebrow|eyebrows|brow|brows", "eyebrowThickness", "eyebrowThickness", "face", "thickness", "eyebrowThickness", "size", "eyebrowThickness");
            p.Noun("jaw|jawline|jaws", "jaw", "jaw", "face", "strength", "jaw", "size", "jaw", "width", "jaw");
            p.Noun("chin", "chin", "chin", "face", "strength", "chin", "size", "chin");
            p.Noun("cheekbone|cheekbones|cheeks", "cheekbones", "cheekbones", "face", "strength", "cheekbones", "size", "cheekbones");
            p.Noun("forehead|brow ridge", "forehead", "forehead", "face", "size", "forehead", "height", "forehead", "width", "forehead");
            p.Noun("face|facial features|features", "face", "expressiveness", "face", "strength", "expressiveness");
            p.Noun("hand|hands|gloves", "handSize", "handSize", "body", "size", "handSize", "length", "handSize");
            p.Noun("foot|feet|boots|boot|shoes", "footSize", "footSize", "body", "size", "footSize", "length", "footSize");
            p.Noun("leg|legs|stride", "legLength", "legLength", "body", "length", "legLength", "size", "legLength");
            p.Noun("arm|arms", "armLength", "armLength", "body", "length", "armLength", "size", "armLength");
            p.Noun("shoulder|shoulders", "shoulders", "shoulders", "body", "width", "shoulders", "size", "shoulders");
            p.Noun("body|frame|physique|build|torso|trunk", "build", "mass", "body", "size", "mass", "width", "mass", "thickness", "mass");
            p.Noun("hair|hairstyle|haircut|hairdo|mane|locks|fringe|bangs", "hairStyle", "hairLength", "hair", "length", "hairLength", "size", "hairVolume", "thickness", "hairVolume", "width", "hairVolume");
            p.Noun("beard|moustache|mustache|goatee|sideburns|facial hair|stubble|whiskers", "beard", "beard", "face");
            p.Noun("style|look|aesthetic|vibe", "style", "cartoon", "style");
            p.Noun("expression|demeanor|demeanour|face expression", "expression", "expressiveness", "face");
            p.Noun("shirt|shirts|kit|jersey|uniform|strip|outfit|team colors|team colours|clothing", "kitColor", "kitColor", "kit");

            p.Value("curly|curls|frizzy|kinky", "hairStyle", "short_curly_07");
            p.Value("afro|afros", "hairStyle", "afro_08");
            p.Value("buzz|buzzcut|buzz cut", "hairStyle", "buzz_02");
            p.Value("bald|baldy|hairless|shaved head|clean shaven head|bald headed|bald-headed", "hairStyle", "", SemanticIntent.Remove);
            p.Value("fade|faded|skin fade", "hairStyle", "fade_06");
            p.Value("wavy|waves", "hairStyle", "medium_wavy_03");
            p.Value("ponytail|tied|tied up|bun|braided|braids|braid|pigtails|man bun", "hairStyle", "long_tied_05");
            p.Value("textured|spiky|messy|crop|crew cut", "hairStyle", "short_textured_04");
            p.Value("straight", "hairTexture", "straight");
            p.Value("coily|tight curls", "hairTexture", "coily");
            p.Value("clean shaven|clean-shaven|shaved face|beardless", "beard", "none");
            p.Value("stubbly|five o'clock shadow|unshaven|scruffy|bearded", "beard", "stubble");
            p.Value("short beard|trimmed beard|neat beard", "beard", "short_beard");
            p.Value("smiling|smiley|cheerful|happy|grinning", "expression", "smile");
            p.Value("determined|focused|resolute|driven", "expression", "determined");
            p.Value("intense|fierce|menacing|glaring|scowling", "expression", "intense");
            p.Value("cheeky|mischievous|smirking|playful", "expression", "cheeky");
            p.Value("neutral face|blank|expressionless|poker faced", "expression", "neutral");

            p.Color("black|jet black|raven", "#1A1A1A");
            p.Color("blond|blonde|golden|platinum", "#E3C77A");
            p.Color("brown|chestnut|chocolate|brunette|auburn", "#5A3A22");
            p.Color("red|ginger|copper|redhead|crimson|scarlet", "#C62828");
            p.Color("grey|gray|silver|greying|graying|salt and pepper", "#9A9A9A");
            p.Color("white", "#F2F2F2");
            p.Color("blue|navy|sky blue", "#1E56C8");
            p.Color("green|emerald", "#2E8B4A");
            p.Color("yellow|lemon", "#F2C500");
            p.Color("orange", "#EF7B10");
            p.Color("purple|violet|lilac", "#7B3FA0");
            p.Color("pink|magenta|fuchsia", "#E85D9E");

            p.Word("fast|faster|fastest|quick|quicker|speedy|rapid|pacey|pacy|swift|speed|pace|quickness|fleet|sprinter|speedster|rapidly|lightning", "speed", 1);
            p.Word("run*|sprint*|race*|dash*|bolt*|fly|flies|flying|zoom*|gallop*|speed up|hurry|hurries", "speed", 1, verb: true);
            p.Word("slow|slower|slowest|sluggish|plodding|lumbering|leaden|unhurried|laboured|laboured", "speed", -1);
            p.Word("explosive|burst|bursts|acceleration|accelerates|quick off the mark|snappy|first step|get up and go|reactive|nimble off the mark", "acceleration", 1);
            p.Word("agile|agility|nimble|elusive|slippery|flexible|lithe|supple|twisty|shifty|dexterous", "agility", 1);
            p.Word("clumsy|awkward|ungainly|stiff|rigid|lumbering|cumbersome|heavy footed|heavy-footed|uncoordinated", "agility", -1);
            p.Word("strong|stronger|powerful|forceful|strength|sturdy|robust|tough|hardy|brawny|physical|beastly|mighty|rugged|solid", "strength", 1, both: true);
            p.Word("weak|weaker|feeble|frail|flimsy|puny|fragile|delicate", "strength", -1, both: true);
            p.Word("stamina|endurance|tireless|untiring|enduring|engine|marathon|lungs|fit for ninety|runs all day|never tires|lasts", "stamina", 1);
            p.Word("tired|tires|tires quickly|gasses|gassed|exhausted|out of breath|low stamina|fades|fatigue|fatigues|burns out", "stamina", -1);
            p.Word("good shot|powerful shot|great shot|strong shot|shot power|thunderous|cannon|hammer|rocket|strike|striker's shot|sharpshooter|marksman|shooter", "shootingSkill", 1);
            p.Word("bad shot|poor shot|weak shot|wild shots|wayward|shoots poorly", "shootingSkill", -1);
            p.Word("finishing|finisher|clinical|deadly|lethal|predator|poacher|killer|ruthless|cold|composed in front of goal|prolific|cool finisher", "finishing", 1);
            p.Word("good passer|great passer|passer|vision|visionary|playmaking vision|accurate passing|passing ability|pinpoint|laser passes|distributor|range of passing", "passingSkill", 1);
            p.Word("control|controls|first touch|touch|good touch|great touch|close control|ball control|trap|traps|settles|cushions|receives well|receiving", "control", 1);
            p.Word("technical|technique|skilful|skillful|refined|elegant|silky|graceful|exquisite|gifted|finesse|crafty|clever on the ball|slick|polished|virtuoso|artistry|flair", "technique", 1);
            p.Word("defensive|defender|defending ability|stopper|tackler|tackling|solid at the back|rock solid|sturdy defender|marker|marking|shields the defence|shield|sweeper|clean tackles|lockdown", "defense", 1);
            p.Word("dribbler|dribbling skill|good dribbler|great dribbler|dribbling ability|trickster|trickery|tricky|skillful dribbler|skilful dribbler|ball carrier|ball-carrier|jinking|jinks", "dribbling", 1, sense: "ability");
            p.Word("dribbl*|take on|takes on|taking on|take defenders on|beat defenders|beats defenders|beat his man|beat her man|go past|goes past|drive past|nutmeg*|skin defenders|one on one|one-on-one|1v1|isolate|isolates", "dribbling", 1, sense: "tendency", verb: true);
            p.Word("shoot|shoots|shooting|shot|shots|fire away|fires|firing|take shots|takes shots|have a go|has a go|pull the trigger|pulls the trigger|let fly|lets fly|blast*|smash*|hit the target|strike the ball", "shooting", 1, verb: true);
            p.Word("pass|passes|passing|distribute|distributes|play short|plays short|feed|feeds|lay off|lays off|assist|assists|assisting|link up|links up|combine|combines", "passing", 1, verb: true);
            p.Word("risky|risk taker|risk-taker|reckless|daring|bold|adventurous|gambler|gambles|gambling|takes risks|take risks|dangerous passes|bold passes|audacious|brave|fearless|rash|gung-ho|swashbuckling", "risk", 1);
            p.Word("safe|cautious|careful|conservative|prudent|low risk|low-risk|plays it safe|risk averse|risk-averse|sensible|reliable|steady|disciplined|tidy|no frills|no-frills|methodical|unadventurous", "risk", -1);
            p.Word("creative|creativity|imaginative|inventive|ingenious|inspired|magical|magician|wizard|artist|playmaker's mind|unpredictable|surprising|a genius|genius|improvises|improviser|improvisation|inspiration|ingenuity|wizardry|visionary|resourceful|original|innovative", "creativity", 1);
            p.Word("predictable|routine|mechanical|robotic|formulaic|one dimensional|one-dimensional|plain|basic|obvious|stereotyped|uninspired|bland|dull", "creativity", -1);
            p.Word("aggressive|aggression|combative|fiery|feisty|fierce|tenacious|committed|hard nosed|hard-nosed|bullish|snarling|spiteful|nasty|hostile|belligerent|ferocious|pugnacious|bruising|dirty", "aggression", 1);
            p.Word("gentle|calm|peaceful|mild|mild mannered|mild-mannered|placid|easygoing|clean|fair|gentlemanly|non aggressive|non-aggressive|tame|docile|passive|laid back|laid-back", "aggression", -1);
            p.Word("press|presses|pressing|pressure|pressures|harass*|hound*|chase*|close down|closes down|closing down|swarm*|win it back|wins it back|regain*|hustle*|harry|harries|harrying|gegenpress*", "pressing", 1, verb: true);
            p.Word("patient|patience|waits|wait for the right moment|measured|bides his time|bides her time|unhurried play|slows the game|dictates tempo|calculating|poised|composed|cool headed|cool-headed", "patience", 1);
            p.Word("impatient|rushed|hasty|hurried|frantic|panicky|panics|over eager|over-eager|restless|frenetic|impulsive|rash decisions|jumpy", "patience", -1);
            p.Word("direct|vertical|straightforward|no nonsense|no-nonsense|forward thinking|forward-thinking|goes forward|forward passes|penetrative|incisive|positive|straight to goal|plays forward|goal oriented|goal-oriented|dynamic", "directness", 1);
            p.Word("stay wide|stays wide|hug the touchline|hugs the touchline|hugging the line|wide play|wide positions|stretches the pitch|stretch the pitch|stretches play|use the width|uses the width|on the flank|down the flank|flanks|outside lane|out wide|keeps wide|keep wide|goes wide|go wide", "width", 1);
            p.Word("cross|crosses|crossing|whip in|whips in|deliver crosses|delivers crosses|deliveries|delivery from wide|put the ball in the box|puts the ball in the box|float|floats|lofted balls|aerial balls|swing|swings|cut back|cut-back|cutbacks", "crossing", 1, verb: true);
            p.Word("support|supports|supporting|offer|offers|offering|show for the ball|shows for the ball|available|makes himself available|makes herself available|overlap*|underlap*|make runs|makes runs|arrives|arriving|joins in|joins the attack|get forward|gets forward|links|link", "supportRuns", 1, verb: true);

            p.Word("reflexes|great reflexes|quick reflexes|cat like reflexes|cat-like reflexes|reflex saves|reactive|lightning reflexes|sharp reflexes", "gk.reflexes", 1, Gam);
            p.Word("safe hands|secure hands|good hands|handling|strong hands|catches well|holds on to the ball|holds the ball|no rebounds|few rebounds|sticky hands", "gk.handling", 1, Gam);
            p.Word("positioning|good positioning|well positioned|reads the game|reads the play|narrows the angle|narrows the angles|cuts the angles|angles|positional sense", "gk.positioning", 1, Gam);
            p.Word("diving|dives|dives well|acrobatic|athletic saves|stretches|full stretch|spectacular saves|diving saves|lateral saves|flexible keeper", "gk.diving", 1, Gam);
            p.Word("long kicks|long kicking|powerful kicks|long goal kicks|punts|big kick|big kicks|long distribution|booming kicks", "gk.kicking", 1, Gam);
            p.Word("plays with his feet|plays with her feet|good with his feet|good with her feet|footwork|good distribution|builds from the back|plays out from the back|short distribution|accurate throws|starts attacks|starts the attack", "gk.distribution", 1, Gam);
            p.Word("commands the box|commands the area|commands his area|commands her area|organises the defence|organizes the defense|organises the defense|vocal|vocal keeper|comes out to claim|claims crosses|dominant in the air|communicates|communication|presence in the box|aerial command|commanding", "gk.command", 1, Gam);
            p.Word("recovers quickly|quick recovery|gets up quickly|bounces back|back on his feet|back on her feet|fast recovery|quick to recover", "gk.recovery", 1, Gam);

            p.Word("stop and go|stop-and-go|stops and starts|stops and restarts|stops then goes|stutter step|stutter-step|hesitation move|hesitation|pause and burst|stops the ball and goes", "behavior.stopAndGo", 1, implied: SemanticIntent.Add);
            p.Word("feint|feints|body feint|fake|fakes|shimmy|shimmies|step over|step overs|stepovers|step-over|dummy|dummies|drop of the shoulder|sells a dummy|sells the dummy|jink|bicycle|elastico|scissors|double touch", "behavior.bodyFeint", 1, implied: SemanticIntent.Add);
            p.Word("explosive exit|burst away|bursts away|bursts clear|accelerates away|speeds away|sprint away|sprints away|leaves them for dead|burst of pace after|jet away|bolts away", "behavior.explosiveExit", 1, implied: SemanticIntent.Add);
            p.Word("delayed run|delayed runs|late run|late runs|well timed run|well-timed runs|timed runs|waits then runs|late burst|runs late", "behavior.delayedRun", 1, implied: SemanticIntent.Add);
            p.Word("run in behind|runs in behind|run behind|runs behind|blind side|blindside|blind-side|blind side run|blind side runs|behind the defender|behind the defence|behind the defense|beyond the defender|runs off the shoulder|shoulder of the defender|sneaks in behind|slips in behind|ghosts in", "behavior.blindSideRun", 1, implied: SemanticIntent.Add);
            p.Word("cut inside|cuts inside|cutting inside|cut in|cuts in|cut infield|cuts infield|drift inside|drifts inside|come inside|comes inside|inverted|inverted winger|drop the shoulder and cut in|on his stronger foot|on her stronger foot|curl in|curls in", "behavior.insideCut", 1, implied: SemanticIntent.Add);
            p.Word("go outside|goes outside|beat on the outside|beats on the outside|goes round the outside|take the outside|takes the outside|hit the byline|hits the byline|byline|to the byline|touchline runs|outside cut|round the outside|on the outside", "behavior.outsideCut", 1, implied: SemanticIntent.Add);
            p.Word("long shot|long shots|long range shot|long range shots|long-range shot|long-range shots|shoot from distance|shoots from distance|shoot from range|shoots from range|screamer|screamers|speculative shot|speculative shots|from outside the box|outside the box shot|thunderbolt|thunderbolts|rocket from distance|pile driver|piledriver|drive from distance|from range|from distance", "behavior.longShot", 1, implied: SemanticIntent.Add);
            p.Word("first time finish|first-time finish|first time shot|first-time shot|first time shots|first-time shots|first time|first-time|one touch finish|one-touch finish|volley|volleys|volleyed|shoot first time|shoots first time|snap shot|snapshot|snap shots|instinctive finish|without controlling|without a touch|straight off the pass|on the half volley|half volley|half-volley", "behavior.firstTimeFinish", 1, implied: SemanticIntent.Add);
            p.Word("hold up|hold-up|holds up|holding up|hold up play|holds up play|shield the ball|shields the ball|shielding|back to goal|back to the goal|with his back to goal|with her back to goal|target man play|bring others into play|brings others into play|link play|links play|lay it off|lays it off|post up|posts up", "behavior.holdUp", 1, implied: SemanticIntent.Add);
            p.Word("one touch|one-touch|one two|one-two|give and go|give-and-go|wall pass|wall passes|quick combinations|quick combination|quick passing|quick one touch|triangles|triangle play|play quick|plays quick|first time pass|first-time pass|first time passes|first-time passes|tiki taka|tiki-taka|pass and move|passes and moves|combination play|combination plays|interchange|interchanges", "behavior.oneTouch", 1, implied: SemanticIntent.Add);
            p.Word("creative pass|creative passes|unexpected pass|unexpected passes|surprise pass|surprise passes|no look pass|no-look pass|no look passes|no-look passes|backheel|backheels|back heel|back heels|rabona|rabonas|outside of the boot pass|outside of the foot pass|trivela|trivelas|flick pass|flick passes|flicks|audacious pass|audacious passes|cheeky pass|cheeky passes|disguised pass|disguised passes|disguises passes", "behavior.creativePass", 1, implied: SemanticIntent.Add);
            p.Word("through ball|through balls|through-ball|through-balls|threaded pass|threaded passes|thread the needle|threads the needle|splitting pass|splitting passes|splits the defence|splits the defense|killer pass|killer passes|defence splitting|defense splitting|defence-splitting|defense-splitting|slide rule pass|slide-rule pass|slides the ball through|slips the ball through|key pass|key passes", "behavior.throughBall", 1, implied: SemanticIntent.Add);
            p.Word("high press|high pressing|hard press|hard pressing|aggressive press|aggressive pressing|presses high|presses hard|presses aggressively|close the ball carrier down|closes the ball carrier down|closes down hard|hounds the ball carrier|hounding the ball carrier|harries the ball carrier|front foot defending|front-foot defending|presses relentlessly", "behavior.press", 1, implied: SemanticIntent.Add);
            p.Word("late box arrival|late arrival in the box|arrives late in the box|arrives late into the box|late runs into the box|late run into the box|second line runs|second line run|second-line runs|second-line run|late arriving|arrives from deep|arriving from deep|ghosts into the box|ghosts in at the back post|ghost run|ghost runs|ghosting|shows up in the box|appears in the box|pops up in the box|crashes the box|crashes the area|storms the box|storms the area", "behavior.lateBoxArrival", 1, implied: SemanticIntent.Add);

            p.Word("winger|wingers|wing|wide man|wide player|wide forward|wide midfielder|flank player|flanker|touchline player|inverted winger|wing back|wingback|wing-back|outside forward|outside right|outside left|right winger|left winger|wide attacker|wide playmaker", "role.winger", 1, implied: SemanticIntent.Add);
            p.Word("striker|strikers|forward|forwards|centre forward|center forward|centre-forward|center-forward|number nine|number 9|nine|9|poacher|goal hunter|goalscorer|goal scorer|finisher up front|attacker|attackers|spearhead|frontman|front man|front-man|lone striker|second striker|false nine|false 9", "role.striker", 1, implied: SemanticIntent.Add);
            p.Word("playmaker|playmakers|creator|creators|number ten|number 10|ten|10|trequartista|attacking midfielder|advanced playmaker|deep lying playmaker|deep-lying playmaker|regista|architect|orchestrator|conductor|maestro|schemer|fantasista|puppet master|puppeteer", "role.playmaker", 1, implied: SemanticIntent.Add);
            p.Word("midfielder|midfielders|midfield|central midfielder|centre midfielder|center midfielder|box to box|box-to-box|box to box midfielder|engine|workhorse|holding midfielder|defensive midfielder|anchor|anchorman|anchor man|destroyer|ball winner|ball-winner|shuttler|carrilero|mezzala|interior|all rounder|all-rounder|utility midfielder|dynamo|battery|motor|ball winning midfielder", "role.midfielder", 1, implied: SemanticIntent.Add);
            p.Word("defender|defenders|centre back|center back|centre-back|center-back|cb|full back|fullback|full-back|right back|left back|sweeper|libero|stopper|wall|rock|defensive wall|back four|back line|centre half|center half|centre-half|center-half|ball playing defender|ball-playing defender|no nonsense defender|no-nonsense defender|marker|stalwart|bulwark|lockdown defender", "role.defender", 1, implied: SemanticIntent.Add);
            p.Word("goalkeeper|goalkeepers|keeper|keepers|goalie|goalies|shot stopper|shot-stopper|netminder|net minder|custodian|last line of defence|last line of defense|gk|number one|number 1|sweeper keeper|sweeper-keeper|stopper keeper|guardian", "role.goalkeeper", 1, implied: SemanticIntent.Add);
            p.Word("target man|target-man|target forward|big man|big striker|aerial threat|aerial presence|tower|towering striker|focal point|focal-point|hold up striker|hold-up striker|lone target|physical forward|bully|bruiser|pivot|pivot striker|big target", "role.targetMan", 1, implied: SemanticIntent.Add);
            return p;
        }
    }
}
