using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>How far an animation is from existing. Only <see cref="Available"/> ones have a clip behind them.</summary>
    public enum AnimationStatus
    {
        /// <summary>A name and metadata only: no clip exists yet.</summary>
        Planned = 0,
        /// <summary>A placeholder clip exists (not final).</summary>
        Placeholder,
        Available
    }

    /// <summary>
    /// One shared animation, as DATA: what it shows, which rigs can play it, at what speeds, and how it is meant to be selected. No clip, no
    /// Unity asset. Animations are shared by every character on a rig: a character never has animations of its own, it MODULATES shared ones
    /// (<see cref="MovementSignature"/>, <see cref="MovementPersonality"/>).
    /// </summary>
    [Serializable]
    public sealed class AnimationProfile
    {
        public string Id = "";
        public AnimationStatus Status = AnimationStatus.Planned;
        public List<string> Tags = new List<string>();
        /// <summary>The football action it performs (None for locomotion).</summary>
        public FootballActionKind Action;
        /// <summary>The movement style it belongs to (Default = any).</summary>
        public MovementStyle Style;
        /// <summary>Rigs that can play it (retargeting handles the rest).</summary>
        public List<string> RigIds = new List<string> { "fs27_humanoid_v1" };
        /// <summary>The ground speed (m/s) it was authored for: locomotion clips are blended by speed.</summary>
        public float MinSpeed;
        public float MaxSpeed;
        public bool Loop;
        public float DurationSeconds = 1f;
        /// <summary>0 = gameplay moves the player and the clip only decorates (the FS27 rule: movement is never driven by animation root motion).</summary>
        public float RootMotionWeight;
        /// <summary>Behaviour ids this clip is a good fit for (empty = any).</summary>
        public List<string> Behaviors = new List<string>();

        public string ContentId => "anim." + ContentRef.ToSnake(Id);

        public bool HasTag(string tag) { return Tags.Contains(tag); }
    }

    public sealed class AnimationCatalog
    {
        private readonly Dictionary<string, AnimationProfile> byId = new Dictionary<string, AnimationProfile>(StringComparer.Ordinal);
        private readonly List<AnimationProfile> ordered = new List<AnimationProfile>();

        public IReadOnlyList<AnimationProfile> All => ordered;
        public int Count => ordered.Count;

        public bool TryAdd(AnimationProfile a)
        {
            if (a == null || string.IsNullOrEmpty(a.Id) || byId.ContainsKey(a.Id)) return false;
            byId.Add(a.Id, a);
            ordered.Add(a);
            return true;
        }

        public bool TryGet(string id, out AnimationProfile a)
        {
            if (id == null) { a = null; return false; }
            return byId.TryGetValue(id, out a);
        }

        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }

        /// <summary>The tags offered by at least one animation (what the behaviour definitions may ask for).</summary>
        public SortedSet<string> AllTags()
        {
            var tags = new SortedSet<string>(StringComparer.Ordinal);
            foreach (AnimationProfile a in ordered) foreach (string t in a.Tags) tags.Add(t);
            return tags;
        }

        public static AnimationCatalog CreateDefault()
        {
            var c = new AnimationCatalog();
            Loco(c, "idle", 0f, 0.2f, "idle");
            Loco(c, "walk_loop", 0.2f, 1.5f, "walk");
            Loco(c, "jog_loop", 1.5f, 3.5f, "jog", "run");
            Loco(c, "run_loop", 3.5f, 6.0f, "run");
            Loco(c, "sprint_loop", 6.0f, 9.0f, "run", "sprint");
            Act(c, "turn_sharp", FootballActionKind.None, MovementStyle.Default, 0f, 9f, false, 0.4f, "turn");
            Act(c, "stop_plant", FootballActionKind.None, MovementStyle.Stop, 0f, 9f, false, 0.35f, "stop");
            Act(c, "burst_start", FootballActionKind.None, MovementStyle.Burst, 0f, 4f, false, 0.5f, "burst", "restart");
            Act(c, "dribble_carry", FootballActionKind.Dribble, MovementStyle.Default, 1f, 7f, true, 0.8f, "dribble");
            Act(c, "dribble_feint", FootballActionKind.Dribble, MovementStyle.Feint, 0f, 5f, false, 0.6f, "feint");
            Act(c, "dribble_cut_inside", FootballActionKind.Dribble, MovementStyle.CutInside, 1f, 7f, false, 0.55f, "cut");
            Act(c, "dribble_cut_outside", FootballActionKind.Dribble, MovementStyle.CutOutside, 1f, 7f, false, 0.55f, "cut");
            Act(c, "dribble_stop_restart", FootballActionKind.Dribble, MovementStyle.Stop, 0f, 5f, false, 0.7f, "stop", "restart");
            Act(c, "shield_hold", FootballActionKind.Shield, MovementStyle.Shield, 0f, 2f, true, 1.0f, "shield");
            Act(c, "control_touch", FootballActionKind.Control, MovementStyle.Default, 0f, 7f, false, 0.45f, "control");
            Act(c, "pass_short", FootballActionKind.ShortPass, MovementStyle.Default, 0f, 7f, false, 0.5f, "pass");
            Act(c, "pass_long", FootballActionKind.LongPass, MovementStyle.Default, 0f, 6f, false, 0.7f, "pass");
            Act(c, "cross_swing", FootballActionKind.Cross, MovementStyle.Default, 0f, 7f, false, 0.7f, "pass", "cross");
            Act(c, "shot_instep", FootballActionKind.Shot, MovementStyle.Default, 0f, 7f, false, 0.65f, "shot");
            Act(c, "shot_placed", FootballActionKind.PlacedShot, MovementStyle.Default, 0f, 7f, false, 0.65f, "shot");
            Act(c, "header_jump", FootballActionKind.Header, MovementStyle.Default, 0f, 6f, false, 0.8f, "header");
            Act(c, "tackle_stand", FootballActionKind.Tackle, MovementStyle.Press, 0f, 7f, false, 0.6f, "press", "tackle");
            Act(c, "intercept_step", FootballActionKind.Intercept, MovementStyle.Default, 0f, 7f, false, 0.5f, "intercept");
            Act(c, "block_brace", FootballActionKind.Block, MovementStyle.Default, 0f, 4f, false, 0.5f, "block");
            Act(c, "gk_dive", FootballActionKind.GoalkeeperSave, MovementStyle.Default, 0f, 3f, false, 0.9f, "save");
            Act(c, "gk_catch", FootballActionKind.GoalkeeperCatch, MovementStyle.Default, 0f, 3f, false, 0.7f, "save", "catch");
            Act(c, "gk_throw", FootballActionKind.GoalkeeperDistribution, MovementStyle.Default, 0f, 3f, false, 0.8f, "distribute");
            return c;
        }

        private static void Loco(AnimationCatalog c, string id, float min, float max, params string[] tags)
        {
            c.TryAdd(new AnimationProfile { Id = id, MinSpeed = min, MaxSpeed = max, Loop = true, DurationSeconds = 0.9f, Tags = new List<string>(tags) });
        }

        private static void Act(AnimationCatalog c, string id, FootballActionKind action, MovementStyle style, float min, float max, bool loop, float duration, params string[] tags)
        {
            c.TryAdd(new AnimationProfile { Id = id, Action = action, Style = style, MinSpeed = min, MaxSpeed = max, Loop = loop, DurationSeconds = duration, Tags = new List<string>(tags) });
        }
    }
}
