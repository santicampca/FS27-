using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// View-only: mirrors the player's runtime state into an Animator (if one is assigned).
    /// With the placeholder capsule there is no Animator, so this does nothing. When a real character
    /// arrives, give its controller float "Speed" (0..1 of top speed) and bool "Sprinting" parameters.
    /// </summary>
    public class PlayerAnimation : MonoBehaviour
    {
        [SerializeField] private PlayerEntity entity;
        [SerializeField] private Animator animator;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int SprintingParam = Animator.StringToHash("Sprinting");

        private void Update()
        {
            if (animator == null || entity == null || animator.runtimeAnimatorController == null) return;

            float top = entity.Stats.TopSpeed;
            animator.SetFloat(SpeedParam, top > 0f ? entity.State.Speed / top : 0f);
            animator.SetBool(SprintingParam, entity.State.IsSprinting);
        }
    }
}
