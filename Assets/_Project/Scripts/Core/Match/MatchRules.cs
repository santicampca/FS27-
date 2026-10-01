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
            float halfL = field.Length * 0.5f;
            float halfW = field.Width * 0.5f;
            Vec3 p = ball.Position;
            float r = ball.Radius;

            // End lines: goal, corner or goal kick.
            if (Math.Abs(p.X) - r > halfL)
            {
                int endSign = p.X > 0f ? 1 : -1;
                TeamSide attacker = settings.AttackerAtEnd(endSign);

                bool insideMouth = Math.Abs(p.Z) <= field.GoalWidth * 0.5f && p.Y - r <= field.GoalHeight;
                if (insideMouth)
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
                TeamSide taker = hasLastTouch ? lastTouch.Opponent() : settings.KickoffTeam;
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
    }
}
