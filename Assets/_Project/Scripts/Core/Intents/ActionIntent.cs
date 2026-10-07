namespace FS27.Core
{
    /// <summary>
    /// What a player (a person's gesture, or later the AI) wants to DO, as plain data: who, which action, which way, how hard, towards whom, with which skill. It is NOT a second action request:
    /// <see cref="ToRequest"/> turns it into the one <see cref="ActionRequest"/> and that goes through <see cref="MatchController.Perform"/> and the same resolver as every other action.
    /// </summary>
    public struct ActionIntent
    {
        public TeamSide Team;
        public string PlayerId;
        public FootballActionKind Kind;
        public Vec2 Direction;
        /// <summary>0..1; 0 = the default power.</summary>
        public float Power;
        /// <summary>-1..1: the curve of a curved shot.</summary>
        public float Curve;
        public PassTarget Target;
        /// <summary>The catalogue skill (a style of skill move or lob pass) to use; null = the best he owns.</summary>
        public string SkillId;
        /// <summary>Goalkeeper distribution: a long kick instead of a throw.</summary>
        public bool Long;
        /// <summary>Who produced it (context only: it changes nothing about how the action resolves).</summary>
        public ControlSource Source;

        public ActionIntent(TeamSide team, string playerId, FootballActionKind kind, Vec2 direction = default(Vec2), float power = 0f, ControlSource source = ControlSource.Ai)
        {
            Team = team; PlayerId = playerId; Kind = kind; Direction = direction; Power = power; Curve = 0f; Target = default(PassTarget); SkillId = null; Long = false; Source = source;
        }

        /// <summary>The request the Core resolves. A power of 0 means the default power.</summary>
        public ActionRequest ToRequest()
        {
            var r = new ActionRequest(Team, PlayerId, Kind, Direction, Power > 0f ? Power : ActionDefaults.DefaultPower)
            {
                Curve = Curve, SkillId = SkillId, Long = Long, Target = Target, Source = Source
            };
            return r;
        }
    }
}
