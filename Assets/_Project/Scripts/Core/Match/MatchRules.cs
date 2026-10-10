using System;

namespace FS27.Core
{
    /// <summary>
    /// Stateless rules: given where the ball is (and who touched it last) say whether it is in play,
    /// a goal, or out, and how play restarts. Knows nothing about Unity, players or time.
    /// "Out" means the whole ball has crossed the line (centre beyond the line by more than its radius).
    /// </summary>
    public static class MatchRules
    {
        public static BallJudgement Evaluate(FieldDimensions field, MatchSettings settings, in BallObservation ball,
                                             bool hasLastTouch, TeamSide lastTouch)
        {
            return Evaluate(field, settings.HomeAttacksPositiveX, settings.KickoffTeam, ball, hasLastTouch, lastTouch);
        }

        /// <summary>Same, with the attacking direction given explicitly: it changes at the side switch, and settings are configuration, not state.</summary>
        public static BallJudgement Evaluate(FieldDimensions field, bool homeAttacksPositiveX, TeamSide kickoffTeam, in BallObservation ball,
                                             bool hasLastTouch, TeamSide lastTouch)
        {
            float halfL = field.Length * 0.5f;
            float halfW = field.Width * 0.5f;
            Vec3 p = ball.Position;
            float r = ball.Radius;

            // End lines: goal, corner or goal kick.
            if (Math.Abs(p.X) - r > halfL)
            {
                int endSign = p.X > 0f ? 1 : -1;
                TeamSide attacker = AttackerAtEnd(homeAttacksPositiveX, endSign);

                if (HasCrossedGoalLine(field, ball))
                    return new BallJudgement { Outcome = BallOutcome.Goal, ScoringTeam = attacker };

                TeamSide defender = attacker.Opponent();
                if (hasLastTouch && lastTouch == defender)
                {
                    float zSign = p.Z >= 0f ? 1f : -1f;
                    return new BallJudgement
                    {
                        Outcome = BallOutcome.OutOfBounds,
                        Restart = RestartKind.Corner,
                        RestartTeam = attacker,
                        RestartPosition = new Vec2(endSign * (halfL - field.CornerInset), zSign * (halfW - field.CornerInset))
                    };
                }

                return new BallJudgement
                {
                    Outcome = BallOutcome.OutOfBounds,
                    Restart = RestartKind.GoalKick,
                    RestartTeam = defender,
                    RestartPosition = new Vec2(endSign * (halfL - field.GoalKickDistance), 0f)
                };
            }

            // Sidelines: throw-in for the opponent of whoever touched it last.
            if (Math.Abs(p.Z) - r > halfW)
            {
                TeamSide taker = hasLastTouch ? lastTouch.Opponent() : kickoffTeam;
                float x = Math.Max(-halfL + field.ThrowInEndMargin, Math.Min(halfL - field.ThrowInEndMargin, p.X));
                return new BallJudgement
                {
                    Outcome = BallOutcome.OutOfBounds,
                    Restart = RestartKind.ThrowIn,
                    RestartTeam = taker,
                    RestartPosition = new Vec2(x, p.Z > 0f ? halfW : -halfW)
                };
            }

            return new BallJudgement { Outcome = BallOutcome.InPlay };
        }

        /// <summary>
        /// THE test of a goal (Phase 18): the WHOLE ball is beyond the goal line (its centre further than the radius, so a ball touching the line or partly over it is still in play) and it is inside the mouth
        /// (between the posts, with the lowest part of the ball under the bar). The judgement and the keeper's hands (a ball that has crossed completely can no longer be played) both use this one function.
        /// </summary>
        public static bool HasCrossedGoalLine(FieldDimensions field, in BallObservation ball)
        {
            Vec3 p = ball.Position;
            return Math.Abs(p.X) - ball.Radius > field.Length * 0.5f && Math.Abs(p.Z) <= field.GoalWidth * 0.5f && p.Y - ball.Radius <= field.GoalHeight;
        }

        private static TeamSide AttackerAtEnd(bool homeAttacksPositiveX, int endSign)
        {
            bool homeAttacksThatEnd = homeAttacksPositiveX == (endSign > 0);
            return homeAttacksThatEnd ? TeamSide.Home : TeamSide.Away;
        }
    }
}
