using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public partial class ActionsAndBallTests
    {
        /// <summary>
        /// Phase 19 probe (measures, asserts nothing about the result): the quality the Core gives to the same player's short pass, wall pass, through ball and long pass, with the same attributes, foot and stamina.
        /// It was written to check a reading of the code (ShortPass has no case of its own in ResolveKick and falls into the goalkeeper-distribution formula). BAT_OUT = the text file.
        /// </summary>
        [Test, Explicit]
        public void Phase19Probe_PassQualityByKind()
        {
            var sb = new StringBuilder("attributes,kind,quality\n");
            foreach (int level in new[] { 40, 60, 80, 95 })
                foreach (FootballActionKind kind in new[] { FootballActionKind.ShortPass, FootballActionKind.WallPass, FootballActionKind.ThroughPass, FootballActionKind.LongPass })
                {
                    var c = Live(lookup: Uniform(level, "hs1"));
                    Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                    ActionResult r = Do(c, TeamSide.Home, "hs1", kind, East, 0.6f);
                    sb.AppendLine(level + "," + kind + "," + r.Quality.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                }
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }
    }
}
