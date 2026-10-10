namespace FS27.Core
{
    /// <summary>Where a goal came from (Phase 17), judged only from the events the match published. <see cref="Unknown"/> is kept for every goal the evidence does not settle.</summary>
    public enum GoalOrigin
    {
        /// <summary>The goal came from a shot of the scoring team: directly (no touch in between) or after the opposing goalkeeper touched it on or behind his line (inferred).</summary>
        Shot = 0,
        /// <summary>A shot of the scoring team was stopped or knocked away and a player of the scoring team touched the ball again before it went in.</summary>
        Rebound,
        /// <summary>A shot of the scoring team was knocked on (a block, a touch that did not control it) and went in without a new touch of the scoring team.</summary>
        Deflection,
        /// <summary>A cross of the scoring team went in or was finished with nothing else in between. (The Core has no cross action yet: the category waits for it and stays empty until then.)</summary>
        Cross,
        /// <summary>Not settled by the evidence; <see cref="GoalOriginRecord.PreviousKind"/> and <see cref="GoalOriginRecord.PreviousDetail"/> say what came before.</summary>
        Unknown
    }

    /// <summary>How sure the origin of a goal is.</summary>
    public enum GoalEvidence
    {
        /// <summary>The events leave no other reading (the last kick was the shot and nothing touched the ball after it).</summary>
        Direct = 0,
        /// <summary>The origin is read from a chain of events (a shot, a stop and a new touch): likely, not stated by the Core.</summary>
        Inferred,
        /// <summary>No origin could be read.</summary>
        None
    }

    /// <summary>One goal with the origin read from the events (statistics only).</summary>
    public struct GoalOriginRecord
    {
        public string MatchId;
        public float MatchTime;
        public TeamSide Team;
        public string ScorerId;
        public bool OwnGoal;
        public GoalOrigin Origin;
        public GoalEvidence Evidence;
        /// <summary>The event that came just before the goal (the last one published about the ball) and its qualifier.</summary>
        public GameplayEventKind PreviousKind;
        public string PreviousDetail;
        /// <summary>The match time of the shot the goal is read from (-1 when none) and whether that shot is in the shot log of this observer.</summary>
        public float ShotTime;
        public bool ShotLogged;
        /// <summary>The last kick (shot, pass or clearance) before the goal, of either team: its kind, qualifier, team and how many seconds before the goal. Kind is GoalScored when there was none in the window.</summary>
        public GameplayEventKind LastKickKind;
        public string LastKickDetail;
        public TeamSide LastKickTeam;
        public float LastKickAge;
        /// <summary>Where the ball was when the goal was given, and when the previous ball event happened (metres; Y is the height).</summary>
        public Vec3 GoalPosition, PreviousPosition;
    }

    /// <summary>The last events about the ball, kept in a ring, from which <see cref="AiDiagnostics"/> reads the origin of a goal. Reads the bus only; nothing here reaches the match.</summary>
    public sealed class GoalOriginTracker
    {
        public struct Entry { public GameplayEventKind Kind; public TeamSide Team; public bool HasTeam; public string PlayerId; public string Detail; public float Time; public Vec3 Position; }

        /// <summary>The qualifier the Core gives to an interception that was a block.</summary>
        public const string BlockedDetail = "Blocked";

        private readonly Entry[] ring;
        private int count, head;

        public GoalOriginTracker(int capacity) { ring = new Entry[capacity]; }

        public void Clear() { count = 0; head = 0; }

        /// <summary>The events that say how the ball moved; the rest (whistles, cards, period changes) are not kept.</summary>
        public static bool Relevant(GameplayEventKind kind)
        {
            return kind == GameplayEventKind.ShotPerformed || kind == GameplayEventKind.PassPerformed || kind == GameplayEventKind.ClearancePerformed
                || kind == GameplayEventKind.BallDeflected || kind == GameplayEventKind.BallTouched || kind == GameplayEventKind.BallBouncedOffWall
                || kind == GameplayEventKind.InterceptionPerformed || kind == GameplayEventKind.TacklePerformed;
        }

        public void Add(in GameplayEvent e)
        {
            if (!Relevant(e.Kind)) return;
            ring[head] = new Entry { Kind = e.Kind, Team = e.Team, HasTeam = e.HasTeam, PlayerId = e.PlayerId, Detail = e.Detail, Time = e.MatchTime, Position = e.Position };
            head = (head + 1) % ring.Length;
            if (count < ring.Length) count++;
        }

        /// <summary>The i-th most recent entry (0 = the latest).</summary>
        public Entry Recent(int i) { return ring[(head - 1 - i + ring.Length * 2) % ring.Length]; }
        public int Count { get { return count; } }

        /// <summary>Reads the origin of a goal of <paramref name="team"/> at <paramref name="goalTime"/> from the events within <paramref name="window"/> seconds. The own-goal flag is only recorded: what counts is the chain of events.</summary>
        public GoalOriginRecord Read(TeamSide team, string scorer, bool ownGoal, float goalTime, float window)
        {
            var g = new GoalOriginRecord { Team = team, ScorerId = scorer, OwnGoal = ownGoal, MatchTime = goalTime, Origin = GoalOrigin.Unknown, Evidence = GoalEvidence.None, PreviousKind = GameplayEventKind.GoalScored, PreviousDetail = "", ShotTime = -1f };
            if (count > 0) { Entry last = Recent(0); g.PreviousKind = last.Kind; g.PreviousDetail = last.Detail ?? ""; g.PreviousPosition = last.Position; }

            g.LastKickKind = GameplayEventKind.GoalScored; g.LastKickDetail = ""; g.LastKickAge = -1f;
            for (int i = 0; i < count; i++)
            {
                Entry k = Recent(i);
                if (goalTime - k.Time > window) break;
                if (k.Kind == GameplayEventKind.ShotPerformed || k.Kind == GameplayEventKind.PassPerformed || k.Kind == GameplayEventKind.ClearancePerformed)
                { g.LastKickKind = k.Kind; g.LastKickDetail = k.Detail ?? ""; g.LastKickTeam = k.Team; g.LastKickAge = goalTime - k.Time; break; }
            }

            // walk back to the last kick of the scoring team inside the window; remember what happened to the ball after it
            bool touchedAfter = false, deflectedAfter = false, keeperAfter = false;
            for (int i = 0; i < count; i++)
            {
                Entry e = Recent(i);
                if (goalTime - e.Time > window) break;
                bool kick = e.Kind == GameplayEventKind.ShotPerformed || e.Kind == GameplayEventKind.PassPerformed || e.Kind == GameplayEventKind.ClearancePerformed;
                if (kick && e.HasTeam && e.Team == team)
                {
                    if (e.Kind == GameplayEventKind.ShotPerformed)
                    {
                        g.ShotTime = e.Time;
                        if (!touchedAfter && !deflectedAfter && !keeperAfter) { g.Origin = GoalOrigin.Shot; g.Evidence = GoalEvidence.Direct; }
                        // the keeper touched the shot on or behind his line and the Core still gave the goal (the last touch is his, so the Core flags it as an own goal): the shot is what scored
                        else if (keeperAfter && !touchedAfter && !deflectedAfter) { g.Origin = GoalOrigin.Shot; g.Evidence = GoalEvidence.Inferred; }
                        else if ((deflectedAfter || keeperAfter) && touchedAfter) { g.Origin = GoalOrigin.Rebound; g.Evidence = GoalEvidence.Inferred; }
                        else if (deflectedAfter && !touchedAfter) { g.Origin = GoalOrigin.Deflection; g.Evidence = GoalEvidence.Direct; }
                    }
                    else if (e.Kind == GameplayEventKind.PassPerformed && e.Detail == FootballActionKind.Cross.ToString() && !touchedAfter && !deflectedAfter)
                    {
                        g.Origin = GoalOrigin.Cross; g.Evidence = GoalEvidence.Direct;
                    }
                    return g;
                }
                // an event after the last kick of the scoring team and before the goal
                if (e.Kind == GameplayEventKind.BallDeflected || (e.Kind == GameplayEventKind.InterceptionPerformed && e.Detail == BlockedDetail)) deflectedAfter = true;
                else if (e.Kind == GameplayEventKind.BallTouched)
                {
                    if (e.Detail == FootballActionKind.GoalkeeperSave.ToString() || e.Detail == FootballActionKind.GoalkeeperCatch.ToString()) keeperAfter = true;
                    else if (e.HasTeam && e.Team == team) touchedAfter = true;
                    else deflectedAfter = true; // a defender of the other team touched the shot on its way in
                }
            }
            return g;
        }
    }
}
