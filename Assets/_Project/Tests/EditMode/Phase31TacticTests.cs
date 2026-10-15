using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 31: the team tactic (coach identity + game state) as behaviour. A neutral plan changes nothing; a high line stands higher, a high press closes from further, a bold direct team chooses the forward options more, and
    /// the score and the clock push the same plan the way a team would expect. Everything is read from the real engine over states of real matches.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static TeamTacticalIdentity Identity(int line = 50, int width = 50, int tempo = 50, int risk = 50, BuildUpStyle build = BuildUpStyle.Balanced, PressingStyle press = PressingStyle.MidBlock,
                                                      TransitionStyle transition = TransitionStyle.Controlled, Mentality mentality = Mentality.Balanced)
        {
            return new TeamTacticalIdentity { FormationId = "2-1-2", FromCoach = true, BackLineType = BackLineType.Two, Mentality = mentality, BuildUpStyle = build, PressingStyle = press, TransitionStyle = transition, Width = width, Tempo = tempo, Risk = risk, DefensiveLine = line };
        }

        private static string Dump(object o)
        {
            var sb = new StringBuilder();
            foreach (FieldInfo f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(o);
                if (v != null && f.FieldType.IsValueType && !f.FieldType.IsPrimitive && !f.FieldType.IsEnum) sb.Append(f.Name).Append('{').Append(Dump(v)).Append('}');
                else sb.Append(f.Name).Append('=').Append(v is float fl ? BitConverter.SingleToInt32Bits(fl).ToString() : v).Append(';');
            }
            return sb.ToString();
        }

        /// <summary>Real match states: 60 s of a real match played by the real driver, sampled every 3 s (the ball, the carrier and the shape differ from state to state).</summary>
        private static List<WorldView> RealStates(int seed, int samples = 16, int home = 3, int away = 8)
        {
            MatchController c = RealRoles(home, away, seed, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Classic), DefenseTuning.ForMode(GameMode.Classic), null, AttackPositioningTuning.ForMode(GameMode.Classic), BallCarrierTuning.ForMode(GameMode.Classic), GoalkeeperAiTuning.ForMode(GameMode.Classic));
            var states = new List<WorldView>();
            for (int step = 0; step < samples * 180; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (step % 180 == 179 && c.State.Phase == MatchPhase.Playing) states.Add(WorldState.Capture(c).View);
            }
            return states;
        }

        private static BasicDecisionEngine EngineWith(MatchController c, TeamTactic home, TeamTactic away)
        {
            var e = new BasicDecisionEngine(c.Pitch);
            e.SetTactic(TeamSide.Home, home); e.SetTactic(TeamSide.Away, away);
            return e;
        }

        [Test]
        public void ANeutralPlan_ChangesNothing_InAnyRealState()
        {
            MatchController dummy = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            var plain = new BasicDecisionEngine(dummy.Pitch);
            var neutral = EngineWith(dummy, TeamTactic.From(Identity()), TeamTactic.From(Identity()));
            int compared = 0;
            foreach (int seed in new[] { 1, 2, 3 })
                foreach (WorldView v in RealStates(seed))
                {
                    if (v.Match.HomeGoals != 0 || v.Match.AwayGoals != 0) continue;   // a goal makes the score matter (urgency) even to a neutral plan
                    for (int i = 0; i < v.PlayerCount; i++)
                    {
                        if (!v[i].IsOnPitch) continue;
                        Assert.AreEqual(Dump(plain.Decide(v, v[i].PlayerId)), Dump(neutral.Decide(v, v[i].PlayerId)), "player " + v[i].PlayerId + " at " + v.Match.MatchTime);
                        compared++;
                    }
                }
            Assert.Greater(compared, 300, "enough real decisions compared");
        }

        [Test]
        public void TheUrgency_FollowsTheScoreAndTheClock()
        {
            var plan = TeamTactic.From(Identity());
            WorldMatch m = new WorldMatch { HomeGoals = 0, AwayGoals = 2, PeriodRemaining = 600f };
            Assert.AreEqual(0f, plan.Urgency(m, TeamSide.Home), 1e-6f, "plenty of time left: the score does not push yet");
            m.PeriodRemaining = 20f;
            Assert.Greater(plan.Urgency(m, TeamSide.Home), 0.7f, "two down with seconds to go: all out");
            Assert.Less(plan.Urgency(m, TeamSide.Away), -0.7f, "two up with seconds to go: protect it");
            m.HomeGoals = 2;
            Assert.AreEqual(0f, plan.Urgency(m, TeamSide.Home), 1e-6f, "level");
            Assert.Greater(TeamTactic.From(Identity(mentality: Mentality.VeryAttacking)).Urgency(m, TeamSide.Home), 0f, "an attacking mentality pushes on its own");
            Assert.Less(TeamTactic.From(Identity(mentality: Mentality.Defensive)).Urgency(m, TeamSide.Home), 0f);
        }

        [Test]
        public void AHighLine_StandsHigher_AndALowBlockStandsDeeper_WhenTheOpponentHasTheBall()
        {
            MatchController dummy = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            var high = EngineWith(dummy, TeamTactic.From(Identity(line: 100)), null);
            var low = EngineWith(dummy, TeamTactic.From(Identity(line: 0)), null);
            float gap = 0f; int n = 0;
            foreach (WorldView v in new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.SelectMany(seed => RealStates(seed, 24)))
            {
                if (!v.Match.HasPossession || v.Match.PossessingTeam != TeamSide.Away) continue;
                float sign = v.Match.HomeAttacksPositiveX ? 1f : -1f;
                for (int i = 0; i < v.PlayerCount; i++)
                {
                    WorldPlayer p = v[i];
                    if (!p.IsOnPitch || p.Team != TeamSide.Home || p.IsGoalkeeper) continue;
                    PlayerDecision a = high.Decide(v, p.PlayerId), b = low.Decide(v, p.PlayerId);
                    if (a.Reason != b.Reason || !(a.Reason == DecisionReason.OpponentHasBall || a.Reason == DecisionReason.TransitionReturn || a.Reason == DecisionReason.FarFromHome || a.Reason == DecisionReason.TiredReturnHome) || !a.Movement.HasTarget || !b.Movement.HasTarget) continue;   // the defenders who go back to their (planned) place
                    gap += ((a.Movement.Target.X - b.Movement.Target.X) * sign); n++;
                }
            }
            Assert.Greater(n, 20, "defenders who were going back to their place");
            Assert.Greater(gap / n, 4f, "the high line stands clearly further up the pitch than the deep one (metres on average): " + gap / n);
        }

        [Test]
        public void AHighPress_ClosesFromFurther_ThanALowBlock()
        {
            MatchController dummy = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            var press = EngineWith(dummy, TeamTactic.From(Identity(press: PressingStyle.AggressivePress)), null);
            var block = EngineWith(dummy, TeamTactic.From(Identity(press: PressingStyle.LowBlock)), null);
            int pressCloses = 0, blockCloses = 0, n = 0;
            foreach (int seed in new[] { 1, 2, 3, 4 })
                foreach (WorldView v in RealStates(seed, 20))
                {
                    if (!v.Match.HasPossession || v.Match.PossessingTeam != TeamSide.Away) continue;
                    var carrier = v.TryGetBallCarrier(out WorldPlayer holder) ? holder : default(WorldPlayer);
                    for (int i = 0; i < v.PlayerCount; i++)
                    {
                        WorldPlayer p = v[i];
                        if (!p.IsOnPitch || p.Team != TeamSide.Home || p.IsGoalkeeper) continue;
                        PlayerDecision a = press.Decide(v, p.PlayerId), b = block.Decide(v, p.PlayerId);
                        if (a.Reason != DecisionReason.PrimaryDefender && b.Reason != DecisionReason.PrimaryDefender) continue;
                        n++;
                        float toBall = (v.Ball.Ground - p.Position).Magnitude;
                        // closing down = going to the ball itself; standing off = going to a place goal-side of the carrier
                        if (a.Reason == DecisionReason.PrimaryDefender && (a.Movement.Target - v.Ball.Ground).Magnitude < 0.01f) pressCloses++;
                        if (b.Reason == DecisionReason.PrimaryDefender && (b.Movement.Target - v.Ball.Ground).Magnitude < 0.01f) blockCloses++;
                    }
                }
            Assert.Greater(n, 10, "primary defenders compared");
            Assert.Greater(pressCloses, blockCloses, "the aggressive press goes at the ball in more situations than the low block: " + pressCloses + " vs " + blockCloses);
        }

        [Test]
        public void ABoldDirectTeam_ChoosesTheForwardOptionsMore_ThanACautiousShortOne()
        {
            MatchController dummy = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            var bold = EngineWith(dummy, TeamTactic.From(Identity(risk: 100, build: BuildUpStyle.Direct, transition: TransitionStyle.Counter)), TeamTactic.From(Identity(risk: 100, build: BuildUpStyle.Direct, transition: TransitionStyle.Counter)));
            var careful = EngineWith(dummy, TeamTactic.From(Identity(risk: 0, build: BuildUpStyle.Positional, transition: TransitionStyle.PossessionRecovery)), TeamTactic.From(Identity(risk: 0, build: BuildUpStyle.Positional, transition: TransitionStyle.PossessionRecovery)));
            int boldForward = 0, carefulForward = 0, n = 0;
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
                foreach (WorldView v in RealStates(seed, 20))
                {
                    if (!v.TryGetBallCarrier(out WorldPlayer holder) || holder.IsGoalkeeper) continue;
                    bold.Decide(v, holder.PlayerId); CandidateType a = bold.Carrier.Summary.Best;
                    careful.Decide(v, holder.PlayerId); CandidateType b = careful.Carrier.Summary.Best;
                    n++;
                    if (a == CandidateType.ForwardPass || a == CandidateType.ThroughPass || a == CandidateType.Shoot) boldForward++;
                    if (b == CandidateType.ForwardPass || b == CandidateType.ThroughPass || b == CandidateType.Shoot) carefulForward++;
                }
            Assert.Greater(n, 30, "carriers compared");
            Assert.Greater(boldForward, carefulForward, "the bold direct team goes forward more often on the very same states: " + boldForward + " vs " + carefulForward + " of " + n);
        }

        [Test]
        public void AWidePlan_StandsWider_ThanANarrowOne()
        {
            var wide = TeamTactic.From(Identity(width: 100)); var narrow = TeamTactic.From(Identity(width: 0));
            Vec2 home = new Vec2(-5f, 6f);
            Assert.Greater(Math.Abs(wide.ShapeHome(home, 1f, 0f, false).Y), Math.Abs(narrow.ShapeHome(home, 1f, 0f, false).Y));
            Assert.AreEqual(home.Y, TeamTactic.From(Identity()).ShapeHome(home, 1f, 0f, false).Y, 1e-6f);
            Assert.Greater(wide.ShapeHome(home, 1f, 0f, false).X, narrow.ShapeHome(home, 1f, 0f, false).X - 1e-6f);
        }

        [Test]
        public void TheTacticNumbers_LiveInOnePlace_WithTwins_AndTheEngineKeepsNoLooseNumber()
        {
            foreach (FieldInfo f in typeof(TeamTacticDefaults).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                FieldInfo mirror = typeof(TeamTacticTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual((float)f.GetValue(null), (float)mirror.GetValue(new TeamTacticTuning()), f.Name);
                Assert.Greater((float)f.GetValue(null), 0f, f.Name);
            }
            foreach (FieldInfo f in typeof(TeamTacticTuning).GetFields()) Assert.IsNotNull(typeof(TeamTacticDefaults).GetField(f.Name), f.Name + " has no default");
        }

        [Test]
        public void EveryTeamOfTheDatabase_HasAPlan_AndThePlansDiffer()
        {
            var plans = Db.Teams.Select(t => TeamTactic.Of(t, Db)).ToList();
            Assert.AreEqual(Db.Teams.Count, plans.Count);
            Assert.Greater(plans.Select(p => p.Line).Distinct().Count(), 4, "the teams' lines differ");
            Assert.Greater(plans.Select(p => p.BasePress).Distinct().Count(), 2, "the teams' pressing differs");
            Assert.Greater(plans.Select(p => p.Risk).Distinct().Count(), 4, "the teams' appetite for risk differs");
        }

        [Test]
        public void TheExplanation_IsTheDecisionsOwnRecord_AndChangesNothing()
        {
            MatchController dummy = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            var engine = EngineWith(dummy, TeamTactic.From(Identity(line: 80, risk: 80, press: PressingStyle.HighPress)), TeamTactic.From(Identity()));
            int carriers = 0, others = 0;
            foreach (WorldView v in new[] { 1, 2, 3 }.SelectMany(seed => RealStates(seed, 14)))
                for (int i = 0; i < v.PlayerCount; i++)
                {
                    if (!v[i].IsOnPitch) continue;
                    string id = v[i].PlayerId;
                    string before = Dump(engine.Decide(v, id));
                    string text = DecisionExplainer.Explain(engine, v, id);
                    Assert.AreEqual(before, Dump(engine.Decide(v, id)), "explaining does not change the decision");
                    StringAssert.Contains(id, text); StringAssert.Contains("decided:", text);
                    PlayerDecision d = engine.Decide(v, id);
                    StringAssert.Contains(d.Reason.ToString(), text, "the explanation names the very reason the engine gave");
                    if (v[i].HasBall && !v[i].IsGoalkeeper) { StringAssert.Contains("carrier saw", text); carriers++; } else others++;
                    if (v[i].Team == TeamSide.Home) StringAssert.Contains("plan:", text);
                }
            Assert.Greater(carriers, 5); Assert.Greater(others, 50);
        }
    }
}
