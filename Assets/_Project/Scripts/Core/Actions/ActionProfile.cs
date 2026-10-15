using System;

namespace FS27.Core
{
    /// <summary>
    /// What a player brings to the actions, resolved ONCE from his official attributes (no new attribute, no attribute stored on anything but the player's definition):
    /// every number is a quality between 0 and 1 built from the 12 attributes, plus his feet. The match engine resolves it when the match is created.
    ///  Control quality     = Control 55 %, Technique 20 %, Agility 15 %, Speed 10 %.
    ///  Pass quality        = Passing 70 %, Technique 15 %, Control 15 %.
    ///  Shot quality        = close to goal: Finishing 60 %, Shooting 25 %, Technique 15 %; far: Shooting 60 %, Finishing 25 %, Technique 15 %.
    ///  Clearance quality   = Strength 30 %, Passing 30 %, Defense 20 %, Technique 20 %.
    ///  Tackle skill        = Defense 60 %, Strength 20 %, Agility 10 %, Speed 10 %.
    ///  Dribble skill       = Dribbling 40 %, Control 30 %, Agility 20 %, Strength 10 %.
    ///  Challenge strength  = Strength 50 %, Defense 20 %, Agility 10 %, Speed 20 %. Resist strength = Strength 50 %, Control 20 %, Agility 10 %, Dribbling 20 %.
    ///  Intercept quality   = Defense 35 %, Agility 25 %, Control 20 %, Speed 20 %.
    /// (DECISION MADE DURING PHASE 4: the weights.) Stamina is NOT here: it is the player's current state, applied at the moment of the action.
    /// A goalkeeper (or a field player in goal) also has goalkeeper numbers, from his <see cref="GoalkeeperProfile"/> or, for a field player, derived by <see cref="EmergencyKeeperProfile"/>.
    /// </summary>
    public struct ActionProfile
    {
        public float ControlQuality;
        public float PassQuality;
        public float ShotQualityClose;
        public float ShotQualityFar;
        public float ClearanceQuality;
        public float TackleSkill;
        public float DribbleSkill;
        public float ChallengeStrength;
        public float ResistStrength;
        public float InterceptQuality;
        /// <summary>Phase 37: the aerial ability (jump, strength in the duel, balance): Strength 40 %, Agility 30 %, Speed 20 %, Control 10 %.</summary>
        public float AerialQuality;

        public PreferredFoot PreferredFoot;
        /// <summary>0..1 (the weak-foot quality attribute / 100).</summary>
        public float WeakFootQuality;

        public float KeeperHandling;
        public float KeeperReflexes;
        public float KeeperDiving;
        public float KeeperDistribution;
        public float KeeperKicking;
        /// <summary>Phase 16: the keeper's positioning and command (his reading of the game), 0..1. A field player in goal has his derived positioning and uses it for the command too (command is contextual, it has no number of its own).</summary>
        public float KeeperPositioning;
        public float KeeperCommand;

        public static ActionProfile Resolve(in PlayerAttributes a, PreferredFoot foot, int weakFootQuality, GoalkeeperProfile keeper)
        {
            float speed = N(a.Speed), agility = N(a.Agility), strength = N(a.Strength), control = N(a.Control), technique = N(a.Technique);
            float passing = N(a.Passing), shooting = N(a.Shooting), finishing = N(a.Finishing), defense = N(a.Defense), dribbling = N(a.Dribbling);

            var p = new ActionProfile
            {
                ControlQuality = 0.55f * control + 0.20f * technique + 0.15f * agility + 0.10f * speed,
                PassQuality = 0.70f * passing + 0.15f * technique + 0.15f * control,
                ShotQualityClose = 0.60f * finishing + 0.25f * shooting + 0.15f * technique,
                ShotQualityFar = 0.60f * shooting + 0.25f * finishing + 0.15f * technique,
                ClearanceQuality = 0.30f * strength + 0.30f * passing + 0.20f * defense + 0.20f * technique,
                TackleSkill = 0.60f * defense + 0.20f * strength + 0.10f * agility + 0.10f * speed,
                DribbleSkill = 0.40f * dribbling + 0.30f * control + 0.20f * agility + 0.10f * strength,
                ChallengeStrength = 0.50f * strength + 0.20f * defense + 0.10f * agility + 0.20f * speed,
                ResistStrength = 0.50f * strength + 0.20f * control + 0.10f * agility + 0.20f * dribbling,
                InterceptQuality = 0.35f * defense + 0.25f * agility + 0.20f * control + 0.20f * speed,
                AerialQuality = 0.40f * strength + 0.30f * agility + 0.20f * speed + 0.10f * control,
                PreferredFoot = foot,
                WeakFootQuality = Math.Max(0, Math.Min(100, weakFootQuality)) / 100f
            };

            if (keeper != null)
            {
                p.KeeperHandling = N(keeper.Handling);
                p.KeeperReflexes = N(keeper.Reflexes);
                p.KeeperDiving = N(keeper.Diving);
                p.KeeperDistribution = N(keeper.Distribution);
                p.KeeperKicking = N(keeper.Kicking);
                p.KeeperPositioning = N(keeper.Positioning);
                p.KeeperCommand = N(keeper.Command);
            }
            else
            {
                EmergencyKeeperProfile e = EmergencyKeeperProfile.Derive(a);
                p.KeeperHandling = N(e.Handling);
                p.KeeperReflexes = N(e.Reflexes);
                p.KeeperDiving = N(e.Diving);
                p.KeeperDistribution = N(e.Distribution);
                p.KeeperKicking = N(e.Kicking);
                p.KeeperPositioning = N(e.Positioning);
                p.KeeperCommand = N(e.Positioning);
            }
            return p;
        }

        /// <summary>The neutral profile of a player nobody knows (all the default attributes).</summary>
        public static ActionProfile Neutral(GoalkeeperProfile keeper = null)
        {
            return Resolve(PlayerAttributes.CreateDefault(), PreferredFoot.Right, 50, keeper);
        }

        private static float N(int attribute)
        {
            int v = Math.Max(PlayerAttributes.Min, Math.Min(PlayerAttributes.Max, attribute));
            return (v - PlayerAttributes.Min) / (float)(PlayerAttributes.Max - PlayerAttributes.Min);
        }
    }
}
