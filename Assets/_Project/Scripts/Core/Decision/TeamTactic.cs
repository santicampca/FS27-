using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 31: every number of the team tactic (how a team's coach-authored identity and the state of the game change HOW it stands and chooses). One place; the twins are in <see cref="TeamTacticTuning"/>.
    /// A neutral identity (every dial at 50, mid block, balanced) moves nothing: it decides exactly as a team without a tactic.
    /// </summary>
    public static class TeamTacticDefaults
    {
        // ---- shape: where the team stands
        /// <summary>The team's line moves this many metres up (dial 100) or down (dial 0) the pitch from the formation's own places; 50 = the formation as it is.</summary>
        public const float LineMeters = 4.0f;
        /// <summary>Share of that shift the team keeps while it has the ball (it is the defence that sets the line; the attack only follows it).</summary>
        public const float AttackLineShare = 0.5f;
        /// <summary>Metres the line moves forward at full urgency (a deficit late in the game) and back at full negative urgency (a lead to protect).</summary>
        public const float UrgencyLineMeters = 3.0f;
        /// <summary>The lateral places of the formation are scaled by 1 + (width dial - 50 %) x 2 x this: 100 = this much wider, 0 = this much narrower.</summary>
        public const float WidthSpan = 0.4f;

        // ---- pressing: how far from the ball the first defender starts to close it down
        public const float PressLowBlock = 0.7f;
        public const float PressMidBlock = 1.0f;
        public const float PressSelective = 1.0f;
        public const float PressHigh = 1.25f;
        public const float PressAggressive = 1.5f;
        /// <summary>Press scale added at full urgency (and taken at full negative urgency).</summary>
        public const float UrgencyPress = 0.3f;

        // ---- the game: score and time
        /// <summary>The last seconds of a period over which the score matters.</summary>
        public const float UrgencyWindowSeconds = 150.0f;
        /// <summary>Goal difference at which the urgency is at its maximum.</summary>
        public const float UrgencyGoalDifference = 2.0f;
        /// <summary>Urgency a mentality adds on its own (Defensive takes it, Attacking and VeryAttacking add it): step per level away from Balanced.</summary>
        public const float MentalityStep = 0.2f;

        // ---- choosing: a bias on the score of each option of the player with the ball
        /// <summary>Score added to the forward pass and the pass into space for a team that takes risks (risk dial 100), removed for one that does not (0).</summary>
        public const float RiskWeight = 0.6f;
        /// <summary>The same for the shot, as a share of <see cref="RiskWeight"/>.</summary>
        public const float ShotRiskShare = 0.5f;
        /// <summary>The same for the dribble, as a share of <see cref="RiskWeight"/>.</summary>
        public const float DribbleRiskShare = 0.5f;
        /// <summary>Score added to the forward pass and the pass into space for a team that plays direct (build-up Direct / FastVertical) and removed for one that builds short.</summary>
        public const float DirectnessWeight = 0.5f;
        /// <summary>The safe pass loses this share of what the forward options gain.</summary>
        public const float SafePassShare = 0.5f;
        /// <summary>Score taken from holding and protecting the ball for a fast team (tempo dial 100) and added for a slow one (0).</summary>
        public const float TempoWeight = 0.4f;
        /// <summary>Seconds after winning the ball during which the transition style counts.</summary>
        public const float TransitionSeconds = 4.0f;
        /// <summary>Score added to the forward options during that window for a counter-attacking team (and removed for one that recovers possession).</summary>
        public const float TransitionWeight = 0.8f;
    }

    /// <summary>The editable copy of <see cref="TeamTacticDefaults"/>.</summary>
    [Serializable]
    public sealed class TeamTacticTuning
    {
        public float LineMeters = TeamTacticDefaults.LineMeters;
        public float AttackLineShare = TeamTacticDefaults.AttackLineShare;
        public float UrgencyLineMeters = TeamTacticDefaults.UrgencyLineMeters;
        public float WidthSpan = TeamTacticDefaults.WidthSpan;
        public float PressLowBlock = TeamTacticDefaults.PressLowBlock;
        public float PressMidBlock = TeamTacticDefaults.PressMidBlock;
        public float PressSelective = TeamTacticDefaults.PressSelective;
        public float PressHigh = TeamTacticDefaults.PressHigh;
        public float PressAggressive = TeamTacticDefaults.PressAggressive;
        public float UrgencyPress = TeamTacticDefaults.UrgencyPress;
        public float UrgencyWindowSeconds = TeamTacticDefaults.UrgencyWindowSeconds;
        public float UrgencyGoalDifference = TeamTacticDefaults.UrgencyGoalDifference;
        public float MentalityStep = TeamTacticDefaults.MentalityStep;
        public float RiskWeight = TeamTacticDefaults.RiskWeight;
        public float ShotRiskShare = TeamTacticDefaults.ShotRiskShare;
        public float DribbleRiskShare = TeamTacticDefaults.DribbleRiskShare;
        public float DirectnessWeight = TeamTacticDefaults.DirectnessWeight;
        public float SafePassShare = TeamTacticDefaults.SafePassShare;
        public float TempoWeight = TeamTacticDefaults.TempoWeight;
        public float TransitionSeconds = TeamTacticDefaults.TransitionSeconds;
        public float TransitionWeight = TeamTacticDefaults.TransitionWeight;
    }

    /// <summary>
    /// Phase 31: how ONE team plays, as a plan the decision engine reads (the missing link between the coach data and the players): the tactical identity of the team (<see cref="TeamTacticalIdentity"/>, authored per
    /// coach formation) says where its line stands, how wide it plays, how far from the ball it starts to press, how direct and how bold it is, and how it reacts when it wins the ball; the state of the game (score and
    /// time, <see cref="Urgency"/>) pushes that plan one way or the other. It changes WHERE players stand and WHAT they prefer: never an attribute, never a bonus to the execution. A neutral identity changes nothing.
    /// Pure and immutable once built.
    /// </summary>
    public sealed class TeamTactic
    {
        public readonly TeamTacticalIdentity Identity;
        public readonly TeamTacticTuning Tuning;
        /// <summary>The dials, -1 (the low end of the dial) .. 1 (the high end); 0 = the neutral 50.</summary>
        public readonly float Line, Width, Tempo, Risk, Directness, Transition, MentalityLevel;
        /// <summary>The pressing scale before the game state (1 = the engine's own distance).</summary>
        public readonly float BasePress;

        public TeamTactic(in TeamTacticalIdentity identity, TeamTacticTuning tuning = null)
        {
            Identity = identity; Tuning = tuning ?? new TeamTacticTuning();
            Line = Dial(identity.DefensiveLine); Width = Dial(identity.Width); Tempo = Dial(identity.Tempo); Risk = Dial(identity.Risk);
            Directness = BuildUp(identity.BuildUpStyle); Transition = Transit(identity.TransitionStyle);
            MentalityLevel = (int)identity.Mentality - (int)FS27.Core.Mentality.Balanced;
            BasePress = PressOf(identity.PressingStyle, Tuning);
        }

        private static float Dial(int value) { return (Math.Max(0, Math.Min(100, value)) - 50) / 50f; }

        private static float BuildUp(BuildUpStyle s)
        {
            switch (s)
            {
                case BuildUpStyle.Direct: return 1f;
                case BuildUpStyle.FastVertical: return 1f;
                case BuildUpStyle.Short: return -1f;
                case BuildUpStyle.Positional: return -1f;
                default: return 0f;
            }
        }

        private static float Transit(TransitionStyle s)
        {
            switch (s)
            {
                case TransitionStyle.Counter: return 1f;
                case TransitionStyle.Fast: return 1f;
                case TransitionStyle.Direct: return 1f;
                case TransitionStyle.PossessionRecovery: return -1f;
                default: return 0f;
            }
        }

        private static float PressOf(PressingStyle s, TeamTacticTuning t)
        {
            switch (s)
            {
                case PressingStyle.LowBlock: return t.PressLowBlock;
                case PressingStyle.HighPress: return t.PressHigh;
                case PressingStyle.AggressivePress: return t.PressAggressive;
                case PressingStyle.Selective: return t.PressSelective;
                default: return t.PressMidBlock;
            }
        }

        /// <summary>A plan for a team of this identity (null identity = a neutral team).</summary>
        public static TeamTactic From(in TeamTacticalIdentity identity, TeamTacticTuning tuning = null) { return new TeamTactic(identity, tuning); }

        /// <summary>The plan of a team of the database: the identity its coach's formation asks for (or a neutral one).</summary>
        public static TeamTactic Of(TeamDefinition team, GameDatabase db, TeamTacticTuning tuning = null)
        {
            if (db != null && team != null && db.TryGetTacticalIdentity(team, out TeamTacticalIdentity identity)) return new TeamTactic(identity, tuning);
            return new TeamTactic(TeamTacticalIdentity.Neutral(TeamFormationRules.DefaultBaseFormationId, BackLineType.Two), tuning);
        }

        // ------------------------------------------------------------------ the state of the game

        /// <summary>Where the game stands for this team, -1 (a lead to protect, little time) .. 1 (a deficit to chase, little time), plus what its mentality asks for on its own.</summary>
        public float Urgency(in WorldMatch match, TeamSide team)
        {
            int mine = team == TeamSide.Home ? match.HomeGoals : match.AwayGoals, theirs = team == TeamSide.Home ? match.AwayGoals : match.HomeGoals;
            float behind = Math.Max(-Tuning.UrgencyGoalDifference, Math.Min(Tuning.UrgencyGoalDifference, theirs - mine)) / Tuning.UrgencyGoalDifference;
            float late = 1f - MathUtil.Clamp01(match.PeriodRemaining / Tuning.UrgencyWindowSeconds);
            return Math.Max(-1f, Math.Min(1f, behind * late + MentalityLevel * Tuning.MentalityStep));
        }

        // ------------------------------------------------------------------ shape

        /// <summary>The place a field player's formation home becomes under this plan: the line up or down the pitch (<paramref name="attackSign"/> = +1 when the team attacks towards +X) and the width scaled.</summary>
        public Vec2 ShapeHome(Vec2 home, float attackSign, float urgency, bool hasBall)
        {
            float shift = Line * Tuning.LineMeters + urgency * Tuning.UrgencyLineMeters;
            if (hasBall) shift *= Tuning.AttackLineShare;
            return new Vec2(home.X + attackSign * shift, home.Y * (1f + Width * Tuning.WidthSpan));
        }

        /// <summary>How far from the ball the first defender starts to close it down, as a multiple of the engine's own distance.</summary>
        public float PressScale(float urgency) { return Math.Max(Tuning.PressLowBlock * 0.5f, BasePress + urgency * Tuning.UrgencyPress); }

        // ------------------------------------------------------------------ choosing

        /// <summary>The bias of this plan on the options of the player with the ball, on top of <paramref name="inner"/> (the difficulty's reading of the game, or null).</summary>
        public ICandidateBias Wrap(ICandidateBias inner, in WorldBall ball, TeamSide team)
        {
            return new TacticBias(this, inner, ball.SecondsSincePossessionChange < Tuning.TransitionSeconds && ball.HasPossessor ? Transition : 0f);
        }

        private sealed class TacticBias : ICandidateBias
        {
            private readonly TeamTactic p; private readonly ICandidateBias inner; private readonly float transition;
            public TacticBias(TeamTactic plan, ICandidateBias inner, float transition) { p = plan; this.inner = inner; this.transition = transition; }

            public float Bias(in ActionCandidate c, in CarrierSummary s)
            {
                float own = 0f;
                TeamTacticTuning t = p.Tuning;
                float risk = p.Risk * t.RiskWeight, direct = p.Directness * t.DirectnessWeight, rush = transition * t.TransitionWeight;
                switch (c.Type)
                {
                    case CandidateType.ForwardPass:
                    case CandidateType.ThroughPass: own = risk + direct + rush; break;
                    case CandidateType.Pass: own = -(risk + direct + rush) * t.SafePassShare; break;
                    case CandidateType.Shoot: own = risk * t.ShotRiskShare; break;
                    case CandidateType.Dribble: own = risk * t.DribbleRiskShare; break;
                    case CandidateType.Hold:
                    case CandidateType.Protect: own = -p.Tempo * t.TempoWeight; break;
                }
                return (inner != null ? inner.Bias(c, s) : 0f) + own;
            }
        }
    }
}
