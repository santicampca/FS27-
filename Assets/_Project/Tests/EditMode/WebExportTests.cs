using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phases 24-27: the files the web menu reads. The web never copies data by hand: THIS code writes them from the real Core (the closed database, the roster draw, the real penalty and VAR code) and a test fails when a committed file
    /// is stale. Regenerate with `Tools/webdata.sh` (sets FS27_WRITE_WEB_DATA=1). Nothing here changes the game: it only reads it.
    /// Files (Web/menu/public/data): fs27-data.json (players, teams, rosters, rules), fs27-penalty-lab.json (real penalty outcomes), fs27-var-scenarios.json (real VAR reviews), fs27-traces.json (real matches, positions every 0.1 s),
    /// fs27-vectors.json (golden vectors the JavaScript side must reproduce: shoot-out counting, the ZP ledger).
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private const string WebSchema = "FS27.WebData.v1";
        private const int WebRosterSeed = 7;

        private static string WebRoot() { return new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName; }
        private static string WebDataPath(string file) { return Path.Combine(WebRoot(), "Web", "menu", "public", "data", file); }
        private static bool WebWrite { get { return Environment.GetEnvironmentVariable("FS27_WRITE_WEB_DATA") == "1"; } }

        private static JsonValue N(double v) { return JsonValue.Of(Math.Round(v, 3)); }
        private static JsonValue S(string v) { return JsonValue.Of(v ?? ""); }
        private static JsonValue B(bool v) { return JsonValue.Of(v); }
        private static JsonValue Arr<T>(IEnumerable<T> items, Func<T, JsonValue> f) { var a = JsonValue.NewArray(); foreach (T i in items) a.Add(f(i)); return a; }
        private static string Rgb(ColorRgb c) { return "#" + c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }

        private static string Fnv(string text)
        {
            uint h = 2166136261u;
            foreach (char ch in text) { h ^= ch; h *= 16777619u; }
            return h.ToString("x8");
        }

        /// <summary>The id the web sees. The internal ids of the database contain the names of the research (see DATA_LOCK.md: they are never shown), so the public files carry an opaque, stable id instead.</summary>
        private static string Pid(string id) { return string.IsNullOrEmpty(id) ? "" : "p-" + Fnv(id); }

        private static void WebCheckOrWrite(string file, string text)
        {
            string path = WebDataPath(file);
            if (WebWrite) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text + "\n"); return; }
            Assert.IsTrue(File.Exists(path), file + " does not exist: run Tools/webdata.sh");
            Assert.AreEqual(text + "\n", File.ReadAllText(path), file + " is stale: the Core changed. Run Tools/webdata.sh and commit the result.");
        }

        // ================= fs27-data.json =================

        private static JsonValue RulesOf(GameMode mode)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            IGameModeRules r = GameModeRules.For(mode);
            return JsonValue.NewObject()
                .Set("mode", S(mode.ToString())).Set("regulationPeriods", N(s.RegulationPeriods)).Set("periodSeconds", N(s.RegulationPeriodSeconds)).Set("halftimeSeconds", N(s.HalftimeSeconds))
                .Set("extraTimeSeconds", N(s.ExtraTimePeriodSeconds)).Set("shootoutKicksPerTeam", N(s.ShootoutKicksPerTeam)).Set("shootoutSuddenDeath", B(s.ShootoutSuddenDeath)).Set("substitutionLimit", N(s.EffectiveSubstitutionLimit))
                .Set("usesWalls", B(r.UsesWalls)).Set("fouls", B(r.HasFouls)).Set("offside", B(r.HasOffside)).Set("corners", B(r.HasCorners)).Set("throwIns", B(r.HasThrowIns)).Set("goalKicks", B(r.HasGoalKicks))
                .Set("cards", B(r.HasCards)).Set("penalties", B(r.HasPenalties)).Set("referee", B(r.HasReferee)).Set("var", B(r.HasVar)).Set("physicalPenalties", B(r.PlaysPenaltiesPhysically)).Set("halfTimeBreak", B(r.HasHalfTimeBreak));
        }

        private static JsonValue PlayerValue(PlayerDefinition p, PlayerCardBuilder cards)
        {
            PlayerCardData card = cards.Build(p);
            PlayerAttributes a = p.Attributes;
            var attrs = JsonValue.NewObject();
            foreach (PlayerAttributeId id in Enum.GetValues(typeof(PlayerAttributeId))) attrs.Set(id.ToString(), N(a.GetValue(id)));
            var o = JsonValue.NewObject();
            o.Set("id", S(Pid(p.Id))).Set("name", S(p.Name)).Set("shortName", S(p.ResolveShortName())).Set("number", N(p.Number)).Set("line", S(p.Role.ToString())).Set("nationality", S(p.NationalityCode))
                .Set("age", N(p.Age)).Set("heightCm", N(p.HeightCm)).Set("weightKg", N(p.WeightKg)).Set("foot", S(p.PreferredFoot.ToString())).Set("weakFoot", N(p.WeakFootQuality)).Set("body", S(p.BodyType.ToString()))
                .Set("rating", N(card.HeadlineRating)).Set("overall", N(card.Overall)).Set("isGoalkeeper", B(card.IsGoalkeeper)).Set("attributes", attrs);
            if (card.IsGoalkeeper)
            {
                var caps = JsonValue.NewObject();
                foreach (GoalkeeperCapability cap in Enum.GetValues(typeof(GoalkeeperCapability))) caps.Set(cap.ToString(), N(card.GetGoalkeeperCapability(cap)));
                o.Set("keeper", caps).Set("goalkeeperRating", N(card.GoalkeeperRating));
                if (card.TryGetGoalkeeperStyle(out GoalkeeperStyle gs)) o.Set("keeperStyle", S(gs.ToString()));
            }
            else
            {
                RoleAffinity[] roles = card.GetRoles();
                o.Set("roles", Arr(roles, r => JsonValue.NewObject().Set("role", S(r.Role.ToString())).Set("affinity", N(r.Affinity))));
                if (card.TryGetPrimaryRole(out PlayerArchetype pr)) o.Set("primaryRole", S(pr.ToString())).Set("roleGroup", S(RoleInfo.GroupOf(pr).ToString())).Set("style", S(card.StyleLabel));
                o.Set("zone", S(card.PrimaryZone.ToString()));
            }
            return o;
        }

        private static string BuildWebData()
        {
            var cards = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), Db.Profiles, null, id => null, Db.Players);
            var root = JsonValue.NewObject();
            root.Set("schema", S(WebSchema)).Set("rosterSeed", N(WebRosterSeed));
            root.Set("rules", JsonValue.NewObject().Set("Arena", RulesOf(GameMode.Arena)).Set("Classic", RulesOf(GameMode.Classic)));
            root.Set("roles", Arr(RoleInfo.OfficialRoles, r => JsonValue.NewObject().Set("id", S(r.ToString())).Set("group", S(RoleInfo.GroupOf(r).ToString())).Set("name", S(RoleInfo.SpanishName(r)))));
            root.Set("difficulties", Arr(Enum.GetValues(typeof(DifficultyLevel)).Cast<DifficultyLevel>(), l => { DifficultyDefinition d = DefaultDifficulties.Create(l); return JsonValue.NewObject().Set("level", S(l.ToString())).Set("id", S(d.Id)).Set("name", S(d.Name)); }));
            IEnumerable<PlayerDefinition> ordered = Db.Players.All.OrderBy(p => p.Id, StringComparer.Ordinal);
            root.Set("players", Arr(ordered, p => PlayerValue(p, cards)));

            var assigned = new HashSet<string>();
            var teamsArr = JsonValue.NewArray();
            foreach (TeamDefinition t in Db.Teams)
            {
                Assert.IsTrue(Rosters.TryGetRoster(t.Id, out TeamRoster roster), t.Id);
                Assert.IsTrue(Db.TryGetFormation(t, out FormationDefinition f), t.Id);
                foreach (string id in roster.AllPlayerIds) assigned.Add(id);
                var tv = JsonValue.NewObject();
                tv.Set("id", S(t.Id)).Set("name", S(t.Name)).Set("shortName", S(t.ShortName)).Set("kind", S(t.Kind.ToString())).Set("level", S(t.Level.ToString())).Set("primary", S(Rgb(t.Colors.Primary))).Set("secondary", S(Rgb(t.Colors.Secondary)))
                    .Set("theme", S(t.Theme)).Set("description", S(t.Description)).Set("formationId", S(f.Id)).Set("formationName", S(f.Name));
                CoachDefinition coach; if (!string.IsNullOrEmpty(t.CoachId) && Db.Coaches.TryGet(t.CoachId, out coach)) tv.Set("coach", S(coach.Name));
                tv.Set("active", Arr(roster.ActiveIds, x => S(Pid(x)))).Set("bench", Arr(roster.BenchIds, x => S(Pid(x))));
                Assert.IsTrue(new RolePositionAssigner(Db.Profiles).TryAssign(roster, f, Db.Players, out RoleAssignment ra, out string problem), problem);
                tv.Set("lineup", JsonValue.NewObject().Set("goalkeeper", S(Pid(ra.GoalkeeperId))).Set("field", Arr(Enumerable.Range(0, ra.FieldPlayerIds.Count), i =>
                    JsonValue.NewObject().Set("id", S(Pid(ra.FieldPlayerIds[i]))).Set("depth", N(ra.FieldSlots[i].Relative.X)).Set("width", N(ra.FieldSlots[i].Relative.Y)).Set("zone", S(ra.FieldSlots[i].Zone.ToString())))));
                teamsArr.Add(tv);
            }
            root.Set("teams", teamsArr);
            root.Set("unassignedPlayerIds", Arr(Db.Players.All.Select(p => p.Id).Where(id => !assigned.Contains(id)).OrderBy(x => x, StringComparer.Ordinal).Select(Pid), x => S(x)));

            var pt = new PenaltyTuning(); var vt = new VarTuning(); var zt = new ZenitTuning();
            root.Set("config", JsonValue.NewObject()
                .Set("var", JsonValue.NewObject().Set("enabledInClassic", B(vt.Enabled)).Set("costGoalZp", N(vt.CostGoalZp)).Set("costRedCardZp", N(vt.CostRedCardZp)).Set("costPenaltyZp", N(vt.CostPenaltyZp)).Set("requestsPerTeam", N(vt.RequestsPerTeam)).Set("reviewSeconds", N(vt.ReviewSeconds)).Set("windowSeconds", N(vt.WindowSeconds)).Set("requireHumanRequester", B(vt.RequireHumanRequester)))
                .Set("penalty", JsonValue.NewObject().Set("spotDistance", N(pt.SpotDistance)).Set("areaDepth", N(pt.AreaDepth)).Set("maxHeight", N(pt.MaxHeight)))
                .Set("zenit", JsonValue.NewObject().Set("starterGrant", N(zt.StarterGrant)).Set("rewardMatchCompleted", N(zt.RewardMatchCompleted)).Set("rewardMatchWon", N(zt.RewardMatchWon)).Set("rewardMatchDrawn", N(zt.RewardMatchDrawn)).Set("rewardTrainingCompleted", N(zt.RewardTrainingCompleted)).Set("trainingRewardsPerDay", N(zt.TrainingRewardsPerDay)).Set("maxBalance", N(zt.MaxBalance))));
            return Json.Write(root, false);
        }

        [Test]
        public void WebExport_Data_IsTheRealDatabase_AndTheCommittedFileIsNotStale()
        {
            string text = BuildWebData();
            Assert.IsTrue(Json.TryParse(text, out JsonValue v, out string err), err);
            Assert.AreEqual(Db.Players.All.Count(), v.Members["players"].Items.Count, "every player of the database is exported, none is invented");
            Assert.AreEqual(Db.Teams.Count, v.Members["teams"].Items.Count);
            Assert.AreEqual(Db.Players.All.Count(), Db.Players.All.Select(p => Pid(p.Id)).Distinct().Count(), "the public ids do not collide");
            Assert.IsFalse(text.Contains("gk-manuel") || text.Contains("cb-josko") || text.Contains("dc-harry"), "no internal id (they carry real names) reaches a public file");
            WebCheckOrWrite("fs27-data.json", text);
        }

        // ================= fs27-penalty-lab.json =================

        private static readonly float[] LabLateral = { -1.1f, -0.85f, -0.6f, -0.3f, 0f, 0.3f, 0.6f, 0.85f, 1.1f };
        private static readonly float[] LabHeight = { 0.2f, 0.5f, 0.8f, 1.1f };
        private static readonly float[] LabPower = { 0.6f, 0.8f, 1f };
        private static readonly float[] LabDive = { 1f, 0f, -1f };      // left (as the kicker sees it), middle, right
        private const string B64 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

        private static char OutcomeChar(PenaltyOutcome o)
        {
            switch (o) { case PenaltyOutcome.Scored: return 'G'; case PenaltyOutcome.Saved: return 'S'; case PenaltyOutcome.HitPost: return 'P'; case PenaltyOutcome.HitBar: return 'B'; case PenaltyOutcome.Missed: return 'M'; default: return 'X'; }
        }

        /// <summary>A real in-match penalty for home in a real match of the two teams: the chosen taker kicks, the keeper dives where the table says. Returns the 3-char cell: outcome, where the ball was when it was decided (lateral, height).</summary>
        private static string LabKick(int homeTeam, int awayTeam, string takerId, PenaltyShotAim aim, float dive)
        {
            MatchController c = RealMatch(homeTeam, awayTeam, 1, MatchSettings.CreateClassic());
            c.Penalties.Enabled = true; c.SetControl(TeamSide.Home, ControlSource.Human);
            Assert.IsTrue(c.ConfirmRestart());
            string offender = c.State.Away.Players.First(p => !p.IsGoalkeeper).PlayerId, victim = c.State.Home.Players.First(p => !p.IsGoalkeeper).PlayerId;
            Assert.IsTrue(c.CallFoul(TeamSide.Away, offender, victim, new RefereeDecision { IsFoul = true, Reasons = FoulReason.Late, Severity = 0.8f }, new Vec3(17f, 0.2f, 0f)).Called);
            Assert.IsTrue(c.PrepareRestart());
            Assert.AreEqual(PenaltyTakerResult.Accepted, c.ChoosePenaltyTaker(TeamSide.Home, takerId)); c.ConfirmPenaltyTaker(TeamSide.Home);
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = dive });
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(aim));
            float goalX = c.State.Penalty.GoalCentre.X, half = c.Pitch.Field.GoalWidth * 0.5f;
            bool marked = false; float mz = 0f, my = 0f;
            for (int i = 0; i < 600 && c.State.Penalty.Stage != PenaltyStage.Resolved; i++)
            {
                c.Advance(1f / 60f);
                Vec3 b = c.State.Ball.Physics.Position;
                bool atGoal = Math.Abs(b.X) >= Math.Abs(goalX) - 0.05f && Math.Sign(b.X) == Math.Sign(goalX);
                if (!marked && (atGoal || c.State.Penalty.KeeperTouched || c.State.Penalty.HitFrame)) { marked = true; mz = b.Z; my = b.Y; }
            }
            if (!marked) { Vec3 b = c.State.Ball.Physics.Position; mz = b.Z; my = b.Y; }
            // lateral as the kicker sees it (left positive): attacking +X the kicker's left is the +Z side, attacking -X it is -Z
            float lat = Math.Sign(goalX) * mz / half;
            int zi = Math.Max(0, Math.Min(63, (int)Math.Round((lat + 1.5f) / 3f * 63f))), hi = Math.Max(0, Math.Min(63, (int)Math.Round(Math.Max(0f, Math.Min(2.4f, my)) / 2.4f * 63f)));
            return "" + OutcomeChar(c.State.Penalty.Outcome) + B64[zi] + B64[hi];
        }

        private static List<FreeKickTakerCandidate> LabTakers(int team, int other)
        {
            MatchController c = RealMatch(team, other, 1, MatchSettings.CreateClassic());
            c.Penalties.Enabled = true; c.SetControl(TeamSide.Home, ControlSource.Human); Assert.IsTrue(c.ConfirmRestart());
            string offender = c.State.Away.Players.First(p => !p.IsGoalkeeper).PlayerId, victim = c.State.Home.Players.First(p => !p.IsGoalkeeper).PlayerId;
            c.CallFoul(TeamSide.Away, offender, victim, new RefereeDecision { IsFoul = true, Reasons = FoulReason.Late, Severity = 0.8f }, new Vec3(17f, 0.2f, 0f));
            return c.PenaltyTakerCandidates(Db.Players);
        }

        private static JsonValue LabGridValue()
        {
            return JsonValue.NewObject().Set("lateral", Arr(LabLateral, x => N(x))).Set("height", Arr(LabHeight, x => N(x))).Set("power", Arr(LabPower, x => N(x))).Set("dive", Arr(LabDive, x => N(x)))
                .Set("order", S("taker, lateral, height, power, dive (dive fastest); 3 chars per cell: outcome (G scored, S saved, P post, B bar, M missed, X other), lateral and height of the ball when it was decided (base64 index 0-63: lateral -1.5..1.5, height 0..2.4 m)"));
        }

        private static string LabInputs()
        {
            var sb = new StringBuilder();
            sb.Append(BuildWebData().Length).Append('|').Append(Fnv(BuildWebData()));
            var pt = new PenaltyTuning();
            sb.Append('|').Append(pt.AiPower).Append(pt.MaxHeight).Append(pt.SpotDistance).Append(pt.KeeperSpeedFactor);
            foreach (float f in LabLateral.Concat(LabHeight).Concat(LabPower).Concat(LabDive)) sb.Append(f.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            return Fnv(sb.ToString());
        }

        private static string BuildPenaltyLab(out int cells)
        {
            cells = 0;
            var root = JsonValue.NewObject();
            root.Set("schema", S(WebSchema)).Set("kind", S("penalty-lab")).Set("rosterSeed", N(WebRosterSeed)).Set("inputsHash", S(LabInputs())).Set("grid", LabGridValue());
            var pairs = JsonValue.NewObject();
            for (int a = 0; a < Db.Teams.Count; a++)
            {
                var one = JsonValue.NewObject();
                for (int b = 0; b < Db.Teams.Count; b++)
                {
                    if (a == b) continue;
                    List<FreeKickTakerCandidate> takers = LabTakers(a, b);
                    var cellsText = new StringBuilder();
                    foreach (FreeKickTakerCandidate t in takers)
                        foreach (float lat in LabLateral) foreach (float h in LabHeight) foreach (float pw in LabPower) foreach (float dv in LabDive)
                        { cellsText.Append(LabKick(a, b, t.PlayerId, new PenaltyShotAim { Lateral = lat, Height = h, Power = pw }, dv)); cells++; }
                    one.Set(Db.Teams[b].Id, JsonValue.NewObject().Set("takers", Arr(takers, t => JsonValue.NewObject().Set("id", S(Pid(t.PlayerId))).Set("suggested", B(t.IsSuggested)).Set("score", N(t.Score)).Set("shotClose", N(t.ShotClose)).Set("canCurve", B(t.CanCurve))))
                        .Set("cells", S(cellsText.ToString())));
                }
                pairs.Set(Db.Teams[a].Id, one);
            }
            root.Set("pairs", pairs);
            return Json.Write(root, false);
        }

        [Test]
        public void WebExport_PenaltyLab_RealCoreOutcomes_AndTheCommittedFileIsNotStale()
        {
            string path = WebDataPath("fs27-penalty-lab.json");
            if (WebWrite)
            {
                string text = BuildPenaltyLab(out int cells);
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text + "\n");
                TestContext.Progress.WriteLine("penalty lab: " + cells + " cells, " + text.Length + " chars");
                return;
            }
            Assert.IsTrue(File.Exists(path), "run Tools/webdata.sh");
            JsonValue v = Json.Parse(File.ReadAllText(path));
            Assert.AreEqual(LabInputs(), v.GetString("inputsHash"), "the data or the lab grid changed: run Tools/webdata.sh");
            // and the code too: a deterministic sample of cells is played again and must match the file
            int checkedCells = 0; int perCell = 3;
            for (int a = 0; a < Db.Teams.Count; a += 5)
            {
                int b = (a + 3) % Db.Teams.Count;
                JsonValue pair = v.Members["pairs"].Members[Db.Teams[a].Id].Members[Db.Teams[b].Id];
                List<FreeKickTakerCandidate> takers = LabTakers(a, b);
                string cellsText = pair.GetString("cells");
                int perTaker = LabLateral.Length * LabHeight.Length * LabPower.Length * LabDive.Length;
                for (int k = 0; k < 14; k++)
                {
                    int ti = k % takers.Count, idx = (k * 37 + a * 11) % perTaker;
                    int di = idx % LabDive.Length, pi = idx / LabDive.Length % LabPower.Length, hi = idx / (LabDive.Length * LabPower.Length) % LabHeight.Length, li = idx / (LabDive.Length * LabPower.Length * LabHeight.Length);
                    string again = LabKick(a, b, takers[ti].PlayerId, new PenaltyShotAim { Lateral = LabLateral[li], Height = LabHeight[hi], Power = LabPower[pi] }, LabDive[di]);
                    string stored = cellsText.Substring((ti * perTaker + idx) * perCell, perCell);
                    Assert.AreEqual(stored, again, "cell " + Db.Teams[a].Id + " vs " + Db.Teams[b].Id + " taker " + ti + " idx " + idx + ": the Core no longer gives the stored result");
                    checkedCells++;
                }
            }
            Assert.Greater(checkedCells, 30);
        }

        // ================= fs27-var-scenarios.json =================

        private static JsonValue VarViewValue(VarView v)
        {
            return JsonValue.NewObject().Set("stage", S(v.Stage.ToString())).Set("indicator", B(v.Indicator)).Set("category", S(v.Category.ToString())).Set("incidentId", N(v.IncidentId)).Set("hasRequester", B(v.HasRequester))
                .Set("requester", S(v.HasRequester ? v.Requester.ToString() : "")).Set("automatic", B(v.Automatic)).Set("costZp", N(v.CostZp)).Set("costCharged", B(v.CostCharged)).Set("balanceBefore", N(v.BalanceBefore)).Set("balanceAfter", N(v.BalanceAfter))
                .Set("original", S(v.Original.ToString())).Set("final", S(v.Final.ToString())).Set("result", S(v.Result.ToString())).Set("rejection", S(v.Rejection.ToString())).Set("message", S(v.Message)).Set("secondsLeft", N(v.SecondsLeft))
                .Set("canRequest", B(v.CanRequest)).Set("requestableIncidentId", N(v.RequestableIncidentId)).Set("requestableBy", S(v.CanRequest ? v.RequestableBy.ToString() : ""));
        }

        private static JsonValue VarReviewValue(VarReview r)
        {
            return JsonValue.NewObject().Set("number", N(r.Number)).Set("incidentId", N(r.IncidentId)).Set("category", S(r.Category.ToString())).Set("requester", S(r.HasRequester ? r.Requester.ToString() : "")).Set("automatic", B(r.Automatic))
                .Set("requestId", S(r.RequestId)).Set("costZp", N(r.CostZp)).Set("charged", B(r.Charged)).Set("balanceBefore", N(r.BalanceBefore)).Set("balanceAfter", N(r.BalanceAfter)).Set("original", S(r.Original.ToString())).Set("final", S(r.Final.ToString()))
                .Set("result", S(r.Result.ToString())).Set("rejection", S(r.Rejection.ToString())).Set("message", S(r.Message));
        }

        private static JsonValue MatchSnap(MatchController c)
        {
            var o = JsonValue.NewObject().Set("home", N(c.State.Score.Home)).Set("away", N(c.State.Score.Away)).Set("phase", S(c.State.Phase.ToString())).Set("time", N(c.State.MatchTimeSeconds));
            if (c.State.Phase == MatchPhase.WaitingForRestart) o.Set("restart", S(c.State.PendingRestart.Kind.ToString())).Set("restartTeam", S(c.State.PendingRestart.Team.ToString()));
            return o;
        }

        private sealed class VarRealSetup
        {
            public MatchController C; public ZenitLedger Ledger; public TeamSide Human;
            public string HomeField(int i) { return C.State.Home.Players.Where(p => !p.IsGoalkeeper).ElementAt(i).PlayerId; }
            public string AwayField(int i) { return C.State.Away.Players.Where(p => !p.IsGoalkeeper).ElementAt(i).PlayerId; }
        }

        private static VarRealSetup VarReal(TeamSide human, int balance)
        {
            var s = new VarRealSetup { Human = human, Ledger = new ZenitLedger() };
            if (balance > 0) s.Ledger.TryEarn(balance, "Starter grant", "", "starter");
            s.C = RealMatch(0, 1, 1, MatchSettings.CreateClassic());
            s.C.Penalties.Enabled = true;
            s.C.ZenitWallet = new ZenitLedgerWallet(s.Ledger, human);
            s.C.SetControl(human, ControlSource.Human);
            Assert.IsTrue(s.C.ConfirmRestart());
            return s;
        }

        private static readonly RefereeDecision WebNoFoul = new RefereeDecision { IsFoul = false, Severity = 0.3f };
        private static RefereeDecision WebFoul(CardDecision card = CardDecision.None) { return new RefereeDecision { IsFoul = true, Card = card, Reasons = FoulReason.Late, Severity = 0.8f }; }

        private static void WebHomeScoresAPenalty(VarRealSetup s)
        {
            MatchController c = s.C;
            c.CallFoul(TeamSide.Away, s.AwayField(0), s.HomeField(0), WebFoul(), new Vec3(17f, 0.2f, 0f));
            c.PrepareRestart();
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
            c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f });
            for (int i = 0; i < 180 && c.State.Phase != MatchPhase.GoalCelebration; i++) c.Advance(1f / 60f);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
        }

        private static JsonValue VarScenario(string id, string title, string story, VarRealSetup s, Func<RefereeIncident> setup, TeamSide requester, string requestId, bool duplicate = false)
        {
            MatchController c = s.C;
            var o = JsonValue.NewObject().Set("id", S(id)).Set("title", S(title)).Set("story", S(story)).Set("human", S(s.Human.ToString())).Set("requester", S(requester.ToString()));
            RefereeIncident inc = setup();
            o.Set("before", MatchSnap(c)).Set("balanceBefore", N(s.Ledger.Balance)).Set("viewBefore", VarViewValue(c.GetVarView()));
            if (inc != null)
                o.Set("incident", JsonValue.NewObject().Set("id", N(inc.Id)).Set("category", S(inc.Category.ToString())).Set("original", S(inc.Original.ToString())).Set("team", S(inc.Team.ToString())).Set("eligibleTeam", S(inc.EligibleTeam.ToString()))
                    .Set("offender", S(PlayerName(inc.OffenderId))).Set("victim", S(PlayerName(inc.VictimId))).Set("matchTime", N(inc.MatchTime)).Set("preview", S(c.DecisionPreview(inc.Id).ToString())));
            float t0 = c.State.MatchTimeSeconds;
            VarReview r = c.RequestVarReview(requester, inc != null ? inc.Id : 999, requestId);
            o.Set("reviewAtRequest", VarReviewValue(r)).Set("viewDuring", VarViewValue(c.GetVarView())).Set("phaseDuring", S(c.State.Phase.ToString())).Set("clockStoppedDuring", B(true));
            if (duplicate) { VarReview again = c.RequestVarReview(requester, inc.Id, requestId); o.Set("repeatedReview", VarReviewValue(again)); }
            if (c.State.Phase == MatchPhase.VarReview) FkAdvance(c, c.VarRules.ReviewSeconds + 0.2f);
            o.Set("review", VarReviewValue(r));
            o.Set("after", MatchSnap(c)).Set("viewAfter", VarViewValue(c.GetVarView())).Set("balanceAfter", N(s.Ledger.Balance)).Set("clockRanDuringReview", B(Math.Abs(c.State.MatchTimeSeconds - t0) > 0.5f))
                .Set("ledger", Arr(s.Ledger.History, e => JsonValue.NewObject().Set("seq", N(e.Seq)).Set("kind", S(e.Kind.ToString())).Set("amount", N(e.Amount)).Set("reason", S(e.Reason)).Set("reference", S(e.Reference)).Set("tx", S(e.TransactionId)).Set("balanceAfter", N(e.BalanceAfter))));
            return o;
        }

        private static string PlayerName(string internalId) { PlayerDefinition d; return !string.IsNullOrEmpty(internalId) && Db.Players.TryGet(internalId, out d) ? d.Name : ""; }

        private static string BuildVarScenarios()
        {
            var list = JsonValue.NewArray();
            // 1. a goal under review that the VAR confirms (the ZP are charged anyway)
            { var s = VarReal(TeamSide.Away, 10); WebHomeScoresAPenalty(s);
              list.Add(VarScenario("goal-confirmed", "Gol revisado y confirmado", "El local marca de penal. El visitante pide revisarlo, pero el VAR no ve falta del equipo que marcó: el gol sigue y los ZP se cobran igual.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, OffenderId = s.HomeField(1), VictimId = s.AwayField(1), Position = new Vec2(10f, 2f), Call = WebNoFoul, Assessment = WebNoFoul, GoalIndex = 0, DisciplineIndex = -1 }); return s.C.State.Var.Find(id); }, TeamSide.Away, "web-goal-confirmed")); }
            // 2. a goal the VAR disallows
            { var s = VarReal(TeamSide.Away, 10); WebHomeScoresAPenalty(s);
              list.Add(VarScenario("goal-disallowed", "Gol anulado por el VAR", "Mismo gol, pero el VAR ve una falta previa del equipo que marcó: el Core corrige el marcador y el visitante saca el tiro libre.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, OffenderId = s.HomeField(1), VictimId = s.AwayField(1), Position = new Vec2(10f, 2f), Call = WebNoFoul, Assessment = WebFoul(), GoalIndex = 0, DisciplineIndex = -1 }); return s.C.State.Var.Find(id); }, TeamSide.Away, "web-goal-disallowed")); }
            // 3. a penalty given that the VAR cancels
            { var s = VarReal(TeamSide.Away, 10);
              list.Add(VarScenario("penalty-cancelled", "Penal concedido y cancelado", "El árbitro pita un penal con amarilla. El visitante pide revisión y el VAR no ve falta: el penal y la tarjeta se retiran.", s,
                  () => { s.C.CallFoul(TeamSide.Away, s.AwayField(0), s.HomeField(0), WebFoul(CardDecision.Yellow), new Vec3(17f, 0.2f, 0f)); RefereeIncident inc = s.C.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty); inc.Assessment = WebNoFoul; return inc; }, TeamSide.Away, "web-penalty-cancelled")); }
            // 4. a penalty not given that the VAR awards
            { var s = VarReal(TeamSide.Home, 10);
              list.Add(VarScenario("penalty-given", "Penal no concedido y concedido", "El árbitro deja seguir una falta dentro del área. El local pide revisión: el VAR sí ve la falta y se concede el penal.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Penalty, Original = VarOriginalDecision.PenaltyNotAwarded, Team = TeamSide.Away, OffenderId = s.AwayField(1), VictimId = s.HomeField(1), Position = new Vec2(17f, 1f), Call = WebNoFoul, Assessment = WebFoul() }); return s.C.State.Var.Find(id); }, TeamSide.Home, "web-penalty-given")); }
            // 5. nothing changes (still charged)
            { var s = VarReal(TeamSide.Home, 10);
              list.Add(VarScenario("penalty-not-given-confirmed", "Revisión que no cambia nada", "El local pide revisar una jugada sin penal. El VAR confirma la decisión del árbitro. La revisión era válida, así que los ZP se cobran igual.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Penalty, Original = VarOriginalDecision.PenaltyNotAwarded, Team = TeamSide.Away, OffenderId = s.AwayField(1), VictimId = s.HomeField(1), Position = new Vec2(17f, 1f), Call = WebNoFoul, Assessment = WebNoFoul }); return s.C.State.Var.Find(id); }, TeamSide.Home, "web-penalty-confirmed")); }
            // 6. a red card rescinded
            { var s = VarReal(TeamSide.Away, 10);
              list.Add(VarScenario("red-rescinded", "Roja revocada", "Expulsan a un jugador visitante. Pide revisión y el VAR lo deja en amarilla: vuelve al campo.", s,
                  () => { s.C.CallFoul(TeamSide.Away, s.AwayField(1), s.HomeField(0), WebFoul(CardDecision.Red), new Vec3(8f, 0.2f, 0f)); RefereeIncident inc = s.C.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard); inc.Assessment = new RefereeDecision { IsFoul = true, Card = CardDecision.Yellow, Severity = 0.7f }; return inc; }, TeamSide.Away, "web-red-rescinded")); }
            // 7. a red card confirmed
            { var s = VarReal(TeamSide.Away, 10);
              list.Add(VarScenario("red-confirmed", "Roja confirmada", "El VAR revisa la expulsión y confirma la roja. Se cobra el coste y el equipo sigue con un jugador menos.", s,
                  () => { s.C.CallFoul(TeamSide.Away, s.AwayField(1), s.HomeField(0), WebFoul(CardDecision.Red), new Vec3(8f, 0.2f, 0f)); RefereeIncident inc = s.C.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard); inc.Assessment = new RefereeDecision { IsFoul = true, Card = CardDecision.Red, Severity = 1.4f }; return inc; }, TeamSide.Away, "web-red-confirmed")); }
            // 8. refused: not enough ZP (nothing is charged)
            { var s = VarReal(TeamSide.Away, 0); WebHomeScoresAPenalty(s);
              list.Add(VarScenario("rejected-no-balance", "Solicitud rechazada: sin saldo", "El visitante pide revisar el gol sin ZP suficientes. El Core rechaza la solicitud antes de empezar: no hay revisión ni cobro.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, OffenderId = s.HomeField(1), VictimId = s.AwayField(1), Position = new Vec2(10f, 2f), Call = WebNoFoul, Assessment = WebFoul(), GoalIndex = 0, DisciplineIndex = -1 }); return s.C.State.Var.Find(id); }, TeamSide.Away, "web-rejected-balance")); }
            // 9. refused: the team the decision favoured cannot ask
            { var s = VarReal(TeamSide.Home, 10);
              list.Add(VarScenario("rejected-not-eligible", "Solicitud rechazada: no es tu jugada", "El árbitro concede un penal al local. El local no puede pedir que se revise lo que le favorece: el Core lo rechaza y no cobra.", s,
                  () => { s.C.CallFoul(TeamSide.Away, s.AwayField(0), s.HomeField(0), WebFoul(), new Vec3(17f, 0.2f, 0f)); return s.C.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty); }, TeamSide.Home, "web-rejected-eligible")); }
            // 10. the same request twice (a double tap): charged once
            { var s = VarReal(TeamSide.Away, 10); WebHomeScoresAPenalty(s);
              list.Add(VarScenario("duplicate-request", "Doble toque: se cobra una vez", "El visitante toca dos veces «Pedir VAR» con la misma solicitud. El Core la reconoce y no cobra de nuevo.", s,
                  () => { int id = s.C.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, OffenderId = s.HomeField(1), VictimId = s.AwayField(1), Position = new Vec2(10f, 2f), Call = WebNoFoul, Assessment = WebFoul(), GoalIndex = 0, DisciplineIndex = -1 }); return s.C.State.Var.Find(id); }, TeamSide.Away, "web-duplicate", true)); }
            var root = JsonValue.NewObject().Set("schema", S(WebSchema)).Set("kind", S("var-scenarios")).Set("scenarios", list)
                .Set("note", S("Cada escenario es una ejecución real del Core (partido real de Solaris Meridian contra Obsidian Bastion, jugadores con nombres publicados). La web solo muestra lo que el Core respondió."));
            return Json.Write(root, false);
        }

        [Test]
        public void WebExport_VarScenarios_AreRealCoreReviews_AndTheCommittedFileIsNotStale()
        {
            string text = BuildVarScenarios();
            JsonValue v = Json.Parse(text);
            var byId = v.Members["scenarios"].Items.ToDictionary(x => x.GetString("id"));
            Assert.AreEqual("Corrected", byId["goal-disallowed"].Members["review"].GetString("result")); Assert.AreEqual("Confirmed", byId["goal-confirmed"].Members["review"].GetString("result"));
            Assert.AreEqual("Confirmed", byId["penalty-not-given-confirmed"].Members["review"].GetString("result"));
            Assert.AreEqual(9, byId["goal-confirmed"].GetNumber("balanceAfter"), "the cost is charged although nothing changed"); Assert.AreEqual(1, byId["goal-confirmed"].Members["ledger"].Items.Count(e => e.GetString("kind") == "Spend"));
            Assert.AreEqual(0, byId["rejected-no-balance"].GetNumber("balanceAfter")); Assert.IsFalse(byId["rejected-no-balance"].Members["review"].Members["charged"].Bool); Assert.AreEqual("InsufficientBalance", byId["rejected-no-balance"].Members["review"].GetString("rejection"));
            Assert.AreEqual("NotEligible", byId["rejected-not-eligible"].Members["review"].GetString("rejection")); Assert.AreEqual(10, byId["rejected-not-eligible"].GetNumber("balanceAfter"));
            Assert.AreEqual(9, byId["duplicate-request"].GetNumber("balanceAfter"), "charged once"); Assert.IsFalse(byId["goal-confirmed"].Members["clockRanDuringReview"].Bool, "the clock did not run during the review");
            WebCheckOrWrite("fs27-var-scenarios.json", text);
        }

        // ================= fs27-vectors.json =================

        private static string BuildVectors()
        {
            // shoot-out counting: PenaltyShootoutState is pure, so the JavaScript copy is verified against these sequences
            var shoot = JsonValue.NewArray();
            int[] configs = { 1, 3, 5 };
            var rng = new Random(2027);
            foreach (int kicks in configs) foreach (bool sudden in new[] { true, false })
                for (int n = 0; n < 14; n++)
                {
                    var st = new PenaltyShootoutState(kicks, sudden, n % 2 == 0 ? TeamSide.Home : TeamSide.Away);
                    var seq = JsonValue.NewArray(); var states = JsonValue.NewArray();
                    for (int i = 0; i < 40 && !st.IsDecided; i++)
                    {
                        TeamSide next = st.NextTeam; bool scored = rng.NextDouble() < (n % 3 == 0 ? 0.85 : (n % 3 == 1 ? 0.5 : 0.2));
                        Assert.IsTrue(st.RecordKick(scored));
                        seq.Add(B(scored));
                        states.Add(JsonValue.NewObject().Set("team", S(next.ToString())).Set("decided", B(st.IsDecided)).Set("winner", S(st.HasWinner ? st.Winner.ToString() : "")).Set("goalsHome", N(st.Goals(TeamSide.Home))).Set("goalsAway", N(st.Goals(TeamSide.Away)))
                            .Set("takenHome", N(st.Taken(TeamSide.Home))).Set("takenAway", N(st.Taken(TeamSide.Away))).Set("nextTeam", S(st.IsDecided ? "" : st.NextTeam.ToString())).Set("suddenDeath", B(st.InSuddenDeath)).Set("round", N(st.Round)));
                    }
                    shoot.Add(JsonValue.NewObject().Set("kicksPerTeam", N(kicks)).Set("suddenDeath", B(sudden)).Set("first", S(st.FirstTeam.ToString())).Set("scored", seq).Set("states", states));
                }
            // the ZP ledger: scripted operations with the expected result of each one and the final stored document
            var ledgerScripts = JsonValue.NewArray();
            Func<string, ZenitLedger, JsonValue, JsonValue> finish = (name, l, ops) => JsonValue.NewObject().Set("name", S(name)).Set("ops", ops).Set("finalJson", Json.Parse(ZenitLedgerJson.ToJson(l)));
            {
                var l = new ZenitLedger(); var ops = JsonValue.NewArray();
                Action<string, string, int, string, string, string, ZenitResult> rec = (op, kind, amount, reason, reference, tx, r) =>
                    ops.Add(JsonValue.NewObject().Set("op", S(op)).Set("amount", N(amount)).Set("reason", S(reason)).Set("reference", S(reference)).Set("tx", S(tx)).Set("stamp", S("t"))
                        .Set("expect", JsonValue.NewObject().Set("ok", B(r.Ok)).Set("duplicate", B(r.Duplicate)).Set("failure", S(r.Failure.ToString())).Set("balanceBefore", N(r.BalanceBefore)).Set("balanceAfter", N(r.BalanceAfter))));
                Action<int, string, string, string> earn = (a, rs, rf, tx) => rec("earn", "", a, rs, rf, tx, l.TryEarn(a, rs, rf, tx, "t"));
                Action<int, string, string, string> spend = (a, rs, rf, tx) => rec("spend", "", a, rs, rf, tx, l.TrySpend(a, rs, rf, tx, "t"));
                earn(10, "Starter grant", "", "starter"); spend(1, "VAR review", "VAR:Goal:1", "req-1"); spend(1, "VAR review", "VAR:Goal:1", "req-1"); spend(50, "Shop", "item", "buy-1"); earn(0, "x", "", "zero"); spend(3, "Shop", "item-2", "buy-2");
                earn(5, "Match completed", "m1", "reward:match:m1:completed"); earn(5, "Match completed", "m1", "reward:match:m1:completed"); spend(1, "x", "", ""); spend(100000, "x", "", "huge"); earn(99999, "x", "", "cap");
                ledgerScripts.Add(finish("basic", l, ops));
            }
            var rewards = JsonValue.NewArray();
            { var l = new ZenitLedger(); var ops = JsonValue.NewArray();
              Action<string, Func<ZenitResult>> r = (name, f) => { ZenitResult x = f(); ops.Add(JsonValue.NewObject().Set("op", S(name)).Set("expect", JsonValue.NewObject().Set("ok", B(x.Ok)).Set("duplicate", B(x.Duplicate)).Set("failure", S(x.Failure.ToString())).Set("balanceAfter", N(x.BalanceAfter)))); };
              r("match:m1:true:Won", () => ZenitRewards.AwardMatchCompleted(l, "m1", true, ZenitMatchOutcome.Won, "t")); r("match:m1:true:Won", () => ZenitRewards.AwardMatchCompleted(l, "m1", true, ZenitMatchOutcome.Won, "t"));
              r("match:m2:false:Won", () => ZenitRewards.AwardMatchCompleted(l, "m2", false, ZenitMatchOutcome.Won, "t")); r("match:m3:true:Drawn", () => ZenitRewards.AwardMatchCompleted(l, "m3", true, ZenitMatchOutcome.Drawn, "t")); r("match:m4:true:Lost", () => ZenitRewards.AwardMatchCompleted(l, "m4", true, ZenitMatchOutcome.Lost, "t"));
              for (int i = 1; i <= 5; i++) { int k = i; r("training:2026-01-01:s" + k + ":true", () => ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s" + k, true, "t")); }
              r("training:2026-01-01:s1:true", () => ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s1", true, "t")); r("training:2026-01-02:s9:true", () => ZenitRewards.AwardTrainingCompleted(l, "2026-01-02", "s9", true, "t")); r("training:2026-01-02:s10:false", () => ZenitRewards.AwardTrainingCompleted(l, "2026-01-02", "s10", false, "t"));
              ledgerScripts.Add(finish("rewards", l, ops)); }
            var root = JsonValue.NewObject().Set("schema", S(WebSchema)).Set("kind", S("vectors")).Set("shootout", shoot).Set("ledger", ledgerScripts)
                .Set("zenit", JsonValue.NewObject().Set("tuning", JsonValue.NewObject().Set("starterGrant", N(ZenitDefaults.StarterGrant)).Set("rewardMatchCompleted", N(ZenitDefaults.RewardMatchCompleted)).Set("rewardMatchWon", N(ZenitDefaults.RewardMatchWon)).Set("rewardMatchDrawn", N(ZenitDefaults.RewardMatchDrawn)).Set("rewardTrainingCompleted", N(ZenitDefaults.RewardTrainingCompleted)).Set("trainingRewardsPerDay", N(ZenitDefaults.TrainingRewardsPerDay)).Set("maxBalance", N(ZenitDefaults.MaxBalance))));
            return Json.Write(root, false);
        }

        [Test]
        public void WebExport_Vectors_AreFresh()
        {
            WebCheckOrWrite("fs27-vectors.json", BuildVectors());
        }

        // ================= fs27-traces.json =================

        private static JsonValue TraceOf(GameMode mode, int seed, int homeTeam, int awayTeam, float seconds)
        {
            var frames = JsonValue.NewArray(); var events = JsonValue.NewArray(); var order = new List<MatchPlayer>(); int nextEvent = 0;
            MatchController captured = null;
            StepHook = (c, step) =>
            {
                captured = c;
                if (step == 0) { order.AddRange(c.State.Home.Players.Where(p => p.Status == MatchPlayerStatus.OnPitch).Take(6)); order.AddRange(c.State.Away.Players.Where(p => p.Status == MatchPlayerStatus.OnPitch).Take(6)); }
                var log = c.Events.Log;
                for (; nextEvent < log.Count; nextEvent++)
                {
                    GameplayEvent e = log[nextEvent];
                    if (e.Kind == GameplayEventKind.GoalScored || e.Kind == GameplayEventKind.FoulCommitted || e.Kind == GameplayEventKind.YellowCardIssued || e.Kind == GameplayEventKind.RedCardIssued || e.Kind == GameplayEventKind.PenaltyAwarded
                        || e.Kind == GameplayEventKind.ShotPerformed || e.Kind == GameplayEventKind.CornerAwarded || e.Kind == GameplayEventKind.FreeKickTaken || e.Kind == GameplayEventKind.OffsideDetected || e.Kind == GameplayEventKind.PeriodEnded || e.Kind == GameplayEventKind.HalfTimeStarted)
                        events.Add(JsonValue.NewObject().Set("t", N(e.MatchTime)).Set("kind", S(e.Kind.ToString())).Set("team", S(e.HasTeam ? e.Team.ToString() : "")).Set("player", S(Pid(e.PlayerId))).Set("x", N(e.Position.X)).Set("z", N(e.Position.Z)));
                }
                if (step % 6 != 0 || order.Count < 12) return;
                var f = JsonValue.NewArray(); Vec3 b = c.State.Ball.Physics.Position;
                f.Add(N(Math.Round(c.State.MatchTimeSeconds * 10)));
                f.Add(N(Math.Round(b.X * 100))); f.Add(N(Math.Round(b.Z * 100))); f.Add(N(Math.Round(Math.Max(0f, b.Y) * 100)));
                foreach (MatchPlayer p in order) { f.Add(N(Math.Round(p.Locomotion.Position.X * 100))); f.Add(N(Math.Round(p.Locomotion.Position.Y * 100))); }
                frames.Add(f);
            };
            try { SimulateAttack(mode, seed, true, homeTeam, awayTeam, seconds, true, null, DifficultyLevel.Professional, DifficultyLevel.Professional); }
            finally { StepHook = null; }
            Assert.IsNotNull(captured); Assert.AreEqual(12, order.Count);
            return JsonValue.NewObject().Set("mode", S(mode.ToString())).Set("seed", N(seed)).Set("home", S(Db.Teams[homeTeam].Id)).Set("away", S(Db.Teams[awayTeam].Id)).Set("seconds", N(seconds)).Set("hz", N(10))
                .Set("pitch", JsonValue.NewObject().Set("length", N(captured.Pitch.Field.Length)).Set("width", N(captured.Pitch.Field.Width)).Set("goalWidth", N(captured.Pitch.Field.GoalWidth)))
                .Set("players", Arr(order, p => S(Pid(p.PlayerId)))).Set("units", S("frame = [tenths of a second, ball x, ball z, ball height, then x,z of the 12 players (home 6, away 6)], all in centimetres of field space"))
                .Set("frames", frames).Set("events", events);
        }

        private static string BuildTraces()
        {
            var list = JsonValue.NewArray();
            list.Add(TraceOf(GameMode.Arena, 5003, 0, 3, 90f));
            list.Add(TraceOf(GameMode.Classic, 5007, 1, 4, 90f));
            var root = JsonValue.NewObject().Set("schema", S(WebSchema)).Set("kind", S("traces")).Set("traces", list)
                .Set("note", S("Grabaciones de partidos reales simulados por el Core (IA del Core en ambos equipos, dificultad Professional). Son repeticiones: la web las reproduce, no las decide."));
            return Json.Write(root, false);
        }

        [Test]
        public void WebExport_Traces_AreRealMatches_AndTheCommittedFileIsNotStale()
        {
            string text = BuildTraces();
            JsonValue v = Json.Parse(text);
            foreach (JsonValue t in v.Members["traces"].Items) { Assert.Greater(t.Members["frames"].Items.Count, 800); Assert.AreEqual(4 + 24, t.Members["frames"].Items[0].Items.Count); }
            WebCheckOrWrite("fs27-traces.json", text);
        }
    }
}
