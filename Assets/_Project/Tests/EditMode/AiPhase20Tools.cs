using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public partial class AiInfrastructureTests
    {
        /// <summary>
        /// Phase 20 probe (measures, changes nothing): the passes into space and the passes in general over many short matches, to read a share that the eight-match tests only sample. Environment: BAT_MODE, BAT_FIRST, BAT_SEEDS,
        /// FS27_EXP (a one-variable experiment), BAT_OUT = the text file.
        /// </summary>
        [Test, Explicit]
        public void Phase20Probe_SpacePasses()
        {
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("BAT_MODE") ?? "Arena");
            int first = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "301"), count = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "40");
            var total = NewDiagnostics();
            for (int k = 0; k < count; k++)
            {
                int seed = first + k;
                AiDiagnostics one = NewDiagnostics();
                SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 120f, true, one);
                total.Merge(one);
            }
            double passes = total.PassAttempts, done = total.PassOutcomeCount(PassOutcome.Completed);
            string text = mode + " seeds " + first + ".." + (first + count - 1) + " exp='" + Environment.GetEnvironmentVariable("FS27_EXP") + "': space attempts " + total.SpaceAttempts + ", completed " + total.SpaceOutcomeCount(PassOutcome.Completed) + " (" + (total.SpaceAttempts > 0 ? (total.SpaceOutcomeCount(PassOutcome.Completed) / total.SpaceAttempts).ToString("F3") : "-") + "); all passes " + passes + ", completed " + done + " (" + (done / Math.Max(1.0, passes)).ToString("F3") + ")";
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, text + "\n");
            TestContext.WriteLine(text);
        }
    }
}
