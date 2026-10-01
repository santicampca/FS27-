using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// A player on the pitch. Only ties data together: static attributes, tuning, derived stats and
    /// runtime state. It holds no behaviour: movement, animation, ball interaction etc. are separate
    /// components that read from here. Later, attributes will come from a PlayerDefinition asset.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerEntity : MonoBehaviour
    {
        [SerializeField] private TuningProfile tuning;
        [SerializeField] private PlayerAttributes attributes = PlayerAttributes.CreateDefault();

        private readonly PlayerRuntimeState state = new PlayerRuntimeState();
        private MovementTuning fallbackTuning;
        private PlayerStats stats;

        public PlayerAttributes Attributes => attributes;
        public PlayerRuntimeState State => state;
        public PlayerStats Stats => stats;

        public MovementTuning Tuning
        {
            get
            {
                if (tuning != null) return tuning.Movement;
                if (fallbackTuning == null)
                {
                    Debug.LogWarning("PlayerEntity has no TuningProfile assigned; using default values.", this);
                    fallbackTuning = new MovementTuning();
                }
                return fallbackTuning;
            }
        }

        private void Awake()
        {
            stats = PlayerStats.Resolve(attributes, Tuning);
            state.Reset(stats, transform.eulerAngles.y * MathUtil.Deg2Rad);
        }

        /// <summary>Re-resolve stats from attributes + tuning (cheap). Lets tuning be edited live.</summary>
        public void RefreshStats()
        {
            stats = PlayerStats.Resolve(attributes, Tuning);
            state.SetCapacity(stats.StaminaCapacity);
        }

        public void SetAttributes(PlayerAttributes newAttributes)
        {
            attributes = newAttributes;
            RefreshStats();
        }
    }
}
