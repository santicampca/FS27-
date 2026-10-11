using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 23: the shoot-out. Classic plays every kick for real (ball physics, goal frame, goalkeeper's dive); Arena resolves every kick as an arcade duel; both count with the same state, order and eligibility rules.
    /// A short knockout match (3 s) that ends level goes straight to the shoot-out.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController ShootoutMatch(GameMode mode, int seed = 1, int kicks = 3, bool simulated = true, bool extraTime = false, CompetitionMatchType type = CompetitionMatchType.Knockout, bool shootoutEnabled = true)
        {
            MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena(type) : MatchSettings.CreateClassic(type);
            s.RegulationPeriods = 1; s.DurationSeconds = 3f; s.PeriodSeconds = 3f; s.HalftimeSeconds = 0f;
            s.ExtraTimeEnabled = extraTime; s.ExtraTimePeriodSeconds = 3f; s.ShootoutEnabled = shootoutEnabled;
            s.ShootoutKicksPerTeam = kicks; s.Seed = seed;
            s.ShootoutExecution = simulated ? ShootoutExecutionMode.Simulated : ShootoutExecutionMode.Recorded;
            var c = new MatchController(new FieldDimensions(), s, new MatchSquad(FkStarters("h"), FkBench("h")), new MatchSquad(FkStarters("a"), FkBench("a")));
            c.Start();
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -6f + i, -10f + i * 4f), 0f);
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 10f + i, -10f + i * 4f), 3.14159f);
            }
            Assert.IsTrue(c.ConfirmRestartReady());
            return c;
        }

        /// <summary>Plays the regulation (no goals) until the shoot-out starts.</summary>
        private static void ToShootout(MatchController c)
        {
            for (int i = 0; i < 60 * 8 && c.State.Phase != MatchPhase.Shootout && c.State.Phase != MatchPhase.Finished; i++) c.Advance(FkDt);
        }

        private static void PlayShootout(MatchController c, List<GameplayEvent> events = null, int maxSeconds = 900)
        {
            for (int i = 0; i < 60 * maxSeconds && c.State.Phase != MatchPhase.Finished; i++) c.Advance(FkDt);
        }

        // ================= when there is a shoot-out =================

        [Test]
        public void ShootoutRules_ADrawIsADrawInANormalMatch_AKnockoutGoesOnOnlyIfTheRulesSay()
        {
            foreach (GameMode mode in new[] { GameMode.Classic, GameMode.Arena })
            {
                MatchController normal = ShootoutMatch(mode, 1, 3, true, false, CompetitionMatchType.Normal);
                for (int i = 0; i < 60 * 5; i++) normal.Advance(FkDt);
                Assert.AreEqual(MatchPhase.Finished, normal.State.Phase, mode + ": a draw stands in a normal match"); Assert.IsTrue(normal.State.Result.IsDraw); Assert.IsNull(normal.State.Shootout);

                MatchController noShootout = ShootoutMatch(mode, 1, 3, true, false, CompetitionMatchType.Knockout, false);
                for (int i = 0; i < 60 * 5; i++) noShootout.Advance(FkDt);
                Assert.AreEqual(MatchPhase.Finished, noShootout.State.Phase, mode + ": a knockout without shoot-out ends level"); Assert.IsNull(noShootout.State.Shootout);

                MatchController ko = ShootoutMatch(mode);
                ToShootout(ko);
                Assert.AreEqual(MatchPhase.Shootout, ko.State.Phase, mode + ": a knockout with extra time off goes straight to the shoot-out");
                Assert.IsFalse(ko.State.ExtraTimeReached); Assert.AreNotEqual(MatchPeriod.ExtraTimeFirstHalf, ko.State.Period);
            }
            MatchController et = ShootoutMatch(GameMode.Classic, 1, 3, true, true);
            var periods = new List<MatchPeriod>();
            for (int i = 0; i < 60 * 40 && et.State.Phase != MatchPhase.Shootout; i++)
            {
                et.Advance(FkDt); if (periods.Count == 0 || periods.Last() != et.State.Period) periods.Add(et.State.Period);
                if (et.State.Phase == MatchPhase.Break) et.Advance(30f);
                if (et.State.Phase == MatchPhase.WaitingForKickoff) et.ConfirmRestartReady();
            }
            Assert.AreEqual(MatchPhase.Shootout, et.State.Phase); CollectionAssert.Contains(periods, MatchPeriod.ExtraTimeFirstHalf, "the extra time that the rules ask for is played before the shoot-out");
        }

        [Test]
        public void ShootoutRules_NoShootoutWhileTheResultCanStillChange_BruteForce_FiveKicksAndSuddenDeath()
        {
            // every sequence of up to 10 kicks (5 per team): the shoot-out is decided early ONLY when nothing can change any more
            for (int mask = 0; mask < 1 << 10; mask++)
            {
                var s = new PenaltyShootoutState(5, true, TeamSide.Home);
                int n = 0;
                for (; n < 10 && !s.IsDecided; n++) s.RecordKick(((mask >> n) & 1) == 1);
                if (!s.IsDecided) { Assert.AreEqual(10, n); Assert.AreEqual(s.Goals(TeamSide.Home), s.Goals(TeamSide.Away), "after all the regular kicks, still undecided means level (sudden death)"); Assert.IsTrue(s.InSuddenDeath); continue; }
                if (n < 10)
                {
                    int remHome = 5 - s.Taken(TeamSide.Home), remAway = 5 - s.Taken(TeamSide.Away);
                    int h = s.Goals(TeamSide.Home), a = s.Goals(TeamSide.Away);
                    TeamSide w = s.Winner;
                    if (w == TeamSide.Home) Assert.Greater(h, a + remAway, "decided early for home, but away could still catch up (mask " + mask + ")");
                    else Assert.Greater(a, h + remHome, "decided early for away, but home could still catch up (mask " + mask + ")");
                }
            }
            var sd = new PenaltyShootoutState(5, true, TeamSide.Home);
            for (int i = 0; i < 10; i++) sd.RecordKick(true);                       // 5-5
            Assert.IsFalse(sd.IsDecided); sd.RecordKick(true); Assert.IsFalse(sd.IsDecided, "sudden death: one more kick each; decided only after both have kicked");
            sd.RecordKick(false); Assert.IsTrue(sd.IsDecided); Assert.AreEqual(TeamSide.Home, sd.Winner);
        }

        // ================= Arena: the arcade duel =================

        [Test]
        public void ArenaShootout_IsPlayedByTheCore_EveryKickResolvedAtOnce_AndTheWinnerIsOneOfTheTeams()
        {
            MatchController c = ShootoutMatch(GameMode.Arena);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            ToShootout(c);
            Assert.AreEqual(MatchPhase.Shootout, c.State.Phase);
            PlayShootout(c);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase); Assert.AreEqual(MatchTerminationReason.PenaltyShootout, c.State.Result.Reason);
            PenaltyShootoutState so = c.State.Shootout;
            Assert.IsTrue(so.IsDecided && so.HasWinner); Assert.AreEqual(so.Winner, c.State.Result.Winner);
            Assert.AreEqual(so.Kicks.Count, events.Count(e => e.Kind == GameplayEventKind.PenaltyKickTaken), "one result event per kick");
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyShootoutEnded)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyShootoutStarted));
            Assert.AreEqual(0, c.State.Result.HomeGoals + c.State.Result.AwayGoals, "the shoot-out never changes the score");
            Assert.AreEqual(so.Kicks.Count, c.State.PenaltiesAwarded);
            Assert.IsFalse(events.Any(e => e.Kind == GameplayEventKind.PenaltyFrameHit || e.Kind == GameplayEventKind.PenaltyAwarded || e.Kind == GameplayEventKind.FreeKickAwarded), "Arena has no frame physics and no penalties for fouls");
            Assert.AreEqual(so.Goals(TeamSide.Home), c.State.Result.ShootoutHome);
        }

        [Test]
        public void ArenaShootout_AHumanKicker_AimsAndTheKeeperDives_BothAimsMatter()
        {
            PenaltyOutcome Kick(float shotLateral, float keeperLateral, float power)
            {
                MatchController c = ShootoutMatch(GameMode.Arena);
                c.SetControl(TeamSide.Home, ControlSource.Human);
                ToShootout(c);
                for (int i = 0; i < 600 && !(c.State.Penalty != null && c.State.Penalty.Stage == PenaltyStage.Ready); i++) c.Advance(FkDt);
                Assert.AreEqual(TeamSide.Home, c.State.Penalty.Team);
                c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = keeperLateral });
                Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(new PenaltyShotAim { Lateral = shotLateral, Height = 0.3f, Power = power }));
                Assert.AreEqual(PenaltyStage.Resolved, c.State.Penalty.Stage, "an Arena kick is resolved at once");
                return c.State.Penalty.Outcome;
            }
            Assert.AreEqual(PenaltyOutcome.Saved, Kick(0.6f, 0.6f, 0.5f), "the goalkeeper guessed the corner");
            Assert.AreEqual(PenaltyOutcome.Scored, Kick(0.6f, -0.6f, 0.5f), "and when he guessed the other one it is a goal");
            Assert.AreEqual(PenaltyOutcome.Missed, Kick(1.5f, 0f, 0.8f), "wide");
        }

        [Test]
        public void ArenaResolver_AttributesAndPowerMatter_TheNoiseIsBounded_AndItIsDeterministic()
        {
            var t = new PenaltyTuning();
            PenaltyKeeperAim stay = new PenaltyKeeperAim { HasChoice = true, Lateral = 0f };
            Func<float, float, float, float, PenaltyOutcome> go = (lat, power, quality, diving) =>
                ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = lat, Height = 0.3f, Power = power }, new PenaltyKeeperAim { HasChoice = true, Lateral = lat }, quality, diving, 0.5f, 0f, 0f, t).Outcome;
            Assert.AreEqual(PenaltyOutcome.Saved, go(0.6f, 0.5f, 0.5f, 1f), "the keeper that guessed the corner and dives well saves");
            // a shot to the corner the goalkeeper guessed: saved by a good diver, scored against a poor one when it is hard enough
            int goodSaves = 0, poorSaves = 0;
            for (float lat = -0.9f; lat <= 0.9f; lat += 0.1f) { if (go(lat, 1f, 0.5f, 1f) == PenaltyOutcome.Saved) goodSaves++; if (go(lat, 1f, 0.5f, 0f) == PenaltyOutcome.Saved) poorSaves++; }
            Assert.GreaterOrEqual(goodSaves, poorSaves, "a better goalkeeper does not save less");
            Assert.AreEqual(PenaltyOutcome.Scored, ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0.9f, Height = 0.3f, Power = 1f }, new PenaltyKeeperAim { HasChoice = true, Lateral = -0.9f }, 0.9f, 0.5f, 0.5f, 0f, 0f, t).Outcome);
            // the accuracy error of the same aim: a worse kicker, a harder shot -> farther from the aim; never beyond the bound
            float Error(float quality, float power) { return Math.Abs(ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, Power = power }, stay, quality, 0.5f, 0.5f, 1f, 0f, t).ActualLateral); }
            Assert.Greater(Error(0.1f, 1f), Error(0.9f, 1f)); Assert.Greater(Error(0.5f, 1f), Error(0.5f, 0.3f)); Assert.LessOrEqual(Error(0f, 1f), t.ArenaErrorBase + t.ArenaErrorPower + 1e-4f);
            // wide, post, over, bar
            Assert.AreEqual(PenaltyOutcome.Missed, go(1.4f, 0.5f, 1f, 0.5f));
            var over = ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0f, Height = 1.4f, Power = 0.5f }, stay, 1f, 0.5f, 0.5f, 0f, 0f, t); Assert.AreEqual(PenaltyOutcome.Missed, over.Outcome);
            var bar = ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0f, Height = 1.04f, Power = 0.5f }, stay, 1f, 0.5f, 0.5f, 0f, 0f, t); Assert.AreEqual(PenaltyOutcome.HitBar, bar.Outcome);
            var post = ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 1.04f, Height = 0.2f, Power = 0.5f }, stay, 1f, 0.5f, 0.5f, 0f, 0f, t); Assert.AreEqual(PenaltyOutcome.HitPost, post.Outcome);
            var a = ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0.3f, Height = 0.4f, Power = 0.7f }, stay, 0.6f, 0.4f, 0.4f, 0.37f, -0.2f, t);
            var b = ArenaPenaltyResolver.Resolve(new PenaltyShotAim { Lateral = 0.3f, Height = 0.4f, Power = 0.7f }, stay, 0.6f, 0.4f, 0.4f, 0.37f, -0.2f, t);
            Assert.AreEqual(a.Outcome, b.Outcome); Assert.AreEqual(a.ActualLateral, b.ActualLateral);
        }

        // ================= Classic: the real kick =================

        [Test]
        public void ClassicShootout_EveryKickIsPlayedWithTheRealBall_OneResultEach_NoSecondShot_NoDoubleGoal()
        {
            MatchController c = ShootoutMatch(GameMode.Classic);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            var shots = new List<ActionResult>(); c.ActionPerformed += r => { if (r.Outcome != ActionOutcome.Rejected && (r.Kind == FootballActionKind.Shot || r.Kind == FootballActionKind.CurvedShot)) shots.Add(r); };
            ToShootout(c);
            PlayShootout(c);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            PenaltyShootoutState so = c.State.Shootout;
            Assert.IsTrue(so.IsDecided && so.HasWinner);
            Assert.AreEqual(so.Kicks.Count, shots.Count, "one shot per kick: nobody shoots twice");
            Assert.AreEqual(so.Kicks.Count, events.Count(e => e.Kind == GameplayEventKind.PenaltyKickTaken));
            Assert.AreEqual(so.Kicks.Count, events.Count(e => e.Kind == GameplayEventKind.PenaltyPrepared)); Assert.AreEqual(so.Kicks.Count, events.Count(e => e.Kind == GameplayEventKind.PenaltyWhistled));
            Assert.AreEqual(0, c.State.Score.Home + c.State.Score.Away, "the match score does not change: the kicks are not goals of the match");
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.GoalScored), "a goal in a shoot-out is a kick that scored, not a match goal");
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyShootoutEnded));
            for (int i = 0; i < so.Kicks.Count; i++) Assert.AreEqual(so.Kicks[i].Scored, events.Where(e => e.Kind == GameplayEventKind.PenaltyKickTaken).ElementAt(i).Detail.EndsWith("Scored"), "event and count agree on kick " + i);
        }

        [Test]
        public void ClassicShootout_TheTakerNeverPlaysTheBallAgain_TheKickEndsAtItsFirstResult()
        {
            MatchController c = ShootoutMatch(GameMode.Classic);
            ToShootout(c);
            c.SetControl(TeamSide.Home, ControlSource.Human);
            for (int i = 0; i < 600 && !(c.State.Penalty != null && c.State.Penalty.Stage == PenaltyStage.Ready); i++) c.Advance(FkDt);
            string taker = c.State.Penalty.TakerId;
            var touches = new List<GameplayEvent>(); c.Events.Published += e => { if (e.Kind == GameplayEventKind.BallTouched && e.PlayerId == taker) touches.Add(e); };
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0.6f });
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.5f, Height = 0.3f, Power = 0.4f }));
            for (int i = 0; i < 200 && c.State.Penalty.Stage != PenaltyStage.Resolved; i++)
            {
                Vec3 b = c.State.Ball.Physics.Position; c.PlacePlayer(TeamSide.Home, taker, new Vec2(b.X - 0.3f, b.Z), 0f);   // the taker chases his own ball
                c.Advance(FkDt);
            }
            Assert.AreEqual(PenaltyStage.Resolved, c.State.Penalty.Stage);
            Assert.AreEqual(1, touches.Count, "only the kick itself is his touch");
            Assert.AreEqual(1, c.State.Shootout.Taken(TeamSide.Home) + c.State.Shootout.Taken(TeamSide.Away), "one kick recorded");
        }

        // ================= the order of the takers =================

        [Test]
        public void ShootoutOrder_IsKept_AndEveryKickFollowsIt_WhatTheScreenShowsIsWhoKicks()
        {
            foreach (GameMode mode in new[] { GameMode.Classic, GameMode.Arena })
            {
                MatchController c = ShootoutMatch(mode, 2);
                Assert.AreEqual(ShootoutOrderResult.Accepted, c.SetShootoutOrder(TeamSide.Home, new[] { "hs5", "hs3", "hs1" }));
                Assert.AreEqual(ShootoutOrderResult.Accepted, c.SetShootoutOrder(TeamSide.Away, new[] { "as2" }));
                List<string> home = c.ShootoutOrderFor(TeamSide.Home);
                CollectionAssert.AreEqual(new[] { "hs5", "hs3", "hs1" }, home.Take(3), "the order the person set comes first"); Assert.AreEqual(6, home.Count, "and the rest of the eligible players complete it"); Assert.AreEqual(6, home.Distinct().Count());
                Assert.AreEqual("hs0", home.Last(), "the goalkeeper is the last one on the automatic order"); Assert.AreEqual("as2", c.ShootoutOrderFor(TeamSide.Away)[0]);
                var shown = new List<string>(); var kicked = new List<string>();
                c.Events.Published += e =>
                {
                    if (e.Kind == GameplayEventKind.PenaltyPrepared) shown.Add(e.Team + ":" + c.NextShootoutTaker(e.Team) + ":" + e.PlayerId);
                    if (e.Kind == GameplayEventKind.PenaltyKickTaken) kicked.Add(e.Team + ":" + e.PlayerId);
                };
                ToShootout(c); PlayShootout(c);
                Assert.IsTrue(shown.Count >= 3, "several kicks were taken");
                foreach (string s in shown) { string[] p = s.Split(':'); Assert.AreEqual(p[1], p[2], "the taker the screen shows is the one that kicks (" + s + ")"); }
                CollectionAssert.AreEqual(shown.Select(s => s.Split(':')[0] + ":" + s.Split(':')[2]).ToList(), kicked, "and the kick is taken by that same player");
                var homeKicks = kicked.Where(k => k.StartsWith("Home")).Select(k => k.Split(':')[1]).ToList();
                CollectionAssert.AreEqual(home.Take(homeKicks.Count), homeKicks.Take(Math.Min(homeKicks.Count, 6)), "home kicks in the order it set");
                Assert.AreEqual("as2", kicked.First(k => k.StartsWith("Away")).Split(':')[1]);
            }
        }

        [Test]
        public void ShootoutOrder_InvalidOrders_AreRefused_AndTheOrderCannotChangeOnceThatTeamHasKicked()
        {
            MatchController c = ShootoutMatch(GameMode.Arena);
            string snapshot = string.Join(",", c.ShootoutOrderFor(TeamSide.Home));
            Assert.AreEqual(ShootoutOrderResult.Empty, c.SetShootoutOrder(TeamSide.Home, new string[0]));
            Assert.AreEqual(ShootoutOrderResult.UnknownPlayer, c.SetShootoutOrder(TeamSide.Home, new[] { "hs1", "nobody" }));
            Assert.AreEqual(ShootoutOrderResult.WrongTeam, c.SetShootoutOrder(TeamSide.Home, new[] { "as1" }));
            Assert.AreEqual(ShootoutOrderResult.NotEligible, c.SetShootoutOrder(TeamSide.Home, new[] { "hb1" }), "a substitute who is not on the pitch");
            Assert.AreEqual(ShootoutOrderResult.Duplicate, c.SetShootoutOrder(TeamSide.Home, new[] { "hs1", "hs2", "hs1" }));
            Assert.AreEqual(snapshot, string.Join(",", c.ShootoutOrderFor(TeamSide.Home)), "a refused order changes nothing");
            Assert.AreEqual(ShootoutOrderResult.Accepted, c.SetShootoutOrder(TeamSide.Home, new[] { "hs2" }));
            ToShootout(c);
            for (int i = 0; i < 60 * 30 && c.State.Shootout.Taken(TeamSide.Home) == 0; i++) c.Advance(FkDt);
            Assert.AreEqual(ShootoutOrderResult.AlreadyStarted, c.SetShootoutOrder(TeamSide.Home, new[] { "hs3" }));
        }

        [Test]
        public void ShootoutOrder_APlayerWhoStopsBeingEligible_IsSkippedByAnExplicitRule_NobodyIsReplacedBehindTheScenes()
        {
            MatchController c = ShootoutMatch(GameMode.Classic, 3);
            c.SetShootoutOrder(TeamSide.Home, new[] { "hs5", "hs4", "hs3" });
            c.IssueCard(TeamSide.Home, "hs5", CardKind.Red);                       // he is sent off after the order was set
            List<string> order = c.ShootoutOrderFor(TeamSide.Home);
            Assert.IsFalse(order.Contains("hs5"), "a sent-off player is not eligible");
            Assert.AreEqual("hs4", order[0], "his turn goes to the next one of the person's order"); Assert.AreEqual("hs3", order[1]);
            ToShootout(c);
            Assert.AreEqual("hs4", c.NextShootoutTaker(TeamSide.Home));
            var kicked = new List<string>(); c.Events.Published += e => { if (e.Kind == GameplayEventKind.PenaltyKickTaken && e.Team == TeamSide.Home) kicked.Add(e.PlayerId); };
            PlayShootout(c);
            Assert.AreEqual("hs4", kicked[0]); Assert.IsFalse(kicked.Contains("hs5"));
        }

        [Test]
        public void ShootoutOrder_TheCycleStartsAgainInTheSameOrder_OnlyAfterEveryEligiblePlayerHasKicked()
        {
            MatchController c = ShootoutMatch(GameMode.Arena, 4, 3);
            c.Penalties.ArenaErrorBase = 3f;      // a lot of wide shots, so the shoot-out is long
            ToShootout(c);
            var kicked = new List<string>(); c.Events.Published += e => { if (e.Kind == GameplayEventKind.PenaltyKickTaken && e.Team == TeamSide.Home) kicked.Add(e.PlayerId); };
            PlayShootout(c);
            List<string> order = c.ShootoutOrderFor(TeamSide.Home);
            for (int i = 0; i < kicked.Count; i++) Assert.AreEqual(order[i % order.Count], kicked[i], "kick " + i);
            if (kicked.Count >= 2) Assert.AreEqual(kicked.Count, kicked.Take(Math.Min(kicked.Count, 6)).Distinct().Count() + Math.Max(0, kicked.Count - 6), "nobody kicks twice before all six have");
        }

        [Test]
        public void Shootout_TheSameSeedGivesTheSameShootout_InBothModes()
        {
            foreach (GameMode mode in new[] { GameMode.Classic, GameMode.Arena })
            {
                string Run()
                {
                    MatchController c = ShootoutMatch(mode, 9);
                    var sb = new System.Text.StringBuilder(); c.Events.Published += e => sb.Append((int)e.Kind).Append(e.Detail).Append(e.PlayerId).Append(';');
                    ToShootout(c); PlayShootout(c);
                    return sb.ToString() + c.State.Result.ShootoutHome + "-" + c.State.Result.ShootoutAway;
                }
                Assert.AreEqual(Run(), Run(), mode.ToString());
            }
        }

        [Test]
        public void Shootout_RecordedModeStillWorks_AsBefore_AndTheSimulatedModeRefusesAManualRecord()
        {
            MatchController legacy = ShootoutMatch(GameMode.Classic, 1, 3, false);
            ToShootout(legacy);
            Assert.AreEqual(MatchPhase.Shootout, legacy.State.Phase); Assert.IsNull(legacy.State.Penalty);
            Assert.IsTrue(legacy.RecordShootoutKick(true));
            MatchController sim = ShootoutMatch(GameMode.Classic, 1, 3, true);
            ToShootout(sim);
            Assert.IsFalse(sim.RecordShootoutKick(true), "the Core plays the kicks: nobody records them by hand");
        }
    }
}
