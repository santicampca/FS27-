using System;
using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// The bridge between the Core match and Unity. It owns a <see cref="MatchController"/>, advances it in FIXED steps (so the match never depends on the frame rate),
    /// and makes the ball's rigidbody REFLECT <c>MatchState.Ball</c>. Unity does not simulate the ball here and does not change the authoritative state: a touch comes in through
    /// <see cref="RequestTouch"/>, which is an explicit command to the Core.
    /// Player movement is not part of this phase: players still move as before.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class MatchBridge : MonoBehaviour, IBallCoreLink
    {
        [SerializeField] private BallController ball;
        [Header("Match")]
        [SerializeField] private GameMode mode = GameMode.Arena;
        [SerializeField] private CompetitionMatchType competition = CompetitionMatchType.Normal;
        [SerializeField] private FieldDimensions field = new FieldDimensions();
        [SerializeField] private bool startAutomatically = true;
        [Header("Touches from Unity")]
        [Tooltip("The team a Unity-reported touch is credited to (the sandbox has no teams yet).")]
        [SerializeField] private TeamSide touchTeam = TeamSide.Home;

        private MatchController match;
        private SimulationClock clock;
        private MatchSettings settings;

        /// <summary>The Core match. Read its State; call its API; never copy state out of Unity back into it.</summary>
        public MatchController Match => match;

        /// <summary>Raised for every action the Core answered (requested or automatic touches). Animation, sound and effects listen here; nothing decides anything from it.</summary>
        public event Action<ActionResult> ActionPerformed;

        private void Awake()
        {
            settings = mode == GameMode.Arena ? MatchSettings.CreateArena(competition) : MatchSettings.CreateClassic(competition);
            match = new MatchController(field, settings);
            Hook(match);
            clock = new SimulationClock(settings.SimulationStepSeconds);
            if (ball != null)
            {
                ball.SetCoreDriven(true);
                ball.CoreLink = this;
            }
            if (startAutomatically) StartMatch();
        }

        /// <summary>
        /// Gives the match its two squads (before <see cref="StartMatch"/>): the players then exist in the Core and can move. Each player's movement abilities are read from the lookup.
        /// Turn off "Start Automatically" if squads are going to be configured.
        /// </summary>
        public void Configure(MatchSquad home, MatchSquad away, IPlayerLookup players)
        {
            match = new MatchController(field, settings, home, away, null, "match", null, players);
            Hook(match);
            Present();
        }

        private void Hook(MatchController m)
        {
            m.ActionPerformed += r => ActionPerformed?.Invoke(r);
        }

        /// <summary>Starts the match and takes the opening kick-off (there are no players to place yet).</summary>
        public void StartMatch()
        {
            match.Start();
            // With squads the players still stand at the origin: confirming now would let the Core's automatic touch hand the kick-off ball to whoever is first in the list.
            // The host places the players (MatchController.PlacePlayer) and then calls ConfirmRestartReady.
            if (!HasPlayers) match.ConfirmRestartReady();
            Present();
        }

        private bool HasPlayers => match.State.Home.HasSquad || match.State.Away.HasSquad;

        /// <summary>The players are in place for the pending restart: play begins.</summary>
        public bool ConfirmRestartReady()
        {
            bool started = match.ConfirmRestartReady();
            Present();
            return started;
        }

        private void FixedUpdate()
        {
            // frame time only decides HOW MANY fixed steps are due; every step is the same length
            int steps = clock.Advance(Time.fixedDeltaTime);
            for (int i = 0; i < steps; i++) match.Advance(clock.StepSeconds);
            Present();
        }

        /// <summary>Mirrors the Core ball into the rigidbody. One direction only: Core to Unity.</summary>
        private void Present()
        {
            if (ball == null) return;
            BallState s = match.State.Ball.Physics;
            ball.ApplyCoreState(new Vector3(s.Position.X, s.Position.Y, s.Position.Z), new Vector3(s.Velocity.X, s.Velocity.Y, s.Velocity.Z));
        }

        public void RequestTouch(Vector3 desiredHorizontalVelocity, float strength)
        {
            // When the Core has players, contact with the ball is the Core's own (first touch, dribbling, actions): the older Unity-side touch would steal possession from it.
            if (HasPlayers) return;
            Vec3 v = match.State.Ball.Physics.Velocity;
            float k = Mathf.Clamp01(strength);
            var delta = new Vec3((desiredHorizontalVelocity.x - v.X) * k, 0f, (desiredHorizontalVelocity.z - v.Z) * k);
            match.ApplyBallImpulse(delta, touchTeam, null, BallMode.Free);
        }
    }
}
