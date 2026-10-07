using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 14 explicit investigation tools (not part of the normal run).</summary>
    public partial class AiInfrastructureTests
    {
        /// <summary>Why does the ball so often come back to the passer within a tenth of a second? Logs the events around each such pass.</summary>
        [Test, Explicit]
        public void Phase14Tool_PassReturnInvestigation()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT");
            var sb = new StringBuilder();
            int passes = 0, returned = 0, returnedFast = 0;
            var causes = new Dictionary<string, int>();
            for (int k = 0; k < 4; k++)
            {
                int seed = 301 + k;
                MatchController c = RealRoles(seed % 12, (seed * 5 + 3) % 12, seed, MatchSettings.CreateArena());
                c.ConfirmRestart();
                var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Arena), DefenseTuning.ForMode(GameMode.Arena), null, AttackPositioningTuning.ForMode(GameMode.Arena), BallCarrierTuning.ForMode(GameMode.Arena));
                int seen = 0; string passer = null; int since = 0; GameplayEvent passEvent = default(GameplayEvent);
                for (int step = 0; step < 7200; step++)
                {
                    driver.Step(c); c.Advance(Dt);
                    if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                    else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                    var log = c.Events.Log;
                    for (; seen < log.Count; seen++) if (log[seen].Kind == GameplayEventKind.PassPerformed) { passes++; passer = log[seen].PlayerId; since = 0; passEvent = log[seen]; }
                    if (passer != null)
                    {
                        since++;
                        MatchBall b = c.State.Ball;
                        if (b.HasPossession && b.PossessingPlayerId == passer)
                        {
                            returned++; if (since <= 6) returnedFast++;
                            string key = "mode " + b.Mode + " since " + (since <= 6 ? "<=6" : since <= 18 ? "<=18" : "later");
                            causes[key] = causes.TryGetValue(key, out int n) ? n + 1 : 1;
                            if (returnedFast <= 10 && since <= 6)
                            {
                                sb.AppendLine("seed " + seed + " pass " + passEvent.Detail + " by " + passer + " t=" + passEvent.MatchTime.ToString("F3") + " steps " + since + " ball mode " + b.Mode + " speed " + b.Physics.HorizontalSpeed.ToString("F1") + " lastTouch " + b.LastTouchPlayerId);
                                for (int q = Math.Max(0, log.Count - 6); q < log.Count; q++) sb.AppendLine("      " + log[q] + " @" + log[q].MatchTime.ToString("F3"));
                            }
                            passer = null;
                        }
                        else if (b.HasPossession || since > 120) passer = null;
                    }
                }
            }
            sb.AppendLine("passes " + passes + " returnedToPasserNext " + returned + " fast " + returnedFast);
            foreach (var kv in causes) sb.AppendLine("  " + kv.Key + " " + kv.Value);
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "phase14_passreturn.txt"), sb.ToString());
        }
    }
}
