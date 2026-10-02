using System;

namespace FS27.Core
{
    /// <summary>
    /// Turns frames of any length into FIXED simulation steps, so the Core never depends on the frame rate: the same inputs, in the same number of steps,
    /// always give the same result. The remainder carries over to the next call. A cap on steps per call stops a very long frame from spiralling.
    /// </summary>
    public sealed class SimulationClock
    {
        private float accumulator;

        public float StepSeconds { get; }
        public int MaxStepsPerAdvance { get; }
        /// <summary>Steps taken in total (the simulation's own tick count).</summary>
        public long TotalSteps { get; private set; }

        public SimulationClock(float stepSeconds, int maxStepsPerAdvance = 8)
        {
            if (!(stepSeconds > 0f)) throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            StepSeconds = stepSeconds;
            MaxStepsPerAdvance = Math.Max(1, maxStepsPerAdvance);
        }

        /// <summary>Adds frame time and returns how many whole steps are due now (at most <see cref="MaxStepsPerAdvance"/>; the excess time is dropped).</summary>
        public int Advance(float frameSeconds)
        {
            if (!(frameSeconds > 0f)) return 0;
            accumulator += frameSeconds;
            int steps = (int)(accumulator / StepSeconds);
            if (steps > MaxStepsPerAdvance)
            {
                steps = MaxStepsPerAdvance;
                accumulator = 0f;
            }
            else
            {
                accumulator -= steps * StepSeconds;
            }
            TotalSteps += steps;
            return steps;
        }
    }
}
