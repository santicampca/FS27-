using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 37: every number of the high ball as a team play (REQ-0068, 0181, 0185, 0192, 0193). One place; the twins are in <see cref="AerialPlayTuning"/>. Metres, seconds.
    /// </summary>
    public static class AerialPlayDefaults
    {
        /// <summary>How far ahead the ball's flight is looked at, and the time between two looks.</summary>
        public const float HorizonSeconds = 3.2f, StepSeconds = 0.1f;
        /// <summary>The ball is a high ball when it reaches at least this height at some point of its flight (lower balls are passes, not aerial play).</summary>
        public const float MinApex = 2.0f;
        /// <summary>The height at which the players expect to play a descending ball (head height). The place of the ball at that moment is the place of the duel.</summary>
        public const float ContactHeight = 1.6f;
        /// <summary>Share of his top speed a player uses to judge when he is under the ball, and the distance (m) from the point at which he counts as arrived.</summary>
        public const float SpeedShare = 0.9f, ReachDistance = 0.6f;
        /// <summary>A player who arrives later than the ball by more than this does not contest it (seconds).</summary>
        public const float LateSlack = 0.7f;
        /// <summary>The player a ball was aimed at is given this much of a head start in the claim (he knows where it is going).</summary>
        public const float ReceiverHeadStart = 0.35f;
        /// <summary>A player further than this from the point does not go (metres).</summary>
        public const float MaxClaimDistance = 26.0f;
        public const float ClaimMagnitude = 1.0f, SecondMagnitude = 0.85f;
        /// <summary>He waits under the ball (does not run on) once he is this close to the point (metres); he is not "jumping" before the ball is there: contact only happens in the head window.</summary>
        public const float StandRadius = 0.5f;
        /// <summary>The second ball: the next best placed team-mates, within this distance of the point, stand this far from it on the side the header will go (his attack direction).</summary>
        public const float SecondBallRadius = 12.0f, SecondBallOffset = 3.5f;
        /// <summary>At most this many team-mates (besides the claimant) take a second-ball place.</summary>
        public const int SecondBallPlayers = 2;
        /// <summary>The volley at goal (REQ-0108, 0146, 0849): the ball is in the air within this distance of his feet (m) and the goal is this close or closer (m).</summary>
        public const float VolleyReach = 1.0f, VolleyShootRange = 14.0f;
    }

    [Serializable]
    public sealed class AerialPlayTuning
    {
        public float HorizonSeconds = AerialPlayDefaults.HorizonSeconds, StepSeconds = AerialPlayDefaults.StepSeconds;
        public float MinApex = AerialPlayDefaults.MinApex;
        public float ContactHeight = AerialPlayDefaults.ContactHeight;
        public float SpeedShare = AerialPlayDefaults.SpeedShare, ReachDistance = AerialPlayDefaults.ReachDistance;
        public float LateSlack = AerialPlayDefaults.LateSlack;
        public float ReceiverHeadStart = AerialPlayDefaults.ReceiverHeadStart;
        public float MaxClaimDistance = AerialPlayDefaults.MaxClaimDistance;
        public float ClaimMagnitude = AerialPlayDefaults.ClaimMagnitude, SecondMagnitude = AerialPlayDefaults.SecondMagnitude;
        public float StandRadius = AerialPlayDefaults.StandRadius;
        public float SecondBallRadius = AerialPlayDefaults.SecondBallRadius, SecondBallOffset = AerialPlayDefaults.SecondBallOffset;
        public int SecondBallPlayers = AerialPlayDefaults.SecondBallPlayers;
        public float VolleyReach = AerialPlayDefaults.VolleyReach, VolleyShootRange = AerialPlayDefaults.VolleyShootRange;
    }

    public enum AerialRole { None = 0, Claim = 1, Second = 2 }

    /// <summary>
    /// Phase 37: the high ball as the players read it. WHERE and WHEN it comes down to head height (the flight is simulated on a copy), WHO of each team gets there first (the nearest in running time, the player the ball is aimed at has a
    /// head start, a keeper does not come in here), and what the others do (the next best placed stand where the header will land: the second ball). Pure: it reads the world and writes nothing; the plan is cached per instant.
    /// </summary>
    public sealed class AerialPlay
    {
        public AerialPlayTuning Tuning = new AerialPlayTuning();

        private readonly Vec3[] path = new Vec3[64];
        private readonly AerialRole[] roles = new AerialRole[64];
        private readonly float[] arrivals = new float[64];
        private float planTime = float.NaN;
        private int planStamp = -1;
        private bool has;
        private Vec2 point;
        private float ballTime;
        private int claim0 = -1, claim1 = -1;

        /// <summary>Where the ball will be at head height, and in how many seconds. False when no high ball is coming down.</summary>
        public bool Point(out Vec2 where, out float seconds) { where = point; seconds = ballTime; return has; }
        public int ClaimantOf(TeamSide team) { return team == TeamSide.Home ? claim0 : claim1; }
        /// <summary>The seconds the best placed field player of a team needs to be under the ball (infinity: nobody comes).</summary>
        public float BestArrival(TeamSide team) { int c = ClaimantOf(team); return c >= 0 ? arrivals[c] : float.PositiveInfinity; }

        /// <summary>Makes (or reuses) the plan for this instant. True when a high ball is coming down and nobody has it.</summary>
        public bool Plan(WorldView view)
        {
            int stamp = view.PlayerCount;
            Vec3 here = view.Ball.Position;
            if (planStamp == stamp && planTime == view.Match.MatchTime && cachedBall.X == here.X && cachedBall.Y == here.Y && cachedBall.Z == here.Z) return has;
            planStamp = stamp; planTime = view.Match.MatchTime; cachedBall = here;
            has = false; claim0 = claim1 = -1;
            for (int i = 0; i < roles.Length; i++) roles[i] = AerialRole.None;
            WorldBall ball = view.Ball;
            AerialPlayTuning t = Tuning;
            if (ball.HasPossessor || ball.Mode == BallMode.Dead || ball.Mode == BallMode.Goal || ball.Mode == BallMode.InShot) return false;
            if (ball.Height <= 0.05f && ball.Velocity.Y <= 0.5f) return false;

            int n = Math.Min((int)(t.HorizonSeconds / t.StepSeconds), path.Length);
            n = view.PredictBallPath(t.StepSeconds, path, n);
            float apex = ball.Position.Y;
            int found = -1;
            float previous = ball.Position.Y;
            for (int k = 0; k < n; k++)
            {
                float y = path[k].Y;
                if (y > apex) apex = y;
                if (found < 0 && y <= t.ContactHeight && y < previous) found = k;
                previous = y;
            }
            if (apex < t.MinApex && ball.Position.Y < t.MinApex) return false;
            if (found < 0)
            {
                // already at or below head height and coming down: the point is here and now
                if (ball.Position.Y <= t.ContactHeight && ball.Velocity.Y < 0f) { point = ball.Ground; ballTime = 0f; }
                else return false;
            }
            else { point = new Vec2(path[found].X, path[found].Z); ballTime = (found + 1) * t.StepSeconds; }

            // who gets there first, team by team
            float best0 = float.PositiveInfinity, best1 = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount && i < arrivals.Length; i++)
            {
                arrivals[i] = float.PositiveInfinity;
                WorldPlayer p = view[i];
                if (!p.IsOnPitch || p.IsGoalkeeper || p.MaxSpeed <= 0f) continue;
                float d = (point - p.Position).Magnitude;
                if (d > t.MaxClaimDistance) continue;
                float a = Math.Max(0f, d - t.ReachDistance) / (p.MaxSpeed * t.SpeedShare);
                if (ball.IntendedReceiverId != null && ball.IntendedReceiverId == p.PlayerId) a -= t.ReceiverHeadStart;
                arrivals[i] = a;
                if (p.Team == TeamSide.Home) { if (a < best0 || (a == best0 && (claim0 < 0 || i < claim0))) { best0 = a; claim0 = i; } }
                else if (a < best1 || (a == best1 && (claim1 < 0 || i < claim1))) { best1 = a; claim1 = i; }
            }
            if (claim0 >= 0) roles[claim0] = AerialRole.Claim;
            if (claim1 >= 0) roles[claim1] = AerialRole.Claim;
            // the next best placed of each team, near enough, stand where the header will land
            for (int side = 0; side < 2; side++)
            {
                TeamSide team = side == 0 ? TeamSide.Home : TeamSide.Away;
                for (int taken = 0; taken < t.SecondBallPlayers; taken++)
                {
                    int pick = -1; float bestA = float.PositiveInfinity;
                    int start = view.TeamStart(team), end = start + view.TeamCount(team);
                    for (int i = start; i < end && i < arrivals.Length; i++)
                    {
                        if (roles[i] != AerialRole.None || float.IsPositiveInfinity(arrivals[i])) continue;
                        if ((view[i].Position - point).Magnitude > t.SecondBallRadius) continue;
                        if (arrivals[i] < bestA) { bestA = arrivals[i]; pick = i; }
                    }
                    if (pick >= 0) roles[pick] = AerialRole.Second;
                }
            }
            has = claim0 >= 0 || claim1 >= 0;
            return has;
        }

        private Vec3 cachedBall;

        public AerialRole RoleOf(int index) { return index >= 0 && index < roles.Length ? roles[index] : AerialRole.None; }

        /// <summary>The seconds he needs to be under the ball (infinity: he does not come).</summary>
        public float ArrivalOf(int index) { return index >= 0 && index < arrivals.Length ? arrivals[index] : float.PositiveInfinity; }

        /// <summary>The place of a second-ball player: beside the point, on the side the header goes (his team's attack direction).</summary>
        public Vec2 SecondPoint(Vec2 attackDirection) { return point + attackDirection * Tuning.SecondBallOffset; }
    }
}
