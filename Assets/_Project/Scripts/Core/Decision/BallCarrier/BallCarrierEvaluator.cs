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
        // ---- Phase 12: what it saw and how clear the win was
        /// <summary>The best candidate of a DIFFERENT type, and its score (type None / minus infinity when there is none).</summary>
        public CandidateType RunnerUpType;
        public float RunnerUpScore;
        /// <summary>BestScore minus RunnerUpScore (the margin of the decision between kinds of action); infinity when the winner stood alone.</summary>
        public float Margin;
        /// <summary>Bit per <see cref="CandidateType"/> (1 &lt;&lt; type): there was at least one candidate of it.</summary>
        public int AvailableMask;
        /// <summary>Bit per <see cref="RunKind"/>: a team-mate of this kind of run was on the pitch.</summary>
        public int RunMask;
        public float Pressure, Stamina, DistanceToGoal;
        /// <summary>The free distance ahead of the dribble the carrier scored (up to the horizon), when there was one.</summary>
        public float DribbleFreeDistance;
        /// <summary>What the carrier saw ahead of him: free distance over the clear distance (0..1), how well the way goes to goal (0..1), and whether a free way existed.</summary>
        public float DribbleSpace, DribbleProgress;
        public bool DribbleFree;
        public int CandidateCount;
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
        private readonly int[] rejected = new int[8 * (int)CandidateRejection.Count];

        /// <summary>Count rejections of the next evaluations (diagnostics): off by default, so that normal play pays nothing for it.</summary>
        public bool CollectRejections { get; set; }
        /// <summary>How many things of this kind were NOT candidates in the last evaluation, for this reason (only when <see cref="CollectRejections"/> is on).</summary>
        public int RejectionCount(CandidateType type, CandidateRejection why) { return rejected[(int)type * (int)CandidateRejection.Count + (int)why]; }

        private void Reject(CandidateType type, CandidateRejection why)
        {
            if (CollectRejections && why != CandidateRejection.NotApplicable && why != CandidateRejection.None) rejected[(int)type * (int)CandidateRejection.Count + (int)why]++;
        }

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
            if (CollectRejections) Array.Clear(rejected, 0, rejected.Length);
            var summary = new CarrierSummary();
            WorldPlayer me = view[idx];
            float pressure = PressureOn(view, me);
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            Vec2 attack = (goal - WorldGeometry.OwnGoal(view.Match, view.Field, me.Team)).Normalized;
            ITeammateRuns reading = runs ?? NoRuns.Instance;
            summary.Pressure = pressure; summary.Stamina = me.Stamina; summary.DistanceToGoal = (goal - me.Position).Magnitude;

            if (canKick) AddShot(view, me, goal, pressure, ref summary); else Reject(CandidateType.Shoot, CandidateRejection.CannotKick);
            bool anyRun = false, anyPass = false;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer r = view[i];
                if (!r.IsOnPitch || r.Team != me.Team) continue;
                RunKind run = reading.RunOf(i);
                if (run != RunKind.None) summary.RunMask |= 1 << (int)run;
                if (run == RunKind.Forward) anyRun = true;
                if (!canKick) { Reject(CandidateType.Pass, CandidateRejection.CannotKick); continue; }
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
            else Reject(CandidateType.ThroughPass, CandidateRejection.CannotKick);
            AddDribble(view, me, goal, canKick, pressure, ref summary);
            AddProtect(view, me, pressure, anyPass, anyRun);
            AddHold(view, me, pressure, anyRun, ref summary);

            // how this player reads the game (Phase 14): a bias on each candidate's score; none = the Phase 13 scores
            if (Bias != null) for (int k = 0; k < count; k++) candidates[k].Score += Bias.Bias(candidates[k], summary);

            ActionCandidate best = default(ActionCandidate);
            best.Score = float.NegativeInfinity; best.ReceiverIndex = -1;
            for (int k = 0; k < count; k++) { summary.AvailableMask |= 1 << (int)candidates[k].Type; if (candidates[k].Score > best.Score) best = candidates[k]; }
            // the second best of ALL (another receiver of the same kind counts): what a mistake would choose
            HasSecond = false; Second = default(ActionCandidate);
            for (int k = 0; k < count; k++)
                if (!SameCandidate(candidates[k], best) && (!HasSecond || candidates[k].Score > Second.Score)) { Second = candidates[k]; HasSecond = true; }
            // the best candidate of any OTHER kind of action: how clearly the decision was won
            float runnerUp = float.NegativeInfinity; CandidateType runnerType = CandidateType.None;
            for (int k = 0; k < count; k++) if (candidates[k].Type != best.Type && candidates[k].Score > runnerUp) { runnerUp = candidates[k].Score; runnerType = candidates[k].Type; }
            summary.Best = best.Type; summary.BestScore = best.Score; summary.CandidateCount = count;
            summary.RunnerUpType = runnerType; summary.RunnerUpScore = runnerUp; summary.Margin = best.Score - runnerUp;
            Summary = summary;
            return best;
        }

        /// <summary>The player took this candidate instead of the best one (a mistake of his level): what the evaluator reports from now on is what he did.</summary>
        public void Choose(in ActionCandidate taken)
        {
            CarrierSummary s = Summary;
            CandidateType wasBest = s.Best; float wasScore = s.BestScore;
            s.Best = taken.Type; s.BestScore = taken.Score;
            s.RunnerUpType = wasBest; s.RunnerUpScore = wasScore; s.Margin = taken.Score - wasScore;
            Summary = s;
        }

        private static bool SameCandidate(in ActionCandidate a, in ActionCandidate b) { return a.Type == b.Type && a.ReceiverIndex == b.ReceiverIndex; }

        /// <summary>How the player reading the game values each candidate on top of its score (Phase 14), or null.</summary>
        public ICandidateBias Bias;
        /// <summary>The second best candidate of the last evaluation (any kind, any receiver), when there was one.</summary>
        public ActionCandidate Second;
        public bool HasSecond;

        private void Add(CandidateType type, float score, int receiver, Vec2 target, RunKind run, DecisionReason reason, in CandidateParts parts)
        {
            if (count >= candidates.Length) return;
            candidates[count++] = new ActionCandidate { Type = type, Score = score, ReceiverIndex = receiver, Target = target, Run = run, Reason = reason, Parts = parts };
        }

        // ------------------------------------------------------------------ the shot

        private void AddShot(WorldView view, in WorldPlayer me, Vec2 goal, float pressure, ref CarrierSummary summary)
        {
            float distance = (goal - me.Position).Magnitude;
            if (distance > t.ShootRange) { Reject(CandidateType.Shoot, CandidateRejection.OutOfRange); return; }
            float angle = Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, goal));
            if (angle > t.ShootMaxAngleDegrees) { Reject(CandidateType.Shoot, CandidateRejection.BadAngle); return; }
            if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, goal, t.ShootLaneClearance, true)) { Reject(CandidateType.Shoot, CandidateRejection.LaneBlocked); return; }

            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, goal, true);
            float laneTerm = float.IsPositiveInfinity(room) ? 1f : Clamp01(room / b.ShootLaneCap);
            float keeperTerm = 1f;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (o.IsOnPitch && o.Team != me.Team && o.IsGoalkeeper) { keeperTerm = Clamp01(WorldGeometry.DistanceToSegment(o.Position, me.Position, goal) / b.ShootKeeperCap); break; }
            }
            float proximity = b.ShootDistanceWeight * (1f - distance / t.ShootRange), angleTerm = b.ShootAngleWeight * (1f - angle / t.ShootMaxAngleDegrees);
            float lineTerm = b.ShootLaneWeight * laneTerm, keeperPart = b.ShootKeeperWeight * keeperTerm;
            float threat = proximity + angleTerm + lineTerm + keeperPart;
            float pressurePenalty = b.ShootPressureWeight * pressure;
            float score = b.ShootScale * threat - pressurePenalty;
            if (score < b.ShootMinScore) { Reject(CandidateType.Shoot, CandidateRejection.LowQuality); return; }
            summary.ShootValid = true; summary.ShootScore = score;
            var parts = new CandidateParts { P0 = b.ShootScale * proximity, P1 = b.ShootScale * angleTerm, P2 = b.ShootScale * lineTerm, P3 = b.ShootScale * keeperPart, P4 = -pressurePenalty };
            Add(CandidateType.Shoot, score, -1, goal, RunKind.None, DecisionReason.ClearShot, parts);
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
            float baseScore = ReceiverScore.Evaluate(view, idx, i, true, allowKeeper, t, a, out float gain, out ReceiverParts rp, out CandidateRejection why);
            if (float.IsNegativeInfinity(baseScore)) { Reject(CandidateType.Pass, why); return false; }
            Vec2 attack = (WorldGeometry.AttackGoal(view.Match, view.Field, me.Team) - WorldGeometry.OwnGoal(view.Match, view.Field, me.Team)).Normalized;
            float runBonus = RunBonus(run, r, attack);
            float score = b.PassWeight * baseScore + runBonus;
            bool forward = gain >= b.ForwardPassGain;
            if (forward) score += b.ForwardPassBonus;
            // Phase 28: a cross from the flank to a team-mate in the box, lofted over the defenders; worth what the ball gains times the chance it arrives
            float crossBonus = 0f;
            bool cross = IsCross(view, me, r);
            if (cross)
            {
                crossBonus = b.CrossBonus * PassRisk.SuccessChance(view, me, r, (r.Position - me.Position).Magnitude, t, true);
                score += crossBonus;
            }

            // under pressure a safe pass is worth more: a free receiver on a clear lane
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position);
            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, r.Position);
            float safety = Clamp01(space / b.SafeSpaceCap) * (float.IsPositiveInfinity(room) ? 1f : Clamp01(room / b.SafeLaneCap));
            float safePass = b.SafePassWeight * pressure * safety + b.SafePassBaseWeight * safety;
            score += safePass;

            // a receiver who has his back to the passer and an opponent at his heels
            Vec2 dir = ActionModel.DirectionOr(r.Position - me.Position, me.Facing);
            float backTurned = 0f;
            if (r.Facing.X * dir.X + r.Facing.Y * dir.Y > b.BackTurnedCosine && WorldGeometry.OpponentsWithin(view, me.Team, r.Position, a.ReceiverPressureRadius) > 0) { backTurned = b.BackTurnedPenalty; score -= backTurned; }

            // a rival who presses the passer reaches the ball in the first metres of its path: the pass is worth less the surer he is to
            float releasePenalty = 0f;
            if (b.ReleaseWeight > 0f && WorldGeometry.NearestOpponentDistanceTo(view, me.Team, me.Position) < t.PressureRadius + b.ReleaseReach)
            {
                float margin = PassRisk.ReleaseMargin(view, me, r.Position, t, b);
                if (!float.IsPositiveInfinity(margin)) releasePenalty = b.ReleaseWeight * Clamp01((b.ReleaseSlack - margin) / b.ReleaseScale);
                score -= releasePenalty;
            }

            summary.PassCandidates++;
            float w = b.PassWeight;
            var parts = new CandidateParts
            {
                P0 = w * rp.Advance, P1 = w * rp.Space, P2 = w * rp.Distance, P3 = w * rp.Angle, P4 = w * rp.Lane, P5 = w * rp.Pressure, P6 = w * rp.Move,
                P7 = runBonus, P8 = (forward ? b.ForwardPassBonus : 0f) + crossBonus, P9 = safePass, P10 = -backTurned, P11 = w * rp.Risk - releasePenalty
            };
            Add(forward ? CandidateType.ForwardPass : CandidateType.Pass, score, i, r.Position, run,
                cross ? DecisionReason.CrossToBox : (run == RunKind.Forward ? DecisionReason.RunnerPass : (pressure > 0f ? DecisionReason.UnderPressurePass : DecisionReason.FreePass)), parts);
            return true;
        }

        /// <summary>A cross: the carrier is wide, in the attacking third, and the receiver stands in the box with room around him, at a distance a lofted ball can cover.</summary>
        /// <summary>Phase 36: the defence marks the box, so a receiver in it needs less room (see <see cref="BallCarrierTuning.CrossMinSpaceMarked"/>).</summary>
        public bool MarkedCross { get; set; }

        private bool IsCross(WorldView view, in WorldPlayer me, in WorldPlayer r)
        {
            if (b.CrossBonus <= 0f) return false;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            if (Math.Abs(me.Position.Y) < view.Field.Width / 2f - b.CrossWideZone || (goal - me.Position).Magnitude > b.CrossMaxGoalDistance) return false;
            float depthLimit = MarkedCross && Math.Abs(goal.X - me.Position.X) <= b.CrossBylineDistance ? b.CrossCutbackDepth : b.CrossBoxDepth;
            if (Math.Abs(goal.X - r.Position.X) > depthLimit || Math.Abs(r.Position.Y - goal.Y) > b.CrossBoxHalfWidth) return false;
            float d = (r.Position - me.Position).Magnitude;
            if (d < b.CrossMinDistance || d > b.CrossMaxDistance) return false;
            return WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position) >= (MarkedCross ? b.CrossMinSpaceMarked : b.CrossMinSpace);
        }

        /// <summary>
        /// A pass to the point a runner is running to, not to where he is. Only for a team-mate who is running forward (told to, or seen to). It exists only when the point is inside the pitch, in range, ahead of the
        /// carrier, on a clear lane, with room around it, and the runner and the ball get there clearly before any opponent; otherwise there is no such candidate.
        /// </summary>
        private void AddThrough(WorldView view, in WorldPlayer me, int i, in WorldPlayer r, RunKind run, Vec2 attack, ref CarrierSummary summary)
        {
            float along = r.Velocity.X * attack.X + r.Velocity.Y * attack.Y;
            if (run != RunKind.Forward && along < b.ThroughMinRunnerSpeed) { Reject(CandidateType.ThroughPass, CandidateRejection.NotRunner); return; }
            if (!view.CanUse(me.PlayerId, FootballActionKind.ThroughPass).Allowed) { Reject(CandidateType.ThroughPass, CandidateRejection.CannotUse); return; }
            Vec2 heading = r.Speed >= b.ThroughMinRunnerSpeed ? r.Velocity.Normalized : attack;
            float lead = b.ThroughLeadDistance + r.Speed * b.ThroughLeadSeconds;
            Vec2 point = r.Position + heading * lead;
            float halfL = view.Field.Length / 2f - a.SearchPitchMargin, halfW = view.Field.Width / 2f - a.SearchPitchMargin;
            if (Math.Abs(point.X) > halfL || Math.Abs(point.Y) > halfW) { Reject(CandidateType.ThroughPass, CandidateRejection.OutsidePitch); return; }

            if (b.ThroughKeeperMargin > 0f)
                for (int k = 0; k < view.PlayerCount; k++)
                {
                    WorldPlayer o = view[k];
                    if (o.IsOnPitch && o.Team != me.Team && o.IsGoalkeeper && (point - o.Position).Magnitude < t.KeeperReactDistance + b.ThroughKeeperMargin) { Reject(CandidateType.ThroughPass, CandidateRejection.NearKeeper); return; }
                }
            float distance = (point - me.Position).Magnitude;
            if (distance < t.PassMinDistance || distance > t.PassMaxDistance) { Reject(CandidateType.ThroughPass, CandidateRejection.OutOfRange); return; }
            float gain = ((point - me.Position).X * attack.X + (point - me.Position).Y * attack.Y) / view.Field.Length;
            if (gain < b.ThroughMinGain) { Reject(CandidateType.ThroughPass, CandidateRejection.NoGain); return; }
            float room = WorldGeometry.PassLaneRoom(view, me.Team, me.Position, point);
            if (room < view.Tuning.PassLaneClearance) { Reject(CandidateType.ThroughPass, CandidateRejection.LaneBlocked); return; }
            float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, point);
            if (space < b.ThroughMinSpace) { Reject(CandidateType.ThroughPass, CandidateRejection.NoSpace); return; }

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
            if (margin < b.ThroughRaceMargin) { Reject(CandidateType.ThroughPass, CandidateRejection.LostRace); return; }

            float raceTerm = float.IsPositiveInfinity(margin) ? 1f : Clamp01(margin / b.ThroughRaceCap);
            float progress = b.ThroughProgressWeight * gain, spaceTerm = b.ThroughSpaceWeight * Clamp01(space / b.ThroughSpaceCap), raceScore = b.ThroughRaceWeight * raceTerm, runBonus = RunBonus(RunKind.Forward, r, attack);
            float score = b.ThroughBase + progress + spaceTerm + raceScore + runBonus - b.ThroughRisk;
            summary.ThroughCandidates++;
            var parts = new CandidateParts { P0 = b.ThroughBase, P1 = progress, P2 = spaceTerm, P3 = raceScore, P4 = runBonus, P5 = -b.ThroughRisk };
            Add(CandidateType.ThroughPass, score, i, point, run, DecisionReason.SpacePass, parts);
        }

        // ------------------------------------------------------------------ carrying, shielding, holding

        private void AddDribble(WorldView view, in WorldPlayer me, Vec2 goal, bool canKick, float pressure, ref CarrierSummary summary)
        {
            DribbleAssessment d = DribbleSpace.Assess(view, me, goal, canKick, t);
            summary.DribbleFree = d.Free; summary.DribbleProgress = d.Progress; summary.DribbleSpace = Clamp01(d.FreeDistance / t.DribbleClearDistance);
            if (d.SkillMove)
            {
                float pressurePenalty = b.DribblePressureWeight * pressure;
                Add(CandidateType.Dribble, b.SkillMoveScore - pressurePenalty, -1, me.Position + d.SkillDirection, RunKind.None, DecisionReason.SkillMoveAvailable, new CandidateParts { P1 = -pressurePenalty, P4 = b.SkillMoveScore });
                summary.DribbleValid = true;
            }
            if (d.Free)
            {
                float own = Clamp01(1f - WorldGeometry.DistanceToOwnGoal(view, me) / b.DribbleOwnGoalZone);
                float tired = me.Stamina < b.DribbleTiredStamina ? 1f - me.Stamina / b.DribbleTiredStamina : 0f;
                float freeAhead = DribbleSpace.FreeAhead(view, me, d.ProbeDirection, t, b.DribbleSpaceHorizon);
                summary.DribbleFreeDistance = freeAhead;
                float space = Clamp01(freeAhead / b.DribbleSpaceHorizon);
                float advance = b.DribbleScale * d.Progress * space, pressurePenalty = b.DribblePressureWeight * pressure, ownPenalty = b.DribbleOwnGoalWeight * own, tiredPenalty = b.DribbleTiredWeight * tired;
                float score = advance - pressurePenalty - ownPenalty - tiredPenalty;
                Add(CandidateType.Dribble, score, -1, me.Position + d.Direction * t.DribbleLookahead, RunKind.None, DecisionReason.SpaceToDribble, new CandidateParts { P0 = advance, P1 = -pressurePenalty, P2 = -ownPenalty, P3 = -tiredPenalty });
                summary.DribbleValid = true;
            }
            else if (!d.SkillMove)
            {
                Reject(CandidateType.Dribble, CandidateRejection.NoFreeWay);
                Add(CandidateType.Dribble, b.DribbleFallbackScore, -1, goal, RunKind.None, DecisionReason.NoActionFallback, new CandidateParts { P0 = b.DribbleFallbackScore });
            }
        }

        private void AddProtect(WorldView view, in WorldPlayer me, float pressure, bool anyPass, bool anyRun)
        {
            if (WorldGeometry.NearestOpponentDistanceTo(view, me.Team, me.Position) >= t.PressureRadius) { Reject(CandidateType.Protect, CandidateRejection.NoOpponentNear); return; }
            float pressureTerm = b.ProtectPressureWeight * pressure, wait = !anyPass && anyRun ? b.ProtectWaitBonus : 0f;
            float score = b.ProtectBase + pressureTerm + wait;
            Add(CandidateType.Protect, score, -1, me.Position, RunKind.None, DecisionReason.UnderPressureProtect, new CandidateParts { P0 = b.ProtectBase, P1 = pressureTerm, P2 = wait });
        }

        private void AddHold(WorldView view, in WorldPlayer me, float pressure, bool anyRun, ref CarrierSummary summary)
        {
            float phase = view.Match.MatchTime - b.HoldCycleSeconds * (float)Math.Floor(view.Match.MatchTime / b.HoldCycleSeconds);
            if (pressure > b.HoldMaxPressure) { Reject(CandidateType.Hold, CandidateRejection.TooMuchPressure); return; }
            if (phase >= b.HoldWindowSeconds) { Reject(CandidateType.Hold, CandidateRejection.OutsideHoldWindow); return; }
            summary.HoldAllowed = true;
            float run = anyRun ? b.HoldRunBonus : 0f;
            Add(CandidateType.Hold, b.HoldBase + run, -1, me.Position, RunKind.None, DecisionReason.HoldForRun, new CandidateParts { P0 = b.HoldBase, P1 = run });
        }
    }
}
