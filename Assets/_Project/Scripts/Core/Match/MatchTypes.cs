using System;

namespace FS27.Core
{
    public enum TeamSide
    {
        Home = 0,
        Away = 1
    }

    public static class TeamSideExtensions
    {
        public static TeamSide Opponent(this TeamSide side)
        {
            return side == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        }
    }

    public enum MatchPhase
    {
        NotStarted,
        /// <summary>Waiting for the presentation to place players and ball for a kick-off.</summary>
        WaitingForKickoff,
        Playing,
        /// <summary>A goal was just scored: celebration time before the next kick-off.</summary>
        GoalCelebration,
        /// <summary>Ball left the pitch: waiting for the presentation to set up a throw-in / corner / goal kick.</summary>
        WaitingForRestart,
        Paused,
        Finished,
        /// <summary>The interval between two timed periods (half-time, extra-time break). Only modes with a break use it. The clock is stopped.</summary>
        Break,
        /// <summary>The penalty shoot-out. The match clock is not used; no ball is judged.</summary>
        Shootout
    }

    public enum RestartKind
    {
        KickOff,
        ThrowIn,
        GoalKick,
        Corner
    }

    public enum BallOutcome
    {
        InPlay,
        Goal,
        OutOfBounds
    }

    /// <summary>The ball as the rules see it. Filled by the game from the real ball every tick.</summary>
    public struct BallObservation
    {
        public Vec3 Position;
        public float Radius;

        public BallObservation(Vec3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>What the rules say about the ball right now.</summary>
    public struct BallJudgement
    {
        public BallOutcome Outcome;
        public TeamSide ScoringTeam;
        public RestartKind Restart;
        public TeamSide RestartTeam;
        /// <summary>Where the restart is taken, in field space (X length, Y width).</summary>
        public Vec2 RestartPosition;
    }

    /// <summary>"Please set up this restart": raised by the controller, handled by the presentation.</summary>
    public struct RestartRequest
    {
        public RestartKind Kind;
        public TeamSide Team;
        public Vec2 Position;

        public RestartRequest(RestartKind kind, TeamSide team, Vec2 position)
        {
            Kind = kind;
            Team = team;
            Position = position;
        }
    }

    public struct MatchResult
    {
        public int HomeGoals;
        public int AwayGoals;
        public bool IsDraw;
        /// <summary>Only meaningful when <see cref="IsDraw"/> is false.</summary>
        public TeamSide Winner;
        /// <summary>How the match ended. Default (0) = regulation time, so results built by older code keep their meaning.</summary>
        public MatchTerminationReason Reason;
        /// <summary>Shoot-out goals (only when <see cref="Reason"/> is PenaltyShootout). The game score never includes them.</summary>
        public int ShootoutHome;
        public int ShootoutAway;
    }

    /// <summary>Pitch geometry in field space: centre at (0,0), X = length, Y (world Z) = width.</summary>
    [Serializable]
    public class FieldDimensions
    {
        public float Length = 40f;
        public float Width = 25f;
        public float GoalWidth = 5f;
        /// <summary>Crossbar height.</summary>
        public float GoalHeight = 2f;
        /// <summary>Corner kicks are taken this far inside the corner.</summary>
        public float CornerInset = 0.5f;
        /// <summary>Goal kicks are taken this far in front of the end line.</summary>
        public float GoalKickDistance = 2f;
        /// <summary>Throw-ins keep at least this distance from the end lines.</summary>
        public float ThrowInEndMargin = 1f;
    }

    /// <summary>
    /// ALL the configuration of a match, in one place: nothing in the engine hard-codes a duration, a recovery or a limit.
    /// Configuration only: the mutable match lives in <see cref="MatchState"/>. A plain <c>new MatchSettings()</c> keeps the original single-period
    /// behaviour (one period of <see cref="DurationSeconds"/>, Classic rules, no competition); use <see cref="CreateArena"/> / <see cref="CreateClassic"/>
    /// for the architecture defaults of each mode.
    /// </summary>
    [Serializable]
    public class MatchSettings
    {
        /// <summary>Largest allowed <see cref="SubstitutionLimit"/>: the architecture's hard cap.</summary>
        public const int MaxSubstitutionsAllowed = MatchDefaults.MaxSubstitutions;

        // ---- how it is played / how the result is resolved (independent concepts) ----
        public GameMode Mode = GameMode.Classic;
        public CompetitionMatchType CompetitionType = CompetitionMatchType.Normal;

        // ---- regulation time ----
        /// <summary>Total regulation seconds. Used as the whole match when <see cref="PeriodSeconds"/> is 0.</summary>
        public float DurationSeconds = 180f;
        /// <summary>1 (a single period) or 2 (two halves).</summary>
        public int RegulationPeriods = 1;
        /// <summary>Length of each regulation period. 0 = <see cref="DurationSeconds"/> divided by <see cref="RegulationPeriods"/>.</summary>
        public float PeriodSeconds;

        // ---- interval between periods ----
        /// <summary>How long the half-time / extra-time break lasts, in Core time (only modes with a break phase use it; Arena has none).</summary>
        public float HalftimeSeconds = MatchDefaults.ClassicHalftimeSeconds;
        /// <summary>Swap the attacking directions at the interval between two periods of the same stage (half-time / extra-time break).</summary>
        public bool SwitchSidesAtBreak = true;
        /// <summary>Fraction (0..1) of maximum stamina recovered during the Classic half-time.</summary>
        public float HalfTimeStaminaRecovery = MatchDefaults.ClassicHalfTimeRecovery;
        /// <summary>Fraction (0..1) of maximum stamina recovered at the Arena side switch.</summary>
        public float SideSwitchStaminaRecovery = MatchDefaults.ArenaSideSwitchRecovery;
        /// <summary>Recovery at the extra-time break: the same as the mode's own interval (<see cref="CreateArena"/> / <see cref="CreateClassic"/> set it).</summary>
        public float ExtraTimeStaminaRecovery = MatchDefaults.ClassicExtraTimeRecovery;
        /// <summary>Recovery between the end of regulation and the start of extra time: the same as the mode's own interval.</summary>
        public float PreExtraTimeStaminaRecovery = MatchDefaults.ClassicExtraTimeRecovery;

        // ---- extra time and shoot-out (only used when CompetitionType is Knockout) ----
        public bool ExtraTimeEnabled = true;
        /// <summary>Length of each of the two extra-time periods: a third of a regulation period (the factories set it per mode).</summary>
        public float ExtraTimePeriodSeconds = MatchDefaults.ClassicExtraTimePeriodSeconds;
        public bool ShootoutEnabled = true;
        /// <summary>Kicks per team before sudden death (3). The ONLY place the number is read from; the shoot-out state takes it as a parameter.</summary>
        public int ShootoutKicksPerTeam = MatchDefaults.ShootoutKicksPerTeam;
        public bool ShootoutSuddenDeath = true;
        public TeamSide ShootoutFirstTeam = TeamSide.Home;

        // ---- squads, substitutions, minimum players ----
        /// <summary>Substitutions per team and match. Never above <see cref="MaxSubstitutionsAllowed"/>.</summary>
        public int SubstitutionLimit = MatchDefaults.MaxSubstitutions;
        /// <summary>A team with fewer active players than this ends the match (technical victory for the rival).</summary>
        public int MinimumActivePlayers = MatchDefaults.MinimumActivePlayers;

        // ---- Arena boundary ----
        /// <summary>Fraction of the normal speed the ball keeps after hitting an Arena wall.</summary>
        public float ArenaWallRestitution = MatchDefaults.ArenaWallRestitution;
        /// <summary>Tangential friction of an Arena wall.</summary>
        public float ArenaWallFriction = MatchDefaults.ArenaWallFriction;

        // ---- simulation ----
        /// <summary>Fixed simulation step (seconds). The Core never uses a variable step.</summary>
        public float SimulationStepSeconds = MatchDefaults.SimulationStepSeconds;

        // ---- existing fields ----
        public float GoalCelebrationSeconds = 3f;
        public TeamSide KickoffTeam = TeamSide.Home;
        /// <summary>If true Home attacks the +X end (and defends -X) at the start of the match.</summary>
        public bool HomeAttacksPositiveX = true;

        /// <summary>Length of one regulation period, in seconds.</summary>
        public float RegulationPeriodSeconds
        {
            get
            {
                if (PeriodSeconds > 0f) return PeriodSeconds;
                return DurationSeconds / Math.Max(1, RegulationPeriods);
            }
        }

        /// <summary>Substitutions actually allowed (the limit, capped by the architecture's maximum).</summary>
        public int EffectiveSubstitutionLimit => Math.Max(0, Math.Min(SubstitutionLimit, MaxSubstitutionsAllowed));

        /// <summary>The team that attacks the end line on the given side (+1 = +X end, -1 = -X end), at the START of the match.</summary>
        public TeamSide AttackerAtEnd(int endSign)
        {
            bool homeAttacksThatEnd = HomeAttacksPositiveX == (endSign > 0);
            return homeAttacksThatEnd ? TeamSide.Home : TeamSide.Away;
        }

        /// <summary>Architecture defaults for Arena: 2 x 6 minutes, 10 % stamina at the side switch, no half-time.</summary>
        public static MatchSettings CreateArena(CompetitionMatchType competition = CompetitionMatchType.Normal)
        {
            return new MatchSettings
            {
                Mode = GameMode.Arena,
                CompetitionType = competition,
                RegulationPeriods = 2,
                PeriodSeconds = MatchDefaults.ArenaPeriodSeconds,
                DurationSeconds = 2f * MatchDefaults.ArenaPeriodSeconds,
                HalftimeSeconds = 0f,
                ExtraTimePeriodSeconds = MatchDefaults.ArenaExtraTimePeriodSeconds,
                ExtraTimeStaminaRecovery = MatchDefaults.ArenaExtraTimeRecovery,
                PreExtraTimeStaminaRecovery = MatchDefaults.ArenaExtraTimeRecovery
            };
        }

        /// <summary>Architecture defaults for Classic: 2 x 8 minutes, 25 % stamina at half-time.</summary>
        public static MatchSettings CreateClassic(CompetitionMatchType competition = CompetitionMatchType.Normal)
        {
            return new MatchSettings
            {
                Mode = GameMode.Classic,
                CompetitionType = competition,
                RegulationPeriods = 2,
                PeriodSeconds = MatchDefaults.ClassicPeriodSeconds,
                DurationSeconds = 2f * MatchDefaults.ClassicPeriodSeconds,
                HalftimeSeconds = MatchDefaults.ClassicHalftimeSeconds,
                ExtraTimePeriodSeconds = MatchDefaults.ClassicExtraTimePeriodSeconds,
                ExtraTimeStaminaRecovery = MatchDefaults.ClassicExtraTimeRecovery,
                PreExtraTimeStaminaRecovery = MatchDefaults.ClassicExtraTimeRecovery
            };
        }

        /// <summary>Checks the configuration. Returns false and says why instead of failing later in the middle of a match.</summary>
        public bool IsValid(out string problem)
        {
            problem = "";
            if (RegulationPeriods < 1 || RegulationPeriods > 2) problem = "RegulationPeriods must be 1 or 2.";
            else if (!(RegulationPeriodSeconds > 0f)) problem = "The regulation period length must be positive.";
            else if (!(ExtraTimePeriodSeconds > 0f)) problem = "ExtraTimePeriodSeconds must be positive.";
            else if (HalftimeSeconds < 0f) problem = "HalftimeSeconds cannot be negative.";
            else if (SubstitutionLimit < 0 || SubstitutionLimit > MaxSubstitutionsAllowed) problem = "SubstitutionLimit must be between 0 and " + MaxSubstitutionsAllowed + ".";
            else if (MinimumActivePlayers < 1) problem = "MinimumActivePlayers must be at least 1.";
            else if (ShootoutKicksPerTeam < 1) problem = "ShootoutKicksPerTeam must be at least 1.";
            else if (!Fraction(HalfTimeStaminaRecovery) || !Fraction(SideSwitchStaminaRecovery) || !Fraction(ExtraTimeStaminaRecovery) || !Fraction(PreExtraTimeStaminaRecovery)) problem = "Stamina recovery is a fraction between 0 and 1.";
            else if (!Fraction(ArenaWallRestitution) || !Fraction(ArenaWallFriction)) problem = "Arena wall restitution and friction are fractions between 0 and 1.";
            else if (!(SimulationStepSeconds > 0f)) problem = "SimulationStepSeconds must be positive.";
            return problem.Length == 0;
        }

        private static bool Fraction(float f) { return f >= 0f && f <= 1f; }
    }

    public sealed class MatchScore
    {
        public int Home { get; private set; }
        public int Away { get; private set; }

        public int Get(TeamSide side)
        {
            return side == TeamSide.Home ? Home : Away;
        }

        public void Add(TeamSide side)
        {
            if (side == TeamSide.Home) Home++;
            else Away++;
        }
    }

    public sealed class MatchClock
    {
        public float Duration { get; set; }
        public float Elapsed { get; private set; }
        public float Remaining => Math.Max(0f, Duration - Elapsed);
        public bool IsFinished => Elapsed >= Duration;

        public MatchClock(float duration)
        {
            Duration = duration;
        }

        public void Advance(float dt)
        {
            if (dt <= 0f) return;
            Elapsed = Math.Min(Duration, Elapsed + dt);
        }

        /// <summary>Starts a new period: new length, nothing elapsed.</summary>
        public void Reset(float duration)
        {
            Duration = duration;
            Elapsed = 0f;
        }
    }
}
