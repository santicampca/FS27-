using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using FS27.Core;

namespace FS27.Client
{
    /// <summary>
    /// The browser host of the real Core. It owns one <see cref="MatchController"/> and the real <see cref="BasicDecisionDriver"/> (the AI of both teams), advances them in fixed steps from the time the page gives it,
    /// feeds the person's touch input through the Core's own <see cref="MatchController.SubmitHumanInput"/>, and hands the page a compact snapshot to draw. It decides nothing about football: no rule, no physics, no AI lives here.
    /// </summary>
    public static partial class GameBridge
    {
        private static GameDatabase db;
        private static RosterGeneration rosters;
        private static MatchController match;
        private static BasicDecisionDriver driver;
        private static SimulationClock clock;
        private static Vec2 stick;
        private static bool hasPress;
        private static NormalizedInput press;
        private static float restartWait;
        private static string lastAction = "";
        private static float lastActionAt = -100f;
        private static int homeTeam, awayTeam;
        private static readonly List<string> order = new List<string>();
        // Phase 35: what the page needs to make sound, vibrate, show notices and the final statistics (read from the Core's own events and actions; nothing here decides anything)
        private static readonly StringBuilder eventQueue = new StringBuilder();
        private static readonly int[] shots = new int[2], passes = new int[2], fouls = new int[2], corners = new int[2], throwIns = new int[2], headers = new int[2], tackles = new int[2];
        private static readonly double[] possession = new double[2];

        public const float RestartPauseSeconds = 1.2f;

        // ------------------------------------------------------------------ data

        private static readonly Dictionary<string, string> files = new Dictionary<string, string>();

        /// <summary>A JSON string literal (escaped).</summary>
        private static string Q(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in text ?? "")
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
                else sb.Append(ch);
            }
            return sb.Append('"').ToString();
        }

        /// <summary>The authoring files the page has to fetch and hand over with <see cref="AddFile"/>, as a JSON array of names.</summary>
        [JSExport]
        public static string RequiredFiles()
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (string f in DataFiles.AllFiles()) { sb.Append(first ? "" : ",").Append(Q(f)); first = false; }
            return sb.Append(']').ToString();
        }

        [JSExport] public static void AddFile(string name, string text) { files[name] = text; }

        /// <summary>Builds the database from the files added. Returns "" when it loaded without problem, otherwise the problems.</summary>
        [JSExport]
        public static string LoadData()
        {
            db = GameDatabaseLoader.Load(source => { string f = DataFiles.FileOf(source); return f != null && files.TryGetValue(f, out string text) ? text : null; });
            rosters = RosterGenerator.Generate(7, db.Players, db.Profiles, db.Teams);
            return db.LoadProblems.Count == 0 ? "" : string.Join("\n", db.LoadProblems);
        }

        [JSExport]
        public static string Teams()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < db.Teams.Count; i++) sb.Append(i > 0 ? "," : "").Append("{\"i\":").Append(i).Append(",\"id\":").Append(Q(db.Teams[i].Id)).Append(",\"name\":").Append(Q(db.Teams[i].Name)).Append('}');
            return sb.Append(']').ToString();
        }

        // ------------------------------------------------------------------ a match

        /// <summary>Starts a match (Home is the person's team). mode: "Arena" or "Classic"; level: the difficulty of the AI of both teams. Returns the roster as JSON, or "error: ...".</summary>
        [JSExport]
        public static string NewMatch(int home, int away, int seed, string mode, string level)
        {
            TeamDefinition h = db.Teams[home], a = db.Teams[away];
            if (!db.TryGetFormation(h, out FormationDefinition fh) || !db.TryGetFormation(a, out FormationDefinition fa)) return "error: formation";
            if (!rosters.TryGetRoster(h.Id, out TeamRoster rh) || !rosters.TryGetRoster(a.Id, out TeamRoster ra)) return "error: roster";
            if (!rh.TryToMatchSquad(db.Players, out MatchSquad sh, out string problem) || !ra.TryToMatchSquad(db.Players, out MatchSquad sa, out problem)) return "error: " + problem;
            GameMode gm = mode == "Arena" ? GameMode.Arena : GameMode.Classic;
            MatchSettings settings = gm == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            settings.Seed = seed;
            match = new MatchController(new FieldDimensions(), settings, sh, sa, null, "play", null, db.Players, null, db.PlayerSkills, db.Skills);
            match.SetFormation(TeamSide.Home, fh); match.SetFormation(TeamSide.Away, fa);
            match.Start();
            match.SetPlayingProfiles(db.Profiles);
            var assigner = new RolePositionAssigner(db.Profiles);
            if (!assigner.TryAssign(rh, fh, db.Players, out RoleAssignment ah, out problem) || !assigner.TryAssign(ra, fa, db.Players, out RoleAssignment aa, out problem)) return "error: " + problem;
            match.SetRoleAssignment(TeamSide.Home, ah); match.SetRoleAssignment(TeamSide.Away, aa);
            if (!match.ConfirmRestart()) return "error: kick-off";

            driver = new BasicDecisionDriver(match, BasicDecisionTuning.ForMode(gm), DefenseTuning.ForMode(gm), null, AttackPositioningTuning.ForMode(gm), BallCarrierTuning.ForMode(gm), GoalkeeperAiTuning.ForMode(gm));
            DifficultyLevel lv = DifficultyLevel.Professional;
            if (!string.IsNullOrEmpty(level) && Enum.TryParse(level, out DifficultyLevel parsed)) lv = parsed;
            var profileTuning = new DecisionProfileTuning();
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(lv), db.Players, match, profileTuning);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(lv), db.Players, match, profileTuning);
            // the teams play the way their coaches' data says (Phase 31) and the person's passes and shots are helped to go where he meant
            driver.SetTactic(TeamSide.Home, TeamTactic.Of(h, db)); driver.SetTactic(TeamSide.Away, TeamTactic.Of(a, db));
            match.AimAssist.Enabled = aimAssist;
            eventQueue.Clear(); Array.Clear(shots, 0, 2); Array.Clear(passes, 0, 2); Array.Clear(fouls, 0, 2); Array.Clear(corners, 0, 2); Array.Clear(throwIns, 0, 2); Array.Clear(headers, 0, 2); Array.Clear(tackles, 0, 2); possession[0] = possession[1] = 0;
            match.Events.Published += OnEvent;
            match.ActionPerformed += OnAction;
            match.HeadersEnabled = true; driver.CrossActions = true; driver.CrossMechanics = true; match.HumanCrossEnabled = true;   // Phase 35: crosses are real crosses   // Phase 33: a ball in the air is played in the air
            match.SetControl(TeamSide.Home, ControlSource.Human);
            clock = new SimulationClock(settings.SimulationStepSeconds);
            stick = Vec2.Zero; hasPress = false; restartWait = 0f; lastAction = ""; lastActionAt = -100f;
            homeTeam = home; awayTeam = away;
            order.Clear();
            var sb = new StringBuilder("{\"home\":").Append(Q(h.Name)).Append(",\"away\":").Append(Q(a.Name))
                .Append(",\"fieldLength\":").Append(match.Pitch.Field.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",\"fieldWidth\":").Append(match.Pitch.Field.Width.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\"goalWidth\":").Append(match.Pitch.Field.GoalWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",\"players\":[");
            bool first = true;
            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                foreach (MatchPlayer p in match.State.Team(side).Players)
                {
                    order.Add(p.PlayerId);
                    db.Players.TryGet(p.PlayerId, out PlayerDefinition def);
                    sb.Append(first ? "" : ",").Append("{\"id\":").Append(Q(p.PlayerId)).Append(",\"team\":").Append(side == TeamSide.Home ? 0 : 1)
                      .Append(",\"gk\":").Append(p.IsGoalkeeper ? "true" : "false").Append(",\"number\":").Append(def != null ? def.Number : 0).Append(",\"name\":").Append(Q(def != null ? def.DisplayName : p.PlayerId)).Append('}');
                    first = false;
                }
            return sb.Append("]}").ToString();
        }

        // ------------------------------------------------------------------ input (the person's side is Home)

        private static bool aimAssist = true;

        /// <summary>Whether the person's passes and shots are helped to go where he pushed (on by default; takes effect at the next match).</summary>
        [JSExport] public static void SetAimAssist(bool on) { aimAssist = on; if (match != null) match.AimAssist.Enabled = on; }

        [JSExport] public static void SetStick(double x, double y) { stick = new Vec2((float)x, (float)y); }

        /// <summary>A button press: button 1 = A, 2 = B, 3 = C; gesture 0 = tap, 1 = swipe up, 2 = swipe down; the aim is a direction in field space (0,0 = where he faces); power 0..1.</summary>
        [JSExport]
        public static void Press(int button, int gesture, double aimX, double aimY, double power)
        {
            press = NormalizedInput.Press(Vec2.Zero, (ActionButton)button, (ActionGesture)gesture, new Vec2((float)aimX, (float)aimY), (float)power);
            hasPress = true;
        }

        [JSExport] public static void SwitchPlayer() { if (match != null) match.SelectNearestToBall(TeamSide.Home); }

        // ------------------------------------------------------------------ time

        /// <summary>Advances the match by this much real time (the Core keeps its own fixed step). Returns the number of Core steps taken.</summary>
        [JSExport]
        public static int Advance(double seconds)
        {
            if (match == null) return 0;
            int steps = clock.Advance((float)seconds);
            for (int i = 0; i < steps; i++) StepOnce();
            return steps;
        }

        private static void StepOnce()
        {
            driver.Step(match);
            NormalizedInput input = hasPress ? press : NormalizedInput.Stick(Vec2.Zero);
            input.Move = stick;
            HumanInputResult r = match.SubmitHumanInput(TeamSide.Home, input);
            if (hasPress) { hasPress = false; if (r.ActionAttempted) { lastAction = r.Action.Kind + ":" + r.Action.Outcome; lastActionAt = match.State.MatchTimeSeconds; } }
            if (match.State.Phase == MatchPhase.Playing && match.State.Ball.HasPossession) possession[match.State.Ball.PossessingTeam == TeamSide.Home ? 0 : 1] += clock.StepSeconds;
            match.Advance(clock.StepSeconds);
            MatchPhase phase = match.State.Phase;
            if (phase == MatchPhase.WaitingForKickoff || phase == MatchPhase.WaitingForRestart)
            {
                restartWait += clock.StepSeconds;
                // Phase 33: a throw-in or a corner of the person's team waits for him (aim, charge, release) before the AI takes it
                int dead = HumanDeadBall(); float limit = dead == 0 ? RestartPauseSeconds : (dead <= 2 ? match.SetPiece.HumanWaitSeconds : match.FreeKick.HumanMaxWaitSeconds);
                if (restartWait >= limit) { match.ConfirmRestart(); restartWait = 0f; }
            }
            else restartWait = 0f;
        }

        // ------------------------------------------------------------------ what to draw

        public const int PlayerFields = 8;
        public const int HeaderFields = 13;

        /// <summary>
        /// Header: [0] match time, [1] phase, [2] home goals, [3] away goals, [4] period, [5] clock remaining, [6] ball x, [7] ball height, [8] ball z, [9] controlled player index, [10] seconds since the last action, [11] finished (1/0), [12] 1 when Home attacks +X (it changes at the side switch).
        /// Then per player (Home squad, then Away): x, z, heading (rad), has ball, stamina share, status (0 on pitch, 1 off), in goal, red/yellow.
        /// </summary>
        [JSExport]
        public static double[] Snapshot()
        {
            if (match == null) return new double[0];
            MatchState s = match.State;
            var o = new double[HeaderFields + order.Count * PlayerFields];
            o[0] = s.MatchTimeSeconds; o[1] = (int)s.Phase; o[2] = s.Score.Home; o[3] = s.Score.Away; o[4] = (int)s.Period; o[5] = s.Clock.Remaining;
            o[6] = s.Ball.Physics.Position.X; o[7] = s.Ball.Physics.Position.Y; o[8] = s.Ball.Physics.Position.Z;
            string ctrl = match.ControlledPlayerId(TeamSide.Home);
            o[9] = ctrl == null ? -1 : order.IndexOf(ctrl);
            o[10] = s.MatchTimeSeconds - lastActionAt; o[11] = s.Phase == MatchPhase.Finished ? 1 : 0; o[12] = s.HomeAttacksPositiveX ? 1 : 0;
            MatchPlayer carrier = match.BallCarrier;
            for (int i = 0; i < order.Count; i++)
            {
                MatchPlayer p = (i < s.Home.Players.Count ? s.Home.Players[i] : s.Away.Players[i - s.Home.Players.Count]);
                int b = HeaderFields + i * PlayerFields;
                o[b] = p.Locomotion.Position.X; o[b + 1] = p.Locomotion.Position.Y; o[b + 2] = p.Locomotion.Heading;
                o[b + 3] = carrier != null && carrier.PlayerId == p.PlayerId ? 1 : 0;
                o[b + 4] = p.Locomotion.CurrentStamina;
                o[b + 5] = p.IsActive ? 0 : 1; o[b + 6] = p.InGoal ? 1 : 0; o[b + 7] = p.RedCards > 0 ? 2 : (p.YellowCards > 0 ? 1 : 0);
            }
            return o;
        }

        [JSExport] public static string LastAction() { return lastAction; }

        private static void OnEvent(GameplayEvent ev)
        {
            int t = ev.HasTeam ? (ev.Team == TeamSide.Home ? 0 : 1) : -1;
            switch (ev.Kind)
            {
                case GameplayEventKind.ShotPerformed: if (t >= 0) shots[t]++; break;
                case GameplayEventKind.PassPerformed: if (t >= 0) passes[t]++; break;
                case GameplayEventKind.FoulCommitted: if (t >= 0) fouls[t]++; break;
                case GameplayEventKind.CornerAwarded: if (t >= 0) corners[t]++; break;
                case GameplayEventKind.ThrowInAwarded: if (t >= 0) throwIns[t]++; break;
                case GameplayEventKind.TacklePerformed: if (t >= 0) tackles[t]++; break;
            }
            switch (ev.Kind)
            {
                case GameplayEventKind.GoalScored: case GameplayEventKind.ShotPerformed: case GameplayEventKind.PassPerformed: case GameplayEventKind.FoulCommitted: case GameplayEventKind.CornerAwarded:
                case GameplayEventKind.ThrowInAwarded: case GameplayEventKind.GoalKickAwarded: case GameplayEventKind.FreeKickAwarded: case GameplayEventKind.PenaltyAwarded: case GameplayEventKind.TacklePerformed:
                case GameplayEventKind.YellowCardIssued: case GameplayEventKind.RedCardIssued: case GameplayEventKind.HalfTimeStarted: case GameplayEventKind.MatchEnded: case GameplayEventKind.BallDeflected: case GameplayEventKind.BallOutOfBounds:
                    if (eventQueue.Length < 2000) eventQueue.Append(ev.Kind).Append(':').Append(t).Append(',');
                    break;
            }
        }

        private static void OnAction(ActionResult r)
        {
            if (r.Kind == FootballActionKind.Header) { int t = r.Team == TeamSide.Home ? 0 : 1; headers[t]++; if (eventQueue.Length < 2000) eventQueue.Append("Header:").Append(t).Append(','); }
        }

        /// <summary>Phase 36: true while the person's carrier is on a flank in the attacking third: the swipe up on A is a cross (the page shows it on the button).</summary>
        [JSExport] public static bool CanCross() { return match != null && match.CanHumanCross(TeamSide.Home); }

        /// <summary>For the automatic test of the page ONLY (Client/test/offline.mjs): puts the person's player with the ball on a flank in the attacking third, a team-mate in the middle of the pitch and the rivals far away. Returns "ok" or an error.</summary>
        [JSExport]
        public static string TestSetup(string scene)
        {
            if (match == null || scene != "cross") return "error";
            float sign = match.State.HomeAttacksPositiveX ? 1f : -1f; string me = null, mate = null;
            foreach (MatchPlayer p in match.State.Home.Players) { if (!p.IsActive || p.InGoal) continue; if (me == null) me = p.PlayerId; else if (mate == null) mate = p.PlayerId; }
            if (me == null) return "error";
            match.PlacePlayer(TeamSide.Home, me, new Vec2(sign * 10f, -10f), (sign > 0 ? new Vec2(1, 0) : new Vec2(-1, 0)).ToHeading());
            if (mate != null) match.PlacePlayer(TeamSide.Home, mate, new Vec2(sign * 15f, 0f), (sign > 0 ? new Vec2(1, 0) : new Vec2(-1, 0)).ToHeading());
            int k = 0; foreach (MatchPlayer p in match.State.Away.Players) { if (!p.IsActive || p.InGoal) continue; match.PlacePlayer(TeamSide.Away, p.PlayerId, new Vec2(-sign * 9f, -8f + 4f * k++), (sign > 0 ? new Vec2(-1, 0) : new Vec2(1, 0)).ToHeading()); }   // the rivals out of the way: the test is about the cross, not about the defence
            match.State.Ball.Physics = new BallState(new Vec3(sign * 10.8f, match.BallParameters.Radius, -10f), Vec3.Zero) { IsGrounded = true };
            match.SelectPlayer(TeamSide.Home, me); match.GiveBallTo(TeamSide.Home, me);
            return "ok";
        }

        /// <summary>Phase 35: the events since the last call, as "Kind:team,Kind:team" (team 0 = the person's, 1 = the rival's, -1 = none). For sound, vibration and notices.</summary>
        [JSExport] public static string DrainEvents() { string s = eventQueue.ToString(); eventQueue.Clear(); return s; }

        /// <summary>Phase 35: the statistics of the match so far, as JSON (home, away): shots, passes, fouls, corners, throw-ins, headers, tackles and the share of possession.</summary>
        [JSExport]
        public static string Stats()
        {
            double total = possession[0] + possession[1];
            string One(int i) { return "{\"shots\":" + shots[i] + ",\"passes\":" + passes[i] + ",\"fouls\":" + fouls[i] + ",\"corners\":" + corners[i] + ",\"throwIns\":" + throwIns[i] + ",\"headers\":" + headers[i] + ",\"tackles\":" + tackles[i] + ",\"possession\":" + (total > 0 ? Math.Round(100.0 * possession[i] / total) : 50).ToString(System.Globalization.CultureInfo.InvariantCulture) + "}"; }
            return "{\"home\":" + One(0) + ",\"away\":" + One(1) + "}";
        }

        /// <summary>Phase 35 (FS27-REQ-0080): the index (as in the snapshot) of the player the automatic choice would pick now, or -1.</summary>
        [JSExport]
        public static int SuggestedIndex()
        {
            if (match == null || driver == null) return -1;
            string id = AutoSelection.Choose(WorldState.Capture(match).View, TeamSide.Home, match.StoredSelection(TeamSide.Home), driver.Engine.DefenseTuning);
            return id == null ? -1 : order.IndexOf(id);
        }

        /// <summary>The dead ball the person is asked to take, if any: 1 throw-in, 2 corner, 3 free kick, 4 penalty (0 none).</summary>
        private static int HumanDeadBall()
        {
            if (match == null || match.GetControl(TeamSide.Home) != ControlSource.Human) return 0;
            if (match.AwaitingHumanSetPiece(TeamSide.Home)) return match.State.PendingRestart.Kind == RestartKind.Corner ? 2 : 1;
            MatchState s = match.State;
            if (s.Phase == MatchPhase.WaitingForRestart && s.PendingRestart.Kind == RestartKind.FreeKick && s.FreeKick != null && s.FreeKick.Active && s.FreeKick.Beneficiary == TeamSide.Home) return 3;
            if (s.Penalty != null && s.Penalty.Waiting && s.Penalty.Team == TeamSide.Home && s.Phase != MatchPhase.Shootout) return 4;
            return 0;
        }

        /// <summary>Phase 33/35: the dead ball the person is asked to take: [kind (0 none, 1 throw-in, 2 corner, 3 free kick, 4 penalty), spot x, spot z, seconds left, seconds of a full charge, attack direction x (+1/-1)].</summary>
        [JSExport]
        public static double[] SetPieceInfo()
        {
            int kind = HumanDeadBall();
            if (kind == 0) return new double[] { 0, 0, 0, 0, 0, 1 };
            MatchState s = match.State; Vec2 spot = kind == 3 ? s.FreeKick.Spot : kind == 4 ? s.Penalty.Spot : s.PendingRestart.Position;
            float wait = kind <= 2 ? match.SetPiece.HumanWaitSeconds : match.FreeKick.HumanMaxWaitSeconds;
            return new double[] { kind, spot.X, spot.Y, Math.Max(0.0, wait - restartWait), kind == 4 ? match.Penalties.FullChargeSeconds : match.FreeKick.FullChargeSeconds, s.HomeAttacksPositiveX ? 1 : -1 };
        }

        /// <summary>Phase 33/35: takes it with the aim of the person (field frame, like <see cref="Press"/>): the direction he pushed, how long he held the button and a zone of the goal (-1 none, 0 left, 1 centre, 2 right; a free kick without a zone is a pass along the drag; a penalty without one goes to the middle). Returns the Core's answer.</summary>
        [JSExport]
        public static string TakeSetPiece(double aimX, double aimY, double holdSeconds, int zone)
        {
            if (match == null) return "NoMatch";
            int kind = HumanDeadBall();
            GoalZone gz = zone == 0 ? GoalZone.Left : zone == 2 ? GoalZone.Right : GoalZone.Centre;
            string result;
            if (kind == 4) result = match.ExecutePenalty(PenaltyShotAim.At(gz, 0.7f, 0f, match.Penalties) is PenaltyShotAim a ? WithHold(a, (float)holdSeconds) : default(PenaltyShotAim)).ToString();
            else
            {
                var aim = new FreeKickAim { Intent = FreeKickIntent.Pass, Direction = new Vec2((float)aimX, (float)aimY), HoldSeconds = (float)holdSeconds };
                if ((kind == 2 || kind == 3) && zone >= 0) { aim.Intent = FreeKickIntent.Shot; aim.HasZone = true; aim.Zone = gz; }
                result = kind == 3 ? match.ExecuteFreeKick(aim).ToString() : match.ExecuteSetPiece(aim).ToString();
            }
            if (result == "Executed") restartWait = 0f;
            return result;
        }

        private static PenaltyShotAim WithHold(PenaltyShotAim a, float hold) { a.HoldSeconds = hold; return a; }

        /// <summary>What the match is doing, as text (for diagnosing a stuck restart).</summary>
        [JSExport]
        public static string DebugState()
        {
            MatchState s = match.State; SetPieceState sp = s.SetPiece; RestartRequest r = s.PendingRestart;
            return "phase=" + s.Phase + " t=" + s.MatchTimeSeconds.ToString("0.0") + " restart=" + r.Kind + "/" + r.Team + "/" + r.Status + " taker=" + r.TakerId + " needsGK=" + s.Home.NeedsGoalkeeper + "/" + s.Away.NeedsGoalkeeper
                + " setPiece=" + (sp != null ? sp.Active + "/" + sp.Kind + "/" + sp.Team + "/" + sp.TakerId + "/" + sp.Seconds.ToString("0.0") : "-") + " controlled=" + match.ControlledPlayerId(TeamSide.Home) + " ready=" + match.ConfirmRestartReady()
                + " freeKick=" + (s.FreeKick != null ? s.FreeKick.Stage.ToString() : "-") + " penalty=" + (s.Penalty != null ? s.Penalty.ToString() : "-");
        }

        /// <summary>A stable hash of the whole snapshot (positions rounded to a millimetre): equal on two runtimes means the same match, step for step. For determinism checks, not for the game.</summary>
        [JSExport]
        public static string Checksum()
        {
            double[] snap = Snapshot();
            ulong h = 14695981039346656037UL;
            foreach (double v in snap) { long q = (long)Math.Round(v * 1000.0); for (int i = 0; i < 8; i++) { h ^= (ulong)((q >> (i * 8)) & 0xFF); h *= 1099511628211UL; } }
            return h.ToString("x16");
        }
    }
}
