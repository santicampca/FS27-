using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Where one player of a team starts: his id, his place on the pitch and the way he faces.</summary>
    public struct PlacedPlayer
    {
        public string PlayerId;
        public Vec2 Position;
        public float Heading;
        public PlacedPlayer(string playerId, Vec2 position, float heading) { PlayerId = playerId; Position = position; Heading = heading; }
    }

    /// <summary>
    /// Formation -> starting positions (Phase 7). A <see cref="FormationDefinition"/> has one slot per player (1 goalkeeper + 5 field players); this turns it into field coordinates for a team
    /// that attacks a given direction: the slots are converted by <see cref="FormationPosition.ToFieldPosition"/> (so a team attacking -X is the same layout turned 180 degrees and both sides
    /// are exact mirrors), the goalkeeper is kept in his goal zone, everybody is kept inside the pitch and, for a kick-off, inside his own half; finally the team is spread so nobody overlaps.
    /// There is NO tactical movement here: only formation -> starting positions.
    /// </summary>
    public static class FormationPlacement
    {
        /// <summary>How many times the team is pushed apart and put back inside its limits.</summary>
        private const int SpreadPasses = 40;

        /// <summary>
        /// Computes the placement. <paramref name="fieldPlayerIds"/> are the field players in order: the i-th field slot (by lineup index) is given to the i-th id. Fills <paramref name="result"/>
        /// (cleared first) and returns false, writing nothing useful, when the formation has no goalkeeper slot. With fewer ids than field slots (or null ids) the slots left over stay empty.
        /// </summary>
        public static bool Compute(FormationDefinition formation, PitchBounds bounds, bool homeAttacksPositiveX, TeamSide team, string goalkeeperId, IList<string> fieldPlayerIds,
                                   bool kickoffOwnHalf, List<PlacedPlayer> result)
        {
            result.Clear();
            if (formation == null || bounds == null || fieldPlayerIds == null) return false;
            bool attacksPlusX = homeAttacksPositiveX == (team == TeamSide.Home);
            var fieldSlots = new List<FormationPosition>();
            FormationPosition? keeperSlot = null;
            foreach (FormationPosition s in formation.Positions)
            {
                if (s.Role == PlayerRole.Goalkeeper) { if (keeperSlot == null) keeperSlot = s; }
                else fieldSlots.Add(s);
            }
            if (keeperSlot == null || goalkeeperId == null) return false;
            fieldSlots.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));

            float heading = new Vec2(attacksPlusX ? 1f : -1f, 0f).ToHeading();
            InfrastructureTuning t = bounds.Tuning;

            Vec2 keeper = keeperSlot.Value.ToFieldPosition(bounds.Field, attacksPlusX);
            result.Add(new PlacedPlayer(goalkeeperId, bounds.ClampToGoalZone(keeper, homeAttacksPositiveX, team), heading));
            for (int i = 0; i < fieldSlots.Count; i++)
            {
                // a slot nobody fills (a player was sent off) stays empty; a null id also leaves its slot empty
                if (i >= fieldPlayerIds.Count || fieldPlayerIds[i] == null) continue;
                Vec2 p = bounds.ClampPlayer(fieldSlots[i].ToFieldPosition(bounds.Field, attacksPlusX));
                if (kickoffOwnHalf) p = KeepInOwnHalf(p, bounds, homeAttacksPositiveX, team, t.KickoffHalfMargin);
                result.Add(new PlacedPlayer(fieldPlayerIds[i], p, heading));
            }

            // nobody overlaps: spread the team (the keeper stays in his zone, everybody stays in the pitch)
            var points = new Vec2[result.Count];
            for (int i = 0; i < points.Length; i++) points[i] = result[i].Position;
            // push apart, then put everybody back where he may stand, again and again: a push that would leave the pitch, the goal zone or his half is absorbed by the others
            for (int pass = 0; pass < SpreadPasses; pass++)
            {
                if (PlayerSeparation.Resolve(points, points.Length, t.MinSeparation, 1, attacksPlusX ? 1f : -1f) && pass > 0) break;
                for (int i = 0; i < points.Length; i++)
                {
                    Vec2 q = i == 0 ? bounds.ClampToGoalZone(points[i], homeAttacksPositiveX, team) : bounds.ClampPlayer(points[i]);
                    if (kickoffOwnHalf && i > 0) q = KeepInOwnHalf(q, bounds, homeAttacksPositiveX, team, t.KickoffHalfMargin);
                    points[i] = q;
                }
            }
            for (int i = 0; i < points.Length; i++) result[i] = new PlacedPlayer(result[i].PlayerId, points[i], heading);
            return true;
        }

        private static Vec2 KeepInOwnHalf(Vec2 p, PitchBounds bounds, bool homeAttacksPositiveX, TeamSide team, float margin)
        {
            float sign = PitchBounds.OwnGoalSign(homeAttacksPositiveX, team); // +1: own goal at +X, so the own half is X >= 0
            float limit = margin;
            float x = sign > 0f ? Math.Max(limit, p.X) : Math.Min(-limit, p.X);
            return new Vec2(x, p.Y);
        }
    }
}
