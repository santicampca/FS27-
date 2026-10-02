using System;
using System.Collections.Generic;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// Stable content ids: "category.name", lower-case snake_case, one dot (hair.short_curly_07, behavior.stop_and_go, style.cartoon_sports,
    /// material.skin_toon). They never change once released; data (and the runtime package) can refer to them instead of copying definitions.
    /// Existing catalog ids (for example "StopAndGo", "FS27_CARTOON_SPORTS") are grandfathered: they keep working, and each has a content id
    /// derived from it by <see cref="ContentIndex"/>.
    /// </summary>
    public static class ContentRef
    {
        public static bool IsValid(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            int dot = id.IndexOf('.');
            if (dot <= 0 || dot != id.LastIndexOf('.') || dot == id.Length - 1) return false;
            return Segment(id, 0, dot) && Segment(id, dot + 1, id.Length);
        }

        private static bool Segment(string s, int from, int to)
        {
            if (!(s[from] >= 'a' && s[from] <= 'z')) return false;
            for (int i = from; i < to; i++)
            {
                char c = s[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) return false;
            }
            return s[to - 1] != '_';
        }

        public static string Category(string id)
        {
            int dot = id == null ? -1 : id.IndexOf('.');
            return dot > 0 ? id.Substring(0, dot) : "";
        }

        /// <summary>"StopAndGo" -> "stop_and_go"; "FS27_CARTOON_SPORTS" -> "fs27_cartoon_sports".</summary>
        public static string ToSnake(string legacy)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < legacy.Length; i++)
            {
                char c = legacy[i];
                if (char.IsUpper(c))
                {
                    bool prevLowerOrDigit = i > 0 && (char.IsLower(legacy[i - 1]) || char.IsDigit(legacy[i - 1]));
                    bool nextLower = i + 1 < legacy.Length && char.IsLower(legacy[i + 1]);
                    if (sb.Length > 0 && sb[sb.Length - 1] != '_' && (prevLowerOrDigit || (char.IsUpper(legacy[i - 1]) && nextLower))) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }
    }

    /// <summary>A material as a name and a recipe, never a Unity asset: a renderer maps <see cref="ShaderFamily"/> to a shader.</summary>
    [Serializable]
    public sealed class MaterialDefinition
    {
        public string Id = "";
        public string ShaderFamily = "fs27_toon_lit";
        /// <summary>Where its colour comes from: "slot:hair.color" (a colour slot of the character), "skin" (the skin ramp) or "#RRGGBB" (fixed).</summary>
        public string ColorSource = "#FFFFFF";
        /// <summary>0..1.</summary>
        public float Smoothness = 0.2f;
        public bool Outlined = true;
    }

    public sealed class MaterialCatalog
    {
        private readonly Dictionary<string, MaterialDefinition> byId = new Dictionary<string, MaterialDefinition>(StringComparer.Ordinal);
        private readonly List<MaterialDefinition> ordered = new List<MaterialDefinition>();

        public IReadOnlyList<MaterialDefinition> All => ordered;

        public bool TryAdd(MaterialDefinition m)
        {
            if (m == null || !ContentRef.IsValid(m.Id) || ContentRef.Category(m.Id) != "material" || byId.ContainsKey(m.Id)) return false;
            byId.Add(m.Id, m);
            ordered.Add(m);
            return true;
        }

        public bool TryGet(string id, out MaterialDefinition m)
        {
            if (id == null) { m = null; return false; }
            return byId.TryGetValue(id, out m);
        }

        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }

        public static MaterialCatalog CreateDefault()
        {
            var c = new MaterialCatalog();
            M(c, "material.skin_toon", "skin", 0.15f);
            M(c, "material.hair_toon", "slot:hair.color", 0.35f);
            M(c, "material.eye_white", "#F5F5F0", 0.6f, false);
            M(c, "material.eye_iris", "slot:eyes.color", 0.7f, false);
            M(c, "material.brow_toon", "slot:hair.color", 0.2f, false);
            M(c, "material.mouth_dark", "#6A2A2A", 0.3f, false);
            M(c, "material.shirt_cloth", "slot:kit.primary", 0.1f);
            M(c, "material.shorts_cloth", "slot:kit.secondary", 0.1f);
            M(c, "material.socks_cloth", "slot:kit.accent", 0.1f);
            M(c, "material.boots_gloss", "slot:kit.boots", 0.6f);
            M(c, "material.gloves_grip", "slot:kit.gloves", 0.25f);
            return c;
        }

        private static void M(MaterialCatalog c, string id, string color, float smooth, bool outlined = true)
        {
            c.TryAdd(new MaterialDefinition { Id = id, ColorSource = color, Smoothness = smooth, Outlined = outlined });
        }
    }

    public enum ContentDomain
    {
        Part = 0,
        Behavior,
        Style,
        Material,
        Animation
    }

    /// <summary>Where a content id points.</summary>
    [Serializable]
    public struct ContentTarget
    {
        public ContentDomain Domain;
        /// <summary>The slot, for parts.</summary>
        public string Slot;
        /// <summary>The catalog id (a part id, behaviour id, style id, material id...).</summary>
        public string Id;
    }

    /// <summary>
    /// Maps stable content ids ("hair.short_curly_07") to the catalogs and back. This is what lets runtime data refer to content by a short,
    /// stable name, and lets a validator prove every reference resolves.
    /// </summary>
    public sealed class ContentIndex
    {
        private readonly Dictionary<string, ContentTarget> byContentId = new Dictionary<string, ContentTarget>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly List<string> Problems = new List<string>();

        public int Count => byContentId.Count;
        public IEnumerable<string> Ids => byContentId.Keys;

        /// <summary>The category of each part slot (several slots can share nothing: the category is part of the stable name).</summary>
        public static readonly Dictionary<string, string> SlotCategory = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "hair.style", "hair" }, { "hair.texture", "hair_texture" }, { "face.beard", "beard" }, { "face.nose", "nose" }, { "face.mouth", "mouth" },
            { "face.eyebrow", "eyebrow" }, { "face.eyeShape", "eyes" }, { "face.expression", "expression" }, { "head.shape", "head" }, { "body.preset", "body" },
            { "kit.shirt", "shirt" }, { "kit.shorts", "shorts" }, { "kit.socks", "socks" }, { "kit.boots", "boots" }, { "kit.gloves", "gloves" }
        };

        public static ContentIndex Build(CreatorCatalogs catalogs, MaterialCatalog materials, AnimationCatalog animations = null)
        {
            var idx = new ContentIndex();
            foreach (string slot in catalogs.Appearance.Slots)
            {
                if (!SlotCategory.TryGetValue(slot, out string category)) { idx.Problems.Add("Slot '" + slot + "' has no content category."); continue; }
                foreach (string part in catalogs.Appearance.PartIds(slot))
                    idx.Add(category + "." + ContentRef.ToSnake(part), new ContentTarget { Domain = ContentDomain.Part, Slot = slot, Id = part });
            }
            foreach (SignatureBehaviorDefinition b in catalogs.Behaviors.All)
                idx.Add("behavior." + ContentRef.ToSnake(b.Id), new ContentTarget { Domain = ContentDomain.Behavior, Id = b.Id });
            foreach (StylePreset s in catalogs.Styles.All)
            {
                string snake = ContentRef.ToSnake(s.Id);
                if (snake.StartsWith("fs27_", StringComparison.Ordinal)) snake = snake.Substring(5);
                idx.Add("style." + snake, new ContentTarget { Domain = ContentDomain.Style, Id = s.Id });
            }
            if (materials != null)
                foreach (MaterialDefinition m in materials.All) idx.Add(m.Id, new ContentTarget { Domain = ContentDomain.Material, Id = m.Id });
            if (animations != null)
                foreach (AnimationProfile a in animations.All) idx.Add(a.ContentId, new ContentTarget { Domain = ContentDomain.Animation, Id = a.Id });
            return idx;
        }

        private void Add(string contentId, ContentTarget target)
        {
            if (!ContentRef.IsValid(contentId)) { Problems.Add("'" + contentId + "' is not a valid content id."); return; }
            if (byContentId.ContainsKey(contentId)) { Problems.Add("Content id '" + contentId + "' is used twice."); return; }
            byContentId.Add(contentId, target);
            byKey[Key(target)] = contentId;
        }

        private static string Key(ContentTarget t) { return (int)t.Domain + "|" + t.Slot + "|" + t.Id; }

        public bool TryResolve(string contentId, out ContentTarget target)
        {
            if (contentId == null) { target = default; return false; }
            return byContentId.TryGetValue(contentId, out target);
        }

        public string PartId(string slot, string partId) { return byKey.TryGetValue(Key(new ContentTarget { Domain = ContentDomain.Part, Slot = slot, Id = partId }), out string c) ? c : null; }
        public string BehaviorId(string behaviorId) { return byKey.TryGetValue(Key(new ContentTarget { Domain = ContentDomain.Behavior, Id = behaviorId }), out string c) ? c : null; }
        public string StyleId(string styleId) { return byKey.TryGetValue(Key(new ContentTarget { Domain = ContentDomain.Style, Id = styleId }), out string c) ? c : null; }
    }
}
