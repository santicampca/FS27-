using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 32: the scenes of the GameplayBench (see GameplayBench.cs). Home attacks +X; every scene lists the five Home and the five Away field players, who has the ball and what is judged.</summary>
    public partial class AiInfrastructureTests
    {
        private static Dictionary<string, int> J(params object[] kv) { var d = new Dictionary<string, int>(); for (int i = 0; i < kv.Length; i += 2) d[(string)kv[i]] = (int)kv[i + 1]; return d; }

        private static float NoiseDegrees(int seed, int salt, float amplitude) { return (float)(new Random(seed * 7919 + salt).NextDouble() * 2.0 - 1.0) * amplitude; }

        internal static List<GbScenario> GbScenes()
        {
            var list = new List<GbScenario>
            {
                // ---------------- attack
                new GbScenario { Id = "A1_BuildUp", Family = "attack", Story = "a centre-back builds from his own half against a mid block", Seconds = 25f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(-12, 0, -9, -7, -9, 7, -4, 0, 0, -4), Away = GbV(-3, -6, -3, 6, 1, 0, 5, -4, 5, 4),
                    Judge = J("progress", 1, "h_passOkRate", 1, "h_passLostRate", -1, "h_xg", 1, "a_xg", -1) },
                new GbScenario { Id = "A2_WingPlay", Family = "attack", Story = "a winger with the ball on the flank in the final third: do crosses come, arrive, and become shots", Seconds = 20f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(6, -10, 2, -3, 4, 2, -4, -6, -8, 0), Away = GbV(10, -7, 11, -1, 12, 4, 5, 3, 2, -1),
                    Judge = J("h_crosses", 1, "h_crossReach", 1, "h_crossShots", 1, "h_xg", 1, "h_passLostRate", -1, "a_xg", -1) },
                new GbScenario { Id = "A3_Combination", Family = "attack", Story = "a carrier with a team-mate close and a defender pressing: give-and-return or loss", Seconds = 15f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(-2, 0, 4, 3, 4, -4, -8, 5, -8, -5), Away = GbV(-0.3f, 1.2f, 4, 6, 6, -2, 8, 2, 8, -6),
                    Judge = J("h_giveAndReturn", 1, "h_passLostRate", -1, "progress", 1, "h_xg", 1) },
                new GbScenario { Id = "A4_RunsOffTheBall", Family = "attack", Story = "a carrier in midfield with two team-mates who can run in behind a line of five", Seconds = 20f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(-4, 0, 2, -8, 2, 8, -9, 3, -9, -3), Away = GbV(4, -4, 4, 4, 7, 0, 9, -7, 9, 7),
                    Judge = J("progress", 1, "h_forwardPasses", 1, "h_passOkRate", 1, "h_xg", 1, "a_xg", -1) },
                new GbScenario { Id = "A5_FinalThird", Family = "chances", Story = "the carrier at the edge of the box with options: does he create and take a good chance", Seconds = 15f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(10, 3, 12, -4, 8, -1, 2, 5, 2, -5), Away = GbV(14, 1, 13, -2, 11, 6, 6, 0, 5, -4),
                    Judge = J("h_shots", 1, "h_xg", 1, "h_bigChances", 1, "h_firstShotAt", -1, "h_passLostRate", -1) },
                // ---------------- pressure and transitions
                new GbScenario { Id = "B1_UnderPressure", Family = "pressure", Story = "the carrier is closed down at 1.5 m and 3.8 m with outlets behind and beside him", Seconds = 12f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(-6, 0, -10, -5, -10, 5, -2, -7, -2, 7), Away = GbV(-4.2f, 0.8f, -3, -2.5f, 0, 0, 2, -5, 2, 5),
                    Judge = J("h_passLostRate", -1, "firstLossAt", 1, "h_passOkRate", 1, "a_xg", -1) },
                new GbScenario { Id = "B2_TransitionDefence", Family = "transition", Story = "the rival has just won the ball in the home half while the home team is up the pitch: how fast it recovers and how much it concedes", Seconds = 10f, CarrierSide = 1, CarrierIndex = 0,
                    Home = GbV(8, -3, 6, 4, 1, -6, -1, 5, 3, 0), Away = GbV(-4, 0, -8, -5, -7, 5, 0, 2, 2, -3),
                    Judge = J("a_xg", -1, "a_shots", -1, "a_firstShotAt", 1, "regainSeconds", -1, "h_interceptions", 1) },
                new GbScenario { Id = "B3_TransitionAttack", Family = "transition", Story = "the home team wins the ball with the rival pushed up and two runners already beyond its line: does it use the space", Seconds = 12f, CarrierSide = 0, CarrierIndex = 0,
                    Home = GbV(-5, 3, 3, -8, 5, 8, -9, -2, -11, 4), Away = GbV(-1, -4, 1, 3, -2, 7, 2, -1, 8, 0),
                    Judge = J("h_xg", 1, "h_shots", 1, "progress", 1, "a_xg", -1, "h_firstShotAt", -1) },
                // ---------------- defence
                new GbScenario { Id = "C1_PressingBuildUp", Family = "defence", Story = "the rival builds from its own goal against a pressing home team", Seconds = 15f, CarrierSide = 1, CarrierIndex = 0,
                    Home = GbV(10, -2, 9, 3, 5, -6, 3, 4, -1, 0), Away = GbV(15, 0, 12, -6, 12, 6, 8, 0, 4, -3),
                    Judge = J("h_interceptions", 1, "a_passLostRate", 1, "progressAway", -1, "a_xg", -1, "regainSeconds", -1) },
                new GbScenario { Id = "C2_CompactDefence", Family = "defence", Story = "a rival carrier with three runners against four home defenders near their own box", Seconds = 14f, CarrierSide = 1, CarrierIndex = 0,
                    Home = GbV(-13, 0, -11, 3, -11, -3, -7, 0, -5, -5), Away = GbV(-3, 4, -5, -4, -1, -1, -8, 7, 2, 2),
                    Judge = J("a_xg", -1, "a_shots", -1, "a_goals", -1, "h_interceptions", 1, "progressAway", -1) },
                new GbScenario { Id = "C3_WingDefence", Family = "defence", Story = "a rival winger runs down the flank with a runner at the far post", Seconds = 14f, CarrierSide = 1, CarrierIndex = 0,
                    Home = GbV(-12, -6, -10, 0, -14, 3, -6, -2, -5, 5), Away = GbV(-7, -9, -13, -2, -9, 3, -3, 0, -1, -6),
                    Judge = J("a_crosses", -1, "a_crossReach", -1, "a_xg", -1, "h_interceptions", 1) },
                new GbScenario { Id = "C4_CoverSecond", Family = "defence", Story = "a rival carrier against two defenders, primary and cover, with room behind", Seconds = 12f, CarrierSide = 1, CarrierIndex = 0,
                    Home = GbV(-11, 0.8f, -12.5f, -2.5f, -16, 4, -5, -8, -2, 6), Away = GbV(-8, 0, -4, -6, -4, 5, 2, 0, 5, 3),
                    Judge = J("a_xg", -1, "a_shots", -1, "progressAway", -1, "h_interceptions", 1) },
            };
            // ---------------- the goalkeeper (the existing ScenarioBench scenes, through the same scorecard)
            foreach (string id in new[] { "S2_CoveredReceiver", "S4_Rival7m" })
            {
                string scene = id;
                list.Add(new GbScenario
                {
                    Id = "K_" + scene, Family = "keeper", Story = "the keeper holds the ball (" + scene + "): distribution on or off", Seconds = 9f,
                    Judge = J("a_goals", -1, "h_passOkRate", 1, "firstLossAt", 1),
                    Custom = (cfg, seed) =>
                    {
                        BenchVariant v = BenchVariants().First(x => x.Scenario == scene);
                        string[] row = BenchRun(v, seed, cfg.Distribution).Split(',');
                        var m = AllKeys.Concat(DerivedKeys).ToDictionary(k => k, k => 0.0);
                        m["a_goals"] = BenchCell(row, "goalBy") == "away" ? 1 : 0; m["h_goals"] = BenchCell(row, "goalBy") == "home" ? 1 : 0;
                        string r3 = BenchCell(row, "retainedAt3s"); m["h_passOkRate"] = r3 == "yes" ? 1 : 0;
                        m["firstLossAt"] = BenchCell(row, "released") == "yes" ? double.Parse(BenchCell(row, "releaseSeconds"), CultureInfo.InvariantCulture) : 9;
                        foreach (string k in DerivedKeys) if (!new[] { "h_passOkRate", "firstLossAt" }.Contains(k)) m[k] = double.NaN;
                        return m;
                    }
                });
            }
            list.Add(new GbScenario
            {
                Id = "K_GoalKick", Family = "keeper", Story = "a goal kick for the home team against a pressing rival (real restart procedure)", Seconds = 12f,
                Judge = J("h_passOkRate", 1, "h_passLostRate", -1, "a_xg", -1, "a_shots", -1),
                Custom = (cfg, seed) =>
                {
                    MatchController c = GbBuild(cfg, seed, out BasicDecisionDriver driver);
                    SendBallOut(c, -20.3f, (seed % 2 == 0 ? 1f : -1f) * (5f + (seed % 5)), TeamSide.Away);
                    Assert.AreEqual(RestartKind.GoalKick, c.State.PendingRestart.Kind);
                    Assert.IsTrue(c.ConfirmRestart());
                    var rec = new GbRecorder(c); rec.Attach();
                    for (int step = 0; step < (int)(12f / Dt) && !rec.Stop; step++) { driver.Step(c); c.Advance(Dt); if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); if (step % 15 == 0) rec.Sample(); }
                    return rec.Finish(12f);
                }
            });
            // ---------------- the person and the aim assistance
            list.Add(new GbScenario
            {
                Id = "H1_AssistPasses", Family = "human", Story = "a scripted person passes with a seeded aiming error of up to 25 degrees (the configurations differ only in the aim assistance)", Seconds = 120f,
                Judge = J("h_passToTargetRate", 1, "h_passLostRate", -1),
                Custom = (cfg, seed) =>
                {
                    PassOutcomes o = PlayWithAScriptedPerson(seed, cfg.Assist, 25f, 120f);
                    var m = AllKeys.Concat(DerivedKeys).ToDictionary(k => k, k => double.NaN);
                    foreach (string k in AllKeys) m[k] = 0;
                    m["h_passes"] = o.Passes; m["h_passOk"] = o.ToIntended + o.ToTeammate; m["h_passToTarget"] = o.ToIntended; m["h_passLost"] = o.Lost; m["h_shots"] = o.Shots; m["h_goals"] = o.Goals;
                    m["h_passOkRate"] = o.Passes > 0 ? (double)(o.ToIntended + o.ToTeammate) / o.Passes : double.NaN; m["h_passLostRate"] = o.Passes > 0 ? (double)o.Lost / o.Passes : double.NaN; m["h_passToTargetRate"] = o.Passes > 0 ? (double)o.ToIntended / o.Passes : double.NaN;
                    return m;
                }
            });
            list.Add(new GbScenario
            {
                Id = "H2_AssistShots", Family = "human", Story = "a scripted person shoots from the edge of the box with an aiming error of up to 30 degrees", Seconds = 5f, CarrierSide = 0, CarrierIndex = 0,
                Home = GbV(10, 2, 2, 6, 2, -6, -4, 0, -8, 3), Away = GbV(13, -1, 12, 4, 7, 0, 5, -5, 4, 5),
                Judge = J("h_xg", 1, "h_goals", 1, "h_shots", 1),
                Human = (c, seed, step) =>
                {
                    if (step != 20) return;   // a third of a second after the start: one shot
                    string id = c.ControlledPlayerId(TeamSide.Home); MatchPlayer me = c.State.Home.Find(id);
                    if (me == null || c.BallCarrier != me) return;
                    Vec2 goal = c.Pitch.OpponentGoalCentre(c.State.HomeAttacksPositiveX, TeamSide.Home); Vec2 want = (goal - me.Locomotion.Position).Normalized;
                    float a = NoiseDegrees(seed, 3, 30f) * MathUtil.Deg2Rad; Vec2 aim = new Vec2(want.X * (float)Math.Cos(a) - want.Y * (float)Math.Sin(a), want.X * (float)Math.Sin(a) + want.Y * (float)Math.Cos(a));
                    c.SubmitHumanInput(TeamSide.Home, NormalizedInput.Press(Vec2.Zero, ActionButton.A, ActionGesture.SwipeDown, aim, 0.9f));
                }
            });
            // ---------------- who the person is playing: the automatic selection of the controlled player while the person gives no input
            list.Add(new GbScenario
            {
                Id = "H3_AutoSelection", Family = "human", Story = "a person who touches nothing for five minutes: is the player he controls the one with the ball, or the nearest to it, and how often does the selection flicker", Seconds = 300f,
                Judge = J("selCarrier", 1, "selNearestWhenDefending", 1, "selDefSwitchesPerMinute", -1, "selFlickerPerMinute", -1, "selFar", -1),
                Custom = (cfg, seed) =>
                {
                    MatchController c = GbBuild(cfg, seed, out BasicDecisionDriver driver);
                    c.SetControl(TeamSide.Home, ControlSource.Human);
                    var m = AllKeys.Concat(DerivedKeys).ToDictionary(k => k, k => double.NaN);
                    foreach (string k in AllKeys) m[k] = 0;
                    int own = 0, ownCarrier = 0, def = 0, defNearest = 0, defFar = 0, switches = 0, defSwitches = 0, flicker = 0, playing = 0; string last = null, beforeLast = null; int lastSwitchStep = -1000, prevSwitchStep = -1000;
                    for (int step = 0; step < (int)(300f / Dt) && c.State.Phase != MatchPhase.Finished; step++)
                    {
                        driver.Step(c); c.Advance(Dt);
                        if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                        if (step % 15 != 0 || c.State.Phase != MatchPhase.Playing) continue;
                        string id = c.ControlledPlayerId(TeamSide.Home); MatchPlayer me = id == null ? null : c.State.Home.Find(id); if (me == null) continue;
                        playing++;
                        if (last != null && id != last)
                        {
                            switches++; bool homeHasBall = c.State.Ball.HasPossession && c.State.Ball.PossessingTeam == TeamSide.Home; if (!homeHasBall) defSwitches++;
                            if (beforeLast != null && id == beforeLast && step - prevSwitchStep <= 90) flicker++;   // back to the player he had a moment ago (within 1.5 s)
                            beforeLast = last; prevSwitchStep = lastSwitchStep; lastSwitchStep = step;
                        }
                        last = id;
                        Vec2 ball = new Vec2(c.State.Ball.Physics.Position.X, c.State.Ball.Physics.Position.Z);
                        if (c.State.Ball.HasPossession && c.State.Ball.PossessingTeam == TeamSide.Home) { own++; if (c.BallCarrier == me) ownCarrier++; }
                        else
                        {
                            def++; float mine = (me.Locomotion.Position - ball).Magnitude, best = float.MaxValue;
                            foreach (MatchPlayer p in c.State.Home.Players) if (p.IsActive && !p.InGoal) best = Math.Min(best, (p.Locomotion.Position - ball).Magnitude);
                            if (mine <= best + 2f) defNearest++; if (mine > best + 6f) defFar++;
                        }
                    }
                    m["selCarrier"] = own > 0 ? (double)ownCarrier / own : double.NaN; m["selNearestWhenDefending"] = def > 0 ? (double)defNearest / def : double.NaN;
                    m["selFar"] = def > 0 ? (double)defFar / def : double.NaN; m["selSwitchesPerMinute"] = playing > 0 ? switches / (playing * 0.25 / 60.0) : double.NaN; m["selDefSwitchesPerMinute"] = playing > 0 ? defSwitches / (playing * 0.25 / 60.0) : double.NaN; m["selFlickerPerMinute"] = playing > 0 ? flicker / (playing * 0.25 / 60.0) : double.NaN;
                    return m;
                }
            });
            list.Add(new GbScenario
            {
                Id = "H4_ManualSwitch", Family = "human", Story = "a person presses the switch-player button while the rival has the ball: does the player he chose stay his", Seconds = 300f,
                Judge = J("manualKept", 1),
                Custom = (cfg, seed) =>
                {
                    MatchController c = GbBuild(cfg, seed, out BasicDecisionDriver driver);
                    c.SetControl(TeamSide.Home, ControlSource.Human);
                    var m = AllKeys.Concat(DerivedKeys).ToDictionary(k => k, k => double.NaN);
                    foreach (string k in AllKeys) m[k] = 0;
                    int attempts = 0, kept = 0, nextAt = 600;
                    for (int step = 0; step < (int)(300f / Dt) && c.State.Phase != MatchPhase.Finished; step++)
                    {
                        driver.Step(c); c.Advance(Dt);
                        if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                        if (step < nextAt || c.State.Phase != MatchPhase.Playing || (c.State.Ball.HasPossession && c.State.Ball.PossessingTeam == TeamSide.Home)) continue;
                        string chosen = c.CycleSelection(TeamSide.Home); attempts++; nextAt = step + 600;
                        for (int k = 0; k < 36; k++) { driver.Step(c); c.Advance(Dt); }
                        if (c.ControlledPlayerId(TeamSide.Home) == chosen) kept++;
                    }
                    m["manualKept"] = attempts > 0 ? (double)kept / attempts : double.NaN;
                    return m;
                }
            });
            // ---------------- whole matches
            list.Add(new GbScenario
            {
                Id = "M1_FullMatch", Family = "match", Story = "a whole Classic match, Professional against Professional, real pairs of teams", Seconds = 900f,
                Judge = J("h_passOkRate", 1, "h_xg", 0, "a_xg", 0),
                Custom = (cfg, seed) =>
                {
                    int home = seed % Db.Teams.Count, away = (seed * 5 + 3) % Db.Teams.Count; if (away == home) away = (away + 1) % Db.Teams.Count;
                    GbConfig mc = cfg.With(cfg.Name, x => { x.HomeTeam = home; x.AwayTeam = away; });
                    MatchController c = GbBuild(mc, seed, out BasicDecisionDriver driver);
                    var rec = new GbRecorder(c); rec.Attach();
                    int steps = 0;
                    while (c.State.Phase != MatchPhase.Finished && steps++ < 60 * 1300)
                    {
                        driver.Step(c); c.Advance(Dt);
                        if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                        if (steps % 15 == 0) rec.Sample();
                        rec.Stop = false;
                    }
                    return rec.Finish(900f);
                }
            });
            return list;
        }
    }
}
