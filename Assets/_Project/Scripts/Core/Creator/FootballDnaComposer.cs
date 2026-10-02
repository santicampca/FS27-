using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>What a person decided by hand. It wins over everything inferred.</summary>
    [Serializable]
    public sealed class ManualPreferences
    {
        public SortedDictionary<string, float> Params = new SortedDictionary<string, float>(StringComparer.Ordinal);
        public List<BehaviorEntry> Behaviors = new List<BehaviorEntry>();
        public List<string> RemoveBehaviors = new List<string>();
    }

    public sealed class ComposerInput
    {
        // The attributes are NOT stored here: only PlayerDefinition stores a player's attributes. They are passed to Compose as an argument.
        public PlayerPlayingProfile Profile;
        public IEnumerable<FootballObservation> Observations;
        public ManualPreferences Manual;
        /// <summary>0 = no variation. Otherwise adds a small, reproducible difference to inferred (not observed, not manual) tendencies, so players of one archetype are not clones.</summary>
        public uint VariationSeed;
        public bool DeriveBehaviors = true;
    }

    public sealed class ComposerResult
    {
        public FootballDNA Dna = new FootballDNA();
        public readonly List<AggregatedPattern> Patterns = new List<AggregatedPattern>();
        /// <summary>Per tendency / behaviour: how its value was reached, stage by stage.</summary>
        public readonly List<string> Explanation = new List<string>();
        public readonly List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// attributes + playing profile + observations + manual preferences = a <see cref="FootballDNA"/>. Deterministic: the same input (in any
    /// order) always gives the same DNA, and every number can be explained. Stages, each one gentler than the next is firm:
    ///   1. attributes NUDGE tendencies (a fast player is not automatically someone who dribbles),
    ///   2. the playing profile and roles pull them towards what that kind of player does,
    ///   3. observations pull towards what was SEEN, in proportion to how sure we are,
    ///   4. manual preferences set exact values.
    /// It names no player and invents nothing: with no input it returns neutral DNA.
    /// </summary>
    public static class FootballDnaComposer
    {
        /// <summary>How far attributes may move a tendency away from neutral (0.5 = at most 0.25 either way).</summary>
        public const float AttributeInfluence = 0.5f;
        public const float DeriveThreshold = 0.72f;
        public const int MaxDerivedBehaviors = 4;
        public const float VariationSize = 0.04f;

        private struct AW
        {
            public PlayerAttributeId A;
            public float W;
            public AW(PlayerAttributeId a, float w) { A = a; W = w; }
        }

        private static readonly Dictionary<string, AW[]> AttributeTable = new Dictionary<string, AW[]>
        {
            { "movement.accelerationTendency", new[] { new AW(PlayerAttributeId.Acceleration, 1f) } },
            { "movement.turningTendency", new[] { new AW(PlayerAttributeId.Agility, 1f) } },
            { "movement.spaceSeeking", new[] { new AW(PlayerAttributeId.Speed, 0.5f), new AW(PlayerAttributeId.Stamina, 0.5f) } },
            { "dribbling.takeOn", new[] { new AW(PlayerAttributeId.Dribbling, 0.6f), new AW(PlayerAttributeId.Agility, 0.4f) } },
            { "dribbling.closeControl", new[] { new AW(PlayerAttributeId.Control, 1f) } },
            { "dribbling.changeOfPace", new[] { new AW(PlayerAttributeId.Acceleration, 0.6f), new AW(PlayerAttributeId.Dribbling, 0.4f) } },
            { "dribbling.bodyFeint", new[] { new AW(PlayerAttributeId.Technique, 0.6f), new AW(PlayerAttributeId.Agility, 0.4f) } },
            { "possession.holdUp", new[] { new AW(PlayerAttributeId.Strength, 0.6f), new AW(PlayerAttributeId.Control, 0.4f) } },
            { "possession.shielding", new[] { new AW(PlayerAttributeId.Strength, 0.7f), new AW(PlayerAttributeId.Control, 0.3f) } },
            { "passing.progressive", new[] { new AW(PlayerAttributeId.Passing, 0.6f), new AW(PlayerAttributeId.Technique, 0.4f) } },
            { "passing.throughBall", new[] { new AW(PlayerAttributeId.Passing, 0.7f), new AW(PlayerAttributeId.Technique, 0.3f) } },
            { "passing.short", new[] { new AW(PlayerAttributeId.Control, 0.5f), new AW(PlayerAttributeId.Passing, 0.5f) } },
            { "shooting.frequency", new[] { new AW(PlayerAttributeId.Shooting, 0.5f), new AW(PlayerAttributeId.Finishing, 0.5f) } },
            { "shooting.longShot", new[] { new AW(PlayerAttributeId.Shooting, 1f) } },
            { "shooting.power", new[] { new AW(PlayerAttributeId.Shooting, 0.6f), new AW(PlayerAttributeId.Strength, 0.4f) } },
            { "shooting.finesse", new[] { new AW(PlayerAttributeId.Technique, 0.5f), new AW(PlayerAttributeId.Finishing, 0.5f) } },
            { "shooting.insideBox", new[] { new AW(PlayerAttributeId.Finishing, 1f) } },
            { "defending.pressing", new[] { new AW(PlayerAttributeId.Defense, 0.5f), new AW(PlayerAttributeId.Stamina, 0.5f) } },
            { "defending.marking", new[] { new AW(PlayerAttributeId.Defense, 1f) } },
            { "defending.interception", new[] { new AW(PlayerAttributeId.Defense, 0.6f), new AW(PlayerAttributeId.Control, 0.4f) } },
            { "defending.aggression", new[] { new AW(PlayerAttributeId.Strength, 0.6f), new AW(PlayerAttributeId.Defense, 0.4f) } },
            { "positioning.attackingRuns", new[] { new AW(PlayerAttributeId.Speed, 0.5f), new AW(PlayerAttributeId.Stamina, 0.5f) } }
        };

        private static readonly Dictionary<PlayerArchetype, KeyValuePair<string, float>[]> RoleTable = new Dictionary<PlayerArchetype, KeyValuePair<string, float>[]>
        {
            { PlayerArchetype.Winger, T("positioning.width", 0.75f, "dribbling.outsideCut", 0.65f, "passing.cross", 0.65f, "dribbling.takeOn", 0.7f) },
            { PlayerArchetype.Finisher, T("shooting.frequency", 0.75f, "positioning.boxPresence", 0.8f, "shooting.insideBox", 0.75f) },
            { PlayerArchetype.GoalHunter, T("shooting.frequency", 0.8f, "positioning.boxPresence", 0.8f, "shooting.insideBox", 0.8f, "shooting.firstTime", 0.7f) },
            { PlayerArchetype.Creator, T("passing.progressive", 0.75f, "passing.throughBall", 0.7f, "decision.scanning", 0.7f) },
            { PlayerArchetype.Architect, T("passing.progressive", 0.75f, "passing.throughBall", 0.7f, "decision.patience", 0.7f) },
            { PlayerArchetype.Engine, T("movement.supportMovement", 0.75f, "defending.pressing", 0.65f) },
            { PlayerArchetype.Wall, T("positioning.defensive", 0.8f, "defending.marking", 0.75f, "defending.retreat", 0.7f) },
            { PlayerArchetype.Anchor, T("positioning.defensive", 0.75f, "defending.interception", 0.7f) },
            { PlayerArchetype.Explosive, T("movement.accelerationTendency", 0.8f, "dribbling.changeOfPace", 0.7f) },
            { PlayerArchetype.Target, T("possession.holdUp", 0.8f, "positioning.boxPresence", 0.7f) },
            { PlayerArchetype.Guardian, T("positioning.defensive", 0.85f, "decision.patience", 0.65f, "defending.retreat", 0.7f) },
            { PlayerArchetype.Builder, T("passing.short", 0.7f, "decision.patience", 0.65f) }
        };

        private static KeyValuePair<string, float>[] T(params object[] pairs)
        {
            var list = new List<KeyValuePair<string, float>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) list.Add(new KeyValuePair<string, float>((string)pairs[i], (float)pairs[i + 1]));
            return list.ToArray();
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        public static ComposerResult Compose(ComposerInput input, CreatorCatalogs catalogs, PlayerAttributes? attributes = null)
        {
            bool hasAttributes = attributes.HasValue;
            PlayerAttributes attrs = attributes ?? default;
            var res = new ComposerResult();
            ParameterCatalog pc = catalogs.Parameters;
            var level = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var trail = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var firm = new HashSet<string>(StringComparer.Ordinal);   // observed or manual: no variation

            // ---- 1. attributes nudge
            if (hasAttributes)
            {
                foreach (KeyValuePair<string, AW[]> kv in AttributeTable)
                {
                    if (!pc.Contains(kv.Key)) continue;
                    float sum = 0f, w = 0f;
                    foreach (AW a in kv.Value) { sum += a.W * attrs.GetValue(a.A); w += a.W; }
                    float avg01 = MathUtil.Clamp01(sum / w / PlayerAttributes.Max);
                    float v = MathUtil.Clamp01(0.5f + (avg01 - 0.5f) * AttributeInfluence);
                    level[kv.Key] = v;
                    trail[kv.Key] = "attributes " + F(v);
                }
            }

            // ---- 2. profile and roles pull
            PlayerPlayingProfile profile = input.Profile;
            if (profile != null)
            {
                Pull(level, trail, pc, "passing.risky", profile.RiskPreference / 100f, 0.6f, "risk preference");
                Pull(level, trail, pc, "dribbling.takeOnRisk", profile.RiskPreference / 100f, 0.6f, "risk preference");
                Pull(level, trail, pc, "passing.throughBall", profile.Creativity / 100f, 0.4f, "creativity");
                Pull(level, trail, pc, "dribbling.bodyFeint", profile.Creativity / 100f, 0.4f, "creativity");
                Pull(level, trail, pc, "defending.aggression", profile.Aggression / 100f, 0.5f, "aggression");
                Pull(level, trail, pc, "movement.aggression", profile.Aggression / 100f, 0.5f, "aggression");
                Pull(level, trail, pc, "defending.pressing", profile.Aggression / 100f, 0.3f, "aggression");
                foreach (RoleAffinity role in profile.Roles)
                {
                    if (!RoleTable.TryGetValue(role.Role, out KeyValuePair<string, float>[] targets)) continue;
                    foreach (KeyValuePair<string, float> t in targets)
                        Pull(level, trail, pc, t.Key, t.Value, 0.6f * MathUtil.Clamp01(role.Affinity / 100f), "role " + role.Role);
                }
            }

            // ---- variation of what was only inferred
            if (input.VariationSeed != 0)
            {
                foreach (string k in new List<string>(level.Keys))
                {
                    float r = new SeededRandom(StableHash.Combine(input.VariationSeed, StableHash.Of(k))).NextFloat01() * 2f - 1f;
                    level[k] = MathUtil.Clamp01(level[k] + r * VariationSize);
                    trail[k] += " +variation";
                }
            }

            // ---- 3. observations pull towards what was seen
            var patterns = ObservationAggregator.Aggregate(input.Observations ?? new List<FootballObservation>());
            res.Patterns.AddRange(patterns);
            var behaviors = new SortedDictionary<string, BehaviorEntry>(StringComparer.Ordinal);
            foreach (AggregatedPattern p in patterns)
            {
                if (p.Kind == ObservationKind.Tendency)
                {
                    if (!pc.TryGet(p.Pattern, out ParameterDefinition pd) || pd.Domain != ParameterDomain.FootballDna) { res.Warnings.Add("Ignored an observation of unknown tendency '" + p.Pattern + "'."); continue; }
                    float cur = level.TryGetValue(p.Pattern, out float c0) ? c0 : 0.5f;
                    float w = Math.Min(0.85f, p.Confidence);
                    float v = MathUtil.Clamp01(cur + (p.Value - cur) * w);
                    level[p.Pattern] = v;
                    trail[p.Pattern] = (trail.TryGetValue(p.Pattern, out string t0) ? t0 + " -> " : "") + "observed " + F(p.Value) + " (confidence " + F(p.Confidence) + ") " + F(v);
                    firm.Add(p.Pattern);
                    if (p.Confidence < 0.995f) res.Dna.ParamConfidence[p.Pattern] = p.Confidence;
                }
                else if (catalogs.Behaviors.Contains(p.Pattern))
                {
                    var e = new BehaviorEntry(p.Pattern, p.Value) { Confidence = p.Confidence, Origin = "observed" };
                    if (p.Context != BehaviorContext.None) e.Condition = new BehaviorCondition { Prefers = p.Context };
                    behaviors[p.Pattern] = e;
                    res.Explanation.Add("behaviour " + p.Pattern + ": observed " + F(p.Value) + " (confidence " + F(p.Confidence) + ", " + p.ObservationCount + " observations)");
                }
                else res.Warnings.Add("Ignored an observation of unknown behaviour '" + p.Pattern + "'.");
            }

            // ---- 4. manual preferences are exact
            ManualPreferences manual = input.Manual;
            if (manual != null)
            {
                foreach (KeyValuePair<string, float> kv in manual.Params)
                {
                    if (!pc.TryGet(kv.Key, out ParameterDefinition pd) || pd.Domain != ParameterDomain.FootballDna) { res.Warnings.Add("Ignored manual preference for unknown tendency '" + kv.Key + "'."); continue; }
                    level[kv.Key] = MathUtil.Clamp01(kv.Value);
                    trail[kv.Key] = (trail.TryGetValue(kv.Key, out string t0) ? t0 + " -> " : "") + "manual " + F(kv.Value);
                    firm.Add(kv.Key);
                    res.Dna.ParamConfidence.Remove(kv.Key);
                }
            }

            foreach (KeyValuePair<string, float> kv in level)
            {
                pc.TryGet(kv.Key, out ParameterDefinition pd);
                res.Dna.Params.Set(pc, kv.Key, pd.FromLevel(kv.Value));
                res.Explanation.Add(kv.Key + ": " + trail[kv.Key]);
            }

            // ---- behaviours: observed, derived from the tendencies, then manual
            if (input.DeriveBehaviors)
            {
                var derived = new List<KeyValuePair<float, SignatureBehaviorDefinition>>();
                foreach (SignatureBehaviorDefinition b in catalogs.Behaviors.All)
                {
                    if (behaviors.ContainsKey(b.Id)) continue;
                    float sum = 0f, w = 0f;
                    foreach (WeightedParameter d in b.Drivers) { sum += d.Weight * res.Dna.Get(pc, d.ParameterId); w += d.Weight; }
                    float drive = w > 0f ? sum / w : 0f;
                    if (drive < DeriveThreshold) continue;
                    if (hasAttributes && BehaviorResolver.AttributeFit(b, attrs) < 0.7f) continue;
                    derived.Add(new KeyValuePair<float, SignatureBehaviorDefinition>(drive, b));
                }
                derived.Sort((x, y) => { int c = y.Key.CompareTo(x.Key); return c != 0 ? c : string.CompareOrdinal(x.Value.Id, y.Value.Id); });
                for (int i = 0; i < derived.Count && i < MaxDerivedBehaviors; i++)
                {
                    behaviors[derived[i].Value.Id] = new BehaviorEntry(derived[i].Value.Id, MathUtil.Clamp01(derived[i].Key * 0.8f)) { Confidence = 0.8f, Origin = "derived" };
                    res.Explanation.Add("behaviour " + derived[i].Value.Id + ": derived from tendencies (drive " + F(derived[i].Key) + ")");
                }
            }
            if (manual != null)
            {
                foreach (BehaviorEntry b in manual.Behaviors)
                {
                    if (!catalogs.Behaviors.Contains(b.Id)) { res.Warnings.Add("Ignored manual behaviour '" + b.Id + "' (not in the catalog)."); continue; }
                    BehaviorEntry copy = b.Clone();
                    copy.Origin = "manual";
                    behaviors[b.Id] = copy;
                    res.Explanation.Add("behaviour " + b.Id + ": manual " + F(b.Weight));
                }
                foreach (string id in manual.RemoveBehaviors)
                    if (behaviors.Remove(id)) res.Explanation.Add("behaviour " + id + ": removed by hand");
            }
            foreach (KeyValuePair<string, BehaviorEntry> kv in behaviors) res.Dna.Behaviors.Add(kv.Value);

            foreach (BehaviorSequence tpl in catalogs.Behaviors.SequenceTemplates)
            {
                bool all = true;
                foreach (string step in tpl.Steps)
                    if (!behaviors.TryGetValue(step, out BehaviorEntry e) || e.Weight < 0.5f) { all = false; break; }
                if (all) { res.Dna.Sequences.Add(tpl.Clone()); res.Explanation.Add("sequence " + tpl.Id + ": all its behaviours are present"); }
            }
            return res;
        }

        private static void Pull(SortedDictionary<string, float> level, SortedDictionary<string, string> trail, ParameterCatalog pc, string param, float target, float strength, string why)
        {
            if (!pc.Contains(param)) return;
            float cur = level.TryGetValue(param, out float c) ? c : 0.5f;
            float v = MathUtil.Clamp01(cur + (MathUtil.Clamp01(target) - cur) * strength);
            level[param] = v;
            string step = why + " " + v.ToString("0.00", CultureInfo.InvariantCulture);
            trail[param] = trail.TryGetValue(param, out string old) ? old + " -> " + step : "neutral 0.50 -> " + step;
        }
    }
}
