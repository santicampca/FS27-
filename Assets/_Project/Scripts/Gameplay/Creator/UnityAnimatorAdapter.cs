using UnityEngine;
using FS27.Core;

namespace FS27.Gameplay.Creator
{
    /// <summary>
    /// ⚠️ NOT COMPILED OR RUN IN UNITY (see UnityCharacterAssembler.cs).
    /// View-only: applies the Core's animation decision (<see cref="AnimationPlan"/>) and the character's <see cref="MovementPersonality"/> to a
    /// character. It plays a clip only when the plan says one exists, never turns on root motion, and never moves the root transform: where the
    /// player goes is Movement's job. Without an Animator or a clip it simply leans and bobs the visual child, so the mannequin still reads as alive.
    /// </summary>
    public sealed class UnityAnimatorAdapter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        /// <summary>The visual child that receives lean and bob (never the physics root).</summary>
        [SerializeField] private Transform visual;

        private MovementPersonality personality;
        private float leanDegrees;
        private float time;
        private string lastAnimation = "";

        public void Configure(MovementPersonality p)
        {
            personality = p;
        }

        /// <summary>Called by the host each frame with what the Core resolved.</summary>
        public void Apply(in AnimationPlan plan, float speedMetersPerSecond, float deltaTime)
        {
            if (animator != null)
            {
                animator.applyRootMotion = false;
                if (!plan.NoClipYet && !string.IsNullOrEmpty(plan.AnimationId) && plan.AnimationId != lastAnimation)
                {
                    animator.CrossFadeInFixedTime(plan.AnimationId, 0.12f);
                    lastAnimation = plan.AnimationId;
                }
                animator.speed = plan.PlaybackSpeed;
            }

            if (visual == null) return;
            time += deltaTime * (0.6f + speedMetersPerSecond * 0.5f);
            leanDegrees = Mathf.Lerp(leanDegrees, plan.LeanDegrees, 1f - Mathf.Exp(-8f * deltaTime));
            float bob = Mathf.Sin(time * 6.0f) * 0.02f * personality.Bounce * Mathf.Clamp01(speedMetersPerSecond / 4f);
            visual.localRotation = Quaternion.Euler(leanDegrees, 0f, 0f);
            visual.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
