using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 31: the aim assistance of a person's passes and shots (off in the Core, on in the games that ask for it): "roughly towards him" becomes a pass to him with the power the ball needs; a push at the goal becomes a
    /// shot at the corner aimed at. The campaign (explicit) measures it with a scripted person who aims with a seeded error and counts where his passes end.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static Vec2 Aim(float degrees) { float r = degrees * MathUtil.Deg2Rad; return new Vec2((float)Math.Cos(r), (float)Math.Sin(r)); }

        private static MatchController AssistScene(bool assist)
        {
            MatchController c = Quiet();
            c.AimAssist.Enabled = assist;
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            return c;
        }

        [Test]
        public void TheAssist_IsOff_UntilAGameTurnsItOn()
        {
            Assert.IsFalse(new HumanAssistTuning().Enabled);
            Assert.IsFalse(Quiet().AimAssist.Enabled);
        }

        [Test]
        public void APassPushedRoughlyAtATeammate_GoesToHim_WithTheAssist_AndJustInTheDirectionWithout()
        {
            foreach (bool assist in new[] { false, true })
            {
                MatchController c = AssistScene(assist);
                Put(c, TeamSide.Home, "hs2", 12f, 3f, East);   // 14 degrees off the east axis
                ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, Aim(-6f), 0.7f));
                Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
                if (assist) { Assert.AreEqual(PassTargetKind.Player, r.TargetKind); Assert.AreEqual("hs2", r.TargetPlayerId); }
                else Assert.AreEqual(PassTargetKind.None, r.TargetKind);
            }
        }

        [Test]
        public void TheAssist_PrefersTheFreeTeammate_ToACoveredOne_AndDoesNotInventATargetOutsideTheCone()
        {
            MatchController c = AssistScene(true);
            Put(c, TeamSide.Home, "hs2", 11f, 1.5f, East); Put(c, TeamSide.Away, "as2", 11.5f, 1.5f, West);   // covered
            Put(c, TeamSide.Home, "hs3", 12f, -2f, East);                                                      // free
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, Aim(0f), 0.7f));
            Assert.AreEqual("hs3", r.TargetPlayerId, "the open one");
            MatchController d = AssistScene(true);
            Put(d, TeamSide.Home, "hs2", 0f, 12f, East);   // 90 degrees from the push
            ActionResult none = d.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, Aim(0f), 0.7f));
            Assert.AreEqual(PassTargetKind.None, none.TargetKind, "nobody in the cone: the ball goes where he pushed");
        }

        [Test]
        public void AFarTeammate_GetsALoftedPass_AndANearOneAGroundPass()
        {
            MatchController far = AssistScene(true); Put(far, TeamSide.Home, "hs2", 20f, 0f, East);
            Assert.AreEqual(FootballActionKind.LongPass, far.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, Aim(0f), 0.7f)).Kind);
            MatchController near = AssistScene(true); Put(near, TeamSide.Home, "hs2", 8f, 0f, East);
            Assert.AreEqual(FootballActionKind.ShortPass, near.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.B, ActionGesture.Tap, Aim(0f), 0.7f)).Kind);
        }

        [Test]
        public void AShotPushedAtTheGoal_GoesToTheCornerAimedAt_WithTheAssist()
        {
            foreach (float degrees in new[] { 6f, -6f })
            {
                MatchController c = Quiet(); c.AimAssist.Enabled = true;
                Carrier(c, TeamSide.Home, "hs1", 12f, 0f, East);
                ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.A, ActionGesture.SwipeDown, Aim(degrees), 0.9f));
                Assert.AreEqual(FootballActionKind.Shot, r.Kind); Assert.AreEqual(PassTargetKind.Position, r.TargetKind);
                Assert.AreEqual(degrees > 0f ? 1 : -1, Math.Sign(r.Direction.Y), "towards the side he aimed at");
            }
            MatchController away = Quiet(); away.AimAssist.Enabled = true;
            Carrier(away, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(PassTargetKind.None, away.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.A, ActionGesture.SwipeDown, Aim(80f), 0.9f)).TargetKind, "aimed away from the goal: untouched");
        }

        // ---------------------------------------------------------------- the campaign: a scripted person with a seeded aiming error

        private sealed class PassOutcomes { public int Passes, ToIntended, ToTeammate, Lost, Nobody; public int Goals, Shots; }

        private static PassOutcomes PlayWithAScriptedPerson(int seed, bool assist, float noiseDegrees, float seconds)
        {
            MatchController c = RealRoles(3, 8, seed, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            c.AimAssist.Enabled = assist; c.SetControl(TeamSide.Home, ControlSource.Human);
            AttackPositioningTuning attackT = AttackPositioningTuning.ForMode(GameMode.Classic);
            foreach (string item in (Environment.GetEnvironmentVariable("HA_ATT") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { string[] kv = item.Split('='); typeof(AttackPositioningTuning).GetField(kv[0].Trim()).SetValue(attackT, float.Parse(kv[1], CultureInfo.InvariantCulture)); }
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Classic), DefenseTuning.ForMode(GameMode.Classic), null, attackT, BallCarrierTuning.ForMode(GameMode.Classic), GoalkeeperAiTuning.ForMode(GameMode.Classic));
            var profileT = new DecisionProfileTuning();
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            var rng = new Random(seed * 7919 + 13);
            var o = new PassOutcomes();
            string intended = null; float passAt = -10f; string passer = null;
            c.Events.Published += ev =>
            {
                if (ev.Kind == GameplayEventKind.GoalScored && ev.HasTeam && ev.Team == TeamSide.Home) o.Goals++;
                if (ev.Kind == GameplayEventKind.ShotPerformed && ev.HasTeam && ev.Team == TeamSide.Home) o.Shots++;
                if (intended == null || ev.Kind != GameplayEventKind.BallTouched || !ev.HasTeam) return;
                float since = c.State.MatchTimeSeconds - passAt;
                if (ev.PlayerId == passer && since < 0.4f) return;
                if (since > 3.5f) { o.Nobody++; intended = null; return; }
                if (ev.PlayerId == intended) o.ToIntended++; else if (ev.Team == TeamSide.Home) o.ToTeammate++; else o.Lost++;
                intended = null;
            };
            Vec2 stick = Vec2.Zero;
            int steps = (int)(seconds / Dt);
            for (int step = 0; step < steps && c.State.Phase != MatchPhase.Finished; step++)
            {
                driver.Step(c);
                if (c.State.Phase == MatchPhase.Playing)
                {
                    string id = c.ControlledPlayerId(TeamSide.Home); MatchPlayer me = id == null ? null : c.State.Home.Find(id);
                    NormalizedInput input = NormalizedInput.Stick(stick);
                    if (me != null && step % 12 == 0)
                    {
                        float sign = c.State.HomeAttacksPositiveX ? 1f : -1f;
                        Vec2 goal = c.Pitch.OpponentGoalCentre(c.State.HomeAttacksPositiveX, TeamSide.Home);
                        if (c.BallCarrier == me)
                        {
                            MatchPlayer pick = null; float bestProgress = float.NegativeInfinity;
                            foreach (MatchPlayer q in c.State.Home.Players)
                            {
                                if (q == me || !q.IsActive || q.InGoal) continue;
                                float d = (q.Locomotion.Position - me.Locomotion.Position).Magnitude; float progress = (q.Locomotion.Position.X - me.Locomotion.Position.X) * sign;
                                if (d < 6f || d > 18f || progress < 1f) continue;
                                float free = c.State.Away.Players.Where(r => r.IsActive).Select(r => (r.Locomotion.Position - q.Locomotion.Position).Magnitude).DefaultIfEmpty(99f).Min();
                                if (free < 2.5f) continue;
                                if (progress > bestProgress) { bestProgress = progress; pick = q; }
                            }
                            if (pick != null)
                            {
                                Vec2 want = (pick.Locomotion.Position - me.Locomotion.Position).Normalized;
                                float noise = ((float)rng.NextDouble() * 2f - 1f) * noiseDegrees * MathUtil.Deg2Rad;
                                Vec2 aim = new Vec2(want.X * (float)Math.Cos(noise) - want.Y * (float)Math.Sin(noise), want.X * (float)Math.Sin(noise) + want.Y * (float)Math.Cos(noise));
                                input = NormalizedInput.Press(aim, ActionButton.B, ActionGesture.Tap, aim, 0.7f);
                                intended = pick.PlayerId; passer = me.PlayerId; passAt = c.State.MatchTimeSeconds; o.Passes++;
                            }
                            else stick = (goal - me.Locomotion.Position).Normalized;
                        }
                        else stick = (c.State.Ball.Physics.Position.X - me.Locomotion.Position.X) == 0f ? Vec2.Zero : (new Vec2(c.State.Ball.Physics.Position.X, c.State.Ball.Physics.Position.Z) - me.Locomotion.Position).Normalized;
                    }
                    c.SubmitHumanInput(TeamSide.Home, input);
                }
                c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
            }
            return o;
        }

        [Test, Explicit]
        public void HumanAssistCampaign_Prints()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("HA_SEEDS") ?? "12"); float noise = float.Parse(Environment.GetEnvironmentVariable("HA_NOISE") ?? "20", CultureInfo.InvariantCulture);
            var sb = new StringBuilder("assist,seed,passes,toIntended,toOtherTeammate,lost,nobody,shots,goals\n");
            foreach (bool assist in new[] { false, true })
                for (int seed = 1; seed <= n; seed++) { PassOutcomes o = PlayWithAScriptedPerson(seed, assist, noise, 300f); sb.AppendLine(string.Join(",", assist ? "on" : "off", seed, o.Passes, o.ToIntended, o.ToTeammate, o.Lost, o.Nobody, o.Shots, o.Goals)); }
            string path = Environment.GetEnvironmentVariable("HA_OUT"); if (!string.IsNullOrEmpty(path)) System.IO.File.WriteAllText(path, sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
