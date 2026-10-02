using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Test/AI/replay source: returns whatever intent it was last given.</summary>
    public sealed class ScriptedIntentSource : IIntentSource
    {
        public PlayerIntent Current;

        public void Set(Vec2 move)
        {
            Current = new PlayerIntent(move);
        }

        public PlayerIntent ReadIntent()
        {
            return Current;
        }
    }

    public static class IntentMixer
    {
        /// <summary>Replaces NaN/infinite input with "no input"; the length is already capped by PlayerIntent.</summary>
        public static PlayerIntent Sanitize(PlayerIntent intent)
        {
            float x = intent.Move.X, y = intent.Move.Y;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
                return PlayerIntent.None;
            // only the movement is normalised; behaviour, action, style and the dribble/shot/pass details pass through untouched
            PlayerIntent clean = intent;
            clean.Move = new PlayerIntent(intent.Move).Move;
            return clean;
        }

        /// <summary>The intent with the larger stick deflection.</summary>
        public static PlayerIntent Strongest(PlayerIntent a, PlayerIntent b)
        {
            return b.Move.SqrMagnitude > a.Move.SqrMagnitude ? b : a;
        }
    }

    /// <summary>
    /// Chooses which source drives a player: an override (e.g. AI) wins, otherwise the source with the
    /// strongest input (so touch and keyboard can coexist). Pure version of the Unity PlayerIntentProvider.
    /// </summary>
    public sealed class IntentRouter
    {
        private readonly List<IIntentSource> sources = new List<IIntentSource>();
        private IIntentSource overrideSource;

        public int SourceCount => sources.Count;

        public void Add(IIntentSource source)
        {
            if (source != null && !sources.Contains(source)) sources.Add(source);
        }

        public void Remove(IIntentSource source)
        {
            sources.Remove(source);
        }

        public void SetOverride(IIntentSource source)
        {
            overrideSource = source;
        }

        public PlayerIntent Read()
        {
            if (overrideSource != null) return IntentMixer.Sanitize(overrideSource.ReadIntent());

            PlayerIntent best = PlayerIntent.None;
            for (int i = 0; i < sources.Count; i++)
                best = IntentMixer.Strongest(best, IntentMixer.Sanitize(sources[i].ReadIntent()));
            return best;
        }
    }
}
