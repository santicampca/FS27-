using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Describes a base model by id: which rig it uses and which slots it can show. It names the model; it does not contain it
    /// (no meshes in data). The base model is replaceable: characters refer to it by id, and the parts they use are checked against it.
    /// </summary>
    [Serializable]
    public sealed class BaseModelDefinition
    {
        public string Id;
        public string RigId;
        /// <summary>Approximate head heights of the base (a hint for the style system; not verified against any asset).</summary>
        public float HeadHeights = 6.5f;
    }

    /// <summary>A selectable piece (a hairstyle, a kit, boots...) in a slot, by id. Data only: the asset behind the id is bound later.</summary>
    [Serializable]
    public sealed class PartDefinition
    {
        public string Slot;
        public string Id;
        /// <summary>Base models this part fits. Empty = fits every base.</summary>
        public List<string> BaseModels = new List<string>();

        public bool FitsBase(string baseModelId)
        {
            return BaseModels == null || BaseModels.Count == 0 || BaseModels.Contains(baseModelId);
        }
    }

    public sealed class AppearanceCatalog
    {
        private readonly Dictionary<string, BaseModelDefinition> baseModels = new Dictionary<string, BaseModelDefinition>();
        private readonly Dictionary<string, Dictionary<string, PartDefinition>> parts = new Dictionary<string, Dictionary<string, PartDefinition>>();
        private readonly HashSet<string> colorSlots = new HashSet<string>();

        public IEnumerable<BaseModelDefinition> BaseModels => baseModels.Values;
        public IEnumerable<string> Slots => parts.Keys;
        public IEnumerable<string> ColorSlots => colorSlots;

        public bool TryAddBaseModel(BaseModelDefinition b)
        {
            if (b == null || string.IsNullOrEmpty(b.Id) || baseModels.ContainsKey(b.Id)) return false;
            baseModels.Add(b.Id, b);
            return true;
        }

        public bool TryGetBaseModel(string id, out BaseModelDefinition b)
        {
            if (id == null)
            {
                b = null;
                return false;
            }
            return baseModels.TryGetValue(id, out b);
        }

        public bool TryAddPart(PartDefinition p)
        {
            if (p == null || string.IsNullOrEmpty(p.Slot) || string.IsNullOrEmpty(p.Id)) return false;
            if (!parts.TryGetValue(p.Slot, out Dictionary<string, PartDefinition> slot))
            {
                slot = new Dictionary<string, PartDefinition>();
                parts.Add(p.Slot, slot);
            }
            if (slot.ContainsKey(p.Id)) return false;
            slot.Add(p.Id, p);
            return true;
        }

        public bool HasSlot(string slot)
        {
            return slot != null && parts.ContainsKey(slot);
        }

        public bool TryGetPart(string slot, string id, out PartDefinition p)
        {
            p = null;
            return slot != null && id != null && parts.TryGetValue(slot, out Dictionary<string, PartDefinition> s) && s.TryGetValue(id, out p);
        }

        public List<string> PartIds(string slot)
        {
            var ids = new List<string>();
            if (slot != null && parts.TryGetValue(slot, out Dictionary<string, PartDefinition> s)) ids.AddRange(s.Keys);
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        public void AddColorSlot(string slot)
        {
            if (!string.IsNullOrEmpty(slot)) colorSlots.Add(slot);
        }

        public bool HasColorSlot(string slot)
        {
            return slot != null && colorSlots.Contains(slot);
        }
    }

    /// <summary>
    /// The standard catalog. Only ids: there are no assets behind them yet (that is Phase A..E of the Creator Engine roadmap).
    /// Slots are open: new hairstyles, faces, kits or boots are new rows here.
    /// </summary>
    public static class DefaultAppearanceCatalog
    {
        public const string BaseA = "fs27_base_a";
        public const string BodyPresetSlot = "body.preset";

        private static void P(AppearanceCatalog c, string slot, params string[] ids)
        {
            foreach (string id in ids) c.TryAddPart(new PartDefinition { Slot = slot, Id = id });
        }

        public static AppearanceCatalog Create()
        {
            var c = new AppearanceCatalog();
            c.TryAddBaseModel(new BaseModelDefinition { Id = BaseA, RigId = "fs27_humanoid_v1", HeadHeights = 6.5f });

            P(c, BodyPresetSlot, "light", "athletic", "strong", "tall", "compact");
            P(c, "head.shape", "athletic_oval", "round", "angular", "square");
            P(c, "face.eyeShape", "sport_expressive", "round_wide", "narrow_sharp", "soft_almond");
            P(c, "face.eyebrow", "straight", "arched", "heavy", "thin");
            P(c, "face.nose", "straight", "button", "broad", "aquiline");
            P(c, "face.mouth", "neutral_wide", "small", "full", "thin");
            P(c, "face.expression", "neutral", "determined", "smile", "intense", "cheeky");
            P(c, "face.beard", "none", "stubble", "short_beard");
            P(c, "hair.style", "buzz_02", "short_crop_01", "short_curly_07", "short_textured_04", "medium_wavy_03", "long_tied_05", "fade_06", "afro_08");
            P(c, "hair.texture", "straight", "wavy", "curly", "coily");
            P(c, "kit.shirt", "plain", "stripes", "hoops", "sash", "sleeve_trim");
            P(c, "kit.shorts", "plain", "side_stripe");
            P(c, "kit.socks", "plain", "striped", "cuff");
            P(c, "kit.boots", "classic", "bold", "low_profile");
            P(c, "kit.gloves", "none", "gk_standard", "gk_grip");

            foreach (string slot in new[] { "hair.color", "kit.primary", "kit.secondary", "kit.accent", "kit.boots", "kit.gloves", "eyes.color" })
                c.AddColorSlot(slot);
            return c;
        }
    }
}
