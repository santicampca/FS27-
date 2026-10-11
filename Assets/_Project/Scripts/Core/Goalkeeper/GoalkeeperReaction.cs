using System;

namespace FS27.Core
{
    /// <summary>What a keeper makes of a shot, at one instant (Phase 16).</summary>
    public struct GoalkeeperRead
    {
        /// <summary>The ball is coming at his goal fast enough, in the right direction, to be a shot that crosses the line inside the mouth and under the bar.</summary>
        public bool IsShot;
        /// <summary>Seconds until the ball crosses the goal line (the truth of the ball's flight; the keeper only uses it after his reaction time).</summary>
        public float TimeToGoal;
        /// <summary>Where it will cross (field space) and how high.</summary>
        public Vec2 Crossing;
        public float Height;
        /// <summary>The ball's distance to the goal line now (metres along the pitch).</summary>
        public float DistanceToGoal;
        /// <summary>0..1: how much of the shooter's body and ball he can see (players in the way take from it).</summary>
        public float Visibility;
        /// <summary>Seconds he needs, from the strike, before he starts to move.</summary>
        public float Delay;
        /// <summary>Seconds since the strike.</summary>
        public float Elapsed;
        /// <summary>He has had the time to react: only now does he know where the ball is going.</summary>
        public bool Started;
        /// <summary>Where he believes it will cross (his reading, with its error), and the error in metres.</summary>
        public Vec2 Believed;
        public float ReadError;
    }

    /// <summary>
    /// How a keeper reacts to a shot (Phase 16). The ball is read as a shot only if it comes at the goal fast enough and will cross inside the mouth and under the bar. He does NOT start to move before his reaction time has
    /// passed since the strike (which depends on his reflexes, on how well he sees, on his anticipation and on the difficulty), and what he then goes to is where he THINKS the ball will cross: the truth plus a reading
    /// error that is large for a poor reader and shrinks as the ball arrives. He never knows the result of the shot, never moves faster than his legs and never reaches further than the Core lets him.
    /// </summary>
    public static class GoalkeeperReaction
    {
        public static GoalkeeperRead Read(WorldView view, in WorldPlayer me, GoalkeeperAiTuning t, in GoalkeeperSkill skill)
        {
            var r = new GoalkeeperRead { Visibility = 1f };
            WorldBall ball = view.Ball;
            if (ball.HasPossessor || ball.Speed < t.ShotMinSpeed) return r;
            if (ball.Mode != BallMode.InShot && ball.Mode != BallMode.Deflected && ball.Mode != BallMode.Rebound) return r;
            if (ball.HasLastTouch && ball.LastTouchTeam == me.Team) return r;

            Vec2 goal = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            float toward = goal.X > 0f ? 1f : -1f;                      // +1 when the goal is on the positive side
            float vx = ball.Velocity.X, vz = ball.Velocity.Z;
            float closing = vx * toward;
            if (closing <= 0f || ball.Direction.X * toward < t.ShotApproachCosine) return r;
            float remaining = (goal.X - ball.Ground.X) * toward;        // metres still to travel along the pitch
            if (remaining < 0f) return r;
            float time = remaining / closing;
            if (time > t.ShotHorizonSeconds) return r;

            Vec3 at = view.PredictBallPosition(time);
            float halfGoal = view.Field.GoalWidth / 2f;
            if (Math.Abs(at.Z - goal.Y) > halfGoal + t.CrossingMargin || at.Y > view.Field.GoalHeight + t.HeightMargin) return r;

            r.IsShot = true; r.TimeToGoal = time; r.Crossing = new Vec2(goal.X, at.Z); r.Height = at.Y; r.DistanceToGoal = remaining;
            r.Elapsed = ball.SecondsSinceTouch;

            // the view of the shot: players standing between the ball and him take from it
            int blockers = 0;
            Vec2 from = ball.Ground, to = me.Position;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.IsGoalkeeper || o.PlayerId == ball.LastTouchPlayerId) continue;
                if (WorldGeometry.DistanceToSegment(o.Position, from, to) <= t.VisibilityCorridor) blockers++;
            }
            r.Visibility = MathUtil.Clamp01(1f - t.VisibilityPerBlocker * blockers);

            float delay = skill.ReactionSeconds * t.ReactionScale
                          * (1f + t.VisibilityDelayPenalty * (1f - r.Visibility))
                          * (1f + t.SaveTimingPenalty * (1f - MathUtil.Clamp01(skill.SaveTiming)))
                          * (1f - t.AnticipationRelief * MathUtil.Clamp01(skill.Anticipation));
            r.Delay = delay;
            r.Started = r.Elapsed >= delay;

            // what he thinks: the truth plus an error that shrinks as the ball arrives
            float timeLeft = Math.Max(0f, time);
            float known = MathUtil.Clamp01(timeLeft / Math.Max(t.ReadErrorReferenceSeconds, 1e-3f));
            float size = t.ReadErrorMax * t.ReadErrorScale * (1f - MathUtil.Clamp01(skill.ShotReading)) * known * (1f + (1f - r.Visibility));
            // the same shot is read the same way at every step: the key is the moment of the strike
            int shotKey = (int)Math.Round((view.Match.MatchTime - ball.SecondsSinceTouch) / LocomotionDefaults.FixedDeltaTime);
            float sign = DecisionDice.Roll(DecisionDice.Reaction, shotKey, me.Slot) * 2f < 1f ? -1f : 1f;
            float amount = DecisionDice.Roll(DecisionDice.Choice, shotKey, me.Slot);
            r.ReadError = size * amount;
            float believedY = r.Crossing.Y + sign * r.ReadError;
            r.Believed = new Vec2(goal.X, believedY);
            return r;
        }
    }
}
