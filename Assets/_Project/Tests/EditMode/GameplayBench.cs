using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 32 GameplayBench: a reproducible bench of attack and defence situations on the REAL Core (real teams and attributes, the real driver for all twelve players, the controller's own physics and rules). A scene only
    /// places the players and the ball (and optionally the score and the clock); everything after that is the Core playing. A recorder watches the events and the actions and writes the same metrics for every scene: passes
    /// and what became of them, losses, interceptions, shots and the chance each was worth (the calibrated xG model), crosses, progress up the pitch, time to the first shot, give-and-returns, how deep the defence stood.
    /// Every scene runs under two configurations over the same seeds: the REFERENCE (no team tactics, the keeper plays at once, no aim assist: the Phase 28 behaviour) and the CURRENT one, and the summary compares them
    /// pair by pair (same seed, same start) with a direction per metric, so that a change is judged by what it does to the scorecard and not by goals or completed passes alone.
    /// The runs are deterministic: the seeds only move the start positions sideways by a fixed amount and seed the controller's own noise; they are not independent samples of an unknown population.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        // ------------------------------------------------------------------ configurations

        internal sealed class GbConfig
        {
            public string Name; public bool Tactics, Distribution, Assist, Selection, Headers, Crosses, CrossPlay, Aerial;
            public DifficultyLevel Home = DifficultyLevel.Professional, Away = DifficultyLevel.Professional;
            public int HomeTeam = 3, AwayTeam = 8;
            public GbConfig With(string name, Action<GbConfig> edit) { var c = (GbConfig)MemberwiseClone(); c.Name = name; edit(c); return c; }
        }

        internal static readonly GbConfig GbReference = new GbConfig { Name = "ref", Tactics = false, Distribution = false, Assist = false, Selection = false, Headers = false, Crosses = false, CrossPlay = false, Aerial = false };
        internal static readonly GbConfig GbCurrent = new GbConfig { Name = "cur", Tactics = true, Distribution = true, Assist = true, Selection = true, Headers = true, Crosses = true, CrossPlay = true, Aerial = true };

        // ------------------------------------------------------------------ scenes

        internal sealed class GbScenario
        {
            public string Id, Family, Story;
            public float Seconds = 20f;
            /// <summary>Positions of the five Home and the five Away field players (Home faces +X); the goalkeepers stand in their goals.</summary>
            public Vec2[] Home, Away;
            /// <summary>Who has the ball at the start: side 0 = Home, 1 = Away, and the index in that side's array.</summary>
            public int CarrierSide, CarrierIndex;
            public float HomeKeeperX = -18f, AwayKeeperX = 18f;
            /// <summary>Optional: the score and the clock (seconds left in the period) the scene starts with: (home goals, away goals, seconds left).</summary>
            public Func<MatchController, int, bool> Extra;
            /// <summary>The scorecard: metric name -> +1 (higher is better for the Home team), -1 (lower is better). Metrics of the Away team (a_...) are listed as they are: for a defence scene, a_xg lower is better.</summary>
            public Dictionary<string, int> Judge = new Dictionary<string, int>();
            public Func<GbConfig, int, Dictionary<string, double>> Custom;
            /// <summary>Optional: a scripted PERSON for the Home team (the Home side is then human-controlled); called every step with the seed.</summary>
            public Action<MatchController, int, int> Human;
        }

        /// <summary>Phase 37: the ball is in the air already, as if a player had just lofted it (nobody holds it; the carrier of the scene is only the one who "kicked" it).</summary>
        internal static bool GbLaunch(MatchController c, Vec3 position, Vec3 velocity)
        {
            MatchBall b = c.State.Ball;
            b.HasPossession = false; b.PossessingPlayerId = null;
            b.Physics = new BallState(position, velocity) { IsGrounded = false };
            b.Mode = BallMode.InPass;
            return true;
        }

        private static float GbJitter(int seed) { return ((seed % 5) - 2) * 0.4f; }
        private static Vec2[] GbV(params float[] xy) { var r = new Vec2[xy.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = new Vec2(xy[2 * i], xy[2 * i + 1]); return r; }

        // ------------------------------------------------------------------ the recorder

        internal sealed class GbRecorder
        {
            private readonly MatchController c; private readonly float half; private readonly float t0;
            public readonly Dictionary<string, double> M = new Dictionary<string, double>();
            private sealed class Pending { public TeamSide Team; public string Passer, Target; public float At; public int Band = -1; }
            private Pending pass; private readonly ShotQualityTuning shotT = new ShotQualityTuning();
            private TeamSide lastTouchTeam; private string t1, t2; private float time1, time2;
            private float lastGainAt; private bool lostOnce, regained; private float firstLossAt = -1f, regainAt = -1f;
            public float StartDepth; public double MaxDepthHome, MaxDepthAway;
            public bool Stop;
            private string lastCategory = "outOther"; private float lastActionAt = -100f;
            private TeamSide restartTeam; private string restartKind; private float restartAt = -100f;

            public GbRecorder(MatchController c) { this.c = c; half = c.Pitch.Field.Length / 2f; t0 = c.State.MatchTimeSeconds; }
            private double Add(string k, double v = 1.0) { M.TryGetValue(k, out double cur); M[k] = cur + v; return cur + v; }
            private float Now { get { return c.State.MatchTimeSeconds - t0; } }
            private float Sign(TeamSide s) { return (s == TeamSide.Home) == c.State.HomeAttacksPositiveX ? 1f : -1f; }
            private static string P(TeamSide s) { return s == TeamSide.Home ? "h_" : "a_"; }

            public void Attach()
            {
                float bx0 = c.State.Ball.Physics.Position.X;
                StartDepth = bx0 * Sign(TeamSide.Home) + half; StartDepthAway = bx0 * Sign(TeamSide.Away) + half; MaxDepthHome = StartDepth; MaxDepthAway = StartDepthAway;
                c.ActionPerformed += OnAction;
                c.Events.Published += OnEvent;
                // a scene that starts with the rival holding the ball is a ball already lost: the regain time runs from the start
                if (c.BallCarrier != null && c.BallCarrier.Team == TeamSide.Away) { lostOnce = true; firstLossAt = 0f; }
            }

            private void OnAction(ActionResult res)
            {
                if (res.Outcome == ActionOutcome.Rejected) return;
                MatchPlayer p = c.State.Team(res.Team).Find(res.PlayerId); if (p == null) return;
                string pre = P(res.Team);
                lastCategory = res.Kind == FootballActionKind.Cross || res.Kind == FootballActionKind.LongPass || res.Kind == FootballActionKind.ShortPass || res.Kind == FootballActionKind.ThroughPass || res.Kind == FootballActionKind.WallPass ? "outPass"
                    : res.Kind == FootballActionKind.Shot || res.Kind == FootballActionKind.PlacedShot || res.Kind == FootballActionKind.CurvedShot || res.Kind == FootballActionKind.Chip ? "outShot"
                    : res.Kind == FootballActionKind.GoalkeeperDistribution ? "outKeeper" : res.Kind == FootballActionKind.GoalkeeperSave || res.Kind == FootballActionKind.GoalkeeperCatch ? "outSave" : "outOther";
                lastActionAt = Now;
                if (res.Kind == FootballActionKind.Cross) Add(pre + "crossKicks");
                if (res.Detail == ActionDetail.Volley) Add(pre + "volleys");
                if (res.Detail == ActionDetail.Punched) Add("keeperPunches");
                if (res.Kind == FootballActionKind.Header) { Add(pre + "headers"); if (res.Team == crossActionTeam && Now - crossActionAt <= 4f) Add(pre + "crossHeaders"); }
                if ((res.Kind == FootballActionKind.GoalkeeperSave || res.Kind == FootballActionKind.GoalkeeperCatch) && !lastShotCounted && res.Team != lastShotTeam && Now - lastShotAt <= 2.5f) { Add(P(lastShotTeam) + "shotsOnTarget"); lastShotCounted = true; }
                if (res.Kind == FootballActionKind.GoalkeeperSave) Add("keeperParries"); else if (res.Kind == FootballActionKind.GoalkeeperCatch) Add("keeperCatches");
                switch (res.Kind)
                {
                    case FootballActionKind.ShortPass: case FootballActionKind.LongPass: case FootballActionKind.Cross: case FootballActionKind.ThroughPass: case FootballActionKind.WallPass: case FootballActionKind.GoalkeeperDistribution: case FootballActionKind.SkillLobPass:
                    {
                        Add(pre + "passes");
                        float fwd = res.Direction.X * Sign(res.Team); if (fwd > 0.5f) Add(pre + "forwardPasses");
                        Vec2 pos = p.Locomotion.Position; float depth = pos.X * Sign(res.Team) + half;
                        if (depth >= c.Pitch.Field.Length * 2f / 3f) Add(pre + "finalThirdPasses");
                        // a cross: from the flank in the final third, towards the middle
                        if (!p.InGoal && Math.Abs(pos.Y) >= 6.5f && depth >= c.Pitch.Field.Length - 12f && Math.Abs(res.Direction.Y) > 0.25f && Math.Sign(res.Direction.Y) != Math.Sign(pos.Y)) { Add(pre + "crosses"); crossAt = Now; crossTeam = res.Team; crossPasser = res.PlayerId; crossCounted = false; }
                        if (res.Kind == FootballActionKind.Cross) { Add(pre + "cross" + res.CrossStyle); crossActionAt = Now; crossActionTeam = res.Team; if (!(Math.Abs(pos.Y) >= 6.5f && depth >= c.Pitch.Field.Length - 12f)) { crossAt = Now; crossTeam = res.Team; crossPasser = res.PlayerId; crossCounted = false; Add(pre + "crosses"); } }
                        pass = new Pending { Team = res.Team, Passer = res.PlayerId, Target = res.TargetPlayerId, At = Now, Band = LaneBand(res, p) };
                        break;
                    }
                    case FootballActionKind.Shot: case FootballActionKind.PlacedShot: case FootballActionKind.CurvedShot: case FootballActionKind.Chip:
                        RegisterShot(res, p);
                        break;
                }
            }

            private float crossActionAt = -100f, lastShotAt = -100f, goalWatchAt = -100f; private TeamSide crossActionTeam, lastShotTeam; private bool lastShotCounted = true;
            private float crossAt = -100f; private TeamSide crossTeam; private string crossPasser; private bool crossCounted;

            private void RegisterShot(ActionResult res, MatchPlayer p)
            {
                string pre = P(res.Team);
                Vec2 goal = c.Pitch.OpponentGoalCentre(c.State.HomeAttacksPositiveX, res.Team);
                var ctx = new ShotContext { Shooter = p.Locomotion.Position, GoalCentre = goal, GoalWidth = c.Pitch.Field.GoalWidth, BallVelocity = new Vec2(res.BallVelocity.X, res.BallVelocity.Z), NearestDefender = float.MaxValue };
                Vec2 toGoal = goal - ctx.Shooter; float len = toGoal.Magnitude;
                foreach (MatchPlayer o in c.State.Team(res.Team.Opponent()).Players)
                {
                    if (!o.IsActive) continue;
                    if (o.InGoal) { ctx.Keeper = o.Locomotion.Position; ctx.HasKeeper = true; continue; }
                    float d = (o.Locomotion.Position - ctx.Shooter).Magnitude; if (d < ctx.NearestDefender) ctx.NearestDefender = d;
                    if (len > 1e-3f && WorldGeometry.DistanceToSegment(o.Locomotion.Position, ctx.Shooter, goal) <= shotT.LaneHalfWidth) ctx.DefendersInLane++;
                }
                ShotQuality q = ShotQualityEvaluator.Evaluate(ctx, shotT);
                lastShotAt = Now; lastShotTeam = res.Team; lastShotCounted = false;
                Add(pre + "shots"); Add(pre + "xg", q.ExpectedGoal); if (q.ExpectedGoal >= 0.25f) Add(pre + "bigChances");
                if (!M.ContainsKey(pre + "firstShotAt")) M[pre + "firstShotAt"] = Now;
                if (res.Team == crossTeam && Now - crossAt <= 3f && !crossCounted) { Add(pre + "crossShots"); crossCounted = true; }
            }

            private void Restart(string kind, GameplayEvent ev) { Add(kind + "s"); if (ev.HasTeam) { restartKind = kind; restartTeam = ev.Team; restartAt = Now; } }

            private void OnEvent(GameplayEvent ev)
            {
                switch (ev.Kind)
                {
                    case GameplayEventKind.GoalScored: if (ev.HasTeam) { Add(P(ev.Team) + "goals"); if (!lastShotCounted && ev.Team == lastShotTeam && Now - lastShotAt <= 2.5f) { Add(P(ev.Team) + "shotsOnTarget"); lastShotCounted = true; } if (ev.Team == crossActionTeam && Now - crossActionAt <= 6f) Add(P(ev.Team) + "crossGoals"); Stop = true; } break;
                    case GameplayEventKind.InterceptionPerformed: if (ev.HasTeam) Add(P(ev.Team) + "interceptions"); break;
                    case GameplayEventKind.TacklePerformed: if (ev.HasTeam) Add(P(ev.Team) + "tackles"); break;
                    case GameplayEventKind.FoulCommitted: if (ev.HasTeam) Add(P(ev.Team) + "fouls"); break;
                    case GameplayEventKind.BallOutOfBounds: Add(Now - lastActionAt <= 4f ? lastCategory : "outDeflection"); break;
                    case GameplayEventKind.ThrowInAwarded: Restart("throwIn", ev); break;
                    case GameplayEventKind.CornerAwarded: Restart("corner", ev); break;
                    case GameplayEventKind.GoalKickAwarded: Restart("goalKick", ev); break;
                    case GameplayEventKind.FreeKickAwarded: Restart("freeKick", ev); break;
                    case GameplayEventKind.PossessionChanged:
                        if (!ev.HasTeam) break;
                        if (restartKind != null && Now - restartAt <= 6f) { if (ev.Team != restartTeam) Add(restartKind + "sLost"); restartKind = null; }
                        Add(P(ev.Team) + "gains");
                        if (ev.Team == TeamSide.Away && !lostOnce) { lostOnce = true; firstLossAt = Now; }
                        if (ev.Team == TeamSide.Home && lostOnce && !regained) { regained = true; regainAt = Now; }
                        break;
                    case GameplayEventKind.BallTouched:
                        if (!ev.HasTeam || ev.PlayerId == null) break;
                        ResolvePass(ev);
                        if (ev.PlayerId != t2) { if (t1 != null && ev.PlayerId == t1 && t2 != null && Now - time1 <= 3.5f && time2 - time1 <= 3.5f && ev.Team == lastTouchTeam) Add(P(ev.Team) + "giveAndReturn"); t1 = t2; time1 = time2; t2 = ev.PlayerId; time2 = Now; lastTouchTeam = ev.Team; }
                        if (ev.Team == crossTeam && Now - crossAt <= 3f && ev.PlayerId != crossPasser && Math.Abs(ev.Position.Z) <= 5.5f && Math.Abs(ev.Position.X) >= half - 8f) { Add(P(ev.Team) + "crossReach"); crossAt = -100f; }
                        break;
                }
            }

            /// <summary>How tight the lane of a pass to a named team-mate was at the moment of the kick: the nearest rival to the segment, in four bands (0: under 1.5 m, 1: to 3 m, 2: to 5 m, 3: wider).</summary>
            private int LaneBand(ActionResult res, MatchPlayer passer)
            {
                if (res.TargetPlayerId == null) return -1;
                MatchPlayer target = c.State.Team(res.Team).Find(res.TargetPlayerId); if (target == null) return -1;
                Vec2 a = passer.Locomotion.Position, b = target.Locomotion.Position; float best = float.MaxValue;
                foreach (MatchPlayer r in c.State.Team(res.Team.Opponent()).Players) { if (!r.IsActive || r.InGoal) continue; best = Math.Min(best, WorldGeometry.DistanceToSegment(r.Locomotion.Position, a, b)); }
                return best < 1.5f ? 0 : best < 3f ? 1 : best < 5f ? 2 : 3;
            }

            private void ResolvePass(GameplayEvent ev)
            {
                if (pass == null) return;
                float since = Now - pass.At;
                if (ev.PlayerId == pass.Passer && since < 0.4f) return;
                string pre = P(pass.Team);
                if (since > 3.5f) { Add(pre + "passNobody"); pass = null; return; }
                if (pass.Band >= 0) { Add(pre + "laneN" + pass.Band); if (ev.Team != pass.Team) Add(pre + "laneLost" + pass.Band); }
                if (ev.Team == pass.Team) { Add(pre + "passOk"); if (pass.Target != null && ev.PlayerId == pass.Target) Add(pre + "passToTarget"); } else Add(pre + "passLost");
                pass = null;
            }

            private double looseRun; private int samples, homeHas, depthSamples; private double depthSum, shapeSum; private int shapeSamples;
            public void Sample()
            {
                if (c.State.Phase != MatchPhase.Playing) return;
                samples++;
                bool has = c.State.Ball.HasPossession; TeamSide owner = c.State.Ball.PossessingTeam;
                if (has && owner == TeamSide.Home) homeHas++;
                // anomalies: a loose ball that nobody plays, a carrier who stands still with nobody near him
                if (!has && c.State.Ball.Physics.Speed < 0.4f) { looseRun += 0.25; Add("looseStillSeconds", 0.25); if (looseRun > (M.ContainsKey("looseStillMax") ? M["looseStillMax"] : 0)) M["looseStillMax"] = looseRun; } else looseRun = 0;
                if (has) { MatchPlayer cc = c.BallCarrier; if (cc != null && !cc.InGoal && cc.Locomotion.CurrentSpeed < 0.3f) { float near = float.MaxValue; foreach (MatchPlayer r in c.State.Team(cc.Team.Opponent()).Players) if (r.IsActive) near = Math.Min(near, (r.Locomotion.Position - cc.Locomotion.Position).Magnitude); if (near > 3f) Add("carrierIdleSeconds", 0.25); } }
                float bx = c.State.Ball.Physics.Position.X;
                double dHome = bx * Sign(TeamSide.Home) + half, dAway = bx * Sign(TeamSide.Away) + half;
                MaxDepthHome = Math.Max(MaxDepthHome, dHome); MaxDepthAway = Math.Max(MaxDepthAway, dAway);
                if (has && owner == TeamSide.Away)
                {
                    foreach (MatchPlayer p in c.State.Home.Players) { if (!p.IsActive || p.InGoal) continue; depthSum += p.Locomotion.Position.X * Sign(TeamSide.Home) + half; depthSamples++; }
                    MatchPlayer carrier = c.BallCarrier;
                    if (carrier != null) { float best = float.MaxValue; foreach (MatchPlayer p in c.State.Home.Players) if (p.IsActive && !p.InGoal) best = Math.Min(best, (p.Locomotion.Position - carrier.Locomotion.Position).Magnitude); if (best < float.MaxValue) { shapeSum += best; shapeSamples++; } }
                }
            }

            public Dictionary<string, double> Finish(float seconds)
            {
                foreach (string pre in new[] { "h_", "a_" })
                {
                    double passes = M.ContainsKey(pre + "passes") ? M[pre + "passes"] : 0, ok = M.ContainsKey(pre + "passOk") ? M[pre + "passOk"] : 0, lost = M.ContainsKey(pre + "passLost") ? M[pre + "passLost"] : 0;
                    M[pre + "passOkRate"] = passes > 0 ? ok / passes : double.NaN; M[pre + "passLostRate"] = passes > 0 ? lost / passes : double.NaN;
                    double tgt = M.ContainsKey(pre + "passToTarget") ? M[pre + "passToTarget"] : 0; M[pre + "passToTargetRate"] = passes > 0 ? tgt / passes : double.NaN;
                }
                M["progress"] = MaxDepthHome - StartDepth; M["progressAway"] = MaxDepthAway - StartDepthAway;
                M["possession"] = samples > 0 ? (double)homeHas / samples : double.NaN;
                M["defDepth"] = depthSamples > 0 ? depthSum / depthSamples : double.NaN;
                M["nearestToCarrier"] = shapeSamples > 0 ? shapeSum / shapeSamples : double.NaN;
                M["firstLossAt"] = firstLossAt >= 0 ? firstLossAt : seconds;
                M["regainSeconds"] = !lostOnce ? double.NaN : (regained ? regainAt - firstLossAt : seconds - firstLossAt);
                if (!M.ContainsKey("h_firstShotAt")) M["h_firstShotAt"] = seconds; if (!M.ContainsKey("a_firstShotAt")) M["a_firstShotAt"] = seconds;
                foreach (string k in AllKeys) if (!M.ContainsKey(k)) M[k] = 0.0;
                foreach (string k in DerivedKeys) if (!M.ContainsKey(k)) M[k] = double.NaN;   // metrics of other scenes
                return M;
            }
            public float StartDepthAway;
        }

        internal static readonly string[] AllKeys =
        {
            "h_passes", "h_passOk", "h_passLost", "h_passNobody", "h_passToTarget", "h_forwardPasses", "h_finalThirdPasses", "h_crosses", "h_crossReach", "h_crossShots", "h_shots", "h_xg", "h_bigChances", "h_goals", "h_interceptions", "h_tackles", "h_fouls", "h_gains", "h_giveAndReturn",
            "a_passes", "a_passOk", "a_passLost", "a_passNobody", "a_forwardPasses", "a_finalThirdPasses", "a_crosses", "a_crossReach", "a_crossShots", "a_shots", "a_xg", "a_bigChances", "a_goals", "a_interceptions", "a_tackles", "a_fouls", "a_gains", "a_giveAndReturn",
            "throwIns", "throwInsLost", "corners", "cornersLost", "goalKicks", "goalKicksLost", "freeKicks", "freeKicksLost",
            "outPass", "outShot", "outKeeper", "outSave", "outOther", "outDeflection", "h_crossKicks", "a_crossKicks", "keeperParries", "keeperCatches", "h_headers", "a_headers", "h_crossHigh", "h_crossDriven", "h_crossGround", "a_crossHigh", "a_crossDriven", "a_crossGround", "h_crossHeaders", "a_crossHeaders", "h_shotsOnTarget", "a_shotsOnTarget", "h_crossGoals", "a_crossGoals", "h_volleys", "a_volleys", "keeperPunches",
            "looseStillSeconds", "looseStillMax", "carrierIdleSeconds", "h_laneN0", "h_laneN1", "h_laneN2", "h_laneN3", "h_laneLost0", "h_laneLost1", "h_laneLost2", "h_laneLost3", "a_laneN0", "a_laneN1", "a_laneN2", "a_laneN3", "a_laneLost0", "a_laneLost1", "a_laneLost2", "a_laneLost3"
        };

        internal static readonly string[] DerivedKeys = { "h_passOkRate", "h_passLostRate", "h_passToTargetRate", "a_passOkRate", "a_passLostRate", "a_passToTargetRate", "progress", "progressAway", "possession", "defDepth", "nearestToCarrier", "firstLossAt", "regainSeconds", "h_firstShotAt", "a_firstShotAt", "selCarrier", "selNearestWhenDefending", "selFar", "selSwitchesPerMinute", "selDefSwitchesPerMinute", "selFlickerPerMinute", "manualKept" };

        // ------------------------------------------------------------------ the run

        internal static MatchController GbBuild(GbConfig cfg, int seed, out BasicDecisionDriver driver, GameMode mode = GameMode.Classic)
        {
            MatchController c = RealRoles(cfg.HomeTeam, cfg.AwayTeam, seed, mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            GoalkeeperAiTuning keeperT = GoalkeeperAiTuning.ForMode(mode); keeperT.DistributionEnabled = cfg.Distribution;
            BasicDecisionTuning basicT = BasicDecisionTuning.ForMode(mode); DefenseTuning defenseT = DefenseTuning.ForMode(mode); AttackPositioningTuning attackT = AttackPositioningTuning.ForMode(mode); BallCarrierTuning carrierT = BallCarrierTuning.ForMode(mode);
            var profileT = new DecisionProfileTuning();
            ApplyExploration(mode, basicT, defenseT, attackT, carrierT, c.ActionTuning, profileT, keeperT);
            driver = new BasicDecisionDriver(c, basicT, defenseT, null, attackT, carrierT, keeperT);
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(cfg.Home), Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(cfg.Away), Db.Players, c, profileT);
            if (cfg.Tactics) { driver.SetTactic(TeamSide.Home, TeamTactic.Of(Db.Teams[cfg.HomeTeam], Db)); driver.SetTactic(TeamSide.Away, TeamTactic.Of(Db.Teams[cfg.AwayTeam], Db)); }
            c.AimAssist.Enabled = cfg.Assist; driver.SelectionTuning.Enabled = cfg.Selection; c.HeadersEnabled = cfg.Headers; driver.CrossActions = cfg.Crosses; driver.CrossMechanics = cfg.CrossPlay; driver.AerialPlay = cfg.Aerial; c.AerialRulesEnabled = cfg.Aerial;
            return c;
        }

        internal static Dictionary<string, double> GbRun(GbScenario s, GbConfig cfg, int seed)
        {
            if (s.Custom != null) return s.Custom(cfg, seed);
            MatchController c = GbBuild(cfg, seed, out BasicDecisionDriver driver);
            List<MatchPlayer> hf = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).ToList(), af = c.State.Away.Players.Where(p => p.IsActive && !p.InGoal).ToList();
            MatchPlayer hk = c.State.Home.Players.First(p => p.IsActive && p.InGoal), ak = c.State.Away.Players.First(p => p.IsActive && p.InGoal);
            float j = GbJitter(seed);
            for (int i = 0; i < 5; i++)
            {
                Vec2 h = s.Home[i], a = s.Away[i];
                c.PlacePlayer(TeamSide.Home, hf[i].PlayerId, new Vec2(h.X, h.Y + (s.CarrierSide == 0 && i == s.CarrierIndex ? j : j * 0.5f)), East.ToHeading());
                c.PlacePlayer(TeamSide.Away, af[i].PlayerId, new Vec2(a.X, a.Y - (s.CarrierSide == 1 && i == s.CarrierIndex ? j : j * 0.5f)), West.ToHeading());
            }
            c.PlacePlayer(TeamSide.Home, hk.PlayerId, new Vec2(s.HomeKeeperX, 0f), East.ToHeading()); c.PlacePlayer(TeamSide.Away, ak.PlayerId, new Vec2(s.AwayKeeperX, 0f), West.ToHeading());
            if (s.CarrierSide == 0) { Vec2 p = s.Home[s.CarrierIndex]; Carrier(c, TeamSide.Home, hf[s.CarrierIndex].PlayerId, p.X, p.Y + j, East); }
            else { Vec2 p = s.Away[s.CarrierIndex]; Carrier(c, TeamSide.Away, af[s.CarrierIndex].PlayerId, p.X, p.Y - j, West); }
            if (s.Extra != null) Assert.IsTrue(s.Extra(c, seed), s.Id + ": the scene could not be set up");
            if (s.Human != null) c.SetControl(TeamSide.Home, ControlSource.Human);
            var rec = new GbRecorder(c);
            rec.Attach();
            int steps = (int)(s.Seconds / Dt);
            for (int step = 0; step < steps && !rec.Stop; step++)
            {
                driver.Step(c);
                if (s.Human != null && c.State.Phase == MatchPhase.Playing) s.Human(c, seed, step);
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (step % 15 == 0) rec.Sample();
            }
            return rec.Finish(s.Seconds);
        }

        // ------------------------------------------------------------------ the summary

        internal sealed class GbRow { public string Scenario, Family, Variant, Config; public int Seed; public Dictionary<string, double> M; }

        internal static string GbCsv(IEnumerable<GbRow> rows)
        {
            string[] keys = AllKeys.Concat(DerivedKeys).ToArray();
            var sb = new StringBuilder("scenario,family,variant,config,seed," + string.Join(",", keys) + "\n");
            foreach (GbRow r in rows)
                sb.Append(r.Scenario).Append(',').Append(r.Family).Append(',').Append(r.Variant).Append(',').Append(r.Config).Append(',').Append(r.Seed).Append(',')
                  .Append(string.Join(",", keys.Select(k => double.IsNaN(r.M[k]) ? "NA" : r.M[k].ToString("0.####", CultureInfo.InvariantCulture)))).Append('\n');
            return sb.ToString();
        }

        /// <summary>The paired comparison of the current configuration with the reference, per scene: the mean of each scorecard metric with and without the changes, the paired difference with its standard error, and a verdict.</summary>
        internal static string GbCompare(IList<GbScenario> scenes, IList<GbRow> rows, string reference, string current, out int improved, out int regressed)
        {
            improved = 0; regressed = 0; var sb = new StringBuilder();
            foreach (GbScenario s in scenes)
            {
                var refRows = rows.Where(r => r.Scenario == s.Id && r.Config == reference).OrderBy(r => r.Seed).ToList(); var curRows = rows.Where(r => r.Scenario == s.Id && r.Config == current).OrderBy(r => r.Seed).ToList();
                if (refRows.Count == 0 || curRows.Count == 0) continue;
                sb.Append("### ").Append(s.Id).Append(" — ").Append(s.Story).Append(" (").Append(refRows.Count).Append(" seeds, ").Append(s.Seconds).Append(" s)\n\n| metric | judged | ").Append(reference).Append(" | ").Append(current).Append(" | Δ ± SE | verdict |\n|---|---|---|---|---|---|\n");
                foreach (var kv in s.Judge)
                {
                    string key = kv.Key;
                    var d = new List<double>(); var a = new List<double>(); var b = new List<double>();
                    for (int i = 0; i < Math.Min(refRows.Count, curRows.Count); i++) { double x = refRows[i].M[key], y = curRows[i].M[key]; if (double.IsNaN(x) || double.IsNaN(y)) continue; a.Add(x); b.Add(y); d.Add(y - x); }
                    if (d.Count == 0) continue;
                    double mean = d.Average(), se = d.Count > 1 ? Math.Sqrt(d.Sum(v => (v - mean) * (v - mean)) / (d.Count - 1) / d.Count) : 0, floor = Math.Max(0.1 * Math.Abs(a.Average()), 0.05);
                    string verdict = "flat"; double signed = mean * kv.Value;
                    if (signed > Math.Max(2 * se, floor)) { verdict = "**better**"; improved++; } else if (signed < -Math.Max(2 * se, floor)) { verdict = "**WORSE**"; regressed++; }
                    sb.Append("| ").Append(key).Append(" | ").Append(kv.Value > 0 ? "higher is better" : "lower is better").Append(" | ").Append(a.Average().ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ").Append(b.Average().ToString("0.00", CultureInfo.InvariantCulture))
                      .Append(" | ").Append(mean.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture)).Append(" ± ").Append(se.ToString("0.00", CultureInfo.InvariantCulture)).Append(" | ").Append(verdict).Append(" |\n");
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
