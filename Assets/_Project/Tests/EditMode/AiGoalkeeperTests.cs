using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 16: the goalkeeper AI. Where he stands (GoalkeeperPositioning), what he makes of a shot and when he can start to move (GoalkeeperReaction), whether he comes out (GoalkeeperExit), how the difficulty and his own
    /// capabilities change that, how a shot is rated (ShotQualityEvaluator), and that none of it cheats, differs between Arena and Classic only by numbers and is the same every time.
    /// The Away goal is the one at +20 in these scenes (the Home team attacks +X); the Away keeper is "as0".
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static GoalkeeperSkill KeeperSkill(float reaction = 0.30f, float positioning = 1f, float anticipation = 0.5f, float decision = 0.7f, float timing = 0.7f, float reading = 1f, float rebound = 0.7f)
        {
            return new GoalkeeperSkill { ReactionSeconds = reaction, Positioning = positioning, Anticipation = anticipation, DecisionMaking = decision, SaveTiming = timing, ShotReading = reading, ReboundResponse = rebound };
        }

        /// <summary>A keeper with no positioning error at all (to read the geometry on its own).</summary>
        private static GoalkeeperAiTuning PerfectStanding() { return new GoalkeeperAiTuning { PositionErrorBase = 0f, PositionErrorMax = 0f }; }

        private static GoalkeeperStand StandFor(MatchController c, GoalkeeperAiTuning t, GoalkeeperSkill skill)
        {
            WorldView v = ViewOf(c);
            return GoalkeeperPositioning.Stand(v, v[v.IndexOf("as0")], t, skill);
        }

        // ================= where he stands =================

        [Test]
        public void ABallInFront_PutsTheKeeperInTheMiddle_AndABallOnTheWing_PutsHimTowardsTheNearPost()
        {
            MatchController front = Quiet(); Carrier(front, TeamSide.Home, "hs3", 6f, 0f, East);
            GoalkeeperStand f = StandFor(front, PerfectStanding(), KeeperSkill());
            Assert.AreEqual(0f, f.Target.Y, 0.05f, "straight on: in the middle of the goal");
            Assert.Greater(f.Target.X, 15f); Assert.Less(f.Target.X, 20f, "in front of the line, not behind it");

            MatchController wing = Quiet(); Carrier(wing, TeamSide.Home, "hs3", 8f, 9f, East);
            GoalkeeperStand w = StandFor(wing, PerfectStanding(), KeeperSkill());
            Assert.Greater(w.Target.Y, 0.3f, "a ball on the right: he is on the right, covering the near post");
            Assert.LessOrEqual(w.Target.Y, 2.5f, "but never outside the posts");
            MatchController other = Quiet(); Carrier(other, TeamSide.Home, "hs3", 8f, -9f, East);
            Assert.AreEqual(-w.Target.Y, StandFor(other, PerfectStanding(), KeeperSkill()).Target.Y, 0.05f, "and the same on the other side, mirrored");
        }

        [Test]
        public void ADangerousBall_MakesHimAdvance_ABallFarAwayOrOurOwn_DoesNot()
        {
            GoalkeeperAiTuning t = PerfectStanding();
            MatchController far = Quiet(); Carrier(far, TeamSide.Home, "hs3", -10f, 0f, East);
            MatchController near = Quiet(); Carrier(near, TeamSide.Home, "hs3", 12f, 0f, East);
            MatchController own = Quiet(); Carrier(own, TeamSide.Away, "as3", 12f, 0f, West);
            MatchController loose = Quiet(); PutBall(loose, 12f, 0f);
            GoalkeeperStand sFar = StandFor(far, t, KeeperSkill()), sNear = StandFor(near, t, KeeperSkill()), sOwn = StandFor(own, t, KeeperSkill()), sLoose = StandFor(loose, t, KeeperSkill());
            Assert.AreEqual(0f, sFar.Danger, 0.001f); Assert.AreEqual(t.LineDepthMin, sFar.Depth, 0.001f);
            Assert.Greater(sNear.Danger, 0.5f); Assert.Greater(sNear.Depth, sFar.Depth + 1f, "the danger brings him out");
            Assert.AreEqual(0f, sOwn.Danger, 0.001f, "a ball our own team carries is no danger");
            Assert.Less(sLoose.Danger, sNear.Danger, "a free ball is less dangerous than one at an opponent's feet");
            Assert.LessOrEqual(sNear.Depth, t.LineDepthMax + 0.001f);
        }

        [Test]
        public void PlayersInTheWay_CutTheDanger_AndSendHimToTheOpenSide()
        {
            GoalkeeperAiTuning t = PerfectStanding();
            MatchController clear = Quiet(); Carrier(clear, TeamSide.Home, "hs3", 12f, 0f, East);
            MatchController blocked = Quiet(); Carrier(blocked, TeamSide.Home, "hs3", 12f, 0f, East); Put(blocked, TeamSide.Away, "as3", 16f, 0.8f, West);
            GoalkeeperStand a = StandFor(clear, t, KeeperSkill()), b = StandFor(blocked, t, KeeperSkill());
            Assert.AreEqual(0, a.Blockers); Assert.AreEqual(1, b.Blockers);
            Assert.Less(b.Danger, a.Danger, "the shot is harder through a defender");
            Assert.Less(b.Target.Y, a.Target.Y - 0.1f, "the defender is on his left (positive side): he shades to the open side");
        }

        [Test]
        public void TheStandingError_IsDeterministic_Slow_SmallerForABetterKeeper_AndNeverZero()
        {
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 9f, 2f, East);
            var t = new GoalkeeperAiTuning();
            GoalkeeperStand a = StandFor(c, t, KeeperSkill(positioning: 0.3f)), b = StandFor(c, t, KeeperSkill(positioning: 0.3f));
            Assert.AreEqual(a.Target.X, b.Target.X); Assert.AreEqual(a.Target.Y, b.Target.Y); Assert.AreEqual(a.Drift, b.Drift);
            float poor = StandFor(c, t, KeeperSkill(positioning: 0.2f)).Drift, good = StandFor(c, t, KeeperSkill(positioning: 0.95f)).Drift;
            Assert.Greater(poor, good, "a poor positioner is further from his place");
            double sumPoor = 0, sumGood = 0, minGood = double.MaxValue;
            for (int i = 0; i < 60; i++) { c.Advance(t.PositionErrorPeriod); float gp = StandFor(c, t, KeeperSkill(positioning: 0.2f)).Drift; float gg = StandFor(c, t, KeeperSkill(positioning: 0.95f)).Drift; sumPoor += gp; sumGood += gg; minGood = Math.Min(minGood, gg); }
            Assert.Greater(sumPoor, 1.5 * sumGood);
            Assert.Greater(t.PositionErrorBase, 0f, "even the best keeper stands a little off his place: he is not perfect");
            Assert.LessOrEqual(poor, (t.PositionErrorBase + t.PositionErrorMax) * t.PositionErrorScale + 0.001f);
        }

        [Test]
        public void TheKeeperNeverStandsBehindHisLineOrBeyondThePosts_WhateverHisError()
        {
            var t = new GoalkeeperAiTuning { PositionErrorBase = 5f, PositionErrorMax = 5f };
            foreach (float x in new[] { -15f, 0f, 14f }) foreach (float y in new[] { -11f, 0f, 11f })
            {
                MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", x, y, East);
                for (int i = 0; i < 12; i++)
                {
                    c.Advance(t.PositionErrorPeriod);
                    GoalkeeperStand s = StandFor(c, t, KeeperSkill(positioning: 0f));
                    Assert.LessOrEqual(s.Target.X, 20f + 0.001f); Assert.GreaterOrEqual(s.Target.X, 20f - t.StandMaxDepth - 0.001f); Assert.LessOrEqual(Math.Abs(s.Target.Y), 2.5f + 0.001f);
                }
            }
        }

        // ================= what he makes of a shot =================

        /// <summary>The home carrier shoots at the Away goal from far enough for the keeper to have time. Returns the controller right after the strike.</summary>
        private static MatchController AShotAtTheGoal(float fromX, float fromY, float aimY)
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs3", fromX, fromY, East);
            Vec2 dir = ActionModel.DirectionOr(new Vec2(20f, aimY) - new Vec2(fromX, fromY), East);
            ActionResult r = c.PerformIntent(new ActionIntent(TeamSide.Home, "hs3", FootballActionKind.Shot, dir, 1f));
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            return c;
        }

        [Test]
        public void AShotIsRead_ButHeCannotStartBeforeHisReactionTime_AndNeverKnowsBeforeTheStrike()
        {
            // before any shot: carrying the ball, nothing to read
            MatchController carrying = Quiet(); Carrier(carrying, TeamSide.Home, "hs3", 2f, 0f, East);
            WorldView cv = ViewOf(carrying);
            Assert.IsFalse(GoalkeeperReaction.Read(cv, cv[cv.IndexOf("as0")], new GoalkeeperAiTuning(), KeeperSkill()).IsShot, "a carried ball is not a shot");

            MatchController c = AShotAtTheGoal(-4f, 0f, 1.5f);
            var t = new GoalkeeperAiTuning(); GoalkeeperSkill skill = KeeperSkill(reaction: 0.30f, reading: 1f);
            WorldView v = ViewOf(c); WorldPlayer keeper = v[v.IndexOf("as0")];
            GoalkeeperRead r = GoalkeeperReaction.Read(v, keeper, t, skill);
            Assert.IsTrue(r.IsShot); Assert.Greater(r.TimeToGoal, 0.3f); Assert.LessOrEqual(Math.Abs(r.Crossing.Y), 3.3f, "inside the mouth (plus the kick's own error)");
            Assert.Greater(r.Delay, 0f); Assert.IsFalse(r.Started, "at the strike he cannot have started");
            PlayerDecision early = new BasicDecisionEngine(c.Pitch).Decide(v, "as0");
            Assert.AreNotEqual(DecisionReason.KeeperSave, early.Reason, "he does not go to the ball before he has had the time to see it");

            // after his reaction time, if the ball is still on its way, he goes (to where he reads it)
            int steps = (int)Math.Ceiling((r.Delay + 0.05f) / Dt);
            for (int i = 0; i < steps; i++) c.Advance(Dt);
            WorldView later = ViewOf(c); GoalkeeperRead r2 = GoalkeeperReaction.Read(later, later[later.IndexOf("as0")], t, skill);
            Assert.IsTrue(r2.IsShot, "the ball is still travelling"); Assert.IsTrue(r2.Started);
            PlayerDecision go = new BasicDecisionEngine(c.Pitch).Decide(later, "as0");
            Assert.AreEqual(DecisionReason.KeeperSave, go.Reason); Assert.AreEqual(DecisionKind.MoveToPoint, go.Kind);
            Assert.LessOrEqual(go.Movement.Magnitude, 1f + 1e-4f, "never more than a full push of the stick");
            Assert.LessOrEqual(Math.Abs(go.Movement.Target.Y), 2.5f + 0.001f); Assert.LessOrEqual(go.Movement.Target.X, 20f);
        }

        [Test]
        public void TheReactionTime_DependsOnTheReflexes_TheViewAndTheDifficulty_AndNeverReachesZero()
        {
            var g = new GoalkeeperParameters(); var skillT = new GoalkeeperTuning();
            float slowHands = GoalkeeperSkillModel.ResolveNormalized(g, 0.1f, 0.5f, 0.5f, 0.5f, 0.5f, skillT).ReactionSeconds, quickHands = GoalkeeperSkillModel.ResolveNormalized(g, 0.9f, 0.5f, 0.5f, 0.5f, 0.5f, skillT).ReactionSeconds;
            Assert.Less(quickHands, slowHands, "better reflexes start sooner");
            // the same keeper resolved from the definition's attributes gives the same answer (one rule, two doors)
            var profile = new GoalkeeperProfile { PlayerId = "x", Reflexes = 80, Positioning = 60, Command = 55, Diving = 70, Handling = 65 };
            PlayerAttributes attrs = PlayerAttributes.CreateDefault();
            GoalkeeperSkill viaDefinition = GoalkeeperSkillModel.Resolve(g, attrs, profile, skillT);
            GoalkeeperSkill viaNormalised = GoalkeeperSkillModel.ResolveNormalized(g, PlayerAttributes.Normalize(80), GoalkeeperSkillModel.PositioningAbility01(attrs, profile, skillT), PlayerAttributes.Normalize(55), PlayerAttributes.Normalize(70), PlayerAttributes.Normalize(65), skillT);
            Assert.AreEqual(viaDefinition.ReactionSeconds, viaNormalised.ReactionSeconds); Assert.AreEqual(viaDefinition.Positioning, viaNormalised.Positioning); Assert.AreEqual(viaDefinition.DecisionMaking, viaNormalised.DecisionMaking);

            float previous = float.MaxValue;
            foreach (DifficultyLevel l in AllLevels)
            {
                GoalkeeperSkill s = GoalkeeperSkillModel.ResolveNormalized(ProfileOf14(l).Goalkeeper, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f, skillT);
                Assert.Less(s.ReactionSeconds, previous, l.ToString()); previous = s.ReactionSeconds;
                Assert.GreaterOrEqual(s.ReactionSeconds, skillT.MinReactionSeconds, l + ": never faster than the human limit"); Assert.Less(s.ReactionSeconds, 1f, l + ": a novice is slow, not absent");
            }

            // the view: players between the shooter and the keeper make him slower and worse at reading
            MatchController c = AShotAtTheGoal(-4f, 0f, 1.5f);
            WorldView clear = ViewOf(c); var t = new GoalkeeperAiTuning();
            GoalkeeperRead open = GoalkeeperReaction.Read(clear, clear[clear.IndexOf("as0")], t, KeeperSkill());
            MatchController crowded = AShotAtTheGoal(-4f, 0f, 1.5f); Put(crowded, TeamSide.Away, "as3", 12f, 0f, West); Put(crowded, TeamSide.Away, "as4", 15f, 0.5f, West);
            WorldView cv = ViewOf(crowded); GoalkeeperRead blocked = GoalkeeperReaction.Read(cv, cv[cv.IndexOf("as0")], t, KeeperSkill());
            Assert.Less(blocked.Visibility, open.Visibility); Assert.Greater(blocked.Delay, open.Delay);
        }

        [Test]
        public void TheReading_IsBetterForABetterReader_ShrinksAsTheBallArrives_AndIsTheSameEveryStep()
        {
            var t = new GoalkeeperAiTuning();
            MatchController c = AShotAtTheGoal(-4f, 0f, 1.5f); WorldView v = ViewOf(c); WorldPlayer k = v[v.IndexOf("as0")];
            GoalkeeperRead poor = GoalkeeperReaction.Read(v, k, t, KeeperSkill(reading: 0.1f)), good = GoalkeeperReaction.Read(v, k, t, KeeperSkill(reading: 1f)), again = GoalkeeperReaction.Read(v, k, t, KeeperSkill(reading: 0.1f));
            Assert.Greater(poor.ReadError, good.ReadError); Assert.AreEqual(0f, good.ReadError, 1e-5f, "a perfect reader reads the line exactly");
            Assert.AreEqual(poor.ReadError, again.ReadError); Assert.AreEqual(poor.Believed.Y, again.Believed.Y);
            Assert.LessOrEqual(poor.ReadError, t.ReadErrorMax * t.ReadErrorScale * 2f + 0.001f, "bounded");
            Assert.Greater(poor.ReadError, 0f);
            // later in the flight he knows more
            for (int i = 0; i < 8; i++) c.Advance(Dt);
            WorldView later = ViewOf(c);
            GoalkeeperRead laterPoor = GoalkeeperReaction.Read(later, later[later.IndexOf("as0")], t, KeeperSkill(reading: 0.1f));
            if (laterPoor.IsShot) Assert.LessOrEqual(laterPoor.ReadError, poor.ReadError + 1e-4f, "the error never grows as the ball arrives");
        }

        [Test]
        public void TheKeeper_ReadsOnlyShots_NotPassesNotSlowBallsNotShotsAwayFromHisGoal_AndNotHisOwnTeamsBalls()
        {
            var t = new GoalkeeperAiTuning();
            MatchController away = Quiet(); Carrier(away, TeamSide.Home, "hs3", 5f, 0f, East);
            ActionResult pass = away.PerformIntent(new ActionIntent(TeamSide.Home, "hs3", FootballActionKind.ShortPass, West, 1f) { Target = PassTarget.ToPosition(new Vec2(-5f, 0f)) });
            WorldView v1 = ViewOf(away);
            Assert.IsFalse(GoalkeeperReaction.Read(v1, v1[v1.IndexOf("as0")], t, KeeperSkill()).IsShot, "a ball going away from his goal");
            MatchController slow = Quiet(); PutBall(slow, 10f, 0f, 3f, 0f);
            WorldView v2 = ViewOf(slow); Assert.IsFalse(GoalkeeperReaction.Read(v2, v2[v2.IndexOf("as0")], t, KeeperSkill()).IsShot, "too slow to be a shot");
            MatchController wide = AShotAtTheGoal(2f, -8f, -10f);   // aimed well outside the post
            WorldView v3 = ViewOf(wide); Assert.IsFalse(GoalkeeperReaction.Read(v3, v3[v3.IndexOf("as0")], t, KeeperSkill()).IsShot, "going wide: no need to move");
        }

        // ================= leaving or staying =================

        [Test]
        public void ABallHeReachesFirstBringsHimOut_AndOneAnOpponentReachesFirstDoesNot()
        {
            var t = new GoalkeeperAiTuning(); GoalkeeperSkill skill = KeeperSkill(decision: 1f);
            MatchController mine = Quiet(); PutBall(mine, 14f, 0f, 0f, 0f);
            Put(mine, TeamSide.Home, "hs3", 6f, 0f, East);
            WorldView v = ViewOf(mine); GoalkeeperExitChoice a = GoalkeeperExit.Choose(v, v[v.IndexOf("as0")], t, skill, 1f, 0f);
            Assert.IsTrue(a.Considered); Assert.IsTrue(a.Go, "the ball is nearer to him than to anybody"); Assert.Less(Math.Abs(a.Target.X - 14f), 3f);

            MatchController theirs = Quiet(); PutBall(theirs, 14f, 0f, 0f, 0f);
            Put(theirs, TeamSide.Home, "hs3", 13f, 0.5f, East);
            WorldView w = ViewOf(theirs); GoalkeeperExitChoice b = GoalkeeperExit.Choose(w, w[w.IndexOf("as0")], t, skill, 1f, 0f);
            Assert.IsFalse(b.Go, "an attacker is on it");

            MatchController far = Quiet(); PutBall(far, -10f, 0f);
            WorldView f = ViewOf(far); Assert.IsFalse(GoalkeeperExit.Choose(f, f[f.IndexOf("as0")], t, skill, 1f, 0f).Considered, "nothing to consider so far from his goal");
            MatchController held = Quiet(); Carrier(held, TeamSide.Home, "hs3", 14f, 0f, East);
            WorldView h = ViewOf(held); Assert.IsFalse(GoalkeeperExit.Choose(h, h[h.IndexOf("as0")], t, skill, 1f, 0f).Go, "he does not rush a player who has the ball");
        }

        [Test]
        public void AKeeperWhoReadsTheGameBadly_MisjudgesMoreOften_ThanOneWhoReadsItWell()
        {
            var t = new GoalkeeperAiTuning(); int poor = 0, good = 0, judged = 0;
            for (int i = 0; i < 80; i++)
            {
                MatchController c = Quiet(); PutBall(c, 14f, 0f, 0f, 0f); Put(c, TeamSide.Home, "hs3", 8f, 0.2f, East);
                c.Advance(i * t.ExitJudgementPeriod + 0.01f);
                PutBall(c, 14f, 0f, 0f, 0f); Put(c, TeamSide.Home, "hs3", 8f, 0.2f, East);
                WorldView v = ViewOf(c); WorldPlayer k = v[v.IndexOf("as0")];
                GoalkeeperExitChoice p = GoalkeeperExit.Choose(v, k, t, KeeperSkill(decision: 0.05f), 0.05f, 0f), g = GoalkeeperExit.Choose(v, k, t, KeeperSkill(decision: 1f), 1f, 0f);
                if (!p.Considered) continue;
                judged++; if (p.Mistake) poor++; if (g.Mistake) good++;
            }
            Assert.Greater(judged, 20); Assert.GreaterOrEqual(poor, good, "the keeper who reads the game badly is wrong at least as often"); Assert.Greater(poor + good, 0, "and the judgement does go wrong sometimes");
        }

        // ================= the difficulty and the modes =================

        [Test]
        public void TheKeeperOfEachLevel_StandsLessWell_TheLowerTheLevel_ButEvenTheBestIsNotPerfect()
        {
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 9f, 2f, East);
            var t = new GoalkeeperAiTuning(); var skillT = new GoalkeeperTuning();
            double previous = double.MaxValue;
            foreach (DifficultyLevel l in AllLevels)
            {
                double sum = 0;
                for (int i = 0; i < 60; i++) { c.Advance(t.PositionErrorPeriod); sum += StandFor(c, t, GoalkeeperSkillModel.ResolveNormalized(ProfileOf14(l).Goalkeeper, 0.7f, 0.7f, 0.7f, 0.7f, 0.7f, skillT)).Drift; }
                Assert.Less(sum, previous, l.ToString()); previous = sum; Assert.Greater(sum, 0.0, l + ": never perfect");
            }
        }

        [Test]
        public void TheDifficultyReachesTheKeeperThroughTheProfile_NotThroughAnAttributeOrALevelBranch()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string file in Directory.GetFiles(Path.Combine(core, "Goalkeeper"), "*.cs"))
            {
                string text = File.ReadAllText(file), name = Path.GetFileName(file);
                foreach (string level in new[] { "DifficultyLevel.", "Novice", "Amateur", "Elite" }) StringAssert.DoesNotContain(level, text, name + " must not branch on a level");
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "Linq", "PlayerDefinition", "PlayerAttributes" }) StringAssert.DoesNotContain(banned, text, name);
                if (name != "ShotQualityEvaluator.cs") Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\.(Speed|Stamina|Reach)\s*(\*|\+|-)?=\s"), name + " writes no attribute");
            }
            DecisionProfile p = ProfileOf14(DifficultyLevel.Elite);
            Assert.IsNotNull(p.Goalkeeper); Assert.AreEqual(DefaultDifficulties.Create(DifficultyLevel.Elite).Goalkeeper.ReactionSeconds, p.Goalkeeper.ReactionSeconds);
            Assert.Less(ProfileOf14(DifficultyLevel.Elite).Goalkeeper.ReactionSeconds, ProfileOf14(DifficultyLevel.Novice).Goalkeeper.ReactionSeconds);
        }

        [Test]
        public void ArenaAndClassic_DifferByNumbersOnly_ClassicKeepersAreBetterPlacedAndStartSooner()
        {
            GoalkeeperAiTuning arena = GoalkeeperAiTuning.ForMode(GameMode.Arena), classic = GoalkeeperAiTuning.ForMode(GameMode.Classic), common = new GoalkeeperAiTuning();
            Assert.AreEqual(ArenaGoalkeeperDefaults.PositionErrorScale, arena.PositionErrorScale); Assert.AreEqual(ClassicGoalkeeperDefaults.PositionErrorScale, classic.PositionErrorScale);
            Assert.Greater(arena.PositionErrorScale, classic.PositionErrorScale); Assert.Greater(arena.ReactionScale, classic.ReactionScale); Assert.Greater(arena.ReadErrorScale, classic.ReadErrorScale);
            foreach (System.Reflection.FieldInfo f in typeof(GoalkeeperAiTuning).GetFields().Where(f => f.FieldType == typeof(float) && f.Name != "PositionErrorScale" && f.Name != "ReactionScale" && f.Name != "ReadErrorScale"))
            { Assert.AreEqual((float)f.GetValue(common), (float)f.GetValue(arena), f.Name); Assert.AreEqual((float)f.GetValue(common), (float)f.GetValue(classic), f.Name); }
            // the same scene, the same keeper: the Arena one stands further from his place
            MatchController c = Quiet(); Carrier(c, TeamSide.Home, "hs3", 9f, 2f, East);
            double a = 0, k = 0; for (int i = 0; i < 40; i++) { c.Advance(common.PositionErrorPeriod); a += StandFor(c, arena, KeeperSkill(positioning: 0.6f)).Drift; k += StandFor(c, classic, KeeperSkill(positioning: 0.6f)).Drift; }
            Assert.Greater(a, k);
        }

        [Test]
        public void TheGoalkeeperNumbers_LiveInOnePlace_AndTheEngineKeepsTheOldRuleWhenSwitchedOff()
        {
            foreach (System.Reflection.FieldInfo f in typeof(GoalkeeperAiDefaults).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                System.Reflection.FieldInfo mirror = typeof(GoalkeeperAiTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(new GoalkeeperAiTuning())), f.Name);
            }
            foreach (System.Reflection.FieldInfo f in typeof(GoalkeeperAiTuning).GetFields().Where(f => f.Name != "ReactionScale" && f.Name != "ReadErrorScale" && f.Name != "PositionErrorScale" && f.Name != "Skill"))
                Assert.IsNotNull(typeof(GoalkeeperAiDefaults).GetField(f.Name), f.Name + " has no default");
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string file in new[] { Path.Combine(core, "Goalkeeper", "GoalkeeperPositioning.cs"), Path.Combine(core, "Goalkeeper", "GoalkeeperReaction.cs"), Path.Combine(core, "Goalkeeper", "GoalkeeperExit.cs") })
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim(); if (line.StartsWith("//")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(Path.GetFileName(file) + ": loose number " + m.Value + " in: " + line);
                }
            // switched off, the keeper plays the old rule (chases the ball inside seven metres, inside the zone)
            MatchController c = Quiet(); PutBall(c, -14f, 2f); Put(c, TeamSide.Home, "hs0", -18f, 0f, East);
            PlayerDecision old = new BasicDecisionEngine(c.Pitch, null, null, null, null, null, new GoalkeeperAiTuning { Enabled = false }).Decide(WorldState.Capture(c).View, "hs0");
            Assert.AreEqual(DecisionReason.KeeperReact, old.Reason);
        }

        // ================= the shot rating =================

        private static ShotContext Context(float x, float y, float vx, float vy, float keeperY = 0f, float defender = 6f, int inLane = 0, bool weak = false)
        {
            return new ShotContext { Shooter = new Vec2(x, y), GoalCentre = new Vec2(20f, 0f), GoalWidth = 5f, BallVelocity = new Vec2(vx, vy), NearestDefender = defender, DefendersInLane = inLane, Keeper = new Vec2(18.5f, keeperY), HasKeeper = true, WeakFoot = weak };
        }

        [Test]
        public void AnEasyShotIsWorthLessThanAHardOne_AndTheDifferenceComesFromTheParts()
        {
            var t = new ShotQualityTuning();
            // easy: far, tight angle, defender on him, the keeper right on the line
            ShotQuality easy = ShotQualityEvaluator.Evaluate(Context(5f, 3f, 16f, -3.2f, keeperY: 0f, defender: 0.8f, inLane: 1, weak: true), t);
            // clear chance: near, wide open, free, the keeper on the other side
            ShotQuality hard = ShotQualityEvaluator.Evaluate(Context(15f, 0.5f, 22f, 4f, keeperY: -1.5f, defender: 8f), t);
            Assert.Greater(hard.Value, easy.Value + 0.2f); Assert.Greater(hard.ExpectedGoal, easy.ExpectedGoal);
            Assert.Greater(hard.Distance, easy.Distance); Assert.Greater(hard.Angle, easy.Angle); Assert.Greater(hard.Pressure, easy.Pressure); Assert.Greater(hard.KeeperPosition, easy.KeeperPosition); Assert.Greater(hard.Foot, easy.Foot);
            foreach (ShotQuality q in new[] { easy, hard }) foreach (float part in new[] { q.Distance, q.Angle, q.Pressure, q.KeeperPosition, q.Foot, q.Speed, q.Aim, q.Value, q.ExpectedGoal }) { Assert.GreaterOrEqual(part, 0f); Assert.LessOrEqual(part, 1f); }
        }

        [Test]
        public void AShotOffTarget_IsWorthNothing_AShotAwayFromTheGoalToo_AndTheRatingNeverReadsTheResult()
        {
            var t = new ShotQualityTuning();
            ShotQuality wide = ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 20f), t), away = ShotQualityEvaluator.Evaluate(Context(15f, 0f, -22f, 0f), t), onTarget = ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 1f), t);
            Assert.AreEqual(0f, wide.Aim, 1e-5f); Assert.AreEqual(0f, wide.Value, 1e-5f); Assert.Less(wide.ExpectedGoal, 0.01f);
            Assert.AreEqual(0f, away.Aim); Assert.Greater(onTarget.ExpectedGoal, wide.ExpectedGoal);
            // the keeper's place counts: the same shot is worth more when the keeper is far from where it crosses
            Assert.Greater(ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 2f, keeperY: -2f), t).Value, ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 2f, keeperY: 2f), t).Value);
            // a closer shot is worth more than a farther one, every other part equal
            Assert.Greater(ShotQualityEvaluator.Evaluate(Context(17f, 0f, 22f, 0f), t).Distance, ShotQualityEvaluator.Evaluate(Context(8f, 0f, 22f, 0f), t).Distance);
            // the mode offset only moves the probability, not the quality
            var classic = new ShotQualityTuning { ModeIsClassic = 1f };
            ShotQuality a = ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 1f), new ShotQualityTuning()), b = ShotQualityEvaluator.Evaluate(Context(15f, 0f, 22f, 1f), classic);
            Assert.AreEqual(a.Value, b.Value); Assert.Less(b.ExpectedGoal, a.ExpectedGoal);
        }

        [Test]
        public void TheObserver_RatesEveryShot_CountsTheSaves_AndChangesNothing()
        {
            var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
            string watched = SimulateAttack(GameMode.Arena, 351, true, 351 % 12, (351 * 5 + 3) % 12, 240f, true, home, DifficultyLevel.Professional, DifficultyLevel.Professional, away).Fingerprint;
            string plain = Match14(GameMode.Arena, 351, DifficultyLevel.Professional, DifficultyLevel.Professional, null, 240f).Fingerprint;
            Assert.AreEqual(plain, watched, "rating and counting are read-only");
            Assert.Greater(home.ShotsRated + away.ShotsRated, 3.0); Assert.AreEqual(home.ShotsRated, home.ShotLogCount);
            Assert.Greater(home.SavesMade + away.SavesMade, 0.0, "the saves are counted");
            for (int i = 0; i < home.ShotLogCount; i++) { ShotRecord r = home.ShotAt(i); Assert.AreEqual(TeamSide.Home, r.Team); Assert.GreaterOrEqual(r.Quality.ExpectedGoal, 0f); Assert.LessOrEqual(r.Quality.ExpectedGoal, 1f); }
            Assert.AreEqual(home.ShotQualitySum / home.ShotsRated, home.ShotQualityAverage, 1e-9);
        }

        // ================= no tricks, and the same every time =================

        [Test]
        public void TheKeeperAi_IsDeterministic_ChangesTheMatch_AndSwitchedOffReturnsToThePhase15Match()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                int seed = mode == GameMode.Arena ? 352 : 372;
                string a = Match14(mode, seed, DifficultyLevel.Professional, DifficultyLevel.Professional, null, 150f).Fingerprint;
                Assert.AreEqual(a, Match14(mode, seed, DifficultyLevel.Professional, DifficultyLevel.Professional, null, 150f).Fingerprint, mode + " repeats");
                string before = Environment.GetEnvironmentVariable("FS27_EXP");
                try
                {
                    Environment.SetEnvironmentVariable("FS27_EXP", "*.Keeper.Enabled=0");
                    string legacy = Match14(mode, seed, DifficultyLevel.Professional, DifficultyLevel.Professional, null, 150f).Fingerprint;
                    Assert.AreNotEqual(a, legacy, mode + ": the keeper AI is really in the match");
                    Assert.AreEqual(legacy, Match14(mode, seed, DifficultyLevel.Professional, DifficultyLevel.Professional, null, 150f).Fingerprint, mode + ": the old rule is also deterministic");
                }
                finally { Environment.SetEnvironmentVariable("FS27_EXP", before); }
            }
        }

        [Test]
        public void TheKeeper_NeverMovesFasterThanHisLegs_NeverLeavesThePitch_AndHisAttributesAreUntouched()
        {
            MatchController c = RealRoles(3, 8, 353, MatchSettings.CreateArena()); Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Arena), DefenseTuning.ForMode(GameMode.Arena), null, AttackPositioningTuning.ForMode(GameMode.Arena), BallCarrierTuning.ForMode(GameMode.Arena), GoalkeeperAiTuning.ForMode(GameMode.Arena));
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Elite), Db.Players, c); driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Novice), Db.Players, c);
            WorldView before = WorldState.Capture(c).View;
            var maxSpeeds = new Dictionary<string, float>(); var qualities = new Dictionary<string, float>();
            for (int i = 0; i < before.PlayerCount; i++) { maxSpeeds[before[i].PlayerId] = before[i].MaxSpeed; qualities[before[i].PlayerId] = before[i].Profile.KeeperReflexes + before[i].Profile.KeeperDiving + before[i].Profile.KeeperPositioning; }
            var world = new WorldState();
            for (int step = 0; step < 150 * 60; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                if (step % 10 != 0) continue;
                world.Refresh(c);
                for (int i = 0; i < world.PlayerCount; i++)
                {
                    WorldPlayer p = world[i]; if (!p.IsOnPitch || !p.IsGoalkeeper) continue;
                    Assert.LessOrEqual(p.Speed, p.MaxSpeed + 0.01f, p.PlayerId + " speed"); Assert.LessOrEqual(Math.Abs(p.Position.X), 21f, "(the arrival momentum can carry him a little past the line, never out of the pitch)"); Assert.LessOrEqual(Math.Abs(p.Position.Y), 13.1f);
                    Assert.AreEqual(maxSpeeds[p.PlayerId], p.MaxSpeed, 1e-4f, "his top speed is his own"); Assert.AreEqual(qualities[p.PlayerId], p.Profile.KeeperReflexes + p.Profile.KeeperDiving + p.Profile.KeeperPositioning, 1e-4f, "so are his capabilities");
                }
            }
        }

        [Test]
        public void TheWorldView_NowSaysHowLongAgoTheBallWasTouched_AndTheKeeperProfileCarriesPositioningAndCommand()
        {
            MatchController c = AShotAtTheGoal(-4f, 0f, 1.5f);
            WorldView v = ViewOf(c); Assert.Less(v.Ball.SecondsSinceTouch, 0.1f);
            for (int i = 0; i < 12; i++) c.Advance(Dt);
            Assert.Greater(ViewOf(c).Ball.SecondsSinceTouch, v.Ball.SecondsSinceTouch + 0.1f);
            WorldPlayer keeper = v[v.IndexOf("as0")];
            Assert.Greater(keeper.Profile.KeeperPositioning, 0f); Assert.Greater(keeper.Profile.KeeperCommand, 0f);
            ActionProfile emergency = ActionProfile.Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Right, 50, null);
            Assert.Greater(emergency.KeeperPositioning, 0f); Assert.Less(emergency.KeeperPositioning, keeper.Profile.KeeperPositioning + 1f, "a field player in goal has his derived positioning");
            Assert.AreEqual(emergency.KeeperPositioning, emergency.KeeperCommand, "and uses it as his command: command is contextual, it has no number of its own");
        }
    }
}
