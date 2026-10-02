using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>One signature behaviour a player has, and how strongly it defines them (0..1).</summary>
    [Serializable]
    public struct BehaviorEntry
    {
        public string Id;
        public float Weight;

        public BehaviorEntry(string id, float weight)
        {
            Id = id;
            Weight = weight;
        }
    }

    /// <summary>
    /// How a player TENDS to play: tendencies, preferences and signature behaviours. It complements the 12 attributes (what the player CAN do)
    /// and the behaviour values in <see cref="PlayerPlayingProfile"/> (risk, creativity, aggression): it duplicates neither.
    /// It is pure data and tiny (only the values that differ from neutral are stored). The AI reads it through <see cref="BehaviorResolver"/>.
    /// </summary>
    [Serializable]
    public sealed class FootballDNA
    {
        public const string CurrentSchema = "FS27.FootballDNA.v1";

        public string SchemaVersion = CurrentSchema;
        public ParameterSet Params = new ParameterSet();
        public List<BehaviorEntry> Behaviors = new List<BehaviorEntry>();

        public float Get(ParameterCatalog catalog, string parameterId)
        {
            return Params.Get(catalog, parameterId);
        }

        public bool TryGetBehavior(string id, out BehaviorEntry entry)
        {
            foreach (BehaviorEntry b in Behaviors)
            {
                if (b.Id == id)
                {
                    entry = b;
                    return true;
                }
            }
            entry = default;
            return false;
        }

        public bool HasBehavior(string id)
        {
            return TryGetBehavior(id, out _);
        }

        /// <summary>Adds the behaviour, or changes its weight if it is already there (a behaviour is never listed twice).</summary>
        public void SetBehavior(string id, float weight)
        {
            weight = MathUtil.Clamp01(weight);
            for (int i = 0; i < Behaviors.Count; i++)
            {
                if (Behaviors[i].Id == id)
                {
                    Behaviors[i] = new BehaviorEntry(id, weight);
                    return;
                }
            }
            Behaviors.Add(new BehaviorEntry(id, weight));
        }

        public bool RemoveBehavior(string id)
        {
            return Behaviors.RemoveAll(b => b.Id == id) > 0;
        }

        public FootballDNA Clone()
        {
            return new FootballDNA { SchemaVersion = SchemaVersion, Params = Params.Clone(), Behaviors = new List<BehaviorEntry>(Behaviors) };
        }
    }

    public enum BehaviorCategory
    {
        Movement,
        Dribbling,
        Possession,
        Passing,
        Shooting,
        Positioning,
        Defending
    }

    /// <summary>How far a behaviour is from really being playable. Only <see cref="Implemented"/> ones can be executed by gameplay.</summary>
    public enum BehaviorRuntimeStatus
    {
        /// <summary>Only a definition exists (contract, parameters, metadata).</summary>
        Planned = 0,
        /// <summary>Partly working, not for production.</summary>
        Prototype = 1,
        /// <summary>Gameplay can execute it.</summary>
        Implemented = 2
    }

    /// <summary>Situations a behaviour may be chosen in. Gameplay/AI builds the current set from the match state.</summary>
    [Flags]
    public enum BehaviorContext
    {
        None = 0,
        HasBall = 1 << 0,
        ReceivingBall = 1 << 1,
        FacingDefender = 1 << 2,
        OpenSpaceAhead = 1 << 3,
        InsideBox = 1 << 4,
        NearBox = 1 << 5,
        UnderPressure = 1 << 6,
        WideArea = 1 << 7,
        BehindDefenderLine = 1 << 8,
        ShootingRange = 1 << 9,
        LongRange = 1 << 10,
        TeammateHasBall = 1 << 11,
        OpponentHasBall = 1 << 12,
        BackToGoal = 1 << 13
    }

    [Serializable]
    public struct WeightedParameter
    {
        public string ParameterId;
        public float Weight;

        public WeightedParameter(string parameterId, float weight)
        {
            ParameterId = parameterId;
            Weight = weight;
        }
    }

    [Serializable]
    public struct AttributeRequirement
    {
        public PlayerAttributeId Attribute;
        /// <summary>Value (1..99) at which the player has "enough" of this attribute for the behaviour to be fully effective.</summary>
        public int Enough;

        public AttributeRequirement(PlayerAttributeId attribute, int enough)
        {
            Attribute = attribute;
            Enough = enough;
        }
    }

    /// <summary>
    /// A signature behaviour as DATA: what drives it, in which situations it applies, what it needs from the player and from animation.
    /// There is no code per behaviour here and none per player. Executing a behaviour is the job of the future gameplay runtime.
    /// </summary>
    [Serializable]
    public sealed class SignatureBehaviorDefinition
    {
        public string Id;
        public BehaviorCategory Category;
        public string Description;
        public BehaviorRuntimeStatus Status = BehaviorRuntimeStatus.Planned;
        /// <summary>DNA parameters that make the player want to do this.</summary>
        public List<WeightedParameter> Drivers = new List<WeightedParameter>();
        /// <summary>Situations that must ALL hold for the behaviour to be possible.</summary>
        public BehaviorContext Requires;
        /// <summary>Situations that make it more attractive (each one present raises the score).</summary>
        public BehaviorContext Prefers;
        public List<AttributeRequirement> Needs = new List<AttributeRequirement>();
        /// <summary>Tags the animation library must offer for the behaviour to look right (none exist yet).</summary>
        public List<string> AnimationTags = new List<string>();
    }

    public sealed class SignatureBehaviorCatalog
    {
        private readonly Dictionary<string, SignatureBehaviorDefinition> byId = new Dictionary<string, SignatureBehaviorDefinition>();
        private readonly List<SignatureBehaviorDefinition> ordered = new List<SignatureBehaviorDefinition>();

        public int Count => ordered.Count;
        public IReadOnlyList<SignatureBehaviorDefinition> All => ordered;

        public bool TryAdd(SignatureBehaviorDefinition b)
        {
            if (b == null || string.IsNullOrEmpty(b.Id) || byId.ContainsKey(b.Id)) return false;
            byId.Add(b.Id, b);
            ordered.Add(b);
            return true;
        }

        public bool TryGet(string id, out SignatureBehaviorDefinition b)
        {
            if (id == null)
            {
                b = null;
                return false;
            }
            return byId.TryGetValue(id, out b);
        }

        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }
    }

    /// <summary>The initial behaviour catalog. All entries are <see cref="BehaviorRuntimeStatus.Planned"/>: nothing here can be executed yet.</summary>
    public static class DefaultBehaviors
    {
        public const string StopAndGo = "StopAndGo";
        public const string BodyFeint = "BodyFeint";
        public const string ExplosiveExit = "ExplosiveExit";
        public const string DelayedRun = "DelayedRun";
        public const string BlindSideRun = "BlindSideRun";
        public const string LateBoxArrival = "LateBoxArrival";
        public const string HoldUpPlay = "HoldUpPlay";
        public const string FirstTimeFinish = "FirstTimeFinish";
        public const string LongRangeShot = "LongRangeShot";
        public const string InsideCut = "InsideCut";
        public const string OutsideCut = "OutsideCut";
        public const string CreativePass = "CreativePass";
        public const string RiskyThroughBall = "RiskyThroughBall";
        public const string OneTouchCombination = "OneTouchCombination";
        public const string AggressivePress = "AggressivePress";

        private static SignatureBehaviorDefinition B(string id, BehaviorCategory cat, string text, BehaviorContext requires, BehaviorContext prefers,
                                                      WeightedParameter[] drivers, AttributeRequirement[] needs, params string[] animTags)
        {
            return new SignatureBehaviorDefinition
            {
                Id = id, Category = cat, Description = text, Requires = requires, Prefers = prefers,
                Drivers = new List<WeightedParameter>(drivers), Needs = new List<AttributeRequirement>(needs), AnimationTags = new List<string>(animTags)
            };
        }

        private static WeightedParameter W(string id, float w) { return new WeightedParameter(id, w); }
        private static AttributeRequirement N(PlayerAttributeId a, int v) { return new AttributeRequirement(a, v); }

        public static SignatureBehaviorCatalog Create()
        {
            var c = new SignatureBehaviorCatalog();
            BehaviorContext carry = BehaviorContext.HasBall;
            c.TryAdd(B(StopAndGo, BehaviorCategory.Dribbling, "Stops the ball and restarts to unbalance a defender.", carry | BehaviorContext.FacingDefender, BehaviorContext.OpenSpaceAhead,
                new[] { W("dribbling.stopAndGo", 0.6f), W("dribbling.takeOn", 0.25f), W("movement.decelerationTendency", 0.15f) },
                new[] { N(PlayerAttributeId.Agility, 70), N(PlayerAttributeId.Control, 65) }, "stop", "restart"));
            c.TryAdd(B(BodyFeint, BehaviorCategory.Dribbling, "Sells a feint with the body before going the other way.", carry | BehaviorContext.FacingDefender, BehaviorContext.OpenSpaceAhead,
                new[] { W("dribbling.bodyFeint", 0.6f), W("dribbling.takeOn", 0.25f), W("dribbling.directionChange", 0.15f) },
                new[] { N(PlayerAttributeId.Agility, 70), N(PlayerAttributeId.Technique, 70) }, "feint"));
            c.TryAdd(B(ExplosiveExit, BehaviorCategory.Dribbling, "Bursts away from a defender after beating them.", carry | BehaviorContext.OpenSpaceAhead, BehaviorContext.FacingDefender,
                new[] { W("dribbling.changeOfPace", 0.5f), W("movement.accelerationTendency", 0.5f) },
                new[] { N(PlayerAttributeId.Acceleration, 75), N(PlayerAttributeId.Speed, 70) }, "burst"));
            c.TryAdd(B(DelayedRun, BehaviorCategory.Movement, "Starts a run late, once the defender has committed.", BehaviorContext.TeammateHasBall, BehaviorContext.NearBox | BehaviorContext.BehindDefenderLine,
                new[] { W("movement.delayedRuns", 0.6f), W("movement.runTiming", 0.4f) },
                new[] { N(PlayerAttributeId.Acceleration, 65) }, "run"));
            c.TryAdd(B(BlindSideRun, BehaviorCategory.Movement, "Runs where the defender cannot see.", BehaviorContext.TeammateHasBall, BehaviorContext.BehindDefenderLine,
                new[] { W("movement.blindSideRuns", 0.7f), W("movement.spaceSeeking", 0.3f) },
                new[] { N(PlayerAttributeId.Speed, 65) }, "run"));
            c.TryAdd(B(LateBoxArrival, BehaviorCategory.Positioning, "Arrives in the box late to finish the move.", BehaviorContext.TeammateHasBall, BehaviorContext.NearBox | BehaviorContext.InsideBox,
                new[] { W("movement.delayedRuns", 0.4f), W("positioning.boxPresence", 0.3f), W("positioning.attackingRuns", 0.3f) },
                new[] { N(PlayerAttributeId.Stamina, 60) }, "run"));
            c.TryAdd(B(HoldUpPlay, BehaviorCategory.Possession, "Holds the ball with the back to goal until support arrives.", BehaviorContext.HasBall | BehaviorContext.BackToGoal, BehaviorContext.UnderPressure,
                new[] { W("possession.holdUp", 0.55f), W("possession.shielding", 0.45f) },
                new[] { N(PlayerAttributeId.Strength, 75), N(PlayerAttributeId.Control, 65) }, "shield"));
            c.TryAdd(B(FirstTimeFinish, BehaviorCategory.Shooting, "Shoots without controlling the ball first.", BehaviorContext.ReceivingBall | BehaviorContext.ShootingRange, BehaviorContext.InsideBox,
                new[] { W("shooting.firstTime", 0.6f), W("shooting.insideBox", 0.2f), W("shooting.frequency", 0.2f) },
                new[] { N(PlayerAttributeId.Finishing, 75) }, "shot"));
            c.TryAdd(B(LongRangeShot, BehaviorCategory.Shooting, "Shoots from outside the box.", carry | BehaviorContext.ShootingRange | BehaviorContext.LongRange, BehaviorContext.None,
                new[] { W("shooting.longShot", 0.6f), W("shooting.power", 0.2f), W("shooting.frequency", 0.2f) },
                new[] { N(PlayerAttributeId.Shooting, 75) }, "shot"));
            c.TryAdd(B(InsideCut, BehaviorCategory.Dribbling, "Cuts inside from a wide position, towards goal.", carry | BehaviorContext.WideArea, BehaviorContext.FacingDefender,
                new[] { W("dribbling.insideCut", 0.6f), W("positioning.halfSpace", 0.2f), W("dribbling.directionChange", 0.2f) },
                new[] { N(PlayerAttributeId.Dribbling, 70) }, "cut"));
            c.TryAdd(B(OutsideCut, BehaviorCategory.Dribbling, "Goes around the outside along the wing.", carry | BehaviorContext.WideArea, BehaviorContext.FacingDefender,
                new[] { W("dribbling.outsideCut", 0.6f), W("positioning.width", 0.2f), W("dribbling.changeOfPace", 0.2f) },
                new[] { N(PlayerAttributeId.Speed, 70) }, "cut"));
            c.TryAdd(B(CreativePass, BehaviorCategory.Passing, "Tries an unexpected pass.", carry, BehaviorContext.UnderPressure,
                new[] { W("passing.progressive", 0.3f), W("passing.risky", 0.4f), W("decision.directness", 0.3f) },
                new[] { N(PlayerAttributeId.Passing, 75), N(PlayerAttributeId.Technique, 70) }, "pass"));
            c.TryAdd(B(RiskyThroughBall, BehaviorCategory.Passing, "Slides a through ball between defenders.", carry, BehaviorContext.BehindDefenderLine | BehaviorContext.TeammateHasBall,
                new[] { W("passing.throughBall", 0.6f), W("passing.risky", 0.4f) },
                new[] { N(PlayerAttributeId.Passing, 78) }, "pass"));
            c.TryAdd(B(OneTouchCombination, BehaviorCategory.Passing, "Plays quick one-touch passes with a teammate.", BehaviorContext.ReceivingBall, BehaviorContext.UnderPressure,
                new[] { W("passing.oneTouch", 0.6f), W("passing.short", 0.4f) },
                new[] { N(PlayerAttributeId.Passing, 70), N(PlayerAttributeId.Technique, 70) }, "pass"));
            c.TryAdd(B(AggressivePress, BehaviorCategory.Defending, "Closes the ball carrier down hard.", BehaviorContext.OpponentHasBall, BehaviorContext.None,
                new[] { W("defending.pressing", 0.5f), W("defending.aggression", 0.3f), W("movement.aggression", 0.2f) },
                new[] { N(PlayerAttributeId.Stamina, 70), N(PlayerAttributeId.Defense, 60) }, "press"));
            return c;
        }
    }

    public struct BehaviorCandidate
    {
        public string BehaviorId;
        /// <summary>0..1, higher = more attractive right now.</summary>
        public float Score;
    }

    /// <summary>
    /// FootballDNA + attributes + the current situation = a ranked list of behaviours the player would consider. Pure and deterministic:
    /// the same inputs always give the same list. It names NO player. Turning a candidate into a <see cref="PlayerIntent"/> and executing it is the
    /// job of the future gameplay/AI (the Difficulty system will decide how well it is executed).
    /// </summary>
    public static class BehaviorResolver
    {
        /// <summary>Share of the score that comes from the player explicitly listing the behaviour as a signature one.</summary>
        public const float SignatureShare = 0.4f;
        /// <summary>Share of the score that comes from the preferred-situation bonus.</summary>
        public const float PreferenceShare = 0.15f;

        public static List<BehaviorCandidate> Resolve(FootballDNA dna, in PlayerAttributes attributes, BehaviorContext context,
                                                      ParameterCatalog parameters, SignatureBehaviorCatalog behaviors)
        {
            var result = new List<BehaviorCandidate>();
            foreach (SignatureBehaviorDefinition b in behaviors.All)
            {
                if ((context & b.Requires) != b.Requires) continue;

                float driveSum = 0f, weightSum = 0f;
                foreach (WeightedParameter d in b.Drivers)
                {
                    driveSum += d.Weight * dna.Get(parameters, d.ParameterId);
                    weightSum += d.Weight;
                }
                float drive = weightSum > 0f ? driveSum / weightSum : 0f;

                float listed = dna.TryGetBehavior(b.Id, out BehaviorEntry e) ? e.Weight : 0f;
                float preferred = PreferredFraction(b.Prefers, context);
                float baseScore = (1f - SignatureShare - PreferenceShare) * drive + SignatureShare * listed + PreferenceShare * preferred;

                float fit = AttributeFit(b, attributes);
                float score = MathUtil.Clamp01(baseScore * (0.5f + 0.5f * fit));
                if (score > 0f) result.Add(new BehaviorCandidate { BehaviorId = b.Id, Score = score });
            }
            result.Sort((x, y) =>
            {
                int c = y.Score.CompareTo(x.Score);
                return c != 0 ? c : string.CompareOrdinal(x.BehaviorId, y.BehaviorId);
            });
            return result;
        }

        private static float PreferredFraction(BehaviorContext prefers, BehaviorContext context)
        {
            int total = 0, present = 0;
            for (int bit = 0; bit < 31; bit++)
            {
                var flag = (BehaviorContext)(1 << bit);
                if ((prefers & flag) == 0) continue;
                total++;
                if ((context & flag) != 0) present++;
            }
            return total == 0 ? 0f : (float)present / total;
        }

        /// <summary>0..1: how much of what the behaviour needs the player has (1 = all of it).</summary>
        public static float AttributeFit(SignatureBehaviorDefinition b, in PlayerAttributes attributes)
        {
            if (b.Needs.Count == 0) return 1f;
            float sum = 0f;
            foreach (AttributeRequirement n in b.Needs)
                sum += MathUtil.Clamp01(attributes.GetValue(n.Attribute) / (float)Math.Max(1, n.Enough));
            return sum / b.Needs.Count;
        }
    }
}
