using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Phase 4 presentation of the actions. It decides NOTHING: it takes the neutral input of the two action buttons (button, gesture, direction, magnitude), hands it to the Core
    /// (<see cref="MatchController.PerformInput"/>) and returns the Core's answer. The Core says whether the action exists in that moment, what it does to the ball and who ends up
    /// with it; the ball in Unity only follows <c>MatchState.Ball</c> (see <see cref="MatchBridge"/>). Subscribe to <see cref="MatchBridge.ActionPerformed"/> to react (animation, sound).
    /// </summary>
    public class ActionBridge : MonoBehaviour
    {
        [SerializeField] private MatchBridge bridge;

        /// <summary>The direction is in FIELD space (x = along the pitch, y = across it), already converted by whoever reads the touch input.</summary>
        public ActionResult Submit(TeamSide side, string playerId, ActionButton button, ActionGesture gesture, Vector2 fieldDirection, float magnitude)
        {
            if (bridge == null || bridge.Match == null) return new ActionResult { Outcome = ActionOutcome.Rejected, Rejection = ActionRejection.MatchNotLive, PlayerId = playerId, Team = side };
            return bridge.Match.PerformInput(side, playerId, new ActionInput(button, gesture, new Vec2(fieldDirection.x, fieldDirection.y), magnitude));
        }

        /// <summary>What the buttons would do for this player right now (so the HUD can label them). Pure read of the Core.</summary>
        public ActionContext ContextOf(TeamSide side, string playerId)
        {
            return bridge != null && bridge.Match != null ? bridge.Match.GetActionContext(side, playerId) : default(ActionContext);
        }
    }
}
