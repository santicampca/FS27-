using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum PossessionState
    {
        Own = 0,
        Opponent,
        Loose
    }

    public enum AttackPhase
    {
        Attacking = 0,
        Defending,
        Transition
    }

    /// <summary>
    /// A snapshot of the moment, as the AI sees it, for ONE player: where things are and who has the ball. It is a VIEW built from Match Core
    /// data by the host; it owns no rules and never changes the match. Field space: centre (0,0), X along the pitch.
    /// </summary>
    [Serializable]
    public sealed class FootballContext
    {
        public Vec2 PlayerPosition;
        /// <summary>Unit vector the player faces (zero = faces the way the team attacks).</summary>
        public Vec2 Facing;
        public Vec2 BallPosition;
        /// <summary>+1 when the player's team attacks the +X end, -1 for the -X end.</summary>
        public int AttackSign = 1;
        public PossessionState Possession = PossessionState.Loose;
        public bool PlayerHasBall;
        /// <summary>True when the ball is travelling towards the player.</summary>
        public bool BallIncoming;
        public List<Vec2> Teammates = new List<Vec2>();
        public List<Vec2> Opponents = new List<Vec2>();
    }

    /// <summary>The distances that turn raw positions into situations. Tunable; metres on the 40 x 25 pitch.</summary>
    [Serializable]
    public sealed class ContextThresholds
    {
        public float PressureRadius = 3.5f;
        public float PressureFlagAt = 0.4f;
        public float FacingDefenderRadius = 4.5f;
        /// <summary>Cosine of the half-angle of the "in front" cone (0.5 = 60 degrees).</summary>
        public float FrontConeCos = 0.5f;
        public float SpaceAheadDistance = 6f;
        public float OpenSpaceFlagAt = 0.7f;
        public float ShootingRange = 15f;
        public float BoxDepth = 7f;
        public float BoxHalfWidth = 7f;
        public float NearBoxMargin = 5f;
        public float WideMargin = 5f;
        public float BackToGoalCos = -0.5f;
    }

    /// <summary>What the situation means: the <see cref="BehaviorContext"/> flags and the numbers behind them.</summary>
    public sealed class ContextAnalysis
    {
        public BehaviorContext Flags;
        public float Pressure01;
        public float SpaceAhead01;
        public float DistanceToGoal;
        public float DistanceToBall;
        public PitchZone Zone;
        public AttackPhase Phase;
    }

    /// <summary>Turns a <see cref="FootballContext"/> into situations. Pure, deterministic, and independent of the Match Core.</summary>
    public static class FootballContextAnalyzer
    {
        public static ContextAnalysis Analyze(FootballContext c, FieldDimensions field, ContextThresholds t = null)
        {
            t = t ?? new ContextThresholds();
            var a = new ContextAnalysis();
            BehaviorContext f = BehaviorContext.None;
            float sign = c.AttackSign >= 0 ? 1f : -1f;
            Vec2 attack = new Vec2(sign, 0f);
            Vec2 facing = c.Facing.SqrMagnitude > 1e-6f ? c.Facing.Normalized : attack;

            if (c.PlayerHasBall) f |= BehaviorContext.HasBall;
            if (!c.PlayerHasBall && c.Possession == PossessionState.Own && c.BallIncoming) f |= BehaviorContext.ReceivingBall;
            if (c.Possession == PossessionState.Own && !c.PlayerHasBall) f |= BehaviorContext.TeammateHasBall;
            if (c.Possession == PossessionState.Opponent) f |= BehaviorContext.OpponentHasBall;
            a.Phase = c.Possession == PossessionState.Own ? AttackPhase.Attacking : c.Possession == PossessionState.Opponent ? AttackPhase.Defending : AttackPhase.Transition;

            // pressure, facing a defender, space ahead
            float pressure = 0f, nearestAhead = float.MaxValue;
            bool facingDefender = false;
            foreach (Vec2 o in c.Opponents)
            {
                Vec2 d = o - c.PlayerPosition;
                float dist = d.Magnitude;
                if (dist < t.PressureRadius) pressure += 1f - dist / t.PressureRadius;
                if (dist < 1e-4f) continue;
                float cos = (d.X * facing.X + d.Y * facing.Y) / dist;
                if (cos >= t.FrontConeCos)
                {
                    if (dist <= t.FacingDefenderRadius) facingDefender = true;
                    if (dist < nearestAhead) nearestAhead = dist;
                }
            }
            a.Pressure01 = MathUtil.Clamp01(pressure);
            a.SpaceAhead01 = nearestAhead == float.MaxValue ? 1f : MathUtil.Clamp01(nearestAhead / t.SpaceAheadDistance);
            if (a.Pressure01 >= t.PressureFlagAt) f |= BehaviorContext.UnderPressure;
            if (facingDefender) f |= BehaviorContext.FacingDefender;
            if (a.SpaceAhead01 >= t.OpenSpaceFlagAt) f |= BehaviorContext.OpenSpaceAhead;

            // geometry of the pitch
            float half = field.Length * 0.5f;
            float advance = sign * c.PlayerPosition.X;
            Vec2 goal = new Vec2(sign * half, 0f);
            a.DistanceToGoal = (goal - c.PlayerPosition).Magnitude;
            a.DistanceToBall = (c.BallPosition - c.PlayerPosition).Magnitude;
            bool insideBox = advance >= half - t.BoxDepth && Math.Abs(c.PlayerPosition.Y) <= t.BoxHalfWidth;
            bool nearBox = !insideBox && advance >= half - t.BoxDepth - t.NearBoxMargin && Math.Abs(c.PlayerPosition.Y) <= t.BoxHalfWidth + t.NearBoxMargin;
            if (insideBox) f |= BehaviorContext.InsideBox;
            if (nearBox) f |= BehaviorContext.NearBox;
            if (a.DistanceToGoal <= t.ShootingRange) f |= BehaviorContext.ShootingRange;
            if (!insideBox && a.DistanceToGoal <= t.ShootingRange && a.DistanceToGoal > t.BoxDepth) f |= BehaviorContext.LongRange;
            if (Math.Abs(c.PlayerPosition.Y) >= field.Width * 0.5f - t.WideMargin) f |= BehaviorContext.WideArea;
            if (facing.X * attack.X + facing.Y * attack.Y <= t.BackToGoalCos) f |= BehaviorContext.BackToGoal;

            // beyond the last defender (the deepest opponent is taken to be the keeper)
            if (c.Opponents.Count >= 1)
            {
                var advances = new List<float>();
                foreach (Vec2 o in c.Opponents) advances.Add(sign * o.X);
                advances.Sort((x, y) => y.CompareTo(x));
                float line = advances.Count >= 2 ? advances[1] : advances[0];
                if (advance > line) f |= BehaviorContext.BehindDefenderLine;
            }

            Vec2 relative = new Vec2((advance + half) / field.Length, (c.PlayerPosition.Y + field.Width * 0.5f) / field.Width);
            a.Zone = PlayingProfileDefaults.ZoneOfRelativePosition(relative);
            a.Flags = f;
            return a;
        }
    }

    /// <summary>What a player did recently: cooldowns and the last behaviour (for sequences). Deterministic; the host ticks it.</summary>
    public sealed class BehaviorMemory
    {
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>(StringComparer.Ordinal);

        public string LastBehaviorId { get; private set; } = "";
        public float SecondsSinceLast { get; private set; } = float.MaxValue;

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (SecondsSinceLast < float.MaxValue) SecondsSinceLast += dt;
            var keys = new List<string>(cooldowns.Keys);
            foreach (string k in keys)
            {
                float left = cooldowns[k] - dt;
                if (left <= 0f) cooldowns.Remove(k);
                else cooldowns[k] = left;
            }
        }

        public bool IsReady(string behaviorId) { return !cooldowns.ContainsKey(behaviorId); }

        public float CooldownLeft(string behaviorId) { return cooldowns.TryGetValue(behaviorId, out float left) ? left : 0f; }

        /// <summary>Records that the behaviour was started: starts its cooldown and remembers it as the last one.</summary>
        public void Record(string behaviorId, float cooldownSeconds)
        {
            LastBehaviorId = behaviorId;
            SecondsSinceLast = 0f;
            if (cooldownSeconds > 0f) cooldowns[behaviorId] = cooldownSeconds;
        }
    }

    /// <summary>One behaviour, scored for this player in this moment, with the reasons (so a decision can be explained).</summary>
    public sealed class ScoredBehavior
    {
        public string BehaviorId = "";
        /// <summary>0..1: how attractive it is (after risk, confidence and sequence).</summary>
        public float Score;
        /// <summary>Score adjusted by priority: the number candidates are ranked by.</summary>
        public float Utility;
        public float Priority;
        /// <summary>0..1: how well the situation suits it (preferred situations present).</summary>
        public float ContextMatch;
        /// <summary>0..1: how much this player is the kind who does it (DNA drive and signature weight), whatever the situation.</summary>
        public float PlayerAffinity;
        public float AttributeFit;
        public float Risk;
        public float SequenceBoost;
        public FootballActionKind Action;
        public MovementStyle Style;
        public readonly List<string> Reasons = new List<string>();
    }

    public sealed class RejectedBehavior
    {
        public string BehaviorId = "";
        public string Reason = "";
    }

    /// <summary>The result of one decision: everyone considered, who was ruled out and why, and what was chosen (null = play normally).</summary>
    public sealed class BehaviorDecision
    {
        public readonly List<ScoredBehavior> Candidates = new List<ScoredBehavior>();
        public readonly List<RejectedBehavior> Rejected = new List<RejectedBehavior>();
        public ScoredBehavior Chosen;
        public string Explanation = "";
    }

    /// <summary>
    /// FootballDNA + situation + memory = the behaviours a player considers and the one they go for. Deterministic by default (the best
    /// candidate wins); give it a random source to pick among the near-best in proportion to their utility. It names no player and executes
    /// nothing: the choice becomes an intent through <see cref="FootballActionResolver"/>, and the Difficulty system still decides how well it is executed.
    /// </summary>
    public static class BehaviorDecisionEngine
    {
        /// <summary>Below this utility the player simply plays normally.</summary>
        public const float MinimumUtility = 0.15f;
        /// <summary>With a random source: candidates within this fraction of the best utility are eligible.</summary>
        public const float NearBestFraction = 0.85f;

        public static BehaviorDecision Decide(FootballDNA dna, in PlayerAttributes attributes, ContextAnalysis context, BehaviorMemory memory, CreatorCatalogs catalogs,
                                              IRandomSource random = null, float riskAppetite01 = -1f)
        {
            var decision = new BehaviorDecision();
            ParameterCatalog parameters = catalogs.Parameters;
            float appetite = riskAppetite01 >= 0f ? riskAppetite01 : 0.5f * (dna.Get(parameters, "passing.risky") + dna.Get(parameters, "dribbling.takeOnRisk"));

            foreach (SignatureBehaviorDefinition b in catalogs.Behaviors.All)
            {
                dna.TryGetBehavior(b.Id, out BehaviorEntry entry);
                BehaviorCondition cond = entry?.Condition;
                BehaviorContext requires = b.Requires | (cond?.Requires ?? BehaviorContext.None);
                BehaviorContext forbids = b.Forbids | (cond?.Forbids ?? BehaviorContext.None);

                if ((context.Flags & requires) != requires) { decision.Rejected.Add(new RejectedBehavior { BehaviorId = b.Id, Reason = "the situation does not allow it" }); continue; }
                if ((context.Flags & forbids) != BehaviorContext.None) { decision.Rejected.Add(new RejectedBehavior { BehaviorId = b.Id, Reason = "forbidden in this situation" }); continue; }
                if (memory != null && !memory.IsReady(b.Id)) { decision.Rejected.Add(new RejectedBehavior { BehaviorId = b.Id, Reason = "cooling down" }); continue; }

                float driveSum = 0f, weightSum = 0f;
                foreach (WeightedParameter d in b.Drivers)
                {
                    driveSum += d.Weight * dna.Get(parameters, d.ParameterId);
                    weightSum += d.Weight;
                }
                float drive = weightSum > 0f ? driveSum / weightSum : 0f;
                float listed = entry != null ? entry.Weight : 0f;
                float contextMatch = PreferredFraction(b.Prefers | (cond?.Prefers ?? BehaviorContext.None), context.Flags);

                float driveShare = 1f - BehaviorResolver.SignatureShare - BehaviorResolver.PreferenceShare;
                float affinity = MathUtil.Clamp01((driveShare * drive + BehaviorResolver.SignatureShare * listed) / (1f - BehaviorResolver.PreferenceShare));
                float baseScore = driveShare * drive + BehaviorResolver.SignatureShare * listed + BehaviorResolver.PreferenceShare * contextMatch;
                float fit = BehaviorResolver.AttributeFit(b, attributes);
                float score = MathUtil.Clamp01(baseScore * (0.5f + 0.5f * fit));

                float risk = entry != null && entry.Risk != BehaviorEntry.Unset ? entry.Risk : b.DefaultRisk;
                float riskPenalty = Math.Max(0f, risk - appetite) * (0.2f + 0.6f * context.Pressure01);
                score *= 1f - MathUtil.Clamp01(riskPenalty);
                if (entry != null) score *= 0.7f + 0.3f * entry.Confidence;

                float boost = SequenceBoost(dna, memory, b.Id);
                if (boost > 0f) score += boost * (1f - score);

                if (cond != null && cond.Threshold > 0f && score < cond.Threshold)
                {
                    decision.Rejected.Add(new RejectedBehavior { BehaviorId = b.Id, Reason = "below this player's threshold" });
                    continue;
                }
                if (score <= 0f) continue;

                float priority = entry != null && entry.Priority != BehaviorEntry.Unset ? entry.Priority : b.DefaultPriority;
                var s = new ScoredBehavior
                {
                    BehaviorId = b.Id, Score = score, Priority = priority, Utility = score * (0.9f + 0.01f * priority), ContextMatch = contextMatch, PlayerAffinity = affinity,
                    AttributeFit = fit, Risk = risk, SequenceBoost = boost, Action = b.Action, Style = b.Style
                };
                s.Reasons.Add("affinity " + affinity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ", situation " + contextMatch.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                              + ", attribute fit " + fit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                if (riskPenalty > 0.01f) s.Reasons.Add("held back by risk");
                if (boost > 0f) s.Reasons.Add("follows " + memory.LastBehaviorId);
                decision.Candidates.Add(s);
            }

            decision.Candidates.Sort((x, y) =>
            {
                int c = y.Utility.CompareTo(x.Utility);
                return c != 0 ? c : string.CompareOrdinal(x.BehaviorId, y.BehaviorId);
            });

            if (decision.Candidates.Count > 0 && decision.Candidates[0].Utility >= MinimumUtility)
            {
                ScoredBehavior top = decision.Candidates[0];
                decision.Chosen = top;
                if (random != null)
                {
                    float floor = top.Utility * NearBestFraction;
                    float total = 0f;
                    foreach (ScoredBehavior c in decision.Candidates) if (c.Utility >= floor) total += c.Utility;
                    float roll = random.NextFloat01() * total, acc = 0f;
                    foreach (ScoredBehavior c in decision.Candidates)
                    {
                        if (c.Utility < floor) continue;
                        acc += c.Utility;
                        if (roll < acc) { decision.Chosen = c; break; }
                    }
                }
                decision.Explanation = decision.Chosen.BehaviorId + " (" + decision.Chosen.Utility.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "): " + string.Join("; ", decision.Chosen.Reasons);
            }
            else decision.Explanation = "no signature behaviour is attractive here: play normally";
            return decision;
        }

        private static float SequenceBoost(FootballDNA dna, BehaviorMemory memory, string behaviorId)
        {
            if (memory == null || string.IsNullOrEmpty(memory.LastBehaviorId)) return 0f;
            float best = 0f;
            foreach (BehaviorSequence q in dna.Sequences)
            {
                if (memory.SecondsSinceLast > q.MaxGapSeconds) continue;
                for (int i = 0; i + 1 < q.Steps.Count; i++)
                    if (q.Steps[i] == memory.LastBehaviorId && q.Steps[i + 1] == behaviorId) best = Math.Max(best, q.Weight * 0.3f);
            }
            return best;
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
    }

    /// <summary>The action and style the player ends up with, and why.</summary>
    public struct ActionSelection
    {
        public FootballActionKind Action;
        public MovementStyle Style;
        public string BehaviorId;
        public DribbleIntent Dribble;
        public ShotIntent Shot;
        public PassIntent Pass;
        /// <summary>True when the action came from a signature behaviour (AI).</summary>
        public bool FromBehavior;
        /// <summary>True when a person asked for the action and DNA only flavoured it.</summary>
        public bool KeptRequestedAction;
        public string Reason;
    }

    /// <summary>
    /// PlayerIntent + DNA + situation = the action to perform. For an AI player with no explicit action, the decision's behaviour becomes the
    /// action. For a PERSON, the requested action is never replaced: DNA only flavours how it is done (a placed shot, a through ball, a feint).
    /// The ball is untouched: this only fills in the intent; gameplay and the Ball Core decide what happens.
    /// </summary>
    public static class FootballActionResolver
    {
        public static ActionSelection Resolve(in PlayerIntent intent, FootballDNA dna, ParameterCatalog parameters, BehaviorDecision decision, ContextAnalysis context, bool humanControlled)
        {
            var sel = new ActionSelection { Action = intent.Action, Style = intent.Style, BehaviorId = intent.BehaviorId, Dribble = intent.Dribble, Shot = intent.Shot, Pass = intent.Pass };
            ScoredBehavior chosen = decision?.Chosen;

            if (intent.HasAction)
            {
                sel.KeptRequestedAction = true;
                // a behaviour of the SAME kind of action may flavour it (a feint on a dribble), never replace it
                if (chosen != null && chosen.Action == intent.Action && (!humanControlled || intent.Style == MovementStyle.Default))
                {
                    sel.Style = chosen.Style;
                    sel.BehaviorId = chosen.BehaviorId;
                }
                switch (intent.Action)
                {
                    case FootballActionKind.Shot:
                    case FootballActionKind.PlacedShot:
                        if (dna.Get(parameters, "shooting.finesse") > dna.Get(parameters, "shooting.power") + 0.15f) sel.Shot.Placed = true;
                        if (dna.Get(parameters, "shooting.firstTime") > 0.65f && (context.Flags & BehaviorContext.ReceivingBall) != 0) sel.Shot.FirstTime = true;
                        if (sel.Shot.Power <= 0f) sel.Shot.Power = 0.5f + 0.5f * dna.Get(parameters, "shooting.power");
                        break;
                    case FootballActionKind.ShortPass:
                    case FootballActionKind.LongPass:
                        if (dna.Get(parameters, "passing.throughBall") > dna.Get(parameters, "passing.safe") + 0.2f) sel.Pass.Through = true;
                        if (dna.Get(parameters, "passing.oneTouch") > 0.65f) sel.Pass.OneTouch = true;
                        break;
                    case FootballActionKind.Dribble:
                        sel.Dribble.Aggressiveness = dna.Get(parameters, "dribbling.takeOn");
                        break;
                }
                sel.Reason = sel.BehaviorId != null && sel.BehaviorId == chosen?.BehaviorId ? "requested action, flavoured by " + chosen.BehaviorId : "requested action kept";
                return sel;
            }

            if (!humanControlled && chosen != null)
            {
                sel.Action = chosen.Action;
                sel.Style = chosen.Style;
                sel.BehaviorId = chosen.BehaviorId;
                sel.FromBehavior = true;
                sel.Reason = decision.Explanation;
                return sel;
            }
            sel.Reason = humanControlled ? "a person controls this player: no action is chosen for them" : "no behaviour chosen: movement only";
            return sel;
        }

        /// <summary>The same intent with the selection written into it. The movement stays exactly as it was.</summary>
        public static PlayerIntent ApplyTo(PlayerIntent intent, in ActionSelection sel)
        {
            intent.Action = sel.Action;
            intent.Style = sel.Style;
            intent.BehaviorId = sel.BehaviorId;
            intent.Dribble = sel.Dribble;
            intent.Shot = sel.Shot;
            intent.Pass = sel.Pass;
            return intent;
        }
    }
}
