using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>An authoring tag that deliberately has no skill yet, and why (it depends on a rule that is built in a later phase).</summary>
    public sealed class DeferredTag
    {
        public string Tag = "";
        public string Reason = "";
    }

    /// <summary>The official skill catalogue: every skill by id, and which skill each authoring tag means. Data only; the catalogue lives in a data file, never in code.</summary>
    public sealed class SkillCatalog
    {
        private readonly Dictionary<string, SkillDefinition> byId = new Dictionary<string, SkillDefinition>();
        private readonly List<SkillDefinition> ordered = new List<SkillDefinition>();
        private readonly Dictionary<string, SkillDefinition> byTag = new Dictionary<string, SkillDefinition>();
        private readonly Dictionary<string, string> deferred = new Dictionary<string, string>();
        private readonly List<DeferredTag> deferredList = new List<DeferredTag>();

        public IReadOnlyList<SkillDefinition> All => ordered;
        public int Count => ordered.Count;
        public IReadOnlyList<DeferredTag> DeferredTags => deferredList;

        /// <summary>Adds a skill. Refuses a null skill, an invalid id or a repeated id. A tag already claimed by another skill is NOT added to the tag map (the validator reports it).</summary>
        public bool TryAdd(SkillDefinition skill)
        {
            if (skill == null || !DataRules.IsValidId(skill.SkillId) || byId.ContainsKey(skill.SkillId)) return false;
            byId.Add(skill.SkillId, skill);
            ordered.Add(skill);
            foreach (string tag in skill.SourceTags) if (!string.IsNullOrEmpty(tag) && !byTag.ContainsKey(tag)) byTag.Add(tag, skill);
            return true;
        }

        public bool TryAddDeferred(DeferredTag tag)
        {
            if (tag == null || string.IsNullOrEmpty(tag.Tag) || deferred.ContainsKey(tag.Tag)) return false;
            deferred.Add(tag.Tag, tag.Reason);
            deferredList.Add(tag);
            return true;
        }

        public bool Contains(string skillId) { return skillId != null && byId.ContainsKey(skillId); }
        public bool TryGet(string skillId, out SkillDefinition skill) { skill = null; return skillId != null && byId.TryGetValue(skillId, out skill); }
        /// <summary>The skill an authoring tag means (the tag is the importer's normalised signature action).</summary>
        public bool TryResolveTag(string tag, out SkillDefinition skill) { skill = null; return tag != null && byTag.TryGetValue(tag, out skill); }
        public bool IsDeferred(string tag) { return tag != null && deferred.ContainsKey(tag); }
    }

    /// <summary>Looks up the skills of a player. A separate contract from <see cref="IPlayerLookup"/> so that existing implementations do not change.</summary>
    public interface IPlayerSkillLookup
    {
        bool TryGetSkills(string playerId, out PlayerSkillSet skills);
    }

    /// <summary>The skills of every field player, by player id.</summary>
    public sealed class PlayerSkillCatalog : IPlayerSkillLookup
    {
        private readonly Dictionary<string, PlayerSkillSet> byPlayer = new Dictionary<string, PlayerSkillSet>();
        private readonly List<PlayerSkillSet> ordered = new List<PlayerSkillSet>();

        public int Count => ordered.Count;
        public IReadOnlyList<PlayerSkillSet> All => ordered;

        public bool TryAdd(PlayerSkillSet set)
        {
            if (set == null || !DataRules.IsValidId(set.PlayerId) || byPlayer.ContainsKey(set.PlayerId)) return false;
            byPlayer.Add(set.PlayerId, set);
            ordered.Add(set);
            return true;
        }

        public bool TryGetSkills(string playerId, out PlayerSkillSet skills) { skills = null; return playerId != null && byPlayer.TryGetValue(playerId, out skills); }

        public bool Has(string playerId, string skillId)
        {
            return TryGetSkills(playerId, out PlayerSkillSet s) && s.SkillIds.Contains(skillId);
        }

        /// <summary>The skill actions this player may perform (the union of what his skills unlock). Passive skills unlock nothing.</summary>
        public List<FootballActionKind> UnlockedActions(string playerId, SkillCatalog catalog)
        {
            var list = new List<FootballActionKind>();
            if (!TryGetSkills(playerId, out PlayerSkillSet s)) return list;
            foreach (string id in s.SkillIds)
                if (catalog.TryGet(id, out SkillDefinition d) && d.UnlocksAction != FootballActionKind.None && !list.Contains(d.UnlocksAction)) list.Add(d.UnlocksAction);
            return list;
        }
    }

    /// <summary>The skill catalogue as JSON (strict reader, stable writer).</summary>
    public static class SkillCatalogJson
    {
        public const string Schema = "FS27.Skills.v1";

        public static string ToJson(SkillCatalog catalog, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            var list = JsonValue.NewArray();
            foreach (SkillDefinition s in catalog.All)
            {
                var o = JsonValue.NewObject();
                o.Set("skillId", JsonValue.Of(s.SkillId)).Set("name", JsonValue.Of(s.Name)).Set("description", JsonValue.Of(s.Description));
                o.Set("category", JsonValue.Of(s.Category.ToString())).Set("kind", JsonValue.Of(s.Kind.ToString())).Set("unlocksAction", JsonValue.Of(s.UnlocksAction.ToString()));
                var arch = JsonValue.NewArray();
                foreach (PlayerArchetype a in s.Archetypes) arch.Add(JsonValue.Of(a.ToString()));
                o.Set("archetypes", arch);
                var req = JsonValue.NewObject();
                foreach (SkillRequirement r in s.Requirements) req.Set(r.Attribute.ToString(), JsonValue.Of((double)r.Minimum));
                o.Set("requirements", req);
                var tags = JsonValue.NewArray();
                foreach (string t in s.SourceTags) tags.Add(JsonValue.Of(t));
                o.Set("sourceTags", tags);
                list.Add(o);
            }
            root.Set("skills", list);
            var def = JsonValue.NewArray();
            foreach (DeferredTag d in catalog.DeferredTags) def.Add(JsonValue.NewObject().Set("tag", JsonValue.Of(d.Tag)).Set("reason", JsonValue.Of(d.Reason)));
            root.Set("deferredTags", def);
            return Json.Write(root, indented);
        }

        public static bool TryFromJson(string text, out SkillCatalog catalog, out string error)
        {
            catalog = new SkillCatalog();
            error = "";
            if (!Json.TryParse(text, out JsonValue root, out string parseError)) { error = parseError; return false; }
            try
            {
                if (root.Kind != JsonKind.Object || root.GetString("schemaVersion") != Schema) throw new FormatException("schemaVersion must be '" + Schema + "'.");
                foreach (JsonValue o in Arr(root, "skills"))
                {
                    var s = new SkillDefinition { SkillId = Str(o, "skillId"), Name = Str(o, "name"), Description = Str(o, "description") };
                    s.Category = Enum<SkillCategory>(o, "category");
                    s.Kind = Enum<SkillKind>(o, "kind");
                    s.UnlocksAction = Enum<FootballActionKind>(o, "unlocksAction");
                    foreach (JsonValue a in Arr(o, "archetypes"))
                    {
                        if (a.Kind != JsonKind.String || !System.Enum.TryParse(a.String, false, out PlayerArchetype arch) || !System.Enum.IsDefined(typeof(PlayerArchetype), arch)) throw new FormatException("'" + (a.String ?? "?") + "' is not an official role.");
                        s.Archetypes.Add(arch);
                    }
                    if (!o.TryGet("requirements", out JsonValue req) || req.Kind != JsonKind.Object) throw new FormatException("'requirements' must be an object.");
                    foreach (KeyValuePair<string, JsonValue> kv in req.Members)
                    {
                        if (!System.Enum.TryParse(kv.Key, false, out PlayerAttributeId att) || !System.Enum.IsDefined(typeof(PlayerAttributeId), att)) throw new FormatException("'" + kv.Key + "' is not one of the 12 official attributes.");
                        if (kv.Value.Kind != JsonKind.Number || kv.Value.Number != Math.Floor(kv.Value.Number)) throw new FormatException("Requirement '" + kv.Key + "' must be a whole number.");
                        s.Requirements.Add(new SkillRequirement(att, (int)kv.Value.Number));
                    }
                    foreach (JsonValue t in Arr(o, "sourceTags")) { if (t.Kind != JsonKind.String) throw new FormatException("'sourceTags' holds strings."); s.SourceTags.Add(t.String); }
                    if (!catalog.TryAdd(s)) throw new FormatException("Skill '" + s.SkillId + "' is invalid or repeated.");
                }
                foreach (JsonValue d in Arr(root, "deferredTags"))
                    if (!catalog.TryAddDeferred(new DeferredTag { Tag = Str(d, "tag"), Reason = Str(d, "reason") })) throw new FormatException("A deferred tag is empty or repeated.");
                return true;
            }
            catch (FormatException e) { catalog = new SkillCatalog(); error = e.Message; return false; }
        }

        private static List<JsonValue> Arr(JsonValue parent, string key)
        {
            if (!parent.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Array) throw new FormatException("'" + key + "' must be an array.");
            return v.Items;
        }

        private static string Str(JsonValue o, string key)
        {
            if (o.Kind != JsonKind.Object || !o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }

        private static T Enum<T>(JsonValue o, string key) where T : struct
        {
            string s = Str(o, key);
            if (!System.Enum.TryParse(s, false, out T value) || !System.Enum.IsDefined(typeof(T), value)) throw new FormatException("'" + s + "' is not a valid " + typeof(T).Name + ".");
            return value;
        }
    }

    public enum SkillNoteKind
    {
        /// <summary>The skill was given because of this signature tag.</summary>
        Assigned,
        /// <summary>The player's attributes are below the skill's floor, so it was NOT given.</summary>
        RejectedRequirement,
        /// <summary>None of the skill's official roles is in the player's playing profile, so it was NOT given.</summary>
        RejectedArchetype,
        /// <summary>The player already has the maximum number of skills; this one was left out.</summary>
        TrimmedByCap,
        /// <summary>The player had no skill from his tags; this one was derived from his attributes and roles.</summary>
        FallbackAssigned,
        /// <summary>The tag means no skill and is not deferred: a catalogue hole (an error).</summary>
        OrphanTag,
        /// <summary>The tag is deliberately deferred (see <see cref="DeferredTag"/>).</summary>
        DeferredTag
    }

    public sealed class SkillAssignmentNote
    {
        public string PlayerId = "";
        public SkillNoteKind Kind;
        public string SkillId = "";
        public string Tag = "";
        public string Message = "";
        public override string ToString() { return Kind + " [" + PlayerId + "] " + (SkillId.Length > 0 ? SkillId + " " : "") + Message; }
    }

    /// <summary>The numbers of the assignment, in one place.</summary>
    public sealed class SkillAssignmentRules
    {
        /// <summary>Most skills a player owns (his first signature tags win, in the order the source lists them).</summary>
        public int MaxSkills = 4;
        /// <summary>Every field player owns at least this many.</summary>
        public int MinSkills = 1;
    }

    public sealed class SkillAssignmentResult
    {
        public readonly PlayerSkillCatalog Skills = new PlayerSkillCatalog();
        public readonly List<SkillAssignmentNote> Notes = new List<SkillAssignmentNote>();
        public int Count(SkillNoteKind kind) { int n = 0; foreach (SkillAssignmentNote x in Notes) if (x.Kind == kind) n++; return n; }
        public IEnumerable<SkillAssignmentNote> Of(SkillNoteKind kind) { foreach (SkillAssignmentNote x in Notes) if (x.Kind == kind) yield return x; }
    }

    /// <summary>
    /// Gives every FIELD player his skills, deterministically, from data that already exists. Rules, in order, for each field player:
    ///  1. His signature tags (the importers' normalised signature actions, in source order) are resolved to skills through the catalogue (<see cref="SkillDefinition.SourceTags"/>).
    ///     A deferred tag is skipped on purpose; a tag that is neither mapped nor deferred is an OrphanTag (an error).
    ///  2. A skill is only given if the player's attributes reach every floor of the skill AND (when the skill names roles) his playing profile has one of them.
    ///     Overall plays no part: a player can own a skill his Overall would not suggest. Refusals are recorded, never silent.
    ///  3. At most <see cref="SkillAssignmentRules.MaxSkills"/>: the first ones win.
    ///  4. A player left with none gets ONE derived from his data: among the skills he qualifies for, the one he clears by the widest margin (ties: catalogue order).
    /// Goalkeepers get nothing here: their system is <see cref="GoalkeeperProfile"/>.
    /// </summary>
    public static class SkillAssigner
    {
        public static SkillAssignmentResult Assign(PlayerLibrary library, PlayingProfileCatalog profiles, IReadOnlyDictionary<string, List<string>> tagsByPlayer, SkillCatalog catalog, SkillAssignmentRules rules = null)
        {
            rules = rules ?? new SkillAssignmentRules();
            var result = new SkillAssignmentResult();
            foreach (PlayerDefinition p in library.All)
            {
                if (p.Role == PlayerRole.Goalkeeper) continue;
                PlayerPlayingProfile profile = null;
                if (profiles != null) profiles.TryGet(p.Id, out profile);

                var accepted = new List<SkillDefinition>();
                List<string> tags;
                if (tagsByPlayer == null || !tagsByPlayer.TryGetValue(p.Id, out tags)) tags = new List<string>();
                foreach (string tag in tags)
                {
                    if (catalog.IsDeferred(tag)) { Note(result, p.Id, SkillNoteKind.DeferredTag, "", tag, "Tag '" + tag + "' is deferred on purpose."); continue; }
                    if (!catalog.TryResolveTag(tag, out SkillDefinition skill)) { Note(result, p.Id, SkillNoteKind.OrphanTag, "", tag, "Tag '" + tag + "' means no skill and is not deferred."); continue; }
                    if (accepted.Contains(skill)) continue;
                    string why = Refusal(p, profile, skill, out SkillNoteKind refusal);
                    if (why != null) { Note(result, p.Id, refusal, skill.SkillId, tag, why); continue; }
                    if (accepted.Count >= rules.MaxSkills) { Note(result, p.Id, SkillNoteKind.TrimmedByCap, skill.SkillId, tag, "More than " + rules.MaxSkills + " skills; left out."); continue; }
                    accepted.Add(skill);
                    Note(result, p.Id, SkillNoteKind.Assigned, skill.SkillId, tag, "From signature tag '" + tag + "'.");
                }

                if (accepted.Count < rules.MinSkills)
                {
                    SkillDefinition fallback = BestFit(p, profile, catalog, accepted);
                    if (fallback != null) { accepted.Add(fallback); Note(result, p.Id, SkillNoteKind.FallbackAssigned, fallback.SkillId, "", "No skill from his tags; derived from his attributes and roles."); }
                }

                var set = new PlayerSkillSet { PlayerId = p.Id };
                foreach (SkillDefinition s in accepted) set.SkillIds.Add(s.SkillId);
                result.Skills.TryAdd(set);
            }
            return result;
        }

        /// <summary>Null when the player may own the skill; otherwise why not.</summary>
        public static string Refusal(PlayerDefinition p, PlayerPlayingProfile profile, SkillDefinition skill, out SkillNoteKind kind)
        {
            foreach (SkillRequirement r in skill.Requirements)
            {
                int v = p.Attributes.GetValue(r.Attribute);
                if (v < r.Minimum) { kind = SkillNoteKind.RejectedRequirement; return r.Attribute + " " + v + " is below " + r.Minimum + "."; }
            }
            if (skill.Archetypes.Count > 0)
            {
                bool fits = false;
                if (profile != null) foreach (PlayerArchetype a in skill.Archetypes) if (profile.HasArchetype(a)) { fits = true; break; }
                if (!fits) { kind = SkillNoteKind.RejectedArchetype; return "None of its roles (" + string.Join("/", skill.Archetypes) + ") is in the player's profile."; }
            }
            kind = SkillNoteKind.Assigned;
            return null;
        }

        private static SkillDefinition BestFit(PlayerDefinition p, PlayerPlayingProfile profile, SkillCatalog catalog, List<SkillDefinition> already)
        {
            SkillDefinition best = null;
            double bestMargin = double.MinValue;
            foreach (SkillDefinition s in catalog.All)
            {
                if (already.Contains(s) || Refusal(p, profile, s, out SkillNoteKind _) != null || s.Requirements.Count == 0) continue;
                double margin = 0;
                foreach (SkillRequirement r in s.Requirements) margin += p.Attributes.GetValue(r.Attribute) - r.Minimum;
                margin /= s.Requirements.Count;
                if (margin > bestMargin + 1e-9) { bestMargin = margin; best = s; }
            }
            return best;
        }

        private static void Note(SkillAssignmentResult r, string player, SkillNoteKind kind, string skill, string tag, string message)
        {
            r.Notes.Add(new SkillAssignmentNote { PlayerId = player, Kind = kind, SkillId = skill, Tag = tag, Message = message });
        }
    }
}
