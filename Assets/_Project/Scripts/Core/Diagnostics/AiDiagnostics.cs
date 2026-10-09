using System;

namespace FS27.Core
{
    /// <summary>
    /// The observer of the AI (Phase 12): it explains what the carrier chose and why, follows passes and shots to their end, and records every duel for the ball and every foul. It is a pure OBSERVER: it
    /// reads the world view, the decisions, the action results and the published events, and writes only to its own counters. It changes no decision, no action, no position, no ball, no stamina and uses no
    /// random number, so a match plays exactly the same with it attached or not (a test shows it). Counters are doubles in ONE flat array (so that several matches add up by adding arrays); the logs (passes into
    /// space, duels, a trace of the carrier) are fixed-size arrays that stop filling when full and count what they dropped. Nothing is allocated while playing.
    /// </summary>
    public sealed partial class AiDiagnostics
    {
        // ---- sizes
        public const int Types = 8;                                   // CandidateType: None..Hold
        public const int Parts = CandidateParts.Capacity;
        private static readonly int Rejections = (int)CandidateRejection.Count;
        public static readonly int ActionKinds = MaxOf(typeof(FootballActionKind)) + 1;
        private static readonly int ActionRejections = MaxOf(typeof(ActionRejection)) + 1;
        private static readonly int DecisionKinds = MaxOf(typeof(DecisionKind)) + 1;
        private static readonly int PassOutcomes = MaxOf(typeof(PassOutcome)) + 1;
        private static readonly int WindowOutcomes = MaxOf(typeof(WindowOutcome)) + 1;
        private static readonly int ShotOutcomes = MaxOf(typeof(ShotOutcome)) + 1;
        private const int Archetypes = 16;
        private const int Roles = 4;
        private static readonly int MistakeKinds = MaxOf(typeof(DecisionErrorKind)) + 1;
        public const int DuelKinds = 3; // 0 standing tackle, 1 sliding tackle, 2 physical challenge

        private static int MaxOf(Type enumType)
        {
            int max = 0;
            foreach (int v in Enum.GetValues(enumType)) if (v > max) max = v;
            return max;
        }

        // ---- the flat counter array: every block is allocated once, in this order
        private static int cursor;
        private static int Alloc(int n) { int o = cursor; cursor += n; return o; }
        private static readonly int oDecisions = Alloc(1);
        private static readonly int oKinds = Alloc(DecisionKinds);
        private static readonly int oCarrier = Alloc(1);
        private static readonly int oChosen = Alloc(Types);
        private static readonly int oChosenScore = Alloc(Types);
        private static readonly int oChosenPressure = Alloc(Types);
        private static readonly int oChosenStamina = Alloc(Types);
        private static readonly int oChosenGoal = Alloc(Types);
        private static readonly int oChosenMargin = Alloc(Types);
        private static readonly int oChosenMarginN = Alloc(Types);
        private static readonly int oRunnerUp = Alloc(Types);
        private static readonly int oAvailable = Alloc(Types);
        private static readonly int oSecond = Alloc(Types * Types);
        private static readonly int oBestN = Alloc(Types);
        private static readonly int oBestScore = Alloc(Types);
        private static readonly int oBestParts = Alloc(Types * Parts);
        private static readonly int oWinnerParts = Alloc(Types * Parts);
        private static readonly int oRejected = Alloc(Types * Rejections);
        private static readonly int oMarginBuckets = Alloc(5);
        private static readonly int oMargin = Alloc(2);
        private static readonly int oRunPresent = Alloc(5);
        private static readonly int oDribble = Alloc(3);
        private static readonly int oFreeHist = Alloc(12);
        private static readonly int oWindow = Alloc(Types * 6);
        private static readonly int oEpisodes = Alloc(Types);
        private static readonly int oPassAttempts = Alloc(1);
        private static readonly int oPassKind = Alloc(ActionKinds);
        private static readonly int oPassOutcome = Alloc(PassOutcomes);
        private static readonly int oPassKindOutcome = Alloc(ActionKinds * 10);
        private static readonly int oPassReceiver = Alloc(2);
        private static readonly int oPassTime = Alloc(PassOutcomes * 2);
        private static readonly int oReturnTime = Alloc(4);
        private static readonly int oChase = Alloc(PassOutcomes);
        private static readonly int oToKeeper = Alloc(PassOutcomes * 2);
        private static readonly int oPassFeat = Alloc(PassOutcomes * 7);
        private static readonly int oSpaceAttempts = Alloc(1);
        private static readonly int oSpaceOutcome = Alloc(PassOutcomes);
        private static readonly int oSpaceFeatures = Alloc(PassOutcomes * 10);
        private static readonly int oShotAttempts = Alloc(1);
        private static readonly int oShotOutcome = Alloc(ShotOutcomes);
        private static readonly int oShotDistance = Alloc(6 * ShotOutcomes);
        private static readonly int oShotScore = Alloc(5 * ShotOutcomes);
        private static readonly int oActRequested = Alloc(ActionKinds);
        private static readonly int oActAccepted = Alloc(ActionKinds);
        private static readonly int oActRejected = Alloc(ActionKinds);
        private static readonly int oActCompleted = Alloc(ActionKinds);
        private static readonly int oActFailed = Alloc(ActionKinds);
        private static readonly int oActStamina = Alloc(ActionKinds);
        private static readonly int oActReason = Alloc(ActionRejections);
        private static readonly int oDuelRequested = Alloc(DuelKinds);
        private static readonly int oContacts = Alloc(DuelKinds);
        private static readonly int oWon = Alloc(DuelKinds);
        private static readonly int oLooseWin = Alloc(DuelKinds);
        private static readonly int oFailed = Alloc(DuelKinds);
        private static readonly int oFouls = Alloc(DuelKinds);
        private static readonly int oNoContact = Alloc(DuelKinds);
        private static readonly int oAggrContacts = Alloc(DuelKinds * 2);
        private static readonly int oAggrFouls = Alloc(DuelKinds * 2);
        private static readonly int oBehindContacts = Alloc(DuelKinds * 2);
        private static readonly int oBehindFailed = Alloc(DuelKinds * 2);
        private static readonly int oBehindFouls = Alloc(DuelKinds * 2);
        private static readonly int oCarrierKindContacts = Alloc(DecisionKinds + 1);
        private static readonly int oCarrierKindFouls = Alloc(DecisionKinds + 1);
        private static readonly int oMovingContacts = Alloc(2);
        private static readonly int oMovingFouls = Alloc(2);
        private static readonly int oShieldContacts = Alloc(2);
        private static readonly int oShieldFouls = Alloc(2);
        private static readonly int oQuality = Alloc(4);
        private static readonly int oArchContacts = Alloc(Archetypes);
        private static readonly int oArchFouls = Alloc(Archetypes);
        private static readonly int oArchSuffered = Alloc(Archetypes);
        private static readonly int oRoleContacts = Alloc(Roles);
        private static readonly int oRoleFouls = Alloc(Roles);
        private static readonly int oStamina = Alloc(12 * 3);
        private static readonly int oEffort = Alloc(DecisionKinds * 3);
        private static readonly int oMistake = Alloc(MistakeKinds);
        private static readonly int oMistakeGap = Alloc(MistakeKinds);
        private static readonly int oDrift = Alloc(3);
        private static readonly int oProgress = Alloc(6);
        private static readonly int oHome = Alloc(4);
        private static readonly int oContext = Alloc(8);
        private static readonly int oLost = Alloc(1);
        private static readonly int oShotQ = Alloc(8);
        private static readonly int oMatch = Alloc(14);
        private static readonly int Size = cursor;

        // indexes into the small oMatch block
        private const int mSteps = 0, mSwitches = 1, mGainsHome = 2, mGainsAway = 3, mRecoveries = 4, mGoals = 5, mFoulEvents = 6, mInterceptEvents = 7, mOutEvents = 8, mInterceptions = 9, mBlocks = 10, mMatches = 11, mGoalsAfterShot = 12, mGoalsOther = 13;

        private readonly double[] a = new double[Size];
        public DiagnosticsConfig Config { get; }
        /// <summary>The numbers of the shot rating (statistics only).</summary>
        public ShotQualityTuning ShotQualityNumbers { get { return shotQuality; } }
        private readonly ShotQualityTuning shotQuality = new ShotQualityTuning();
        /// <summary>A name for the run (mode, seed, teams): only for reports.</summary>
        public string Label { get; set; }
        /// <summary>A second observer fed the same calls (Phase 14): one for each team of the same match. It must be attached to the match on its own.</summary>
        public AiDiagnostics Twin { get; set; }

        private readonly ShotRecord[] shotLog;
        public int ShotLogCount { get; private set; }
        public int ShotLogDropped { get; private set; }
        public ShotRecord ShotAt(int i) { return shotLog[i]; }
        private readonly SpaceAttempt[] space;
        private readonly ContactRecord[] contacts;
        private readonly CarrierTrace[] trace;
        public int SpaceCount { get; private set; }
        public int ContactCount { get; private set; }
        public int TraceCount { get; private set; }
        public int SpaceDropped { get; private set; }
        public int ContactsDropped { get; private set; }
        public int TraceDropped { get; private set; }

        public AiDiagnostics(DiagnosticsConfig config = null, int spaceCapacity = 128, int contactCapacity = 512, int traceCapacity = 256, int shotCapacity = 192)
        {
            shotLog = new ShotRecord[shotCapacity];
            Config = config ?? new DiagnosticsConfig();
            space = new SpaceAttempt[spaceCapacity];
            contacts = new ContactRecord[contactCapacity];
            trace = new CarrierTrace[Config.TraceEnabled ? traceCapacity : 0];
            for (int i = 0; i < players.Length; i++) players[i].ArchetypeIndex = -1;
        }

        public SpaceAttempt SpaceAt(int i) { return space[i]; }
        public ContactRecord ContactAt(int i) { return contacts[i]; }
        public CarrierTrace TraceAt(int i) { return trace[i]; }

        // ------------------------------------------------------------------ counters (read)
        public double Steps { get { return a[oMatch + mSteps]; } }
        public double Seconds { get { return a[oMatch + mSteps] * LocomotionDefaults.FixedDeltaTime; } }
        public double Matches { get { return a[oMatch + mMatches]; } }
        public double PossessionSwitches { get { return a[oMatch + mSwitches]; } }
        public double Recoveries { get { return a[oMatch + mRecoveries]; } }
        public double Goals { get { return a[oMatch + mGoals]; } }
        /// <summary>Goals scored while a shot of the scoring team was still being followed (3 s), and goals with none (carried in, a deflection, a rebound after the shot was settled).</summary>
        public double GoalsAfterShot { get { return a[oMatch + mGoalsAfterShot]; } }
        public double GoalsOther { get { return a[oMatch + mGoalsOther]; } }
        public double FoulEvents { get { return a[oMatch + mFoulEvents]; } }
        public double Decisions { get { return a[oDecisions]; } }
        public double KindCount(DecisionKind k) { return a[oKinds + (int)k]; }
        public double CarrierDecisions { get { return a[oCarrier]; } }
        public double Chosen(CandidateType t) { return a[oChosen + (int)t]; }
        public double ChosenAverageScore(CandidateType t) { return Div(a[oChosenScore + (int)t], a[oChosen + (int)t]); }
        public double ChosenAveragePressure(CandidateType t) { return Div(a[oChosenPressure + (int)t], a[oChosen + (int)t]); }
        public double ChosenAverageStamina(CandidateType t) { return Div(a[oChosenStamina + (int)t], a[oChosen + (int)t]); }
        public double ChosenAverageMargin(CandidateType t) { return Div(a[oChosenMargin + (int)t], a[oChosenMarginN + (int)t]); }
        public double RunnerUp(CandidateType t) { return a[oRunnerUp + (int)t]; }
        public double Available(CandidateType t) { return a[oAvailable + (int)t]; }
        /// <summary>Decisions in which <paramref name="winner"/> won and <paramref name="second"/> was the best of another kind.</summary>
        public double Second(CandidateType winner, CandidateType second) { return a[oSecond + (int)winner * Types + (int)second]; }
        public double Rejected(CandidateType t, CandidateRejection why) { return a[oRejected + (int)t * Rejections + (int)why]; }
        /// <summary>How many decisions had a candidate of this kind, and the mean score of the best one of that kind (winner or not).</summary>
        public double BestOfKindAverage(CandidateType t) { return Div(a[oBestScore + (int)t], a[oBestN + (int)t]); }
        public double BestOfKindPartAverage(CandidateType t, int part) { return Div(a[oBestParts + (int)t * Parts + part], a[oBestN + (int)t]); }
        public double WinnerPartAverage(CandidateType t, int part) { return Div(a[oWinnerParts + (int)t * Parts + part], a[oChosen + (int)t]); }
        /// <summary>Margin buckets: below 0.10, below 0.25, below 0.50, 0.50 and above, and decisions where nothing else of another kind existed.</summary>
        public double MarginBucket(int i) { return a[oMarginBuckets + i]; }
        public double MarginAverage { get { return Div(a[oMargin], a[oMargin + 1]); } }
        public double MarginMin { get; private set; } = double.PositiveInfinity;
        public double MarginMax { get; private set; } = double.NegativeInfinity;
        public double RunPresent(RunKind k) { return a[oRunPresent + (int)k]; }
        public double DribbleFreeDecisions { get { return a[oDribble]; } }
        public double DribbleAverageSpace { get { return Div(a[oDribble + 1], a[oChosen + (int)CandidateType.Dribble]); } }
        public double DribbleAverageProgress { get { return Div(a[oDribble + 2], a[oChosen + (int)CandidateType.Dribble]); } }
        /// <summary>Episodes of a kind (a dribble, a protect or a hold that begins) and what became of possession a short time after.</summary>
        public double Episodes(CandidateType t) { return a[oEpisodes + (int)t]; }
        public double WindowCount(CandidateType t, WindowOutcome o) { return a[oWindow + (int)t * 6 + (int)o]; }

        /// <summary>Passes of this outcome whose passer, before it was settled, decided to go to the ball (MoveToBall: he chased his own pass).</summary>
        public double PassesChasedByPasser(PassOutcome o) { return a[oChase + (int)o]; }

        /// <summary>Passes of this outcome that ended with the ball in the hands of an opponent GOALKEEPER (all passes, and the ones into space).</summary>
        public double PassesEndingWithKeeper(PassOutcome o, bool intoSpace) { return a[oToKeeper + (int)o * 2 + (intoSpace ? 1 : 0)]; }

        /// <summary>Passes that came back to the passer: within 0.1 s, 0.25 s, 0.5 s, later.</summary>
        public double ReturnedBucket(int i) { return a[oReturnTime + i]; }

        /// <summary>Free distance ahead of the dribbles scored (decisions with a free way): below 1.75, 3.5, 5, 7, 10 m and beyond; all of them, then the ones that won.</summary>
        public double FreeDistanceBucket(int bucket, bool winners) { return a[oFreeHist + (winners ? 6 : 0) + bucket]; }

        /// <summary>Field players' stamina sampled every ten seconds of a match: for each ten-second slot (twelve of them, the last one holds the rest) the mean stamina, the share below 20 and the number of samples.</summary>
        public double StaminaMean(int slot) { return Div(a[oStamina + slot * 3], a[oStamina + slot * 3 + 2]); }
        public double StaminaExhausted(int slot) { return Div(a[oStamina + slot * 3 + 1], a[oStamina + slot * 3 + 2]); }
        public double StaminaSamples(int slot) { return a[oStamina + slot * 3 + 2]; }

        /// <summary>How hard the AI asks its players to move: decisions of a kind by the push of the stick they asked for (below the walk threshold, below the sprint threshold, in the sprint range).</summary>
        public double EffortCount(DecisionKind k, int band) { return a[oEffort + (int)k * 3 + band]; }

        // ---- Phase 14: controlled mistakes, progress and positioning (counted for the filtered team when there is a filter)
        /// <summary>How many times each kind of controlled mistake happened, and the sum of how much better the option not taken was (score points; for a late reaction or a late contest, seconds waited).</summary>
        public double MistakeCount(DecisionErrorKind k) { return a[oMistake + (int)k]; }
        public double MistakeGapSum(DecisionErrorKind k) { return a[oMistakeGap + (int)k]; }
        public double MistakeTotal { get { double n = 0.0; for (int k = 1; k < MistakeKinds; k++) if (k != (int)DecisionErrorKind.LateContest && k != (int)DecisionErrorKind.LateReaction) n += a[oMistake + k]; return n; } }
        /// <summary>Decisions off the ideal place because of positioning, and the sum of the metres.</summary>
        public double DriftDecisions { get { return a[oDrift]; } }
        public double DriftMetresSum { get { return a[oDrift + 1]; } }
        public double DriftSamples { get { return a[oDrift + 2]; } }
        /// <summary>Entries of the ball (carried or passed) into the final third / into the dangerous zone near the goal, possessions that reached the dangerous zone, clear chances (a shot candidate at or above the clear score) and clear chances taken.</summary>
        public double FinalThirdEntries { get { return a[oProgress]; } }
        public double DangerEntries { get { return a[oProgress + 1]; } }
        public double DangerousPossessions { get { return a[oProgress + 2]; } }
        public double ClearChances { get { return a[oProgress + 3]; } }
        public double ClearChancesTaken { get { return a[oProgress + 4]; } }
        public double Possessions { get { return a[oProgress + 5]; } }
        /// <summary>Mean distance (m) of the field players from their place when their team has the ball, and when it has not.</summary>
        public double HomeDistanceAttacking { get { return Div(a[oHome], a[oHome + 1]); } }
        public double HomeDistanceDefending { get { return Div(a[oHome + 2], a[oHome + 3]); } }
        public double HomeSamplesAttacking { get { return a[oHome + 1]; } }
        public double HomeSamplesDefending { get { return a[oHome + 3]; } }

        // ---- Phase 15: the same carrier decisions and mistakes split by the situation they happened in, and cards and possessions lost
        /// <summary>Carrier decisions made under pressure, with no free way to dribble, and tired.</summary>
        public double DecisionsPressed { get { return a[oContext]; } }
        public double DecisionsLowSpace { get { return a[oContext + 1]; } }
        public double DecisionsTired { get { return a[oContext + 2]; } }
        /// <summary>Controlled mistakes (of the carrier) made in those same situations.</summary>
        public double MistakesPressed { get { return a[oContext + 3]; } }
        public double MistakesLowSpace { get { return a[oContext + 4]; } }
        public double MistakesTired { get { return a[oContext + 5]; } }
        public double YellowCards { get { return a[oContext + 6]; } }
        public double RedCards { get { return a[oContext + 7]; } }
        /// <summary>Times the observed team lost the ball to the other team.</summary>
        public double PossessionsLost { get { return a[oLost]; } }

        // ---- Phase 16: the shots, their quality and the saves
        /// <summary>Shots struck by the observed team, the sum of their quality values, the sum of their expected goals, the saves its goalkeeper made against the other team, and the shots the other team struck that were on target.</summary>
        public double ShotsRated { get { return a[oShotQ]; } }
        public double ShotQualitySum { get { return a[oShotQ + 1]; } }
        public double ExpectedGoalsSum { get { return a[oShotQ + 2]; } }
        public double SavesMade { get { return a[oShotQ + 3]; } }
        public double ShotsSavedOwn { get { return a[oShotQ + 4]; } }
        public double ExpectedGoalsOnTargetSum { get { return a[oShotQ + 5]; } }
        public double ShotsOnTargetRated { get { return a[oShotQ + 6]; } }
        public double ShotQualityAverage { get { return Div(a[oShotQ + 1], a[oShotQ]); } }

        private bool Skip(TeamSide team) { return Config.OnlyOneTeam && team != Config.Team; }

        private static double Div(double x, double n) { return n > 0.0 ? x / n : 0.0; }

        // ------------------------------------------------------------------ per player
        public struct PlayerStats
        {
            public string PlayerId;
            public TeamSide Team;
            public PlayerRole Role;
            /// <summary>His most defining role, or -1 when the match had no playing profiles.</summary>
            public int ArchetypeIndex;
            public int Decisions, CarrierDecisions, Passes, PassesCompleted, SpacePasses, Dribbles, Protects, Holds, Shots, Goals;
            public int Possessions, FoulsCommitted, FoulsSuffered, ContactsMade, ContactsSuffered, Tackles, DuelsWon, Interceptions;
        }

        private readonly PlayerStats[] players = new PlayerStats[WorldState.MaxPlayers];
        public PlayerStats PlayerAt(int worldIndex) { return players[worldIndex]; }
        public int PlayerSlots { get { return players.Length; } }

        private ref PlayerStats Player(WorldView view, int idx)
        {
            ref PlayerStats p = ref players[idx];
            if (p.PlayerId == null)
            {
                WorldPlayer w = view[idx];
                p.PlayerId = w.PlayerId; p.Team = w.Team; p.Role = w.Role;
                p.ArchetypeIndex = w.Roles != null && w.Roles.Count > 0 ? (int)w.Roles[0].Role : -1;
            }
            return ref p;
        }

        /// <summary>The stats of a player by id (a copy; the default when he was never seen).</summary>
        public bool TryGetPlayer(string playerId, out PlayerStats stats)
        {
            for (int i = 0; i < players.Length; i++) if (players[i].PlayerId == playerId && playerId != null) { stats = players[i]; return true; }
            stats = default(PlayerStats);
            return false;
        }
    }
}
