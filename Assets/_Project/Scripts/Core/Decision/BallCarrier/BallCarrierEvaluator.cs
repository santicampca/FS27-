using System;

namespace FS27.Core
{
    /// <summary>What the last evaluation found (for measuring and for tests; nothing decides on it).</summary>
    public struct CarrierSummary
    {
        public bool ShootValid;
        public float ShootScore;
        public int PassCandidates;
        public int ThroughCandidates;
        public bool DribbleValid;
        public bool HoldAllowed;
        public CandidateType Best;
        public float BestScore;
    }

    /// <summary>
    /// How the player who has the ball compares what he could do (Phase 11): shoot, pass (safe or forward), pass into the space of a runner, carry the ball, shield it, or hold it for an instant. Every
    /// option gets a score on ONE scale and the best wins: no chain of special cases. A pure function of the world view (and of the runs his team-mates are making, through <see cref="ITeammateRuns"/>): it
    /// reads only the view, changes nothing, uses no chance and allocates nothing (the candidates live in an array owned by the evaluator). The same view always gives the same answer; ties go to the
    /// first candidate in a fixed order (shot, passes by squad order, passes into space, dribble, protect, hold). It does not build the intent: the decision engine does, with its existing builders.
    /// </summary>
    public sealed class BallCarrierEvaluator
    {
        private readonly BasicDecisionTuning t;
        private readonly AttackPositioningTuning a;
        private readonly BallCarrierTuning b;
        private readonly ActionCandidate[] candidates = new ActionCandidate[2 * WorldState.MaxPlayers + 8];
        private int count;

        public BallCarrierTuning Tuning => b;
        public CarrierSummary Summary { get; private set; }
        public int Count => count;
        public ActionCandidate At(int i) { return candidates[i]; }

        public BallCarrierEvaluator(BasicDecisionTuning tuning, AttackPositioningTuning attack, BallCarrierTuning carrier)
        {
            t = tuning ?? throw new ArgumentNullException(nameof(tuning));
            a = attack ?? throw new ArgumentNullException(nameof(attack));
            b = carrier ?? throw new ArgumentNullException(nameof(carrier));
        }

        private static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>The pressure on a player with the ball: 0 = nobody within the pressure radius, 1 = an opponent on top of him (more for more opponents), never above 1.</summary>
        public float PressureOn(WorldView view, in WorldPlayer me)
        {
            float nearest = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, me.Position);
            float p = Clamp01(1f - nearest / t.PressureRadius);
            int near = WorldGeometry.OpponentsWithin(view, me.Team, me.Position, t.PressureRadius);
            if (near > 1) p += b.PressureExtraOpponent * (near - 1);
            return Clamp01(p);
        }

        /// <summary>Scores everything the carrier could do and returns the best (never "none": holding or walking on is always there when nothing else is).</summary>
        public ActionCandidate Evaluate(WorldView view, int idx, ITeammateRuns runs, bool canKick)
        {
            count = 0;
            var summary = new CarrierSummary();
            WorldPlayer me = view[idx];
            float pressure = PressureOn(view, me);
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            Vec2 attack = (goal - WorldGeometry.OwnGoal(view.Match, view.Field, me.Team)).Normalized;
            ITeammateRuns reading = runs ?? NoRuns.Instance;

            if (canKick) AddShot(view, me, goal, pressure, ref summary);
            bool anyRun = false, anyPass = false;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer r = view[i];
                if (!r.IsOnPitch || r.Team != me.Team) continue;
                RunKind run = reading.RunOf(i);
                if (run == RunKind.Forward) anyRun = true;
                if (!canKick) continue;
                if (AddPass(view, idx, me, i, r, run, pressure, ref summary)) anyPass = true;
            }
            if (canKick)
                for (int i = 0; i < view.PlayerCount; i++)
                {
                    if (i == idx) continue;
                    WorldPlayer r = view[i];
                    if (!r.IsOnPitch || r.Team != me.Team || r.IsGoalkeeper) continue;
                    AddThrough(view, me, i, r, reading.RunOf(i), attack, ref summary);
                }
            AddDribble(view, me, goal, canKick, pressure, ref summary);
            AddProtect(view, me, pressure, anyPass, anyRun);
            AddHold(view, me, pressure, anyRun, ref summary);

            ActionCandidate best = default(ActionCandidate);
            best.Score = float.NegativeInfinity; best.ReceiverIndex = -1;
            for (int k = 0; k < count; k++) if (candidates[k].Score > best.Score) best = candidates[k];
            summary.Best = best.Type; summary.BestScore = best.Score;
            Summary = summary;
            return best;
        }

        private void Add(CandidateType type, float score, int receiver, Vec2 target, RunKind run, DecisionReason reason)
        {
            if (count >= candidates.Length) return;
            candidates[count++] = new ActionCandidate { Type = type, Score = score, ReceiverIndex = receiver, Target = target, Run = run, Reason = reason };
        }

        // ------------------------------------------------------------------ the shot

        private void AddShot(WorldView view, in WorldPlayer me, Vec2 goal, float pressure, ref CarrierSummary summary)
        {
            float distance = (goal - me.Position).Magnitude;
            if (distance > t.ShootRange) return;
            float angle = Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, goal));
            if (angle > t.ShootMaxAngleDegrees) return;
            if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, goal, t.ShootLaneClearance, true)) return;

            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, goal, true);
            float laneTerm = float.IsPositiveInfinity(room) ? 1f : Clamp01(room / b.ShootLaneCap);
            float keeperTerm = 1f;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (o.IsOnPitch && o.Team != me.Team && o.IsGoalkeeper) { keeperTerm = Clamp01(WorldGeometry.DistanceToSegment(o.Position, me.Position, goal) / b.ShootKeeperCap); break; }
            }
            float threat = b.ShootDistanceWeight * (1f - distance / t.ShootRange) + b.ShootAngleWeight * (1f - angle / t.ShootMaxAngleDegrees) + b.ShootLaneWeight * laneTerm + b.ShootKeeperWeight * keeperTerm;
            float score = b.ShootScale * threat - b.ShootPressureWeight * pressure;
            summary.ShootValid = true; summary.ShootScore = score;
            Add(CandidateType.Shoot, score, -1, goal, RunKind.None, score > 0f ? DecisionReason.ClearShot : DecisionReason.ClearShot);
        }

        // ------------------------------------------------------------------ the passes

        private float RunBonus(RunKind run, in WorldPlayer r, Vec2 attack)
        {
            switch (run)
            {
                case RunKind.Forward:
                    {
                        float along = r.Velocity.X * attack.X + r.Velocity.Y * attack.Y;
                        float moving = r.MaxSpeed > 0f ? Clamp01(along / r.MaxSpeed) : 0f;
                        return b.RunForwardBonus * (b.RunBaseShare + (1f - b.RunBaseShare) * moving);
                    }
                case RunKind.Support: return b.RunSupportBonus;
                case RunKind.Width: return b.RunWidthBonus;
                case RunKind.GetOpen: return b.RunGetOpenBonus;
                default: return 0f;
            }
        }

        private bool AddPass(WorldView view, int idx, in WorldPlayer me, int i, in WorldPlayer r, RunKind run, float pressure, ref CarrierSummary summary)
        {
            bool allowKeeper = pressure >= b.KeeperPassPressure;
            float baseScore = ReceiverScore.Evaluate(view, idx, i, true, allowKeeper, t, a, out float gain);
            if (float.IsNegativeInfinity(baseScore)) return false;
            Vec2 attack = (WorldGeometry.AttackGoal(view.Match, view.Field, me.Team) - WorldGeometry.OwnGoal(view.Match, view.Field, me.Team)).Normalized;
            float score = b.PassWeight * baseScore + RunBonus(run, r, attack);
            bool forward = gain >= b.ForwardPassGain;
            if (forward) score += b.ForwardPassBonus;

            // under pressure a safe pass is worth more: a free receiver on a clear lane
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position);
            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, r.Position);
            float safety = Clamp01(space / b.SafeSpaceCap) * (float.IsPositiveInfinity(room) ? 1f : Clamp01(room / b.SafeLaneCap));
            score += b.SafePassWeight * pressure * safety;

            // a receiver who has his back to the passer and an opponent at his heels
            Vec2 dir = ActionModel.DirectionOr(r.Position - me.Position, me.Facing);
            if (r.Facing.X * dir.X + r.Facing.Y * dir.Y > b.BackTurnedCosine && WorldGeometry.OpponentsWithin(view, me.Team, r.Position, a.ReceiverPressureRadius) > 0) score -= b.BackTurnedPenalty;

            summary.PassCandidates++;
            Add(forward ? CandidateType.ForwardPass : CandidateType.Pass, score, i, r.Position, run,
                run == RunKind.Forward ? DecisionReason.RunnerPass : (pressure > 0f ? DecisionReason.UnderPressurePass : DecisionReason.FreePass));
            return true;
        }

        /// <summary>
        /// A pass to the point a runner is running to, not to where he is. Only for a team-mate who is running forward (told to, or seen to). It exists only when the point is inside the pitch, in range, ahead of the
        /// carrier, on a clear lane, with room around it, and the runner and the ball get there clearly before any opponent; otherwise there is no such candidate.
        /// </summary>
        private void AddThrough(WorldView view, in WorldPlayer me, int i, in WorldPlayer r, RunKind run, Vec2 attack, ref CarrierSummary summary)
        {
            float along = r.Velocity.X * attack.X + r.Velocity.Y * attack.Y;
            if (run != RunKind.Forward && along < b.ThroughMinRunnerSpeed) return;
            if (!view.CanUse(me.PlayerId, FootballActionKind.ThroughPass).Allowed) return;
            Vec2 heading = r.Speed >= b.ThroughMinRunnerSpeed ? r.Velocity.Normalized : attack;
            float lead = b.ThroughLeadDistance + r.Speed * b.ThroughLeadSeconds;
            Vec2 point = r.Position + heading * lead;
            float halfL = view.Field.Length / 2f - a.SearchPitchMargin, halfW = view.Field.Width / 2f - a.SearchPitchMargin;
            if (Math.Abs(point.X) > halfL || Math.Abs(point.Y) > halfW) return;

            float distance = (point - me.Position).Magnitude;
            if (distance < t.PassMinDistance || distance > t.PassMaxDistance) return;
            float gain = ((point - me.Position).X * attack.X + (point - me.Position).Y * attack.Y) / view.Field.Length;
            if (gain < b.ThroughMinGain) return;
            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, point);
            if (room < view.Tuning.PassLaneClearance) return;
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, point);
            if (space < b.ThroughMinSpace) return;

            // who gets there first: the runner and the ball, or the nearest opponent
            float runner = r.MaxSpeed > 0f ? (point - r.Position).Magnitude / (r.MaxSpeed * b.ThroughSpeedShare) : float.PositiveInfinity;
            float ball = distance / b.ThroughBallSpeed;
            float arrival = Math.Max(runner, ball);
            float opponent = float.PositiveInfinity;
            for (int k = 0; k < view.PlayerCount; k++)
            {
                WorldPlayer o = view[k];
                if (!o.IsOnPitch || o.Team == me.Team || o.MaxSpeed <= 0f) continue;
                float time = (point - o.Position).Magnitude / (o.MaxSpeed * b.ThroughSpeedShare);
                if (time < opponent) opponent = time;
            }
            float margin = opponent - arrival;
            if (margin < b.ThroughRaceMargin) return;

            float raceTerm = float.IsPositiveInfinity(margin) ? 1f : Clamp01(margin / b.ThroughRaceCap);
            float score = b.ThroughBase + b.ThroughProgressWeight * gain + b.ThroughSpaceWeight * Clamp01(space / b.ThroughSpaceCap) + b.ThroughRaceWeight * raceTerm + RunBonus(RunKind.Forward, r, attack) - b.ThroughRisk;
            summary.ThroughCandidates++;
            Add(CandidateType.ThroughPass, score, i, point, run, DecisionReason.SpacePass);
        }

        // ------------------------------------------------------------------ carrying, shielding, holding

        private void AddDribble(WorldView view, in WorldPlayer me, Vec2 goal, bool canKick, float pressure, ref CarrierSummary summary)
        {
            DribbleAssessment d = DribbleSpace.Assess(view, me, goal, canKick, t);
            if (d.SkillMove)
            {
                Add(CandidateType.Dribble, b.SkillMoveScore - b.DribblePressureWeight * pressure, -1, me.Position + d.SkillDirection, RunKind.None, DecisionReason.SkillMoveAvailable);
                summary.DribbleValid = true;
            }
            if (d.Free)
            {
                float own = Clamp01(1f - WorldGeometry.DistanceToOwnGoal(view, me) / b.DribbleOwnGoalZone);
                float tired = me.Stamina < b.DribbleTiredStamina ? 1f - me.Stamina / b.DribbleTiredStamina : 0f;
                float space = Clamp01(d.FreeDistance / t.DribbleClearDistance);
                float score = b.DribbleScale * d.Progress * space - b.DribblePressureWeight * pressure - b.DribbleOwnGoalWeight * own - b.DribbleTiredWeight * tired;
                Add(CandidateType.Dribble, score, -1, me.Position + d.Direction * t.DribbleLookahead, RunKind.None, DecisionReason.SpaceToDribble);
                summary.DribbleValid = true;
            }
            else if (!d.SkillMove)
                Add(CandidateType.Dribble, b.DribbleFallbackScore, -1, goal, RunKind.None, DecisionReason.NoActionFallback);
        }

        private void AddProtect(WorldView view, in WorldPlayer me, float pressure, bool anyPass, bool anyRun)
        {
            if (WorldGeometry.NearestOpponentDistanceTo(view, me.Team, me.Position) >= t.PressureRadius) return;
            float score = b.ProtectBase + b.ProtectPressureWeight * pressure + (!anyPass && anyRun ? b.ProtectWaitBonus : 0f);
            Add(CandidateType.Protect, score, -1, me.Position, RunKind.None, DecisionReason.UnderPressureProtect);
        }

        private void AddHold(WorldView view, in WorldPlayer me, float pressure, bool anyRun, ref CarrierSummary summary)
        {
            float phase = view.Match.MatchTime - b.HoldCycleSeconds * (float)Math.Floor(view.Match.MatchTime / b.HoldCycleSeconds);
            if (pressure > b.HoldMaxPressure || phase >= b.HoldWindowSeconds) return;
            summary.HoldAllowed = true;
            Add(CandidateType.Hold, b.HoldBase + (anyRun ? b.HoldRunBonus : 0f), -1, me.Position, RunKind.None, DecisionReason.HoldForRun);
        }
    }
}
