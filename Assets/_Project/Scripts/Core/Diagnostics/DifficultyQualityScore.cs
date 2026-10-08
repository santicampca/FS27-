using System;

namespace FS27.Core
{
    /// <summary>
    /// A DIAGNOSTIC reading of how well one team played a match (Phase 15), taken from the counters of the observer of that team and of the observer of its opponent. It is NOT a rating of the players, it is
    /// never fed back into any decision, tuning or attribute, and it is deliberately NOT one number: five separate groups, each a list of named ratios, so that a weakness in one cannot hide behind another.
    /// Every ratio is a plain division of counters that the Core can really produce; nothing is estimated. A ratio whose denominator is zero is NaN (not zero), so that an empty sample cannot look like a result.
    /// </summary>
    public struct DifficultyQualityScore
    {
        // ---- ATTACK quality
        /// <summary>Passes completed / passes attempted.</summary>
        public double PassCompletion;
        /// <summary>Shots that scored or were stopped by an opponent (on target, or blocked: the Core does not tell them apart) / shots taken.</summary>
        public double ShotsOnTargetShare;
        /// <summary>Shots taken / carrier decisions in which a shot was possible: how selective the team was.</summary>
        public double ShotTakeRate;
        /// <summary>Clear chances (a shot candidate scoring at least the clear score) per 100 possessions gained.</summary>
        public double ClearChancesPer100Possessions;
        /// <summary>Entries into the final third per possession gained.</summary>
        public double FinalThirdEntriesPerPossession;
        /// <summary>Possessions lost to the opponent per possession gained (lower is better).</summary>
        public double LossesPerPossession;

        // ---- DEFENCE quality (what the OPPONENT got)
        /// <summary>Clear chances the opponent had per 100 possessions it gained (lower is better).</summary>
        public double ChancesConcededPer100OpponentPossessions;
        /// <summary>Entries of the opponent into the dangerous zone per possession it gained (lower is better).</summary>
        public double DangerConcededPerOpponentPossession;
        /// <summary>Own recoveries of the ball per opponent possession.</summary>
        public double RecoveriesPerOpponentPossession;
        /// <summary>Own interceptions (events) per opponent possession.</summary>
        public double InterceptionsPerOpponentPossession;
        /// <summary>Contacts with the carrier in which the defender won the ball / all contacts.</summary>
        public double ContestSuccess;

        // ---- DECISION quality
        /// <summary>Controlled mistakes per carrier decision (lower is better).</summary>
        public double ErrorRate;
        public double ErrorRateUnderPressure;
        public double ErrorRateWithLittleSpace;
        public double ErrorRateWhenTired;
        /// <summary>Mean margin between the winning and the best other kind of option (higher = clearer decisions).</summary>
        public double MeanMargin;

        // ---- MOVEMENT quality
        /// <summary>Mean distance (m) of the field players from their place with the ball and without it.</summary>
        public double HomeDistanceAttacking;
        public double HomeDistanceDefending;
        /// <summary>Share of all decisions that were an off-the-ball attacking job (support, run forward, width, get open).</summary>
        public double OffBallAttackShare;
        /// <summary>Share of all decisions that were a defensive action (close down, tackle, slide, challenge, intercept).</summary>
        public double DefensiveActionShare;

        public static readonly string[] Names =
        {
            "PassCompletion", "ShotsOnTargetShare", "ShotTakeRate", "ClearChancesPer100Possessions", "FinalThirdEntriesPerPossession", "LossesPerPossession",
            "ChancesConcededPer100OpponentPossessions", "DangerConcededPerOpponentPossession", "RecoveriesPerOpponentPossession", "InterceptionsPerOpponentPossession", "ContestSuccess",
            "ErrorRate", "ErrorRateUnderPressure", "ErrorRateWithLittleSpace", "ErrorRateWhenTired", "MeanMargin",
            "HomeDistanceAttacking", "HomeDistanceDefending", "OffBallAttackShare", "DefensiveActionShare"
        };

        /// <summary>Which names are good when HIGH (true) and when LOW (false), in the order of <see cref="Names"/>: only so that a report can say "better" without guessing.</summary>
        public static readonly bool[] HigherIsBetter =
        {
            true, true, false, true, true, false,
            false, false, true, true, true,
            false, false, false, false, true,
            false, false, true, true
        };

        public double[] ToArray()
        {
            return new[]
            {
                PassCompletion, ShotsOnTargetShare, ShotTakeRate, ClearChancesPer100Possessions, FinalThirdEntriesPerPossession, LossesPerPossession,
                ChancesConcededPer100OpponentPossessions, DangerConcededPerOpponentPossession, RecoveriesPerOpponentPossession, InterceptionsPerOpponentPossession, ContestSuccess,
                ErrorRate, ErrorRateUnderPressure, ErrorRateWithLittleSpace, ErrorRateWhenTired, MeanMargin,
                HomeDistanceAttacking, HomeDistanceDefending, OffBallAttackShare, DefensiveActionShare
            };
        }

        private static double Ratio(double part, double whole) { return whole > 0.0 ? part / whole : double.NaN; }

        /// <param name="self">The observer of the team being read (filtered to that team).</param>
        /// <param name="opponent">The observer of the other team of the same matches.</param>
        public static DifficultyQualityScore Of(AiDiagnostics self, AiDiagnostics opponent)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (opponent == null) throw new ArgumentNullException(nameof(opponent));
            double shotsOnTarget = self.ShotOutcomeCount(ShotOutcome.Goal) + self.ShotOutcomeCount(ShotOutcome.OpponentPossession);
            double duelContacts = self.TotalContacts, duelWins = self.DuelWon(0) + self.DuelWon(1) + self.DuelWon(2) + self.DuelLooseWon(0) + self.DuelLooseWon(1) + self.DuelLooseWon(2);
            double offBall = self.KindCount(DecisionKind.Support) + self.KindCount(DecisionKind.ForwardRun) + self.KindCount(DecisionKind.HoldWidth) + self.KindCount(DecisionKind.GetOpen);
            double defensive = self.KindCount(DecisionKind.CloseDown) + self.KindCount(DecisionKind.Tackle) + self.KindCount(DecisionKind.SlidingTackle) + self.KindCount(DecisionKind.Challenge) + self.KindCount(DecisionKind.Intercept);
            return new DifficultyQualityScore
            {
                PassCompletion = Ratio(self.PassOutcomeCount(PassOutcome.Completed), self.PassAttempts),
                ShotsOnTargetShare = Ratio(shotsOnTarget, self.ShotAttempts),
                ShotTakeRate = Ratio(self.Chosen(CandidateType.Shoot), self.Available(CandidateType.Shoot)),
                ClearChancesPer100Possessions = 100.0 * Ratio(self.ClearChances, self.Possessions),
                FinalThirdEntriesPerPossession = Ratio(self.FinalThirdEntries, self.Possessions),
                LossesPerPossession = Ratio(self.PossessionsLost, self.Possessions),
                ChancesConcededPer100OpponentPossessions = 100.0 * Ratio(opponent.ClearChances, opponent.Possessions),
                DangerConcededPerOpponentPossession = Ratio(opponent.DangerEntries, opponent.Possessions),
                RecoveriesPerOpponentPossession = Ratio(self.Recoveries, opponent.Possessions),
                InterceptionsPerOpponentPossession = Ratio(self.InterceptionEvents, opponent.Possessions),
                ContestSuccess = Ratio(duelWins, duelContacts),
                ErrorRate = Ratio(self.MistakeTotal, self.CarrierDecisions),
                ErrorRateUnderPressure = Ratio(self.MistakesPressed, self.DecisionsPressed),
                ErrorRateWithLittleSpace = Ratio(self.MistakesLowSpace, self.DecisionsLowSpace),
                ErrorRateWhenTired = Ratio(self.MistakesTired, self.DecisionsTired),
                MeanMargin = self.MarginAverage,
                HomeDistanceAttacking = self.HomeDistanceAttacking,
                HomeDistanceDefending = self.HomeDistanceDefending,
                OffBallAttackShare = Ratio(offBall, self.Decisions),
                DefensiveActionShare = Ratio(defensive, self.Decisions)
            };
        }
    }
}
