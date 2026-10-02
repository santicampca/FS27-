using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// Every kind of problem the field-player importer reports. ERROR = a valid player cannot be built (nothing is imported for that record).
    /// WARNING = it can be built, but something was defaulted, normalised, ambiguous or is not supported yet (the issue says what, and what was used).
    /// </summary>
    public enum FieldPlayerImportCode
    {
        // ---------------- errors ----------------
        RecordMissing = 0,
        PlayerIdInvalid,
        NameInvalid,
        /// <summary>An attribute of the official 12 is not in the record.</summary>
        AttributeMissing,
        /// <summary>The record has an attribute that is not one of the 12 (Tackling, Heading, Positioning, Reaction...).</summary>
        AttributeUnknown,
        AttributeOutOfRange,
        PrimaryZoneInvalid,
        SecondaryZoneInvalid,
        /// <summary>A valid zone that this database does not allow as its secondary zone (a full-back's secondary zone is Defense or Midfield).</summary>
        SecondaryZoneNotAllowed,
        /// <summary>A role that is not one of the 12 official roles, or not allowed for this database (a defender database allows Wall, Guardian, Anchor).</summary>
        RoleNotAllowed,
        RoleCountInvalid,
        RoleDuplicate,
        AffinityMissing,
        /// <summary>The source lists the roles without affinities and the database allows the project default by rank (90 / 75 / 60): not researched values.</summary>
        AffinityDefaulted,
        AffinityOutOfRange,
        /// <summary>Risk, creativity, aggression or weak foot outside 0..100.</summary>
        ProfileValueOutOfRange,
        PreferredFootInvalid,
        /// <summary>The resulting <see cref="PlayerDefinition"/> or <see cref="PlayerPlayingProfile"/> fails the normal validation.</summary>
        PlayerInvalid,
        DuplicatePlayerId,

        // ---------------- warnings ----------------
        /// <summary>The design target is not the calculated overall by more than the tolerance. The game uses the calculated one.</summary>
        TargetOvrDiffers,
        /// <summary>The source lists more than one secondary zone ("Midfield / Wing"): the first is used (only one is allowed).</summary>
        SecondaryZoneAmbiguous,
        /// <summary>The real secondary positions reach a zone the secondary zone does not cover; kept in the authoring record only.</summary>
        PolyvalenceBeyondZone,
        RolesNotOrderedByAffinity,
        /// <summary>The stated age differs from the date of birth at the reference date; the date of birth wins.</summary>
        AgeMismatch,
        BodyTypeUnrecognised,
        ShirtNumberDefaulted,
        AgeDefaulted,
        WeightDefaulted,
        HeightDefaulted,
        WeakFootDefaulted,
        PreferredFootDefaulted,
        /// <summary>The foot is ambiguous ("Izquierdo / Derecho", "ambidiestro", "both"): the default is kept and the original text is reported. Never turned into Left.</summary>
        PreferredFootAmbiguous,
        BehaviourProfileDefaulted,
        ShortNameDerived,
        NationalityUnknown,
        /// <summary>A signature action is not in the vocabulary.</summary>
        SignatureActionUnknown,
        /// <summary>Two actions normalise to the same tag: kept once.</summary>
        SignatureActionDuplicate,
        /// <summary>More actions than the allowed maximum: the first ones are kept.</summary>
        SignatureActionsTrimmed,
        /// <summary>The source lists no signature action: none is made up.</summary>
        SignatureActionsAbsent,
        /// <summary>The source has no body label: the type was derived from height and weight only (see <see cref="BodyTypeNormalizer"/>).</summary>
        BodyTypeDerivedFromMeasurements,
        NationalityMissing,
        /// <summary>These normalised tags have no football behaviour yet. They stay as authoring tags; nothing is invented.</summary>
        SignatureActionsPending
    }

    public sealed class FieldPlayerImportIssue
    {
        public FieldPlayerImportCode Code;
        public bool IsError;
        public string PlayerId = "";
        public string Field = "";
        public string Value = "";
        public string Message = "";
        public override string ToString() { return (IsError ? "ERROR " : "warning ") + Code + " [" + PlayerId + " / " + Field + "]: " + Message; }
    }

    /// <summary>The design target against the official overall. They are never mixed: the game's overall is always the calculated one.</summary>
    public sealed class OverallCheck
    {
        public string PlayerId = "";
        public bool HasTarget;
        public int TargetOvr;
        public int CalculatedOverall;
        /// <summary>Calculated minus target (0 without a target).</summary>
        public int Difference;
        public bool Matches => HasTarget && Difference == 0;
    }

    /// <summary>How a combined body label became one official <see cref="BodyType"/>.</summary>
    public sealed class BodyTypeNormalization
    {
        public string Label = "";
        public BodyType Result;
        public string Rule = "";
    }

    /// <summary>Settings of one import. <see cref="Defender"/> is the contract of the defender database.</summary>
    public sealed class FieldPlayerImportOptions
    {
        public PlayerRole Role = PlayerRole.Defender;
        /// <summary>Roles the database may use (null = any of the 12 official).</summary>
        public HashSet<PlayerArchetype> AllowedRoles;
        /// <summary>Zones a primary zone may be (null = any except Goal).</summary>
        public HashSet<PitchZone> AllowedPrimaryZones;
        /// <summary>Zones the secondary zone may be (null = any except Goal and the primary zone).</summary>
        public HashSet<PitchZone> AllowedSecondaryZones;
        /// <summary>The "today" of the source, to check stated ages against dates of birth.</summary>
        public DateTime ReferenceDate = new DateTime(2026, 10, 2);
        /// <summary>Allowed distance between target and calculated overall before a warning (a starting value, not a rule).</summary>
        public int OverallTolerance = 5;
        public int MaxSignatureActions = 5;
        /// <summary>When the source lists roles without affinities: use the project default by rank and warn, instead of an error. Off for every database that researches its affinities.</summary>
        public bool DefaultMissingAffinities;
        public int ShirtNumberDefault = 99;
        public IReadOnlyDictionary<string, string> ActionVocabulary = DefenderVocabulary.Create();
        public IReadOnlyDictionary<string, string> TagBehaviors = DefenderVocabulary.TagBehaviors();
        public SignatureBehaviorCatalog Behaviors = DefaultBehaviors.Create();
        public PlayerRatingCalculator Calculator = PlayerRatingCalculator.CreateDefault();

        /// <summary>Full-backs: Wing as the primary zone, Defense or Midfield as the one optional secondary zone, any of the 12 official roles, wing-play action vocabulary.</summary>
        public static FieldPlayerImportOptions Fullback()
        {
            return new FieldPlayerImportOptions
            {
                Role = PlayerRole.Defender,
                AllowedRoles = null,
                AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Wing },
                AllowedSecondaryZones = new HashSet<PitchZone> { PitchZone.Defense, PitchZone.Midfield },
                ActionVocabulary = WingPlayVocabulary.Create(),
                TagBehaviors = WingPlayVocabulary.TagBehaviors()
            };
        }

        /// <summary>Hybrid midfielders (the MDI / MDD database): Midfield as the primary zone, Defense or Wing as the one optional secondary zone, any of the 12 official roles, midfield-play action vocabulary.</summary>
        public static FieldPlayerImportOptions HybridMidfielder()
        {
            return new FieldPlayerImportOptions
            {
                Role = PlayerRole.Midfielder,
                AllowedRoles = null,
                AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Midfield },
                AllowedSecondaryZones = new HashSet<PitchZone> { PitchZone.Defense, PitchZone.Wing },
                ActionVocabulary = MidfieldPlayVocabulary.Create(),
                TagBehaviors = MidfieldPlayVocabulary.TagBehaviors()
            };
        }

        /// <summary>Wingers and wide forwards (the ED / EI database): Wing primary, Attack or Midfield as the one optional secondary zone, any of the 12 roles, attacking-play vocabulary, and the project default affinities when the source gives none.</summary>
        public static FieldPlayerImportOptions Winger()
        {
            FieldPlayerImportOptions o = HybridMidfielder();
            o.Role = PlayerRole.Forward;
            o.AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Wing };
            o.AllowedSecondaryZones = new HashSet<PitchZone> { PitchZone.Attack, PitchZone.Midfield };
            o.DefaultMissingAffinities = true;
            o.ActionVocabulary = AttackPlayVocabulary.Create();
            o.TagBehaviors = AttackPlayVocabulary.TagBehaviors();
            return o;
        }

        /// <summary>Second strikers (the SD database): Attack primary, Midfield or Wing as the one optional secondary zone, any of the 12 roles; same vocabulary and default affinities as <see cref="Winger"/>.</summary>
        public static FieldPlayerImportOptions SecondStriker()
        {
            FieldPlayerImportOptions o = Winger();
            o.AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Attack };
            o.AllowedSecondaryZones = new HashSet<PitchZone> { PitchZone.Midfield, PitchZone.Wing };
            return o;
        }

        /// <summary>Defensive midfielders (the MCD database): the same contract as <see cref="HybridMidfielder"/> (Midfield primary, Defense or Wing secondary, any of the 12 roles).</summary>
        public static FieldPlayerImportOptions DefensiveMidfielder() { return HybridMidfielder(); }

        /// <summary>Central midfielders (the MC database): Midfield primary, Defense, Wing or Attack as the one optional secondary zone, any of the 12 roles.</summary>
        public static FieldPlayerImportOptions CentralMidfielder()
        {
            FieldPlayerImportOptions o = HybridMidfielder();
            o.AllowedSecondaryZones = new HashSet<PitchZone> { PitchZone.Defense, PitchZone.Wing, PitchZone.Attack };
            return o;
        }

        /// <summary>Offensive midfielders (the MCO database): the same contract as <see cref="CentralMidfielder"/> (Midfield primary; Defense, Wing or Attack secondary; any of the 12 roles).</summary>
        public static FieldPlayerImportOptions OffensiveMidfielder() { return CentralMidfielder(); }

        public static FieldPlayerImportOptions Defender()
        {
            return new FieldPlayerImportOptions
            {
                Role = PlayerRole.Defender,
                AllowedRoles = new HashSet<PlayerArchetype> { PlayerArchetype.Wall, PlayerArchetype.Guardian, PlayerArchetype.Anchor },
                AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Defense }
            };
        }
    }

    /// <summary>What importing one field-player record produced. Runtime data (<see cref="Player"/>, <see cref="Profile"/>, <see cref="Dna"/>) plus the checks and warnings.</summary>
    public sealed class FieldPlayerImportResult
    {
        public string PlayerId = "";
        public PlayerDefinition Player;
        public PlayerPlayingProfile Profile;
        /// <summary>References to existing football behaviours only; null when the record has errors.</summary>
        public FootballDNA Dna;
        public OverallCheck Overall;
        public BodyTypeNormalization BodyType;
        /// <summary>The normalised signature tags (authoring information, not runtime data).</summary>
        public readonly List<string> SignatureTags = new List<string>();
        /// <summary>Tags with no football behaviour yet.</summary>
        public readonly List<string> PendingTags = new List<string>();
        public readonly List<DefaultedValue> Defaulted = new List<DefaultedValue>();
        public readonly List<FieldPlayerImportIssue> Issues = new List<FieldPlayerImportIssue>();

        public bool HasErrors { get { foreach (FieldPlayerImportIssue i in Issues) if (i.IsError) return true; return false; } }
        public bool IsComplete => !HasErrors && Player != null && Profile != null && Dna != null;
        public bool Has(FieldPlayerImportCode code) { foreach (FieldPlayerImportIssue i in Issues) if (i.Code == code) return true; return false; }
        public int Count(FieldPlayerImportCode code) { int n = 0; foreach (FieldPlayerImportIssue i in Issues) if (i.Code == code) n++; return n; }
    }

    /// <summary>
    /// The boundary between authoring and runtime for field players. It validates a <see cref="FieldPlayerDataRecord"/> and builds the runtime
    /// objects (<see cref="PlayerDefinition"/>, <see cref="PlayerPlayingProfile"/>, <see cref="FootballDNA"/>) without inventing anything: an attribute
    /// is never defaulted, a value the source lacks is defaulted explicitly and reported, a combined label is normalised by a visible rule. The overall is
    /// asked of <see cref="PlayerRatingCalculator"/>; the target is only compared. Nothing here knows a player by name or id.
    /// </summary>
    public static class FieldPlayerRecordImporter
    {
        public static FieldPlayerImportResult Import(FieldPlayerDataRecord rec, FieldPlayerImportOptions options = null)
        {
            options = options ?? FieldPlayerImportOptions.Defender();
            var res = new FieldPlayerImportResult();
            if (rec == null) { Add(res, FieldPlayerImportCode.RecordMissing, true, "record", "", "Missing."); return res; }
            res.PlayerId = rec.PlayerId ?? "";

            if (!DataRules.IsValidId(rec.PlayerId)) Add(res, FieldPlayerImportCode.PlayerIdInvalid, true, "playerId", rec.PlayerId, "Player id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(rec.Name)) Add(res, FieldPlayerImportCode.NameInvalid, true, "name", rec.Name, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            PlayerAttributes attrs = ReadAttributes(rec, res);
            PlayerPlayingProfile profile = ReadProfile(rec, options, res);
            ReadSignatureActions(rec, options, res);
            if (res.HasErrors) return res;

            PlayerDefinition player = BuildPlayer(rec, attrs, options, res);
            if (res.HasErrors) return res;
            var profileCheck = PlayingProfileValidator.Validate(profile, player);
            foreach (AiDataIssue i in profileCheck.Issues) Add(res, FieldPlayerImportCode.PlayerInvalid, true, "profile", "", i.ToString());
            foreach (ValidationIssue i in DataValidator.ValidatePlayer(player).Issues) Add(res, FieldPlayerImportCode.PlayerInvalid, true, "player", "", i.ToString());
            if (res.HasErrors) return res;

            res.Player = player;
            res.Profile = profile;
            res.Overall = Check(rec, player, profile, options, res);
            if (rec.SignatureActions == null || rec.SignatureActions.Count == 0) Add(res, FieldPlayerImportCode.SignatureActionsAbsent, false, "signatureActions", "", "The source lists no signature action; none is made up.");
            res.Dna = new FootballDNA();
            foreach (string tag in res.SignatureTags)
                if (options.TagBehaviors != null && options.TagBehaviors.TryGetValue(tag, out string behaviorId) && options.Behaviors.Contains(behaviorId))
                    res.Dna.SetBehavior(behaviorId, 1f);
                else res.PendingTags.Add(tag);
            if (res.PendingTags.Count > 0)
                Add(res, FieldPlayerImportCode.SignatureActionsPending, false, "signatureActions", string.Join(", ", res.PendingTags), "No football behaviour exists yet for: " + string.Join(", ", res.PendingTags) + ". They stay as authoring tags.");
            return res;
        }

        // ------------------------------------------------------------------ overall

        private static OverallCheck Check(FieldPlayerDataRecord rec, PlayerDefinition player, PlayerPlayingProfile profile, FieldPlayerImportOptions o, FieldPlayerImportResult res)
        {
            var check = new OverallCheck { PlayerId = rec.PlayerId, CalculatedOverall = o.Calculator.GetOverall(player, profile) };
            if (rec.TargetOvr.HasValue)
            {
                check.HasTarget = true;
                check.TargetOvr = rec.TargetOvr.Value;
                check.Difference = check.CalculatedOverall - check.TargetOvr;
                if (Math.Abs(check.Difference) > o.OverallTolerance)
                    Add(res, FieldPlayerImportCode.TargetOvrDiffers, false, "targetOvr", check.TargetOvr.ToString(CultureInfo.InvariantCulture),
                        "Design target " + check.TargetOvr + ", calculated overall " + check.CalculatedOverall + " (difference " + check.Difference.ToString("+0;-0", CultureInfo.InvariantCulture) + "). The game uses " + check.CalculatedOverall + ".");
            }
            return check;
        }

        // ------------------------------------------------------------------ attributes

        private static PlayerAttributes ReadAttributes(FieldPlayerDataRecord rec, FieldPlayerImportResult res)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
            {
                string name = id == PlayerAttributeId.Control ? "BallControl" : id.ToString();
                if (rec.Attributes == null || !rec.Attributes.TryGetValue(name, out int v)) { Add(res, FieldPlayerImportCode.AttributeMissing, true, name, "", "Not given: an attribute is never defaulted."); continue; }
                if (v < PlayerAttributes.Min || v > PlayerAttributes.Max) { Add(res, FieldPlayerImportCode.AttributeOutOfRange, true, name, v.ToString(CultureInfo.InvariantCulture), "Outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + "."); continue; }
                a = a.With(id, v);
            }
            if (rec.Attributes != null)
                foreach (string key in rec.Attributes.Keys)
                {
                    bool known = key == "BallControl" || (Enum.TryParse(key, false, out PlayerAttributeId id) && Enum.IsDefined(typeof(PlayerAttributeId), id) && id != PlayerAttributeId.Control);
                    if (!known) Add(res, FieldPlayerImportCode.AttributeUnknown, true, key, "", "Not one of the 12 official attributes.");
                }
            return a;
        }

        // ------------------------------------------------------------------ playing profile

        private static PlayerPlayingProfile ReadProfile(FieldPlayerDataRecord rec, FieldPlayerImportOptions o, FieldPlayerImportResult res)
        {
            var p = new PlayerPlayingProfile { PlayerId = rec.PlayerId };

            if (!TryZone(rec.PrimaryZone, out PitchZone primary) || primary == PitchZone.Goal || (o.AllowedPrimaryZones != null && !o.AllowedPrimaryZones.Contains(primary)))
                Add(res, FieldPlayerImportCode.PrimaryZoneInvalid, true, "primaryZone", rec.PrimaryZone, "'" + rec.PrimaryZone + "' is not an allowed primary zone for this database.");
            else p.PrimaryZone = primary;

            p.SecondaryZones = new List<PitchZone>();
            if (!string.IsNullOrWhiteSpace(rec.SecondaryZone))
            {
                string[] parts = rec.SecondaryZone.Split('/');
                if (parts.Length > 1)
                    Add(res, FieldPlayerImportCode.SecondaryZoneAmbiguous, false, "secondaryZone", rec.SecondaryZone, "'" + rec.SecondaryZone + "' lists several zones; only one is allowed, using '" + parts[0].Trim() + "'.");
                if (!TryZone(parts[0], out PitchZone secondary) || secondary == PitchZone.Goal || secondary == p.PrimaryZone)
                    Add(res, FieldPlayerImportCode.SecondaryZoneInvalid, true, "secondaryZone", rec.SecondaryZone, "'" + parts[0].Trim() + "' is not a valid secondary zone.");
                else if (o.AllowedSecondaryZones != null && !o.AllowedSecondaryZones.Contains(secondary))
                    Add(res, FieldPlayerImportCode.SecondaryZoneNotAllowed, true, "secondaryZone", rec.SecondaryZone, "'" + secondary + "' is not an allowed secondary zone for this database.");
                else p.SecondaryZones.Add(secondary);
            }
            CheckPolyvalence(rec, p, res);

            int count = rec.Roles == null ? 0 : rec.Roles.Count;
            if (count < 1 || count > PlayerPlayingProfile.MaxArchetypes)
                Add(res, FieldPlayerImportCode.RoleCountInvalid, true, "roles", count.ToString(CultureInfo.InvariantCulture), "A player needs 1-" + PlayerPlayingProfile.MaxArchetypes + " roles, found " + count + ".");
            p.Roles = new List<RoleAffinity>();
            var seen = new HashSet<PlayerArchetype>();
            int previous = int.MaxValue;
            bool ordered = true;
            if (rec.Roles != null)
                foreach (AuthoredRole r in rec.Roles)
                {
                    if (!Enum.TryParse(r.Role, false, out PlayerArchetype role) || !Enum.IsDefined(typeof(PlayerArchetype), role) || (o.AllowedRoles != null && !o.AllowedRoles.Contains(role)))
                    { Add(res, FieldPlayerImportCode.RoleNotAllowed, true, "roles", r.Role, "'" + r.Role + "' is not an allowed role for this database."); continue; }
                    if (!seen.Add(role)) { Add(res, FieldPlayerImportCode.RoleDuplicate, true, "roles", r.Role, "Role listed twice."); continue; }
                    int? given = r.Affinity;
                    if (!given.HasValue && o.DefaultMissingAffinities)
                    {
                        int[] byRank = PlayerPlayingProfile.DefaultAffinityByRank;
                        given = byRank[Math.Min(p.Roles.Count, byRank.Length - 1)];
                        Add(res, FieldPlayerImportCode.AffinityDefaulted, false, "roles", r.Role, "The source gives no affinity: the project default for rank " + (p.Roles.Count + 1) + " (" + given.Value + ") is used. It is not researched.");
                    }
                    if (!given.HasValue) { Add(res, FieldPlayerImportCode.AffinityMissing, true, "roles", r.Role, "A role needs an affinity."); continue; }
                    if (given.Value < 0 || given.Value > 100) { Add(res, FieldPlayerImportCode.AffinityOutOfRange, true, "roles", r.Role, "Affinity " + given.Value + " is outside 0..100."); continue; }
                    if (given.Value > previous) ordered = false;
                    previous = given.Value;
                    p.Roles.Add(new RoleAffinity(role, given.Value));
                }
            if (!ordered) Add(res, FieldPlayerImportCode.RolesNotOrderedByAffinity, false, "roles", "", "The roles are not listed from the highest affinity to the lowest.");

            p.RiskPreference = Behaviour(rec.RiskPreference, "riskPreference", p.RiskPreference, res);
            p.Creativity = Behaviour(rec.Creativity, "creativity", p.Creativity, res);
            p.Aggression = Behaviour(rec.Aggression, "aggression", p.Aggression, res);
            return p;
        }

        private static int Behaviour(int? value, string field, int fallback, FieldPlayerImportResult res)
        {
            if (!value.HasValue)
            {
                Defaulted(res, FieldPlayerImportCode.BehaviourProfileDefaulted, field, fallback.ToString(CultureInfo.InvariantCulture), "The source has no value.");
                return fallback;
            }
            if (value.Value < 0 || value.Value > 100) { Add(res, FieldPlayerImportCode.ProfileValueOutOfRange, true, field, value.Value.ToString(CultureInfo.InvariantCulture), "Outside 0..100."); return fallback; }
            return value.Value;
        }

        /// <summary>Real secondary positions are authoring information. Warn when one of them belongs to a zone the profile does not declare.</summary>
        private static void CheckPolyvalence(FieldPlayerDataRecord rec, PlayerPlayingProfile p, FieldPlayerImportResult res)
        {
            if (rec.SecondaryPositions == null) return;
            foreach (string pos in rec.SecondaryPositions)
            {
                if (!ZoneOfPosition(pos, out PitchZone zone)) continue;
                if (zone != p.PrimaryZone && !p.SecondaryZones.Contains(zone))
                    Add(res, FieldPlayerImportCode.PolyvalenceBeyondZone, false, "secondaryPositions", pos, "Real position " + pos + " (" + zone + ") is beyond the one allowed secondary zone; kept in the authoring record only.");
            }
        }

        private static bool ZoneOfPosition(string pos, out PitchZone zone)
        {
            zone = PitchZone.Defense;
            switch ((pos ?? "").Trim().ToUpperInvariant())
            {
                case "CB": zone = PitchZone.Defense; return true;
                case "RB": case "LB": case "RWB": case "LWB": case "RW": case "LW": zone = PitchZone.Wing; return true;
                case "CDM": case "CM": case "CAM": case "RM": case "LM": zone = PitchZone.Midfield; return true;
                case "ST": case "CF": case "SD": case "DC": zone = PitchZone.Attack; return true;
                case "ED": case "EI": zone = PitchZone.Wing; return true;
                case "MCO": case "MC": case "MCD": zone = PitchZone.Midfield; return true;
                default: return false;
            }
        }

        private static bool TryZone(string text, out PitchZone zone)
        {
            zone = PitchZone.Defense;
            return Enum.TryParse((text ?? "").Trim(), true, out zone) && Enum.IsDefined(typeof(PitchZone), zone) && int.TryParse((text ?? "").Trim(), out _) == false;
        }

        // ------------------------------------------------------------------ player

        private static PlayerDefinition BuildPlayer(FieldPlayerDataRecord rec, PlayerAttributes attrs, FieldPlayerImportOptions o, FieldPlayerImportResult res)
        {
            var neutral = new PlayerDefinition();
            var p = new PlayerDefinition(rec.PlayerId, rec.Name, 0, o.Role, attrs);

            if (rec.ShirtNumber.HasValue) p.Number = rec.ShirtNumber.Value;
            else { p.Number = o.ShirtNumberDefault; Defaulted(res, FieldPlayerImportCode.ShirtNumberDefaulted, "shirtNumber", Inv(p.Number), "The source has no shirt number."); }

            p.Age = ResolveAge(rec, neutral, o, res);

            if (rec.WeightKg.HasValue) p.WeightKg = rec.WeightKg.Value;
            else { p.WeightKg = neutral.WeightKg; Defaulted(res, FieldPlayerImportCode.WeightDefaulted, "weightKg", Inv(p.WeightKg), "The source has no weight."); }
            if (rec.HeightCm.HasValue) p.HeightCm = rec.HeightCm.Value;
            else { p.HeightCm = neutral.HeightCm; Defaulted(res, FieldPlayerImportCode.HeightDefaulted, "heightCm", Inv(p.HeightCm), "The source has no height."); }

            if (rec.WeakFootQuality.HasValue)
            {
                if (rec.WeakFootQuality.Value < PlayerRules.MinWeakFoot || rec.WeakFootQuality.Value > PlayerRules.MaxWeakFoot) Add(res, FieldPlayerImportCode.ProfileValueOutOfRange, true, "weakFootQuality", Inv(rec.WeakFootQuality.Value), "Outside 0..100.");
                else p.WeakFootQuality = rec.WeakFootQuality.Value;
            }
            else { p.WeakFootQuality = neutral.WeakFootQuality; Defaulted(res, FieldPlayerImportCode.WeakFootDefaulted, "weakFootQuality", Inv(p.WeakFootQuality), "The source has no weak-foot quality."); }

            p.PreferredFoot = neutral.PreferredFoot;
            if (string.IsNullOrEmpty(rec.PreferredFoot)) Defaulted(res, FieldPlayerImportCode.PreferredFootDefaulted, "preferredFoot", p.PreferredFoot.ToString(), "The source has no preferred foot.");
            else
            {
                string f = GoalkeeperRecordImporter.Normalize(rec.PreferredFoot);
                if (f == "right" || f == "derecho") p.PreferredFoot = PreferredFoot.Right;
                else if (f == "left" || f == "izquierdo") p.PreferredFoot = PreferredFoot.Left;
                else if (IsAmbiguousFoot(f)) Add(res, FieldPlayerImportCode.PreferredFootAmbiguous, false, "preferredFoot", rec.PreferredFoot, "'" + rec.PreferredFoot + "' is not a single foot; keeping " + p.PreferredFoot + " (never turned into Left).");
                else Add(res, FieldPlayerImportCode.PreferredFootInvalid, true, "preferredFoot", rec.PreferredFoot, "'" + rec.PreferredFoot + "' is not a foot.");
            }

            if (!string.IsNullOrEmpty(rec.ShortName)) p.ShortName = rec.ShortName;
            else Add(res, FieldPlayerImportCode.ShortNameDerived, false, "shortName", p.ResolveShortName(), "The source has no short name; derived from the name.");

            if (!string.IsNullOrEmpty(rec.Nationality))
            {
                if (NationalityCodes.TryGet(rec.Nationality, out string code)) p.NationalityCode = code;
                else Add(res, FieldPlayerImportCode.NationalityUnknown, false, "nationality", rec.Nationality, "No code for '" + rec.Nationality + "'; left empty.");
            }
            else Add(res, FieldPlayerImportCode.NationalityMissing, false, "nationality", "", "The source has no nationality; left empty.");

            BodyTypeNormalization bt = BodyTypeNormalizer.Normalize(rec.BodyTypeLabel, p.HeightCm, p.WeightKg);
            res.BodyType = bt;
            if (bt.Rule == BodyTypeNormalizer.MeasurementsRule) Add(res, FieldPlayerImportCode.BodyTypeDerivedFromMeasurements, false, "bodyType", bt.Result.ToString(), "No body label in the source; " + bt.Result + " from height and weight only.");
            else if (bt.Rule == BodyTypeNormalizer.UnrecognisedRule) Add(res, FieldPlayerImportCode.BodyTypeUnrecognised, false, "bodyType", rec.BodyTypeLabel, "'" + rec.BodyTypeLabel + "' is not recognised; using " + bt.Result + ".");
            p.BodyType = bt.Result;
            return p;
        }

        private static bool IsAmbiguousFoot(string normalised)
        {
            bool left = normalised.Contains("izquierd") || normalised.Contains("left"), right = normalised.Contains("derech") || normalised.Contains("right");
            return (left && right) || normalised.Contains("ambi") || normalised.Contains("both") || normalised.Contains("ambos");
        }

        private static int ResolveAge(FieldPlayerDataRecord rec, PlayerDefinition neutral, FieldPlayerImportOptions o, FieldPlayerImportResult res)
        {
            if (DateTime.TryParseExact(rec.DateOfBirth ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob))
            {
                int age = o.ReferenceDate.Year - dob.Year - (o.ReferenceDate < dob.AddYears(o.ReferenceDate.Year - dob.Year) ? 1 : 0);
                if (rec.AgeAsWritten.HasValue && rec.AgeAsWritten.Value != age)
                    Add(res, FieldPlayerImportCode.AgeMismatch, false, "age", Inv(rec.AgeAsWritten.Value), "Stated age " + rec.AgeAsWritten.Value + " but the date of birth gives " + age + " on " + o.ReferenceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "; using " + age + ".");
                return age;
            }
            if (rec.AgeAsWritten.HasValue) return rec.AgeAsWritten.Value;
            Defaulted(res, FieldPlayerImportCode.AgeDefaulted, "age", Inv(neutral.Age), "The source has no age or date of birth.");
            return neutral.Age;
        }

        // ------------------------------------------------------------------ signature actions

        private static void ReadSignatureActions(FieldPlayerDataRecord rec, FieldPlayerImportOptions o, FieldPlayerImportResult res)
        {
            if (rec.SignatureActions == null) return;
            foreach (string label in rec.SignatureActions)
            {
                if (o.ActionVocabulary == null || !o.ActionVocabulary.TryGetValue(GoalkeeperRecordImporter.Normalize(label), out string tag))
                { Add(res, FieldPlayerImportCode.SignatureActionUnknown, false, "signatureActions", label, "'" + label + "' is not in the vocabulary; left out."); continue; }
                if (res.SignatureTags.Contains(tag)) { Add(res, FieldPlayerImportCode.SignatureActionDuplicate, false, "signatureActions", label, "'" + label + "' normalises to " + tag + ", already listed."); continue; }
                if (res.SignatureTags.Count >= o.MaxSignatureActions) { Add(res, FieldPlayerImportCode.SignatureActionsTrimmed, false, "signatureActions", label, "More than " + o.MaxSignatureActions + " actions; '" + label + "' is left out."); continue; }
                res.SignatureTags.Add(tag);
            }
        }

        private static string Inv(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        private static void Defaulted(FieldPlayerImportResult res, FieldPlayerImportCode code, string field, string value, string reason)
        {
            res.Defaulted.Add(new DefaultedValue { PlayerId = res.PlayerId, Field = field, Value = value, Reason = reason });
            res.Issues.Add(new FieldPlayerImportIssue { Code = code, IsError = false, PlayerId = res.PlayerId, Field = field, Value = value, Message = "Default " + value + " used. " + reason });
        }

        private static void Add(FieldPlayerImportResult res, FieldPlayerImportCode code, bool error, string field, string value, string message)
        {
            res.Issues.Add(new FieldPlayerImportIssue { Code = code, IsError = error, PlayerId = res.PlayerId, Field = field, Value = value ?? "", Message = message });
        }
    }

    /// <summary>
    /// Turns the source's combined body label ("Heavy / Robust Tank", "Athletic / Compact") into ONE of the five official body types, by visible
    /// rules that use the label and the player's height and weight (no new type is invented). Rules, in order:
    /// "Tall" in the label -> Tall; "Compact" -> Compact; first word Heavy/Robust or "Tank" in the label -> Strong; Robust/Power with a BMI of
    /// <see cref="StrongBmi"/> or more -> Strong; first word Lean/Agile with a BMI under <see cref="LightBmi"/> -> Light; anything else recognised -> Athletic.
    /// </summary>
    public static class BodyTypeNormalizer
    {
        public const float StrongBmi = 25.5f;
        public const float LightBmi = 22f;
        public const string UnrecognisedRule = "unrecognised";
        public const string MeasurementsRule = "no label: from height and weight only";
        /// <summary>Only for a source WITHOUT a body label: from this height up the build is Tall.</summary>
        public const int TallCm = 195;
        /// <summary>Only for a source WITHOUT a body label: a player this short or shorter with a BMI of <see cref="CompactMinBmi"/> or more is Compact.</summary>
        public const int CompactMaxCm = 176;
        public const float CompactMinBmi = 24f;

        public static BodyTypeNormalization Normalize(string label, int heightCm, int weightKg)
        {
            var n = new BodyTypeNormalization { Label = label ?? "" };
            string l = (label ?? "").ToLowerInvariant();
            // a label that already is one of the five official types (and only that) is used as it is
            foreach (BodyType official in Enum.GetValues(typeof(BodyType)))
                if (l.Trim() == official.ToString().ToLowerInvariant()) { n.Result = official; n.Rule = "official label"; return n; }
            var words = l.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
            float h = heightCm / 100f;
            float bmi = h > 0f ? weightKg / (h * h) : 0f;
            if (words.Length == 0)
            {
                // a source with no label at all: the label rules cannot run, so the same BMI limits decide, plus a height for Tall and a short sturdy build for Compact
                n.Rule = MeasurementsRule;
                if (heightCm <= 0 || weightKg <= 0) { n.Result = BodyType.Athletic; n.Rule = UnrecognisedRule; }
                else if (heightCm >= TallCm) n.Result = BodyType.Tall;
                else if (heightCm <= CompactMaxCm && bmi >= CompactMinBmi) n.Result = BodyType.Compact;
                else if (bmi >= StrongBmi) n.Result = BodyType.Strong;
                else if (bmi < LightBmi) n.Result = BodyType.Light;
                else n.Result = BodyType.Athletic;
                return n;
            }

            string first = words[0];
            if (Has(words, "tall")) { n.Result = BodyType.Tall; n.Rule = "label says Tall"; }
            else if (Has(words, "compact")) { n.Result = BodyType.Compact; n.Rule = "label says Compact"; }
            else if (first == "heavy" || first == "robust" || Has(words, "tank")) { n.Result = BodyType.Strong; n.Rule = "label starts Heavy/Robust or says Tank"; }
            else if ((Has(words, "robust") || Has(words, "power")) && bmi >= StrongBmi) { n.Result = BodyType.Strong; n.Rule = "Robust/Power label and BMI >= " + StrongBmi.ToString("0.0", CultureInfo.InvariantCulture); }
            else if ((first == "lean" || first == "agile") && bmi < LightBmi) { n.Result = BodyType.Light; n.Rule = "Lean/Agile label and BMI < " + LightBmi.ToString("0.0", CultureInfo.InvariantCulture); }
            else if (first == "lean" || first == "agile" || first == "athletic" || Has(words, "robust") || Has(words, "power") || Has(words, "balanced") || Has(words, "elegant")) { n.Result = BodyType.Athletic; n.Rule = "athletic build (default for the recognised labels)"; }
            else { n.Result = BodyType.Athletic; n.Rule = UnrecognisedRule; }
            return n;
        }

        private static bool Has(string[] words, string w) { return Array.IndexOf(words, w) >= 0; }
    }
}
