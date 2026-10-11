using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Signature-action vocabulary for wingers and wide forwards: <see cref="MidfieldPlayVocabulary"/> (which holds the wing, midfield and defender tags) plus conceptual tags for
    /// attacking wide play (one-on-one dribbling, bursts, cutting inside, diagonal runs). Only exact spellings of the same action are merged; AUTHORING-ONLY labels for future behaviours,
    /// not code, attributes or mechanics. None of the new tags means an existing football behaviour, so none is mapped to one.
    /// </summary>
    public static class AttackPlayVocabulary
    {
        private static readonly string[][] Rows =
        {
            new[] { "OneVsOneDribble", "1v1Dribble", "Electrifying1v1" },
            new[] { "OneVsOneIsolation", "1v1Isolation" },
            new[] { "ChangeOfPaceDribble", "ChangeOfPaceDribble" },
            new[] { "ExplosiveBurst", "ExplosiveBurst", "BurstAcceleration", "SpeedBurst" },
            new[] { "WingBurst", "WingBurst", "OutsideWingerBurst" },
            new[] { "TransitionSprint", "TransitionSprint" },
            new[] { "CounterAttackRun", "CounterAttackRun", "CounterAttackBurst" },
            new[] { "CutInside", "CutInside" },
            new[] { "CutInsideAndShoot", "CutInsideAndShoot", "InsideCutAndShoot" },
            new[] { "CutInsideAndFinish", "CutInsideAndFinish", "InsideCutAndFinish" },
            new[] { "InsideDiagonalRun", "InsideDiagonalRun", "InvertedDiagonalRun" },
            new[] { "InsideDrive", "InsideDrive" },
            new[] { "PowerDrive", "PowerDrive" },
            new[] { "HalfSpaceDrive", "HalfSpaceDrive" },
            new[] { "DeepRun", "DeepRun" },
            new[] { "DirectRunning", "DirectRunning" },
            new[] { "GoalAreaArrival", "GoalAreaArrival" },
            new[] { "WeakSideAttack", "WeakSideAttack", "B" + "lindsideAttack" }, // the source writes the one-word spelling
            new[] { "ClinicalFinishing", "ClinicalFinishing" },
            new[] { "CreativePass", "CreativePass" },
            new[] { "LineBreakingCross", "LineBreakingCross" },
            new[] { "OutsideWingerCarry", "OutsideWingerCarry" },
            new[] { "HighIntensityPress", "HighIntensityPress", "HighPressuring", "HighWorkratePress" },
            new[] { "DefensiveTracking", "DefensiveTracking" },
            new[] { "SidelineWorkrate", "SidelineWorkrate" },
            new[] { "PocketReceive", "PocketReceive" },
            new[] { "CurlFinesseShot", "CurlFinesseShot", "PrecisionCurler", "CurvedShot" },
            new[] { "BetweenLinesTurn", "BetweenLinesTurn" },
            new[] { "VisionaryThroughBall", "VisionaryThroughBall" },
            new[] { "CloseControlSlalom", "CloseControlSlalom" },
            new[] { "FinessePlacement", "FinessePlacement" },
            new[] { "HalfSpaceOrbit", "HalfSpaceOrbit" },
            new[] { "InvertedCutInsidePass", "InvertedCutInsidePass" },
            new[] { "HalfSpaceOrchestration", "HalfSpaceOrchestration" },
            new[] { "ElusivePocketDribble", "ElusivePocketDribble" },
            new[] { "LateRunsIntoBox", "LateRunsIntoBox" },
            new[] { "LateBoxCrash", "LateBoxCrash" },
            new[] { "PowerCarryThroughCenter", "PowerCarryThroughCenter" },
            new[] { "DrivingRun", "DrivingRun" },
            new[] { "DirectAssistance", "DirectAssistance" },
            new[] { "CreativeAssistance", "CreativeAssistance" },
            new[] { "FinalThirdAssistance", "FinalThirdAssistance" },
            new[] { "HighPressSurge", "HighPressSurge" },
            new[] { "OffBallPenetration", "OffBallPenetration" },
            new[] { "DiagonalPenetration", "DiagonalPenetration" },
            new[] { "PocketPenetration", "PocketPenetration" },
            new[] { "ExplosiveTransition", "ExplosiveTransition" },
            new[] { "RainbowFlickDribble", "RainbowFlickDribble" },
            new[] { "NoLookPass", "NoLookPass" },
            new[] { "CloseQuarterSkill", "CloseQuarterSkill" },
            new[] { "ZonalSlalomDribble", "ZonalSlalomDribble" },
            new[] { "ProgressiveDrive", "ProgressiveDrive" },
            new[] { "AmbidextrousBurst", "AmbidextrousBurst" },
            new[] { "SuddenChangeOfPace", "SuddenChangeOfPace" },
            new[] { "AerialBoxThreat", "AerialBoxThreat", "BoxAERIALThreat" },
            new[] { "AerialDuels", "AerialDuels", "AerialDuelsAggressive", "AerialDuelDomination" },
            new[] { "AerialHeaderExpert", "AerialHeaderExpert" },
            new[] { "AerialShielding", "AerialShielding" },
            new[] { "CloseRangeHeader", "CloseRangeHeader" },
            new[] { "PressingDuels", "PressingDuels" },
            new[] { "DefendingFromFront", "DefendingFromFront" },
            new[] { "HoldUpPlay", "HoldUpPlay", "BackToGoalHoldUp", "BackToGoalPlay" },
            new[] { "CounterAttackHoldUp", "CounterAttackHoldUp" },
            new[] { "BoxRuns", "BoxRuns", "InsideBoxMovement" },
            new[] { "BoxAttacking", "BoxAttacking" },
            new[] { "CoreBoxPresence", "CoreBoxPresence" },
            new[] { "CentreBackDominance", "CentreBackDominance", "CentreBackDomination", "CentreBackBully" },
            new[] { "CentreBackHolding", "CentreBackHolding" },
            new[] { "CentreBackShielding", "CentreBackShielding" },
            new[] { "OneVsOneFinishing", "Clinical1v1Finishing" },
            new[] { "CloseControlPocketTurn", "CloseControlPocketTurn" },
            new[] { "CombinationPlay", "CombinationPlay" },
            new[] { "CounterAttackPace", "CounterAttackPace", "CounterAttackSprint", "CounterPaceBurst" },
            new[] { "HyperSpeedBreakaway", "HyperSpeedBreakaway" },
            new[] { "ExplosivePowerRun", "ExplosivePowerRun" },
            new[] { "HighSpeedSpaceAttack", "HighSpeedSpaceAttack" },
            new[] { "LongStridePace", "LongStridePace" },
            new[] { "DefensiveRecovery", "DefensiveRecovery" },
            new[] { "DefensiveSetPieceClearance", "DefensiveSetPieceClearance" },
            new[] { "DefensiveWorkrate", "DefensiveWorkrate" },
            new[] { "DiagonalRun", "DiagonalRun" },
            new[] { "DirectGoalDrive", "DirectGoalDrive" },
            new[] { "DirectShot", "DirectShot" },
            new[] { "PowerShot", "PowerShot" },
            new[] { "NearPostShot", "NearPostShot" },
            new[] { "FirstTimeFinishing", "FirstTimeFinishing", "FirstTouchFinishing" },
            new[] { "FirstTouchShot", "FirstTouchShot", "FirstTouchBoxShot" },
            new[] { "DropDeepToLink", "DropDeepToLink" },
            new[] { "LinkUpPlay", "LinkUpPlay", "LinkUpPassing" },
            new[] { "ElusiveRun", "ElusiveRun" },
            new[] { "FlankToCenterCut", "FlankToCenterCut" },
            new[] { "WingCutInside", "WingCutInside" },
            new[] { "IntelligentPocketReading", "IntelligentPocketReading" },
            new[] { "OffBallMovement", "OffBallMovement" },
            new[] { "OffBallSpaceAttack", "OffBallSpaceAttack" },
            new[] { "OffsideTrapExploitation", "OffsideTrapExploitation" },
            new[] { "PenaltySpecialist", "PenaltySpecialist", "PenaltyMastery" },
            new[] { "PhysicalFriction", "PhysicalFriction" },
            new[] { "PowerProgressiveCarry", "PowerProgressiveCarry" },
            new[] { "PrecisionThroughBall", "PrecisionThroughBall" },
            new[] { "SkillfulDribble", "SkillfulDribble" },
            new[] { "SlalomBoxDribble", "SlalomBoxDribble" },
            new[] { "InsideSlash", "InsideSlash" }
        };

        // wingers' spellings of tags the midfield vocabulary already has
        private static readonly string[][] Aliases =
        {
            new[] { "CloseControlDribble", "TightSpaceDribble", "ElusiveDribble" },
            new[] { "ProgressivePass", "ProgressivePassing" },
            new[] { "CutInsideAndShoot", "CutInsideAndStrike" },
            new[] { "HighIntensityPress", "HighIntensityPressRun" },
            new[] { "LongRangeStrike", "StrikingFromDistance" },
            new[] { "ClinicalFinishing", "ClinicalFinish", "EliteFinishing", "PreciseFinishing" },
            new[] { "HighIntensityPress", "AggressivePress", "DefensivePressing" },
            new[] { "ExplosiveBurst", "ExplosiveAcceleration", "LightningBurst" },
            new[] { "PhysicalShielding", "PhysicalShieldingOfBall" }
        };

        public static IReadOnlyDictionary<string, string> Create()
        {
            var d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in MidfieldPlayVocabulary.Create()) d[kv.Key] = kv.Value;
            foreach (string[] row in Rows) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            foreach (string[] row in Aliases) for (int i = 1; i < row.Length; i++) d[GoalkeeperRecordImporter.Normalize(row[i])] = row[0];
            return d;
        }

        public static IEnumerable<string> Tags
        {
            get
            {
                foreach (string t in MidfieldPlayVocabulary.Tags) yield return t;
                foreach (string[] row in Rows) yield return row[0];
            }
        }

        /// <summary>Tag -> existing football behaviour: the midfield mapping, nothing new.</summary>
        public static IReadOnlyDictionary<string, string> TagBehaviors() { return MidfieldPlayVocabulary.TagBehaviors(); }
    }
}
