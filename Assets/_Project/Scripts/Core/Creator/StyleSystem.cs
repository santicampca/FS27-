using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>How much of a style axis a parameter moves per unit of shift (signed; in normalised range units).</summary>
    [Serializable]
    public struct StyleAxisEffect
    {
        public string ParameterId;
        public float PerUnit;
        /// <summary>True for the parameters that make a character look childish when pushed too far (head, eyes, hands, feet).</summary>
        public bool ChildlikeRisk;

        public StyleAxisEffect(string parameterId, float perUnit, bool childlikeRisk = false)
        {
            ParameterId = parameterId;
            PerUnit = perUnit;
            ChildlikeRisk = childlikeRisk;
        }
    }

    /// <summary>
    /// A style: a starting look plus how its main axis moves several parameters TOGETHER. "More cartoon" is not one number going up: it
    /// raises stylisation, expressiveness and exaggeration, lowers realism, enlarges head, eyes, hands and feet a little, and leaves the athletic
    /// proportions (shoulders, torso, legs, athleticity) alone.
    /// </summary>
    [Serializable]
    public sealed class StylePreset
    {
        public string Id;
        public string Name;
        /// <summary>Starting look: normalised levels (0..1) for some appearance parameters.</summary>
        public Dictionary<string, float> StartLevels = new Dictionary<string, float>();
        /// <summary>Effects of one full unit of shift TOWARDS cartoon. A negative shift moves towards realism.</summary>
        public List<StyleAxisEffect> CartoonAxis = new List<StyleAxisEffect>();
        /// <summary>Factor applied to childlike-risk effects when the request says "not childish".</summary>
        public float ChildlikeGuardFactor = 0.4f;
    }

    public sealed class StyleCatalog
    {
        private readonly Dictionary<string, StylePreset> byId = new Dictionary<string, StylePreset>();

        public IEnumerable<StylePreset> All => byId.Values;

        public bool TryAdd(StylePreset s)
        {
            if (s == null || string.IsNullOrEmpty(s.Id) || byId.ContainsKey(s.Id)) return false;
            byId.Add(s.Id, s);
            return true;
        }

        public bool TryGet(string id, out StylePreset s)
        {
            if (id == null)
            {
                s = null;
                return false;
            }
            return byId.TryGetValue(id, out s);
        }

        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }
    }

    public static class DefaultStyles
    {
        public const string CartoonSports = "FS27_CARTOON_SPORTS";

        public static StyleCatalog Create()
        {
            var c = new StyleCatalog();
            var s = new StylePreset { Id = CartoonSports, Name = "FS27 Cartoon Sports" };
            // Starting look (~6.5 heads, slightly big head, hands and boots emphasised, athletic): normalised levels.
            s.StartLevels["head.scale"] = 0.30f;
            s.StartLevels["body.handScale"] = 0.20f;
            s.StartLevels["body.footScale"] = 0.20f;
            s.StartLevels["face.expressiveness"] = 0.65f;
            s.StartLevels["style.athleticity"] = 0.70f;
            s.StartLevels["style.stylization"] = 0.70f;
            s.StartLevels["style.realism"] = 0.25f;
            s.StartLevels["style.exaggeration"] = 0.35f;
            s.StartLevels["style.expressiveness"] = 0.60f;
            s.CartoonAxis.Add(new StyleAxisEffect("style.stylization", 1.0f));
            s.CartoonAxis.Add(new StyleAxisEffect("style.realism", -0.8f));
            s.CartoonAxis.Add(new StyleAxisEffect("style.exaggeration", 0.6f));
            s.CartoonAxis.Add(new StyleAxisEffect("style.expressiveness", 0.5f));
            s.CartoonAxis.Add(new StyleAxisEffect("face.expressiveness", 0.4f));
            s.CartoonAxis.Add(new StyleAxisEffect("head.scale", 0.35f, true));
            s.CartoonAxis.Add(new StyleAxisEffect("face.eyeSize", 0.35f, true));
            s.CartoonAxis.Add(new StyleAxisEffect("body.handScale", 0.2f, true));
            s.CartoonAxis.Add(new StyleAxisEffect("body.footScale", 0.2f, true));
            c.TryAdd(s);
            return c;
        }
    }
}
