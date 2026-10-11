using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Shows ONE Core player on screen. The Core moves the player (<c>MatchPlayer.Locomotion</c>); this component only (1) hands the player's input to the Core and (2) copies the Core's
    /// position and facing to the transform. It uses no rigidbody and no Unity time, and nothing it does feeds back into the simulation except the input (the explicit way in).
    /// Players of the sandbox scene, which still use the older PlayerMovement, are not affected.
    /// </summary>
    public class LocomotionPresenter : MonoBehaviour
    {
        [SerializeField] private MatchBridge bridge;
        [SerializeField] private TeamSide team = TeamSide.Home;
        [Tooltip("The id of the player in the match squad.")]
        [SerializeField] private string playerId = "";
        [Tooltip("Optional: where this player's input comes from (touch, keyboard, gamepad, AI...). Without it the player gets no input.")]
        [SerializeField] private PlayerIntentProvider intentProvider;

        private void FixedUpdate()
        {
            if (bridge == null || bridge.Match == null) return;
            MatchController match = bridge.Match;

            if (intentProvider != null) match.SetPlayerInput(team, playerId, intentProvider.Read().Move);

            MatchPlayer player = match.State.Team(team).Find(playerId);
            if (player == null) return;

            LocomotionState s = player.Locomotion;
            Vector3 p = transform.position;
            transform.position = new Vector3(s.Position.X, p.y, s.Position.Y);
            transform.rotation = Quaternion.Euler(0f, s.Heading * MathUtil.Rad2Deg, 0f);
        }
    }
}
