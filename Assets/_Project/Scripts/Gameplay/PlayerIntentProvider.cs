using FS27.Core;
using UnityEngine;

namespace FS27.Gameplay
{
    /// <summary>
    /// Single place where a player gets its <see cref="PlayerIntent"/>. The player never knows whether it
    /// comes from touch, keyboard, gamepad or AI. By default the source with the strongest input wins
    /// (so keyboard and touch can coexist). An override source (e.g. AI) takes priority when set.
    /// </summary>
    public class PlayerIntentProvider : MonoBehaviour
    {
        // Unity cannot serialise interfaces, so sources are assigned as components and checked on Awake.
        [SerializeField] private MonoBehaviour[] sourceBehaviours = new MonoBehaviour[0];

        private IIntentSource[] sources = new IIntentSource[0];
        private IIntentSource overrideSource;

        private void Awake()
        {
            ResolveSources();
        }

        private void ResolveSources()
        {
            int count = 0;
            for (int i = 0; i < sourceBehaviours.Length; i++)
                if (sourceBehaviours[i] is IIntentSource) count++;

            sources = new IIntentSource[count];
            int n = 0;
            for (int i = 0; i < sourceBehaviours.Length; i++)
            {
                if (sourceBehaviours[i] is IIntentSource s) sources[n++] = s;
                else if (sourceBehaviours[i] != null)
                    Debug.LogError(sourceBehaviours[i].name + " does not implement IIntentSource.", this);
            }
        }

        /// <summary>Takes control of this player (e.g. AI). Pass null to go back to the default sources.</summary>
        public void SetOverride(IIntentSource source)
        {
            overrideSource = source;
        }

        public PlayerIntent Read()
        {
            if (overrideSource != null) return overrideSource.ReadIntent();

            PlayerIntent best = PlayerIntent.None;
            float bestSqr = 0f;
            for (int i = 0; i < sources.Length; i++)
            {
                PlayerIntent candidate = sources[i].ReadIntent();
                float sqr = candidate.Move.SqrMagnitude;
                if (sqr > bestSqr)
                {
                    best = candidate;
                    bestSqr = sqr;
                }
            }
            return best;
        }
    }
}
