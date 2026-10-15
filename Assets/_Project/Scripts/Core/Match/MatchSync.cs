using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// Phase 38 (REQ-0882, 0888, 0890, 0896): the Core can be reproduced from discrete commands and a desynchronisation can be detected. There is NO network code: this is the foundation that a future online mode
    /// (or a replay) would need, and it is used offline today by the tests.
    /// </summary>
    public static class MatchChecksum
    {
        private const ulong Offset = 14695981039346656037UL, Prime = 1099511628211UL;

        /// <summary>
        /// A 64-bit fingerprint of everything that decides what happens next: the clock, the phase and the score, the ball (place, speed, mode, who has it) and every player (id, place, speed, heading, stamina, status, cards).
        /// The same inputs from the same start give the same value on every machine (IEEE floats, no randomness, no time of day); one different bit anywhere changes it.
        /// </summary>
        public static ulong Of(MatchController c)
        {
            MatchState s = c.State;
            ulong h = Offset;
            h = Mix(h, s.MatchTimeSeconds); h = Mix(h, (int)s.Phase); h = Mix(h, (int)s.Period); h = Mix(h, s.Score.Home); h = Mix(h, s.Score.Away);
            MatchBall b = s.Ball;
            h = Mix(h, b.Physics.Position.X); h = Mix(h, b.Physics.Position.Y); h = Mix(h, b.Physics.Position.Z);
            h = Mix(h, b.Physics.Velocity.X); h = Mix(h, b.Physics.Velocity.Y); h = Mix(h, b.Physics.Velocity.Z);
            h = Mix(h, (int)b.Mode); h = Mix(h, b.PossessingPlayerId);
            for (int side = 0; side < 2; side++)
            {
                TeamMatchState team = side == 0 ? s.Home : s.Away;
                for (int i = 0; i < team.Players.Count; i++)
                {
                    MatchPlayer p = team.Players[i];
                    h = Mix(h, p.PlayerId); h = Mix(h, p.Locomotion.Position.X); h = Mix(h, p.Locomotion.Position.Y);
                    h = Mix(h, p.Locomotion.Velocity.X); h = Mix(h, p.Locomotion.Velocity.Y); h = Mix(h, p.Locomotion.Heading);
                    h = Mix(h, p.Runtime.Stamina); h = Mix(h, (int)p.Status); h = Mix(h, p.YellowCards); h = Mix(h, p.RedCards);
                }
            }
            return h;
        }

        private static ulong Mix(ulong h, int v) { for (int i = 0; i < 4; i++) { h ^= (byte)(v >> (8 * i)); h *= Prime; } return h; }
        private static ulong Mix(ulong h, float v) { return Mix(h, BitConverter.SingleToInt32Bits(v == 0f ? 0f : v)); }
        private static ulong Mix(ulong h, string v)
        {
            if (v == null) return Mix(h, -1);
            for (int i = 0; i < v.Length; i++) { h ^= (byte)v[i]; h *= Prime; h ^= (byte)(v[i] >> 8); h *= Prime; }
            return Mix(h, v.Length);
        }
    }

    /// <summary>One thing a person did, as a discrete command: on which simulation step, for which side, and the normalised input (stick + one action).</summary>
    public struct InputCommand
    {
        public int Step;
        public TeamSide Side;
        public NormalizedInput Input;
    }

    /// <summary>
    /// The list of the commands of a match, in order. <see cref="Submit"/> sends the input to the Core and records it; <see cref="Replay"/> feeds the Core the commands of a step. Text form: one command per line
    /// (<c>step side moveX moveY button gesture aimX aimY power curve</c>, invariant culture, round-trip floats) so a log can be stored, sent and compared.
    /// </summary>
    public sealed class InputCommandLog
    {
        private readonly List<InputCommand> commands = new List<InputCommand>();
        private int cursor;
        public IReadOnlyList<InputCommand> Commands => commands;

        public HumanInputResult Submit(MatchController c, int step, TeamSide side, NormalizedInput input)
        {
            NormalizedInput n = input.Sanitized();
            commands.Add(new InputCommand { Step = step, Side = side, Input = n });
            return c.SubmitHumanInput(side, n);
        }

        /// <summary>Submits every recorded command of this step (call it once per step, with increasing steps).</summary>
        public void Replay(MatchController c, int step)
        {
            while (cursor < commands.Count && commands[cursor].Step < step) cursor++;
            while (cursor < commands.Count && commands[cursor].Step == step) { c.SubmitHumanInput(commands[cursor].Side, commands[cursor].Input); cursor++; }
        }

        public void Rewind() { cursor = 0; }

        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }

        public string ToText()
        {
            var sb = new StringBuilder();
            foreach (InputCommand k in commands)
            {
                NormalizedInput n = k.Input;
                sb.Append(k.Step).Append(' ').Append((int)k.Side).Append(' ').Append(F(n.Move.X)).Append(' ').Append(F(n.Move.Y)).Append(' ').Append((int)n.Button).Append(' ').Append((int)n.Gesture).Append(' ')
                  .Append(F(n.Aim.X)).Append(' ').Append(F(n.Aim.Y)).Append(' ').Append(F(n.Power)).Append(' ').Append(F(n.Curve)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Parses <see cref="ToText"/>. False (and an empty log) when a line is malformed: a corrupt log is never half applied.</summary>
        public static bool TryParse(string text, out InputCommandLog log)
        {
            log = new InputCommandLog();
            if (text == null) return false;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] f = line.Split(' ');
                if (f.Length != 10) { log = new InputCommandLog(); return false; }
                var n = new NormalizedInput();
                if (!int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int step) || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int side) ||
                    !int.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int button) || !int.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int gesture) ||
                    !float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float mx) || !float.TryParse(f[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float my) ||
                    !float.TryParse(f[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax) || !float.TryParse(f[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay) ||
                    !float.TryParse(f[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float power) || !float.TryParse(f[9], NumberStyles.Float, CultureInfo.InvariantCulture, out float curve) ||
                    side < 0 || side > 1 || step < 0)
                { log = new InputCommandLog(); return false; }
                n.Move = new Vec2(mx, my); n.Button = (ActionButton)button; n.Gesture = (ActionGesture)gesture; n.Aim = new Vec2(ax, ay); n.Power = power; n.Curve = curve;
                log.commands.Add(new InputCommand { Step = step, Side = (TeamSide)side, Input = n.Sanitized() });
            }
            return true;
        }
    }
}
