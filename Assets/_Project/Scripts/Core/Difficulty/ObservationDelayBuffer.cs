using System;

namespace FS27.Core
{
    /// <summary>
    /// Remembers timestamped observations so the AI can act on what was TRUE a moment ago instead of on what
    /// is true now. This is how "reaction time" is enforced structurally: a query "as of time t" can only
    /// return samples recorded at or before t, so information from the future can never reach a decision.
    /// </summary>
    public sealed class ObservationDelayBuffer<T> where T : struct
    {
        private readonly float[] times;
        private readonly T[] values;
        private int newest = -1;   // index of the newest sample
        private int count;

        public int Count => count;
        public int Capacity => times.Length;
        /// <summary>Time of the newest sample (NaN if empty).</summary>
        public float NewestTime => count == 0 ? float.NaN : times[newest];

        public ObservationDelayBuffer(int capacity)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 2.");
            times = new float[capacity];
            values = new T[capacity];
        }

        /// <summary>Records an observation. Samples must arrive in time order: an older one is rejected (returns false).</summary>
        public bool Push(float time, T value)
        {
            if (count > 0 && time < times[newest]) return false;
            newest = (newest + 1) % times.Length;
            times[newest] = time;
            values[newest] = value;
            if (count < times.Length) count++;
            return true;
        }

        /// <summary>The newest sample recorded at or before <paramref name="time"/>. False if there is none (yet).</summary>
        public bool TryGetAsOf(float time, out T value, out float sampleTime)
        {
            for (int n = 0; n < count; n++)
            {
                int i = (newest - n + times.Length) % times.Length;
                if (times[i] <= time)
                {
                    value = values[i];
                    sampleTime = times[i];
                    return true;
                }
            }
            value = default;
            sampleTime = float.NaN;
            return false;
        }

        /// <summary>What the AI perceives at <paramref name="now"/> given a reaction delay: the world as of now - delay.</summary>
        public bool TryGetDelayed(float now, float delaySeconds, out T value)
        {
            return TryGetAsOf(now - Math.Max(0f, delaySeconds), out value, out _);
        }

        public void Clear()
        {
            count = 0;
            newest = -1;
        }
    }

    /// <summary>
    /// "I noticed something at time t; I can act at t + reaction." Once a stimulus is noticed, repeating it
    /// does not restart the timer, so the AI cannot be kept perpetually waiting, and never acts early.
    /// </summary>
    public sealed class ReactionGate
    {
        private bool pending;
        private float readyAt;

        public bool IsPending => pending;
        public float ReadyAt => readyAt;

        public void Notice(float now, float reactionSeconds)
        {
            if (pending) return;
            pending = true;
            readyAt = now + Math.Max(0f, reactionSeconds);
        }

        /// <summary>True once the reaction time has fully elapsed.</summary>
        public bool IsReady(float now)
        {
            return pending && now >= readyAt;
        }

        public void Clear()
        {
            pending = false;
            readyAt = 0f;
        }
    }
}
