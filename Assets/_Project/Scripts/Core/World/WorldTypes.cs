using System;

namespace FS27.Core
{
    /// <summary>Whether a player can be used right now.</summary>
    public enum PlayerAvailability
    {
        OnPitch = 0,
        Bench,
        SubstitutedOff,
        Dismissed
    }

    /// <summary>The match as the AI needs to see it (no rules logic, no history).</summary>
    public struct WorldMatch
    {
        public GameMode Mode;
        public CompetitionMatchType Competition;
        public MatchPhase Phase;
        public MatchPeriod Period;
        /// <summary>Seconds played in total (regulation + extra time). Core time, never Unity time.</summary>
        public float MatchTime;
        public float PeriodElapsed;
        public float PeriodRemaining;
        public int HomeGoals;
        public int AwayGoals;
        public bool HomeAttacksPositiveX;
        /// <summary>The ball is live (the match is in the Playing phase).</summary>
        public bool BallLive;
        public bool HasPossession;
        public TeamSide PossessingTeam;
        public bool RestartPending;
        public RestartKind RestartKind;
        public TeamSide RestartTeam;
        public string RestartTakerId;
        public RestartStatus RestartStatus;
        public Vec2 RestartPosition;
        /// <summary>Phase 22: the taker of the free kick that has just been whistled and not yet played (null otherwise). He must play it (shoot or pass), not carry it.</summary>
        public string FreeKickTakerId;
        /// <summary>Phase 23: the whistle of a penalty has gone and nobody has kicked yet: nobody decides anything (the taker is told by the Core).</summary>
        public bool PenaltyHold;
        /// <summary>Phase 23: the goalkeeper who is diving on a penalty (the Core moves him until he touches the ball); the AI leaves him alone. Null otherwise.</summary>
        public string PenaltyKeeperId;
    }

    /// <summary>The ball as the AI needs to see it.</summary>
    public struct WorldBall
    {
        public Vec3 Position;
        public Vec3 Velocity;
        /// <summary>The ball on the pitch plane (field space).</summary>
        public Vec2 Ground;
        /// <summary>Horizontal speed (m/s).</summary>
        public float Speed;
        /// <summary>Unit direction of the horizontal movement; zero when the ball is (almost) still.</summary>
        public Vec2 Direction;
        /// <summary>Height of the ball's lowest point above the ground.</summary>
        public float Height;
        public BallMode Mode;
        public bool IsGrounded;
        public bool HasPossessor;
        public TeamSide PossessingTeam;
        public string PossessorId;
        public bool HasLastTouch;
        public TeamSide LastTouchTeam;
        public string LastTouchPlayerId;
        /// <summary>The team-mate a pass in the air is aimed at, or null.</summary>
        public string IntendedReceiverId;
        /// <summary>Phase 9: seconds since the possession last changed team.</summary>
        public float SecondsSincePossessionChange;
        /// <summary>Phase 16: seconds since anybody last touched the ball (a shot just struck reads zero).</summary>
        public float SecondsSinceTouch;
    }

    /// <summary>
    /// One player as the AI needs to see him: where he is and how he moves, what he is doing, whether he can act, what he is (role, zone, keeper), what he can do (his action qualities and skills) and
    /// whether a person controls him. A derived COPY: it is never the player's definition (no attributes are copied) and writing to it changes nothing in the match.
    /// </summary>
    public struct WorldPlayer
    {
        public string PlayerId;
        public TeamSide Team;
        /// <summary>His index in his team's squad list (starters first, then the bench).</summary>
        public int Slot;
        public Vec2 Position;
        /// <summary>Phase 8: where his team's formation puts him in open play (turned the way his team attacks now). Only meaningful when <see cref="HasHome"/>.</summary>
        public Vec2 Home;
        public bool HasHome;
        public Vec2 Velocity;
        /// <summary>Unit vector he faces.</summary>
        public Vec2 Facing;
        /// <summary>Radians (the locomotion heading).</summary>
        public float Heading;
        public float Speed;
        /// <summary>His top speed with full stamina (from his Speed attribute).</summary>
        public float MaxSpeed;
        /// <summary>0..100.</summary>
        public float Stamina;
        public PlayerRole Role;
        /// <summary>The zone of the pitch he is standing in now (from his own goal line).</summary>
        public PitchZone Zone;
        public PlayerAvailability Availability;
        /// <summary>He stands in goal (the real keeper, a substitute keeper or an emergency keeper).</summary>
        public bool IsGoalkeeper;
        public bool IsEmergencyKeeper;
        public bool HasBall;
        /// <summary>Phase 21: the yellow cards he has in this match, and the fouls the referee has called against him. The AI reads them to play with care.</summary>
        public int YellowCards;
        public int FoulsCommitted;
        /// <summary>A person controls him.</summary>
        public bool IsControlled;
        // ---- what he is doing
        public bool ActionLocked;
        public bool KickCooldown;
        public bool IsSliding;
        public bool IsPressing;
        public bool IsShielding;
        public bool IsEvading;
        // ---- what he can do
        public PreferredFoot PreferredFoot;
        /// <summary>The qualities his actions are made of (derived from his attributes once, when the match is created).</summary>
        public ActionProfile Profile;
        /// <summary>His skills as they are active in this match (requirements met; the rest is listed as ignored). Read-only.</summary>
        public ResolvedSkills Skills;
        /// <summary>Phase 10: his roles with their affinity (0..100), most defining first; null when the match was given no playing profiles. Read-only (shared with the profile).</summary>
        public System.Collections.Generic.IReadOnlyList<RoleAffinity> Roles;

        /// <summary>His affinity (0..100) with a role; 0 when it is not one of his roles.</summary>
        public int AffinityOf(PlayerArchetype role)
        {
            if (Roles == null) return 0;
            for (int i = 0; i < Roles.Count; i++) if (Roles[i].Role == role) return Roles[i].Affinity;
            return 0;
        }

        public bool IsOnPitch { get { return Availability == PlayerAvailability.OnPitch; } }
        /// <summary>On the pitch and free to start an action.</summary>
        public bool CanAct { get { return Availability == PlayerAvailability.OnPitch && !ActionLocked; } }
    }
}
