using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    public enum IdentityKind
    {
        Player = 0,
        Coach = 1,
        Referee = 2
    }

    /// <summary>
    /// The PUBLISHED identity of one person: the name FS27 shows. The authoring databases keep the research names (historical, never edited); this entry replaces them in the game
    /// data. The id never changes: it is the link to the authoring record. <see cref="OriginalName"/> is the authoring reference ONLY (for audit and for undoing the change): the
    /// game never shows it and nothing at runtime reads it.
    /// </summary>
    [Serializable]
    public sealed class IdentityEntry
    {
        public string Id = "";
        public IdentityKind Kind;
        public string Culture = "";
        public string OriginalName = "";
        /// <summary>The name shown (a player or coach: "Given Family"; a referee: the family name, as the referee data already does).</summary>
        public string Name = "";
        public string FullName = "";
        public string ShortName = "";
        /// <summary>True when a person changed this entry by hand. The generator never overwrites an edited entry, and the regeneration test skips it.</summary>
        public bool Edited;
    }

    /// <summary>The published identities, by id.</summary>
    public sealed class IdentityCatalog
    {
        private readonly Dictionary<string, IdentityEntry> byId = new Dictionary<string, IdentityEntry>();
        private readonly List<IdentityEntry> ordered = new List<IdentityEntry>();

        public IReadOnlyList<IdentityEntry> All => ordered;
        public int Count => ordered.Count;

        public bool TryAdd(IdentityEntry e)
        {
            if (e == null || !DataRules.IsValidId(e.Id) || byId.ContainsKey(e.Id)) return false;
            byId.Add(e.Id, e);
            ordered.Add(e);
            return true;
        }

        public bool TryGet(string id, out IdentityEntry entry) { entry = null; return id != null && byId.TryGetValue(id, out entry); }
        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }
        public int CountOf(IdentityKind kind) { int n = 0; foreach (IdentityEntry e in ordered) if (e.Kind == kind) n++; return n; }
    }

    /// <summary>The identities as JSON (strict reader, stable writer). They live in a data file so that names can be edited without touching code.</summary>
    public static class IdentityJson
    {
        public const string Schema = "FS27.Identities.v1";

        public static string ToJson(IdentityCatalog catalog, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            var list = JsonValue.NewArray();
            foreach (IdentityEntry e in catalog.All)
            {
                list.Add(JsonValue.NewObject().Set("id", JsonValue.Of(e.Id)).Set("kind", JsonValue.Of(e.Kind.ToString())).Set("culture", JsonValue.Of(e.Culture))
                    .Set("originalName", JsonValue.Of(e.OriginalName)).Set("name", JsonValue.Of(e.Name)).Set("fullName", JsonValue.Of(e.FullName))
                    .Set("shortName", JsonValue.Of(e.ShortName)).Set("edited", JsonValue.Of(e.Edited)));
            }
            root.Set("identities", list);
            return Json.Write(root, indented);
        }

        public static bool TryFromJson(string text, out IdentityCatalog catalog, out string error)
        {
            catalog = new IdentityCatalog();
            error = "";
            if (!Json.TryParse(text, out JsonValue root, out string parseError)) { error = parseError; return false; }
            try
            {
                if (root.Kind != JsonKind.Object || root.GetString("schemaVersion") != Schema) throw new FormatException("schemaVersion must be '" + Schema + "'.");
                if (!root.TryGet("identities", out JsonValue arr) || arr.Kind != JsonKind.Array) throw new FormatException("'identities' must be an array.");
                foreach (JsonValue o in arr.Items)
                {
                    var e = new IdentityEntry { Id = Str(o, "id"), Culture = Str(o, "culture"), OriginalName = Str(o, "originalName"), Name = Str(o, "name"), FullName = Str(o, "fullName"), ShortName = Str(o, "shortName") };
                    if (!Enum.TryParse(Str(o, "kind"), false, out IdentityKind kind) || !Enum.IsDefined(typeof(IdentityKind), kind)) throw new FormatException("Invalid kind '" + Str(o, "kind") + "'.");
                    e.Kind = kind;
                    if (!o.TryGet("edited", out JsonValue ed) || ed.Kind != JsonKind.Bool) throw new FormatException("'edited' must be true or false.");
                    e.Edited = ed.Bool;
                    if (!catalog.TryAdd(e)) throw new FormatException("Identity '" + e.Id + "' is invalid or repeated.");
                }
                return true;
            }
            catch (FormatException ex) { catalog = new IdentityCatalog(); error = ex.Message; return false; }
        }

        private static string Str(JsonValue o, string key)
        {
            if (o.Kind != JsonKind.Object || !o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }
    }

    /// <summary>One person the generator has to name: who (id and kind), where from (nationality code, for the name culture) and the research name (so the new name never repeats it).</summary>
    public struct IdentitySubject
    {
        public string Id;
        public IdentityKind Kind;
        public string NationalityCode;
        public string OriginalName;
        public IdentitySubject(string id, IdentityKind kind, string nationalityCode, string originalName) { Id = id; Kind = kind; NationalityCode = nationalityCode; OriginalName = originalName; }
    }

    /// <summary>
    /// The deterministic strategy for fictional names. For each person, in id order:
    ///  - the nationality code picks a name culture (<see cref="NamePools"/>), so the name fits where he is from;
    ///  - a generator seeded by the id (never by time or by chance) draws a given name and a family name from that culture;
    ///  - the draw is repeated (the same sequence, the next attempt) until the family name is unused in the whole database, the full name differs from the research name and no word of any
    ///    research name in the database is reused as a family name, so a published name never points at a real person;
    ///  - Spanish and Latin American names get a second family name in the full name; referees are shown by family name, like the existing referee data;
    ///  - the short name comes from <see cref="ShortNameRule"/> (unique among players, and among coaches).
    /// The same subjects always give the same identities. Entries a person edited by hand are kept exactly as they are.
    /// </summary>
    public static class IdentityGenerator
    {
        public const int MaxAttempts = 400;

        public static IdentityCatalog Generate(IReadOnlyList<IdentitySubject> subjects, IdentityCatalog edited, List<string> problems)
        {
            var result = new IdentityCatalog();
            var ordered = new List<IdentitySubject>(subjects);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var forbiddenWords = new HashSet<string>();
            foreach (IdentitySubject s in ordered) foreach (string w in WordsOf(s.OriginalName)) forbiddenWords.Add(ShortNameRule.Key(w));
            var usedFamily = new HashSet<string>();
            var givenUse = new Dictionary<string, int>();
            var usedFull = new HashSet<string>();
            var shortPlayers = new HashSet<string>();
            var shortCoaches = new HashSet<string>();
            var shortReferees = new HashSet<string>();

            // hand-edited entries keep their place and reserve their names
            if (edited != null)
                foreach (IdentityEntry e in edited.All)
                    if (e.Edited) { usedFull.Add(ShortNameRule.Key(e.FullName)); usedFamily.Add(ShortNameRule.Key(FamilyOf(e.Name))); Reserve(e, shortPlayers, shortCoaches, shortReferees); }

            foreach (IdentitySubject s in ordered)
            {
                if (edited != null && edited.TryGet(s.Id, out IdentityEntry keep) && keep.Edited) { result.TryAdd(keep); continue; }

                NameCulture culture = NamePools.CultureForCode(s.NationalityCode, out bool known);
                if (!known && problems != null) problems.Add("'" + s.Id + "': nationality '" + (s.NationalityCode ?? "") + "' has no name culture; the English-language pool was used.");
                NamePool pool = NamePools.Of(culture);
                bool twoFamilies = culture == NameCulture.Spanish || culture == NameCulture.LatinAmerican;
                string originalKey = ShortNameRule.Key(s.OriginalName);

                int baseSeed = Fnv(s.Id);
                string given = "", family = "", family2 = "";
                bool done = false;
                for (int attempt = 0; attempt < MaxAttempts && !done; attempt++)
                {
                    SeededRandom rng = SeedMixer.Create(baseSeed, attempt);
                    given = pool.Given[Pick(rng, pool.Given.Length)];
                    family = pool.Family[Pick(rng, pool.Family.Length)];
                    family2 = pool.Family[Pick(rng, pool.Family.Length)];
                    string familyKey = ShortNameRule.Key(family);
                    if (familyKey == ShortNameRule.Key(family2)) continue;
                    if (SharesWord(family, forbiddenWords) || SharesWord(family2, forbiddenWords)) continue;
                    string full = given + " " + family + (twoFamilies ? " " + family2 : "");
                    if (ShortNameRule.Key(given + " " + family) == originalKey || ShortNameRule.Key(full) == originalKey) continue;
                    if (usedFull.Contains(ShortNameRule.Key(full))) continue;
                    if (usedFamily.Contains(familyKey) && attempt < MaxAttempts / 2) continue;
                    // variety: a given name is repeated only after the draw has tried hard to find a fresh one
                    string givenKey = culture + "|" + ShortNameRule.Key(given);
                    givenUse.TryGetValue(givenKey, out int times);
                    if (times >= 1 && attempt < MaxAttempts / 4) continue;
                    if (times >= 2 && attempt < MaxAttempts / 2) continue;
                    done = true;
                }
                if (!done && problems != null) problems.Add("'" + s.Id + "': no free name was found in " + MaxAttempts + " attempts; the last draw was kept.");

                string fullName = given + " " + family + (twoFamilies ? " " + family2 : "");
                usedFull.Add(ShortNameRule.Key(fullName));
                usedFamily.Add(ShortNameRule.Key(family));
                string usedKey = culture + "|" + ShortNameRule.Key(given);
                givenUse[usedKey] = (givenUse.TryGetValue(usedKey, out int n) ? n : 0) + 1;

                var entry = new IdentityEntry { Id = s.Id, Kind = s.Kind, Culture = culture.ToString(), OriginalName = s.OriginalName ?? "", FullName = fullName };
                if (s.Kind == IdentityKind.Referee)
                {
                    entry.Name = family;
                    entry.ShortName = RefereeCode(family, shortReferees);
                }
                else
                {
                    entry.Name = given + " " + family;
                    entry.ShortName = ShortNameRule.DeriveUnique(entry.Name, s.Kind == IdentityKind.Coach ? shortCoaches : shortPlayers);
                }
                result.TryAdd(entry);
            }
            return result;
        }

        /// <summary>The three-letter code of a referee (the convention of the referee data): the first letters of the family name, accents removed, unique among referees.</summary>
        public static string RefereeCode(string family, ISet<string> taken)
        {
            string folded = ShortNameRule.Key(family).Replace(" ", "");
            string code = ShortNameRule.Fit(folded, 3);
            for (int skip = 1; !taken.Add(code) && skip < 20; skip++) code = ShortNameRule.Fit(folded, 2) + (folded.Length > 2 + skip ? folded.Substring(2 + skip, 1) : skip.ToString(CultureInfo.InvariantCulture));
            return code;
        }

        private static void Reserve(IdentityEntry e, HashSet<string> players, HashSet<string> coaches, HashSet<string> referees)
        {
            if (e.Kind == IdentityKind.Player) players.Add(ShortNameRule.Key(e.ShortName));
            else if (e.Kind == IdentityKind.Coach) coaches.Add(ShortNameRule.Key(e.ShortName));
            else referees.Add(ShortNameRule.Key(e.ShortName));
        }

        /// <summary>True when any word of the name (three letters or more) is one of the research names' words.</summary>
        private static bool SharesWord(string name, HashSet<string> words)
        {
            foreach (string w in WordsOf(name)) if (words.Contains(ShortNameRule.Key(w))) return true;
            return false;
        }

        private static string FamilyOf(string name)
        {
            string[] w = (name ?? "").Split(' ');
            int last = w.Length - 1;
            // a generational suffix ("Jr", "Junior", "Sr") is not a family name: "Name Jr" has the word before it
            if (last > 0 && IsGenerationalSuffix(w[last])) last--;
            return last >= 0 ? w[last] : "";
        }

        /// <summary>"Jr", "Jr.", "Junior", "Sr", "Sr." (ignoring case): a suffix that follows the name, never a family name by itself.</summary>
        public static bool IsGenerationalSuffix(string word)
        {
            string k = (word ?? "").Trim().TrimEnd('.').ToLowerInvariant();
            return k == "jr" || k == "junior" || k == "sr" || k == "senior";
        }

        private static IEnumerable<string> WordsOf(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) yield break;
            foreach (string w in name.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
                if (w.Length > 2) yield return w;
        }

        private static int Pick(SeededRandom rng, int count) { return Math.Min(count - 1, (int)(rng.NextFloat01() * count)); }

        /// <summary>FNV-1a over the id: a fixed, platform-independent number (string.GetHashCode is not).</summary>
        public static int Fnv(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in text ?? "") { h ^= c; h *= 16777619u; }
                return (int)h;
            }
        }
    }

    /// <summary>Puts the published identities into the game data. The ids are the link; a person with no identity is reported, never left half-renamed.</summary>
    public static class IdentityApplier
    {
        public static void ApplyPlayers(PlayerLibrary library, IdentityCatalog identities, List<string> problems)
        {
            foreach (PlayerDefinition p in library.All)
            {
                if (!identities.TryGet(p.Id, out IdentityEntry e) || e.Kind != IdentityKind.Player) { problems.Add("Player '" + p.Id + "' has no published identity."); continue; }
                p.Name = e.Name;
                p.ShortName = e.ShortName;
            }
        }

        public static void ApplyCoaches(CoachLibrary coaches, IdentityCatalog identities, List<string> problems)
        {
            foreach (CoachDefinition c in coaches.All)
            {
                if (!identities.TryGet(c.CoachId, out IdentityEntry e) || e.Kind != IdentityKind.Coach) { problems.Add("Coach '" + c.CoachId + "' has no published identity."); continue; }
                c.Name = e.Name;
                c.FullName = e.FullName;
                c.ShortName = e.ShortName;
            }
        }

        public static void ApplyReferees(RefereeDatabase referees, IdentityCatalog identities, List<string> problems)
        {
            foreach (RefereeDefinition r in referees.Referees)
            {
                if (!identities.TryGet(r.RefereeId, out IdentityEntry e) || e.Kind != IdentityKind.Referee) { problems.Add("Referee '" + r.RefereeId + "' has no published identity."); continue; }
                r.Name = e.Name;
                r.FullName = e.FullName;
                r.ShortName = e.ShortName;
            }
        }
    }
}
