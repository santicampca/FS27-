using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>A player of a match squad: an id and what the match needs to know about it (is it a goalkeeper, how big is the stamina tank).</summary>
    public struct SquadMember
    {
        public string PlayerId;
        public bool IsGoalkeeper;
        /// <summary>Stamina tank (seconds of full sprint), as <see cref="PlayerRuntimeState.StaminaCapacity"/>. 0 = not set yet.</summary>
        public float StaminaCapacity;

        public SquadMember(string playerId, bool isGoalkeeper, float staminaCapacity = 0f)
        {
            PlayerId = playerId;
            IsGoalkeeper = isGoalkeeper;
            StaminaCapacity = staminaCapacity;
        }
    }

    /// <summary>The 10 players a team brings: 6 starters (1 goalkeeper + 5 field players) and 4 substitutes (at least 1 goalkeeper).</summary>
    public sealed class MatchSquad
    {
        public readonly SquadMember[] Starters;
        public readonly SquadMember[] Bench;

        public MatchSquad(IEnumerable<SquadMember> starters, IEnumerable<SquadMember> bench)
        {
            Starters = new List<SquadMember>(starters ?? new SquadMember[0]).ToArray();
            Bench = new List<SquadMember>(bench ?? new SquadMember[0]).ToArray();
        }

        /// <summary>6 starters with exactly 1 goalkeeper, 4 substitutes with at least 1 goalkeeper, no repeated or empty id.</summary>
        public bool IsValid(out string problem)
        {
            problem = "";
            if (Starters.Length != MatchDefaults.StartersPerTeam) problem = "A squad has " + MatchDefaults.StartersPerTeam + " starters.";
            else if (Bench.Length != MatchDefaults.BenchPerTeam) problem = "A squad has " + MatchDefaults.BenchPerTeam + " substitutes.";
            else
            {
                int starterKeepers = 0, benchKeepers = 0;
                var seen = new HashSet<string>();
                foreach (SquadMember m in Starters) { if (!Check(m, seen, ref problem)) return false; if (m.IsGoalkeeper) starterKeepers++; }
                foreach (SquadMember m in Bench) { if (!Check(m, seen, ref problem)) return false; if (m.IsGoalkeeper) benchKeepers++; }
                if (starterKeepers != 1) problem = "The starters need exactly 1 goalkeeper.";
                else if (benchKeepers < 1) problem = "The bench needs at least 1 goalkeeper.";
            }
            return problem.Length == 0;
        }

        private static bool Check(SquadMember m, HashSet<string> seen, ref string problem)
        {
            if (string.IsNullOrEmpty(m.PlayerId)) { problem = "A squad member has no id."; return false; }
            if (!seen.Add(m.PlayerId)) { problem = "Player '" + m.PlayerId + "' appears twice."; return false; }
            return true;
        }
    }

    public enum MatchPlayerStatus
    {
        OnPitch = 0,
        Bench,
        /// <summary>Was substituted off. Cannot return.</summary>
        SubstitutedOff,
        /// <summary>Sent off (red card or second yellow). Cannot return and is never replaced.</summary>
        Dismissed
    }

    /// <summary>One player in the match: who, where he stands in the squad, his discipline and his runtime state. The attributes stay in the player's definition.</summary>
    public sealed class MatchPlayer
    {
        public string PlayerId { get; }
        public TeamSide Team { get; }
        public bool IsGoalkeeper { get; }
        public MatchPlayerStatus Status { get; internal set; }
        public int YellowCards { get; internal set; }
        /// <summary>Phase 21: red cards he has been shown in this match (a second yellow counts as a red: he is sent off).</summary>
        public int RedCards { get; internal set; }
        /// <summary>Phase 21: fouls the referee has called against him in this match.</summary>
        public int FoulsCommitted { get; internal set; }
        /// <summary>This player currently stands in goal (the real keeper, or a field player who took the gloves).</summary>
        public bool InGoal { get; internal set; }
        /// <summary>A field player in goal. His keeper capabilities are derived at runtime (<see cref="EmergencyKeeperProfile"/>), never stored.</summary>
        public bool IsEmergencyKeeper { get; internal set; }
        public PlayerRuntimeState Runtime { get; }
        /// <summary>Phase 7: he was one of the starters (not a substitute who came on).</summary>
        public bool StartedMatch { get; internal set; }
        /// <summary>Phase 7: his role, read from his definition when the match is created (Goalkeeper for a goalkeeper). The attributes stay in the definition.</summary>
        public PlayerRole Role { get; internal set; } = PlayerRole.Midfielder;
        /// <summary>Phase 7: the priority of the movement intent already applied in this step (internal arbitration).</summary>
        internal int IntentPriority = int.MinValue;
        /// <summary>Phase 8: where his formation puts him when play is on (a derived cache, see <see cref="MatchController.RefreshHomePositions"/>).</summary>
        internal Vec2 Home;
        internal bool HasHome;

        /// <summary>
        /// The Phase-3 movement state (position, velocity, facing, speed, stamina 0..100). Part of the authoritative match state: only the engine steps it.
        /// A goalkeeper uses exactly the same locomotion as everybody else.
        /// </summary>
        public LocomotionState Locomotion;
        /// <summary>His movement abilities, resolved from his attributes when the match is created.</summary>
        public LocomotionProfile LocomotionProfile { get; internal set; }
        /// <summary>His action abilities, resolved from his attributes when the match is created.</summary>
        public ActionProfile ActionProfile { get; internal set; }
        /// <summary>Seconds he cannot start another action nor steer (a slide, a lost tackle).</summary>
        public float ActionLockRemaining { get; internal set; }
        /// <summary>Seconds before he may kick again.</summary>
        public float KickCooldownRemaining { get; internal set; }
        public float PressureRemaining { get; internal set; }
        public float ShieldRemaining { get; internal set; }
        /// <summary>A goalkeeper rush that was accepted and is still active (contract only: Phase 5 moves the keeper).</summary>
        public float RushRemaining { get; internal set; }
        public float SlideRemaining { get; internal set; }
        /// <summary>His skills as they are active in this match (Phase 6): derived from his skill set and attributes, never stored on the player.</summary>
        public ResolvedSkills Skills { get; internal set; } = ResolvedSkills.None;
        /// <summary>The movement modifiers of his skills (zero = none).</summary>
        internal LocomotionModifiers LocomotionModifiers;
        /// <summary>Seconds a skill move still protects the carrier (he has just beaten, or half beaten, his man).</summary>
        public float EvadeRemaining { get; internal set; }
        /// <summary>0..: how strong that protection is (style and quality of the move).</summary>
        public float EvadeStrength { get; internal set; }
        /// <summary>Seconds before he may try another skill move.</summary>
        public float SkillMoveCooldownRemaining { get; internal set; }
        public bool IsEvading => EvadeRemaining > 0f;
        internal Vec2 SlideDirection;
        public bool IsSliding => SlideRemaining > 0f;
        public bool IsPressing => PressureRemaining > 0f;
        public bool IsShielding => ShieldRemaining > 0f;
        /// <summary>The joystick the engine will apply on the next steps (set through <see cref="MatchController.SetPlayerInput"/>).</summary>
        internal Vec2 PendingInput;

        internal MatchPlayer(SquadMember m, TeamSide team, MatchPlayerStatus status)
        {
            Locomotion = LocomotionState.Create(Vec2.Zero);
            PlayerId = m.PlayerId;
            Team = team;
            IsGoalkeeper = m.IsGoalkeeper;
            if (m.IsGoalkeeper) Role = PlayerRole.Goalkeeper;
            Status = status;
            Runtime = new PlayerRuntimeState();
            if (m.StaminaCapacity > 0f) Runtime.Reset(new PlayerStats { StaminaCapacity = m.StaminaCapacity }, 0f);
        }

        public bool IsActive => Status == MatchPlayerStatus.OnPitch;
    }

    /// <summary>The mutable state of one team in the match: its players, substitutions used, and what is pending with the goalkeeper.</summary>
    public sealed class TeamMatchState
    {
        private readonly List<MatchPlayer> players = new List<MatchPlayer>();

        public TeamSide Side { get; }
        public IReadOnlyList<MatchPlayer> Players => players;
        public bool HasSquad => players.Count > 0;
        /// <summary>Substitutions made so far. Never above the match limit.</summary>
        public int SubstitutionsUsed { get; internal set; }
        /// <summary>Phase 21: fouls called against this team in this match.</summary>
        public int Fouls { get; internal set; }
        /// <summary>The keeper was sent off and nobody stands in goal yet: the next restart cannot happen until this is resolved.</summary>
        public bool NeedsGoalkeeper { get; internal set; }

        internal TeamMatchState(TeamSide side, MatchSquad squad)
        {
            Side = side;
            if (squad == null) return;
            foreach (SquadMember m in squad.Starters)
            {
                var p = new MatchPlayer(m, side, MatchPlayerStatus.OnPitch);
                p.InGoal = m.IsGoalkeeper;
                p.StartedMatch = true;
                players.Add(p);
            }
            foreach (SquadMember m in squad.Bench) players.Add(new MatchPlayer(m, side, MatchPlayerStatus.Bench));
        }

        public MatchPlayer Find(string playerId)
        {
            for (int i = 0; i < players.Count; i++) if (players[i].PlayerId == playerId) return players[i];
            return null;
        }

        public int ActivePlayerCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < players.Count; i++) if (players[i].IsActive) n++;
                return n;
            }
        }

        public MatchPlayer GoalkeeperInGoal
        {
            get
            {
                for (int i = 0; i < players.Count; i++) if (players[i].IsActive && players[i].InGoal) return players[i];
                return null;
            }
        }

        /// <summary>Phase 7: the goal slot of this team (starter, bench keeper, occupant and how he got there), derived from the squad.</summary>
        public GoalkeeperSlot GoalkeeperSlot
        {
            get
            {
                var slot = new GoalkeeperSlot { Team = Side, NeedsGoalkeeper = NeedsGoalkeeper };
                MatchPlayer bench = AvailableBenchKeeper;
                slot.BenchKeeperId = bench != null ? bench.PlayerId : null;
                for (int i = 0; i < players.Count; i++) if (players[i].StartedMatch && players[i].IsGoalkeeper) { slot.StarterId = players[i].PlayerId; break; }
                MatchPlayer occupant = GoalkeeperInGoal;
                if (occupant == null) return slot;
                slot.OccupantId = occupant.PlayerId;
                slot.Mode = occupant.IsEmergencyKeeper ? GoalkeeperSlotMode.Emergency : (occupant.StartedMatch ? GoalkeeperSlotMode.Starter : GoalkeeperSlotMode.Substitute);
                return slot;
            }
        }

        /// <summary>A goalkeeper on the bench who has not played yet (can still come on).</summary>
        public MatchPlayer AvailableBenchKeeper
        {
            get
            {
                for (int i = 0; i < players.Count; i++) if (players[i].Status == MatchPlayerStatus.Bench && players[i].IsGoalkeeper) return players[i];
                return null;
            }
        }
    }
}
