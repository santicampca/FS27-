using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 32: the runner of the GameplayBench (explicit, writes CSV + markdown) and the guards that keep it honest (always on, small).</summary>
    public partial class AiInfrastructureTests
    {
        private static List<GbRow> GbMatrix(IEnumerable<GbScenario> scenes, IEnumerable<GbConfig> configs, IEnumerable<int> seeds, string variant = "base")
        {
            var rows = new List<GbRow>();
            foreach (GbScenario s in scenes)
                foreach (GbConfig cfg in configs)
                    foreach (int seed in seeds)
                        rows.Add(new GbRow { Scenario = s.Id, Family = s.Family, Variant = variant, Config = cfg.Name, Seed = seed, M = GbRun(s, cfg, seed) });
            return rows;
        }

        /// <summary>Rows of an earlier CSV (config "cur" only), renamed to the config <paramref name="as"/>, to compare a change of the code (or of FS27_EXP) with a stored run.</summary>
        private static List<GbRow> GbLoad(string path, string asConfig)
        {
            string[] lines = File.ReadAllLines(path); string[] head = lines[0].Split(','); var rows = new List<GbRow>();
            foreach (string line in lines.Skip(1))
            {
                string[] f = line.Split(',');
                if (f.Length != head.Length || f[3] != "cur") continue;
                var m = new Dictionary<string, double>();
                for (int i = 5; i < head.Length; i++) m[head[i]] = f[i] == "NA" ? double.NaN : double.Parse(f[i], System.Globalization.CultureInfo.InvariantCulture);
                rows.Add(new GbRow { Scenario = f[0], Family = f[1], Variant = f[2], Config = asConfig, Seed = int.Parse(f[4]), M = m });
            }
            return rows;
        }

        /// <summary>GB_VS=path of an earlier gameplay_bench.csv: instead of ref/cur, runs the current code (and FS27_EXP) as "cur" and compares it with the stored "cur" rows (as "base"), pair by pair.</summary>
        /// <summary>GB_SEEDS (default 12), GB_FIRST, GB_ONLY (comma list of scene ids or families), GB_OUT (directory), GB_CONFIGS ("ref,cur" default; also "cur" alone).</summary>
        [Test, Explicit]
        public void GameplayBench_Run()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("GB_SEEDS") ?? "12"), first = int.Parse(Environment.GetEnvironmentVariable("GB_FIRST") ?? "1");
            string only = Environment.GetEnvironmentVariable("GB_ONLY"), dir = Environment.GetEnvironmentVariable("GB_OUT") ?? Path.Combine(Path.GetTempPath(), "gameplay_bench");
            string[] filter = string.IsNullOrEmpty(only) ? null : only.Split(',');
            List<GbScenario> scenes = GbScenes().Where(s => filter == null || filter.Contains(s.Id) || filter.Contains(s.Family)).ToList();
            string vs = Environment.GetEnvironmentVariable("GB_VS");
            var cfgs = vs == null ? new List<GbConfig> { GbReference, GbCurrent } : new List<GbConfig> { GbCurrent };
            // GB_ATTR=1: the reference plus ONE change at a time (tactics, keeper distribution, aim assist), then all of them; each is compared with the reference
            // GB_ATTR=cross: the reference, the Phase-35 cross (real action only) and the Phase-36 cross play, to see what the team play adds
            if (Environment.GetEnvironmentVariable("GB_ATTR") == "cross")
                cfgs = new List<GbConfig> { GbReference, GbCurrent.With("cross35", x => x.CrossPlay = false), GbCurrent };
            // GB_ATTR=aerial: the Phase-36 game (aerial rules and high-ball play off) against the Phase-37 one, and the reference
            if (Environment.GetEnvironmentVariable("GB_ATTR") == "aerial")
                cfgs = new List<GbConfig> { GbReference, GbCurrent.With("noaerial", x => x.Aerial = false), GbCurrent };
            if (Environment.GetEnvironmentVariable("GB_ATTR") == "1")
                cfgs = new List<GbConfig> { GbReference, GbReference.With("tactics", x => x.Tactics = true), GbReference.With("keeper", x => x.Distribution = true), GbReference.With("assist", x => x.Assist = true), GbCurrent };
            List<GbRow> rows = GbMatrix(scenes, cfgs, Enumerable.Range(first, n));
            string refName = GbReference.Name;
            if (vs != null) { rows.AddRange(GbLoad(vs, "base").Where(r => scenes.Any(s => s.Id == r.Scenario) && r.Seed >= first && r.Seed < first + n)); refName = "base"; }
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "gameplay_bench.csv"), GbCsv(rows));
            string md = ""; int better = 0, worse = 0;
            foreach (GbConfig cf in cfgs.Where(x => x.Name != refName && x.Name != "base" && !(vs != null && x.Name == "cur" && false)))
            {
                if (vs == null && cf.Name == refName) continue;
                string part = GbCompare(scenes, rows, refName, cf.Name, out int b, out int w); better += b; worse += w;
                md += "## " + refName + " vs " + cf.Name + " (improved " + b + ", worsened " + w + ")\n\n" + part;
            }
            File.WriteAllText(Path.Combine(dir, "gameplay_bench.md"), "# GameplayBench\n\n" + scenes.Count + " scenes x " + n + " paired seeds. Improved metrics: " + better + ", worsened: " + worse + ".\n\n" + md);
            Console.WriteLine("GameplayBench: improved " + better + ", worsened " + worse + " -> " + dir);
        }

        private static readonly string[] GbFast = { "A1_BuildUp", "B1_UnderPressure", "C2_CompactDefence", "A2_WingPlay" };

        [Test]
        public void GameplayBench_IsDeterministic_AndComplete()
        {
            foreach (GbScenario s in GbScenes().Where(x => GbFast.Contains(x.Id)))
            {
                var a = GbRun(s, GbCurrent, 4); var b = GbRun(s, GbCurrent, 4);
                foreach (string k in AllKeys.Concat(DerivedKeys))
                {
                    Assert.IsTrue(a.ContainsKey(k), s.Id + ": missing " + k);
                    Assert.AreEqual(a[k], b[k], 0.0, s.Id + ": " + k + " is not deterministic");
                }
                foreach (string k in s.Judge.Keys) Assert.IsTrue(a.ContainsKey(k), s.Id + ": judged metric " + k + " is not recorded");
            }
        }

        [Test]
        public void GameplayBench_SceneIdsAreUnique_AndJudgedMetricsExist()
        {
            List<GbScenario> scenes = GbScenes();
            Assert.AreEqual(scenes.Count, scenes.Select(s => s.Id).Distinct().Count());
            var keys = new HashSet<string>(AllKeys.Concat(DerivedKeys));
            Assert.AreEqual(keys.Count, AllKeys.Concat(DerivedKeys).Count(), "duplicate metric names");
            foreach (GbScenario s in scenes) foreach (string k in s.Judge.Keys) Assert.IsTrue(keys.Contains(k), s.Id + " judges unknown metric " + k);
        }

        [Test]
        public void GameplayBench_Sanity_PressureLosesBallsMoreThanSpace()
        {
            // a carrier alone with room (A1) keeps the ball longer than one closed down at 1.5 m (B1): the bench can tell a pressed scene from a free one
            GbScenario free = GbScenes().First(s => s.Id == "A1_BuildUp"), pressed = GbScenes().First(s => s.Id == "B1_UnderPressure");
            double f = 0, p = 0;
            for (int seed = 1; seed <= 6; seed++) { f += GbRun(free, GbCurrent, seed)["firstLossAt"]; p += GbRun(pressed, GbCurrent, seed)["firstLossAt"]; }
            Assert.Greater(f, p, "a free carrier should keep the ball longer than a pressed one");
        }
    
        /// <summary>The variation axes: the Home team's difficulty (Novice..Elite) against a Professional rival, and the same scenes with other team pairings (coaches, attributes). Mean of the scorecard per level; a harder level should not play worse.</summary>
        [Test, Explicit]
        public void GameplayBench_Axes()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("GB_SEEDS") ?? "8"); string dir = Environment.GetEnvironmentVariable("GB_OUT") ?? Path.Combine(Path.GetTempPath(), "gameplay_bench");
            string[] ids = (Environment.GetEnvironmentVariable("GB_ONLY") ?? "A1_BuildUp,A4_RunsOffTheBall,B1_UnderPressure,C1_PressingBuildUp,C2_CompactDefence").Split(',');
            List<GbScenario> scenes = GbScenes().Where(s => ids.Contains(s.Id)).ToList();
            var sb = new StringBuilder("# GameplayBench: variation axes\n\n");
            var rows = new List<GbRow>();
            foreach (DifficultyLevel lv in Enum.GetValues(typeof(DifficultyLevel)))
                rows.AddRange(GbMatrix(scenes, new[] { GbCurrent.With("home-" + lv, x => { x.Home = lv; }) }, Enumerable.Range(1, n), "difficulty"));
            // whole matches between levels: a harder team must win, and win by a clear margin
            if (Environment.GetEnvironmentVariable("GB_GAP") == "1")
            {
                GbScenario m1 = GbScenes().First(x => x.Id == "M1_FullMatch");
                foreach (var gap in new[] { (DifficultyLevel.Elite, DifficultyLevel.Novice), (DifficultyLevel.Novice, DifficultyLevel.Elite), (DifficultyLevel.Expert, DifficultyLevel.Amateur), (DifficultyLevel.Professional, DifficultyLevel.Professional) })
                    rows.AddRange(GbMatrix(new[] { m1 }, new[] { GbCurrent.With(gap.Item1 + "-v-" + gap.Item2, x => { x.Home = gap.Item1; x.Away = gap.Item2; }) }, Enumerable.Range(1, n), "gap"));
                var gsb = new StringBuilder("config,goalsFor,goalsAgainst,xgFor,xgAgainst,shotsFor,shotsAgainst,passOkFor,passOkAgainst\n");
                foreach (string cfg in rows.Where(r => r.Variant == "gap").Select(r => r.Config).Distinct())
                {
                    var rs = rows.Where(r => r.Variant == "gap" && r.Config == cfg).ToList();
                    Func<string, string> mean = k => rs.Average(r => r.M[k]).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    gsb.AppendLine(string.Join(",", cfg, mean("h_goals"), mean("a_goals"), mean("h_xg"), mean("a_xg"), mean("h_shots"), mean("a_shots"), mean("h_passOkRate"), mean("a_passOkRate")));
                }
                Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "gameplay_gap.csv"), gsb.ToString());
                Console.WriteLine(gsb.ToString());
            }
            foreach (var pair in new[] { (3, 8), (8, 3), (0, 5), (5, 0), (1, 9), (9, 1) })
                rows.AddRange(GbMatrix(scenes, new[] { GbCurrent.With("teams-" + pair.Item1 + "v" + pair.Item2, x => { x.HomeTeam = pair.Item1; x.AwayTeam = pair.Item2; }) }, Enumerable.Range(1, n), "teams"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "gameplay_axes.csv"), GbCsv(rows));
            foreach (GbScenario s in scenes)
            {
                sb.Append("### ").Append(s.Id).Append("\n\n| config | ").Append(string.Join(" | ", s.Judge.Keys)).Append(" |\n|---|").Append(string.Concat(s.Judge.Keys.Select(k => "---|"))).Append('\n');
                foreach (string cfg in rows.Where(r => r.Scenario == s.Id).Select(r => r.Config).Distinct())
                    sb.Append("| ").Append(cfg).Append(" | ").Append(string.Join(" | ", s.Judge.Keys.Select(k => { var v = rows.Where(r => r.Scenario == s.Id && r.Config == cfg).Select(r => r.M[k]).Where(x => !double.IsNaN(x)).ToList(); return v.Count == 0 ? "NA" : v.Average().ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }))).Append(" |\n");
                sb.Append('\n');
            }
            File.WriteAllText(Path.Combine(dir, "gameplay_axes.md"), sb.ToString());
        }
    
        // ------------------------------------------------------------------ the committed baseline (a guard against regressions)

        private static readonly string[] GbGuardScenes = { "A1_BuildUp", "A2_WingPlay", "A5_FinalThird", "B1_UnderPressure", "B2_TransitionDefence", "C2_CompactDefence", "C4_CoverSecond", "K_GoalKick", "H2_AssistShots" };
        private const int GbGuardSeeds = 6;

        private static string GbBaselinePath() { return Path.Combine(new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName, "Docs", "gameplay_bench_baseline.csv"); }

        private static Dictionary<string, double> GbGuardMeans()
        {
            var means = new Dictionary<string, double>();
            foreach (GbScenario s in GbScenes().Where(x => GbGuardScenes.Contains(x.Id)))
            {
                var rows = Enumerable.Range(1, GbGuardSeeds).Select(seed => GbRun(s, GbCurrent, seed)).ToList();
                foreach (string k in s.Judge.Keys) { var v = rows.Select(r => r[k]).Where(x => !double.IsNaN(x)).ToList(); if (v.Count > 0) means[s.Id + "|" + k] = v.Average(); }
            }
            return means;
        }

        /// <summary>Rewrites Docs/gameplay_bench_baseline.csv from the current Core (do it on purpose, after a change you have judged with the full bench: GB_BASELINE=write).</summary>
        [Test, Explicit]
        public void GameplayBench_WriteBaseline()
        {
            var sb = new StringBuilder("scene,metric,mean\n");
            foreach (var kv in GbGuardMeans().OrderBy(k => k.Key)) { string[] p = kv.Key.Split('|'); sb.Append(p[0]).Append(',').Append(p[1]).Append(',').Append(kv.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append('\n'); }
            File.WriteAllText(GbBaselinePath(), sb.ToString());
        }

        [Test]
        public void GameplayBench_NoJudgedMetric_GotWorseThanTheCommittedBaseline()
        {
            Assert.IsTrue(File.Exists(GbBaselinePath()), "Docs/gameplay_bench_baseline.csv is missing: GameplayBench_WriteBaseline writes it");
            var baseline = new Dictionary<string, double>();
            foreach (string line in File.ReadAllLines(GbBaselinePath()).Skip(1)) { string[] f = line.Split(','); if (f.Length == 3) baseline[f[0] + "|" + f[1]] = double.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture); }
            Dictionary<string, double> now = GbGuardMeans();
            var scenes = GbScenes().ToDictionary(s => s.Id);
            var bad = new List<string>();
            foreach (var kv in baseline)
            {
                if (!now.TryGetValue(kv.Key, out double cur)) { bad.Add(kv.Key + ": no longer measured"); continue; }
                string[] p = kv.Key.Split('|'); int direction = scenes[p[0]].Judge[p[1]];
                double tolerance = Math.Max(0.25 * Math.Abs(kv.Value), 0.1);   // the runs are deterministic but any change of behaviour moves them: only a clear step in the bad direction is a regression
                if ((cur - kv.Value) * direction < -tolerance) bad.Add(kv.Key + ": " + kv.Value.ToString("0.00") + " -> " + cur.ToString("0.00"));
            }
            Assert.IsEmpty(bad, "judged metrics clearly worse than the committed baseline (if a change is meant to do this, justify it and rewrite the baseline):\n" + string.Join("\n", bad));
        }
    }
}
