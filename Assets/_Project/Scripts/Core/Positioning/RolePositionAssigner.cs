using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Which player of the starting six stands in which slot of the formation (Phase 9). The goalkeeper takes the goal slot; the five field players take the five field slots, in the order of the
    /// slots (by lineup index): <see cref="FieldPlayerIds"/>[k] is the player of the k-th field slot. A derived, immutable answer: it changes no roster and no player.
    /// </summary>
    public sealed class RoleAssignment
    {
        public string GoalkeeperId { get; }
        public IReadOnlyList<string> FieldPlayerIds { get; }
        public string FormationId { get; }
        /// <summary>The slot of each field player, in the same order.</summary>
        public IReadOnlyList<FormationPosition> FieldSlots { get; }

        public RoleAssignment(string formationId, string goalkeeperId, IList<string> fieldPlayerIds, IList<FormationPosition> fieldSlots)
        {
            FormationId = formationId; GoalkeeperId = goalkeeperId;
            FieldPlayerIds = new List<string>(fieldPlayerIds).AsReadOnly();
            FieldSlots = new List<FormationPosition>(fieldSlots).AsReadOnly();
        }

        public bool TryGetSlot(string playerId, out FormationPosition slot)
        {
            for (int i = 0; i < FieldPlayerIds.Count; i++) if (FieldPlayerIds[i] == playerId) { slot = FieldSlots[i]; return true; }
            slot = default(FormationPosition);
            return false;
        }

        public bool Contains(string playerId)
        {
            if (playerId == GoalkeeperId) return true;
            for (int i = 0; i < FieldPlayerIds.Count; i++) if (FieldPlayerIds[i] == playerId) return true;
            return false;
        }
    }

    /// <summary>The roles an archetype plays in: its own zone and its second one (a table of facts about the 12 official roles, not numbers).</summary>
    public static class RoleZones
    {
        public static void Of(PlayerArchetype role, out PitchZone primary, out PitchZone secondary)
        {
            switch (role)
            {
                case PlayerArchetype.Wall: primary = PitchZone.Defense; secondary = PitchZone.Midfield; break;
                case PlayerArchetype.Guardian: primary = PitchZone.Defense; secondary = PitchZone.Midfield; break;
                case PlayerArchetype.Anchor: primary = PitchZone.Midfield; secondary = PitchZone.Defense; break;
                case PlayerArchetype.Builder: primary = PitchZone.Midfield; secondary = PitchZone.Defense; break;
                case PlayerArchetype.Engine: primary = PitchZone.Midfield; secondary = PitchZone.Wing; break;
                case PlayerArchetype.Architect: primary = PitchZone.Midfield; secondary = PitchZone.Attack; break;
                case PlayerArchetype.Creator: primary = PitchZone.Midfield; secondary = PitchZone.Attack; break;
                case PlayerArchetype.Winger: primary = PitchZone.Wing; secondary = PitchZone.Attack; break;
                case PlayerArchetype.Explosive: primary = PitchZone.Wing; secondary = PitchZone.Attack; break;
                case PlayerArchetype.Finisher: primary = PitchZone.Attack; secondary = PitchZone.Wing; break;
                case PlayerArchetype.GoalHunter: primary = PitchZone.Attack; secondary = PitchZone.Wing; break;
                default: primary = PitchZone.Attack; secondary = PitchZone.Midfield; break; // Target
            }
        }

        /// <summary>How well a role fits a zone: primary, secondary or not at all (0).</summary>
        public static float Fit(PlayerArchetype role, PitchZone zone, RolePositioningTuning t)
        {
            Of(role, out PitchZone p, out PitchZone s);
            return zone == p ? t.RolePrimaryFit : (zone == s ? t.RoleSecondaryFit : 0f);
        }
    }

    /// <summary>
    /// Assigns the starting players of a roster to the slots of a formation (Phase 9). The goalkeeper goes to the goal slot; for the field the five-by-five assignment that maximises, summed over the
    /// slots, <c>RoleWeight x (best role fit: affinity x how well that role fits the slot's zone) + ZoneWeight x zone suitability</c> is chosen. The zone of a slot is the zone of its position on the
    /// pitch (so wide slots are Wing). It only decides WHO stands WHERE: it never changes the roster, the draw, the formation or any player. Deterministic: ties are broken, in order, by the total role
    /// fit, the total zone suitability, then slot by slot by the higher Overall (only as a tie-break), and finally by player id; there is no randomness.
    /// </summary>
    public sealed class RolePositionAssigner
    {
        private readonly PlayingProfileCatalog profiles;
        private readonly PlayerRatingCalculator calculator;
        private readonly RolePositioningTuning t;

        public RolePositioningTuning Tuning => t;

        public RolePositionAssigner(PlayingProfileCatalog profiles, PlayerRatingCalculator calculator = null, RolePositioningTuning tuning = null)
        {
            this.profiles = profiles;
            this.calculator = calculator ?? PlayerRatingCalculator.CreateDefault();
            t = tuning ?? new RolePositioningTuning();
        }

        /// <summary>Assigns the six starters of a roster. Fails (with the reason) when the roster has no goalkeeper among its starters or not as many field players as the formation has field slots.</summary>
        public bool TryAssign(TeamRoster roster, FormationDefinition formation, IPlayerLookup players, out RoleAssignment assignment, out string problem)
        {
            assignment = null;
            problem = "";
            if (roster == null || formation == null || players == null) { problem = "A roster, a formation and a player lookup are required."; return false; }
            return TryAssign(roster.ActiveIds, formation, players, out assignment, out problem);
        }

        /// <summary>Same, for any set of starting players (the ones on the pitch now).</summary>
        public bool TryAssign(IReadOnlyList<string> starterIds, FormationDefinition formation, IPlayerLookup players, out RoleAssignment assignment, out string problem)
        {
            assignment = null;
            problem = "";
            if (starterIds == null || formation == null || players == null) { problem = "Starters, a formation and a player lookup are required."; return false; }
            string keeper = null;
            var field = new List<PlayerDefinition>();
            foreach (string id in starterIds)
            {
                if (!players.TryGet(id, out PlayerDefinition def) || def == null) { problem = "Player '" + id + "' is not in the library."; return false; }
                if (def.Role == PlayerRole.Goalkeeper) { if (keeper == null) keeper = id; else field.Add(def); }
                else field.Add(def);
            }
            if (keeper == null) { problem = "No goalkeeper among the starters."; return false; }

            var slots = new List<FormationPosition>();
            bool hasKeeperSlot = false;
            foreach (FormationPosition s in formation.Positions) { if (s.Role == PlayerRole.Goalkeeper) hasKeeperSlot = true; else slots.Add(s); }
            if (!hasKeeperSlot) { problem = "The formation has no goalkeeper slot."; return false; }
            slots.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            if (field.Count != slots.Count) { problem = "The formation has " + slots.Count + " field slots and the starters " + field.Count + " field players."; return false; }

            field.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            int n = slots.Count;
            var role = new float[n, n]; var zone = new float[n, n]; var overall = new int[n];
            for (int p = 0; p < n; p++)
            {
                PlayerPlayingProfile profile = null;
                if (profiles == null || !profiles.TryGet(field[p].Id, out profile)) profile = PlayingProfileDefaults.FromPlayer(field[p]);
                overall[p] = calculator.GetOverall(field[p], profile);
                for (int s = 0; s < n; s++)
                {
                    PitchZone slotZone = PlayingProfileDefaults.ZoneOfRelativePosition(slots[s].Relative);
                    float best = 0f;
                    foreach (RoleAffinity r in profile.Roles) best = Math.Max(best, r.Affinity / t.SuitabilityScale * RoleZones.Fit(r.Role, slotZone, t));
                    role[p, s] = best;
                    zone[p, s] = calculator.GetZoneSuitability(field[p], profile, slotZone) / t.SuitabilityScale;
                }
            }

            // every way of giving the n players to the n slots, in a fixed order; a better one replaces the best only when strictly better
            var current = new int[n]; var best2 = new int[n]; var used = new bool[n];
            float bestTotal = float.NegativeInfinity, bestRole = float.NegativeInfinity, bestZone = float.NegativeInfinity;
            Search(0, n, role, zone, overall, current, used, best2, 0f, 0f, ref bestTotal, ref bestRole, ref bestZone);

            var ids = new List<string>(n);
            for (int s = 0; s < n; s++) ids.Add(field[best2[s]].Id);
            assignment = new RoleAssignment(formation.Id, keeper, ids, slots);
            return true;
        }

        // slot s receives player current[s]
        private void Search(int s, int n, float[,] role, float[,] zone, int[] overall, int[] current, bool[] used, int[] best, float roleSum, float zoneSum, ref float bestTotal, ref float bestRole, ref float bestZone)
        {
            if (s == n)
            {
                float total = t.RoleWeight * roleSum + t.ZoneWeight * zoneSum;
                bool better = total > bestTotal + 1e-6f
                    || (Math.Abs(total - bestTotal) <= 1e-6f && (roleSum > bestRole + 1e-6f
                        || (Math.Abs(roleSum - bestRole) <= 1e-6f && (zoneSum > bestZone + 1e-6f
                            || (Math.Abs(zoneSum - bestZone) <= 1e-6f && OverallBeats(current, best, overall, bestTotal))))));
                if (better) { bestTotal = total; bestRole = roleSum; bestZone = zoneSum; Array.Copy(current, best, n); }
                return;
            }
            for (int p = 0; p < n; p++)
            {
                if (used[p]) continue;
                used[p] = true; current[s] = p;
                Search(s + 1, n, role, zone, overall, current, used, best, roleSum + role[p, s], zoneSum + zone[p, s], ref bestTotal, ref bestRole, ref bestZone);
                used[p] = false;
            }
        }

        // slot by slot, the higher Overall wins; equal everywhere: the earlier permutation (by player id) stays
        private static bool OverallBeats(int[] candidate, int[] incumbent, int[] overall, float bestTotal)
        {
            if (float.IsNegativeInfinity(bestTotal)) return true;
            for (int s = 0; s < candidate.Length; s++)
            {
                int a = overall[candidate[s]], b = overall[incumbent[s]];
                if (a != b) return a > b;
            }
            return false;
        }
    }
}
