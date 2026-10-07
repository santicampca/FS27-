using System;

namespace FS27.Core
{
    /// <summary>What a player without the ball does while a team-mate has it (Phase 10). A short closed list.</summary>
    public enum AttackJob
    {
        /// <summary>Not one of the team's field players in play (the carrier, the keeper, a person's player, someone off the pitch).</summary>
        None = 0,
        /// <summary>Offer a safe pass near the carrier.</summary>
        Support = 1,
        /// <summary>Run ahead into room, within his zone.</summary>
        Forward = 2,
        /// <summary>Keep the width: a lateral option.</summary>
        Width = 3,
        /// <summary>Improve his availability near his place.</summary>
        GetOpen = 4
    }

    /// <summary>Where a player wants to be to do his job, and how fast (a plan, not a move: it becomes a MovementIntent).</summary>
    public struct AttackPlan
    {
        public AttackJob Job;
        public Vec2 Target;
        public float Magnitude;
    }

    /// <summary>
    /// The attack without the ball (Phase 10): who does which job while a team-mate has the ball (<see cref="Assign"/>), and where each stands (<see cref="Plan"/>). A pure function of the world view: it reads
    /// only the view, changes nothing, uses no chance and no time; the same view always gives the same answer. The roles are a PREFERENCE added to a score made of where the player is and how much room he has,
    /// never a rule. At most <see cref="AttackPositioningTuning.SupportRunners"/> / <see cref="AttackPositioningTuning.ForwardRunners"/> / <see cref="AttackPositioningTuning.WidthHolders"/> players do the three
    /// jobs; everybody else gets open near his place. It is not a tactic: it decides where a player WANTS to go; the Core's locomotion and separation decide what happens.
    /// </summary>
    public sealed class AttackSupport
    {
        private readonly BasicDecisionTuning t;
        private readonly AttackPositioningTuning a;
        private readonly AttackJob[] jobs = new AttackJob[WorldState.MaxPlayers];
        private readonly float[] support = new float[WorldState.MaxPlayers];
        private readonly float[] forward = new float[WorldState.MaxPlayers];
        private readonly float[] width = new float[WorldState.MaxPlayers];

        public AttackPositioningTuning Tuning => a;

        public AttackSupport(BasicDecisionTuning tuning, AttackPositioningTuning attack)
        {
            t = tuning ?? throw new ArgumentNullException(nameof(tuning));
            a = attack ?? throw new ArgumentNullException(nameof(attack));
        }

        // ------------------------------------------------------------------ the roles as a preference

        /// <summary>How much a role leans towards a job: the preferred table value or the acceptable one, 0 for none (a table of facts about the 12 official roles).</summary>
        private float Lean(PlayerArchetype role, AttackJob job)
        {
            float p = a.RolePreferred, ok = a.RoleAcceptable;
            switch (role)
            {
                case PlayerArchetype.Creator: return job == AttackJob.Support ? p : (job == AttackJob.Forward ? ok : 0f);
                case PlayerArchetype.Architect: return job == AttackJob.Support ? p : (job == AttackJob.Forward ? ok : 0f);
                case PlayerArchetype.Builder: return job == AttackJob.Support ? p : (job == AttackJob.Width ? ok : 0f);
                case PlayerArchetype.Anchor: return job == AttackJob.Support ? p : 0f;
                case PlayerArchetype.Engine: return job == AttackJob.Support || job == AttackJob.Width ? ok : 0f;
                case PlayerArchetype.Winger: return job == AttackJob.Width ? p : (job == AttackJob.Forward ? ok : 0f);
                case PlayerArchetype.Explosive: return job == AttackJob.Forward ? p : (job == AttackJob.Width ? ok : 0f);
                case PlayerArchetype.Finisher: return job == AttackJob.Forward ? p : 0f;
                case PlayerArchetype.GoalHunter: return job == AttackJob.Forward ? p : 0f;
                case PlayerArchetype.Target: return job == AttackJob.Forward ? p : (job == AttackJob.Support ? ok : 0f);
                case PlayerArchetype.Wall: return job == AttackJob.Support ? ok : 0f;
                case PlayerArchetype.Guardian: return job == AttackJob.Support ? ok : 0f;
                default: return 0f;
            }
        }

        /// <summary>The preference of a player for a job: his best role's table value times his affinity (a player without roles falls back on his coarse position).</summary>
        public float RoleBias(in WorldPlayer p, AttackJob job)
        {
            float best = 0f;
            if (p.Roles != null)
                for (int i = 0; i < p.Roles.Count; i++)
                {
                    float v = Lean(p.Roles[i].Role, job) * p.Roles[i].Affinity / a.AffinityScale;
                    if (v > best) best = v;
                }
            else if ((p.Role == PlayerRole.Forward && job == AttackJob.Forward) || (p.Role == PlayerRole.Midfielder && job == AttackJob.Support)) best = a.RoleAcceptable;
            return a.RoleBiasWeight * best;
        }

        // ------------------------------------------------------------------ who does what

        private static Vec2 AttackDirection(WorldView view, TeamSide team)
        {
            return (WorldGeometry.AttackGoal(view.Match, view.Field, team) - WorldGeometry.OwnGoal(view.Match, view.Field, team)).Normalized;
        }

        private static float Along(Vec2 p, Vec2 dir) { return p.X * dir.X + p.Y * dir.Y; }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>
        /// Gives every field player of the carrier's team a job. Fills the internal table (read it with <see cref="JobOf"/>). Order of priority: the support, then the runner, then the width; the best score takes
        /// each (ties: the earlier in the squad); whoever is left gets open.
        /// </summary>
        public void Assign(WorldView view, int carrierIdx)
        {
            WorldPlayer carrier = view[carrierIdx];
            TeamSide team = carrier.Team;
            Vec2 attack = AttackDirection(view, team);
            bool recovering = view.Ball.SecondsSincePossessionChange < a.RecoverySeconds;
            float boost = recovering ? a.RecoveryBoost : 1f;
            int start = view.TeamStart(team), end = start + view.TeamCount(team);
            for (int i = 0; i < jobs.Length; i++) jobs[i] = AttackJob.None;
            for (int i = start; i < end; i++)
            {
                WorldPlayer p = view[i];
                if (i == carrierIdx || !p.IsOnPitch || p.IsGoalkeeper || p.IsControlled) continue;
                jobs[i] = AttackJob.GetOpen;
                Vec2 home = p.HasHome ? p.Home : p.Position;
                float dC = (p.Position - carrier.Position).Magnitude;
                float close = 1f - Math.Min(1f, Math.Abs(dC - a.SupportIdealDistance) / a.SupportIdealDistance);
                support[i] = dC <= t.SupportRadius
                    ? a.SupportCloseWeight * boost * close - a.SupportHomeWeight * Math.Min(1f, (p.Position - home).Magnitude / a.HomeScaleDistance) + RoleBias(p, AttackJob.Support)
                    : float.NegativeInfinity;
                float gain = (Along(p.Position, attack) - Along(carrier.Position, attack)) / view.Field.Length;
                float gain01 = (Math.Max(-a.ForwardGainFloor, Math.Min(a.ForwardGainCap, gain)) + a.ForwardGainFloor) / (a.ForwardGainCap + a.ForwardGainFloor);
                float room = WorldGeometry.NearestOpponentDistanceTo(view, team, p.Position + attack * a.ForwardProbeDistance);
                forward[i] = p.Stamina >= a.ForwardMinStamina
                    ? a.ForwardAdvanceWeight * boost * gain01 + a.ForwardSpaceWeight * Math.Min(room, a.ForwardSpaceCap) / a.ForwardSpaceCap + RoleBias(p, AttackJob.Forward)
                    : float.NegativeInfinity;
                width[i] = a.WidthLateralWeight * Math.Min(1f, Math.Abs(home.Y) / (view.Field.Width / 2f)) + (p.Zone == PitchZone.Wing ? a.WidthWingBonus : 0f) + RoleBias(p, AttackJob.Width);
            }
            Pick(start, end, AttackJob.Support, support, a.SupportRunners);
            Pick(start, end, AttackJob.Forward, forward, a.ForwardRunners);
            Pick(start, end, AttackJob.Width, width, a.WidthHolders);
        }

        private void Pick(int start, int end, AttackJob job, float[] scores, int count)
        {
            for (int n = 0; n < count; n++)
            {
                int best = -1; float bestScore = float.NegativeInfinity;
                for (int i = start; i < end; i++)
                    if (jobs[i] == AttackJob.GetOpen && scores[i] > bestScore) { bestScore = scores[i]; best = i; }
                if (best < 0) return;
                jobs[best] = job;
            }
        }

        /// <summary>The job of a player after <see cref="Assign"/>.</summary>
        public AttackJob JobOf(int playerIndex) { return playerIndex >= 0 && playerIndex < jobs.Length ? jobs[playerIndex] : AttackJob.None; }

        // ------------------------------------------------------------------ where each one stands

        /// <summary>The plan of a player for the job he was given (or, when he cannot do it, for getting open).</summary>
        public AttackPlan Plan(WorldView view, int idx, int carrierIdx, AttackJob job, Vec2 openBase)
        {
            WorldPlayer me = view[idx];
            WorldPlayer carrier = view[carrierIdx];
            Vec2 home = me.HasHome ? me.Home : me.Position;
            Vec2 attack = AttackDirection(view, me.Team);
            var s = new OpenPointSearch { Home = home, HasCarrier = true, Carrier = carrier.Position, Forward = attack };
            Vec2 target;
            float magnitude;
            switch (job)
            {
                case AttackJob.Support:
                    {
                        Vec2 b = home + (carrier.Position - home) * t.SupportBallPull + attack * t.SupportAdvance;
                        s.Center = b; s.MaxHomeDeviation = a.JobMaxHomeDeviation; s.BandMin = t.SupportMinDistance; s.BandMax = t.SupportMaxDistance;
                        target = WorldGeometry.FindBestOpenPoint(view, idx, s, a, out _);
                        magnitude = a.SupportRunMagnitude;
                        break;
                    }
                case AttackJob.Forward:
                    {
                        s.Center = me.Position + attack * a.ForwardRunDistance; s.MaxHomeDeviation = a.ForwardMaxHomeDeviation;
                        s.BandMin = a.ForwardMinCarrierDistance; s.BandMax = t.PassMaxDistance; s.ForwardWeight = a.ForwardAdvanceWeight;
                        target = WorldGeometry.FindBestOpenPoint(view, idx, s, a, out _);
                        magnitude = a.ForwardRunMagnitude;
                        if (Along(target - me.Position, attack) < a.ForwardMinGain) return Plan(view, idx, carrierIdx, AttackJob.GetOpen, openBase);
                        break;
                    }
                case AttackJob.Width:
                    {
                        float side = Math.Abs(home.Y) > t.IdleRadius ? (home.Y > 0f ? 1f : -1f) : (carrier.Position.Y > 0f ? -1f : 1f);
                        Vec2 mid = home + (carrier.Position - home) * t.SupportBallPull;
                        s.Center = new Vec2(mid.X, side * (view.Field.Width / 2f - a.WidthMargin)); s.MaxHomeDeviation = a.JobMaxHomeDeviation;
                        s.BandMin = a.WidthMinCarrierDistance; s.BandMax = a.WidthMaxCarrierDistance; s.LateralWeight = a.WidthLateralWeight;
                        target = WorldGeometry.FindBestOpenPoint(view, idx, s, a, out _);
                        magnitude = a.WidthMagnitude;
                        break;
                    }
                default:
                    {
                        s.Center = openBase; s.MaxHomeDeviation = a.GetOpenMaxHomeDeviation;
                        target = WorldGeometry.FindBestOpenPoint(view, idx, s, a, out _);
                        magnitude = t.PositionMagnitude;
                        job = AttackJob.GetOpen;
                        break;
                    }
            }
            return new AttackPlan { Job = job, Target = target, Magnitude = magnitude };
        }
    }
}
