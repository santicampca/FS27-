using System;
using System.Text;

namespace FS27.Core
{
    public sealed partial class AiDiagnostics
    {
        /// <summary>Adds the counters of another run (another match) to this one, and appends its logs while there is room. Per-player stats are per match and are not merged.</summary>
        public void Merge(AiDiagnostics o)
        {
            if (o == null) throw new ArgumentNullException(nameof(o));
            for (int i = 0; i < a.Length; i++) a[i] += o.a[i];
            if (o.MarginMin < MarginMin) MarginMin = o.MarginMin;
            if (o.MarginMax > MarginMax) MarginMax = o.MarginMax;
            for (int i = 0; i < o.SpaceCount; i++) { if (SpaceCount < space.Length) space[SpaceCount++] = o.space[i]; else SpaceDropped++; }
            for (int i = 0; i < o.ContactCount; i++) { if (ContactCount < contacts.Length) contacts[ContactCount++] = o.contacts[i]; else ContactsDropped++; }
            SpaceDropped += o.SpaceDropped; ContactsDropped += o.ContactsDropped;
        }

        private static string P(double part, double whole) { return whole > 0.0 ? (100.0 * part / whole).ToString("F1") + "%" : "-"; }
        private static string F(double v) { return v.ToString("F2"); }

        /// <summary>A text summary of everything measured (built on demand, never while playing).</summary>
        public string Report()
        {
            var sb = new StringBuilder();
            string[] typeNames = { "None", "Pass", "ForwardPass", "ThroughPass", "Shoot", "Dribble", "Protect", "Hold" };
            sb.AppendLine("== " + (Label ?? "diagnostics") + " ==");
            sb.AppendLine("MATCH   matches " + Matches + "  seconds " + F(Seconds) + "  possession gains H/A " + GainsFor(TeamSide.Home) + "/" + GainsFor(TeamSide.Away) + "  switches " + PossessionSwitches + "  recoveries " + Recoveries + "  goals " + Goals + " (after a followed shot " + GoalsAfterShot + ", other " + GoalsOther + ")  fouls " + FoulEvents);
            var stamina = new StringBuilder("STAMINA field players every 10 s (mean/share<20):");
            for (int slot = 0; slot < 12; slot++) if (StaminaSamples(slot) > 0) stamina.Append(" " + F(StaminaMean(slot)) + "/" + F(StaminaExhausted(slot)));
            sb.AppendLine(stamina.ToString());
            var effort = new StringBuilder("EFFORT asked by decision kind (walk/run/sprint range):");
            foreach (DecisionKind k in Enum.GetValues(typeof(DecisionKind))) if (EffortCount(k, 0) + EffortCount(k, 1) + EffortCount(k, 2) > 0) effort.Append(" " + k + " " + EffortCount(k, 0) + "/" + EffortCount(k, 1) + "/" + EffortCount(k, 2));
            sb.AppendLine(effort.ToString());
            sb.AppendLine("DECIDED all " + Decisions + "  carrier " + CarrierDecisions);
            var kindLine = new StringBuilder("DECISION KINDS (all players):");
            foreach (DecisionKind k in Enum.GetValues(typeof(DecisionKind))) if (KindCount(k) > 0) kindLine.Append(" " + k + " " + KindCount(k));
            sb.AppendLine(kindLine.ToString());
            sb.AppendLine("CARRIER type: chosen | share | avgScore | avgMargin | available | bestOfKindAvg | runnerUp | avgPressure | avgStamina");
            for (int t = 1; t < Types; t++)
            {
                var type = (CandidateType)t;
                sb.AppendLine("  " + typeNames[t].PadRight(12) + Chosen(type).ToString().PadLeft(6) + " " + P(Chosen(type), CarrierDecisions).PadLeft(7) + " " + F(ChosenAverageScore(type)).PadLeft(6) + " " + F(ChosenAverageMargin(type)).PadLeft(6)
                    + "  avail " + P(Available(type), CarrierDecisions).PadLeft(7) + "  best " + F(BestOfKindAverage(type)).PadLeft(6) + "  second " + RunnerUp(type).ToString().PadLeft(5) + "  pressure " + F(ChosenAveragePressure(type)) + "  stamina " + F(ChosenAverageStamina(type)));
            }
            sb.AppendLine("MARGIN  avg " + F(MarginAverage) + "  min " + F(double.IsInfinity(MarginMin) ? 0.0 : MarginMin) + "  max " + F(double.IsInfinity(MarginMax) ? 0.0 : MarginMax) + "  <0.10 " + MarginBucket(0) + "  <0.25 " + MarginBucket(1) + "  <0.50 " + MarginBucket(2) + "  >=0.50 " + MarginBucket(3) + "  alone " + MarginBucket(4));
            sb.AppendLine("RUNS    decisions with a run of kind support/forward/width/getopen present: " + RunPresent(RunKind.Support) + "/" + RunPresent(RunKind.Forward) + "/" + RunPresent(RunKind.Width) + "/" + RunPresent(RunKind.GetOpen));
            sb.AppendLine("SCORE PARTS (average of the best candidate of each kind; winner average in brackets)");
            for (int t = 1; t < Types; t++)
            {
                var type = (CandidateType)t;
                string[] names = CandidatePartNames.For(type);
                if (names.Length == 0) continue;
                var line = new StringBuilder("  " + typeNames[t] + ":");
                for (int p = 0; p < names.Length; p++) line.Append(" " + names[p] + " " + F(BestOfKindPartAverage(type, p)) + "[" + F(WinnerPartAverage(type, p)) + "]");
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine("WON BY (winner -> best of another kind): ");
            for (int w = 1; w < Types; w++)
            {
                var line = new StringBuilder("  " + typeNames[w] + " ->");
                for (int s = 1; s < Types; s++) if (Second((CandidateType)w, (CandidateType)s) > 0) line.Append(" " + typeNames[s] + " " + Second((CandidateType)w, (CandidateType)s));
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine("NOT CANDIDATE (kind: reason count)");
            for (int t = 1; t < Types; t++)
            {
                var line = new StringBuilder("  " + typeNames[t] + ":");
                for (int r = 1; r < Rejections; r++) if (Rejected((CandidateType)t, (CandidateRejection)r) > 0) line.Append(" " + (CandidateRejection)r + " " + Rejected((CandidateType)t, (CandidateRejection)r));
                sb.AppendLine(line.ToString());
            }
            sb.AppendLine("DRIBBLE free-way decisions " + DribbleFreeDecisions + "  chosen avg space " + F(DribbleAverageSpace) + "  avg progress " + F(DribbleAverageProgress));
            sb.AppendLine("  free distance ahead of the dribble (<1.75/<3.5/<5/<7/<10/more) all: " + FreeDistanceBucket(0, false) + "/" + FreeDistanceBucket(1, false) + "/" + FreeDistanceBucket(2, false) + "/" + FreeDistanceBucket(3, false) + "/" + FreeDistanceBucket(4, false) + "/" + FreeDistanceBucket(5, false) + "   won: " + FreeDistanceBucket(0, true) + "/" + FreeDistanceBucket(1, true) + "/" + FreeDistanceBucket(2, true) + "/" + FreeDistanceBucket(3, true) + "/" + FreeDistanceBucket(4, true) + "/" + FreeDistanceBucket(5, true));
            foreach (CandidateType t in new[] { CandidateType.Dribble, CandidateType.Protect, CandidateType.Hold })
                sb.AppendLine("  " + t + " episodes " + Episodes(t) + "  retained " + WindowCount(t, WindowOutcome.Retained) + "  lost " + WindowCount(t, WindowOutcome.Lost) + "  loose " + WindowCount(t, WindowOutcome.Loose) + "  released " + WindowCount(t, WindowOutcome.Released) + "  unknown " + WindowCount(t, WindowOutcome.Unknown)
                    + "  retention(of those not released) " + P(WindowCount(t, WindowOutcome.Retained), WindowCount(t, WindowOutcome.Retained) + WindowCount(t, WindowOutcome.Lost) + WindowCount(t, WindowOutcome.Loose)));
            sb.AppendLine("PASSES  attempts " + PassAttempts + "  completed " + PassOutcomeCount(PassOutcome.Completed) + " (to intended " + CompletedToIntended + ", to other " + CompletedToOther + ")  intercepted " + PassOutcomeCount(PassOutcome.Intercepted)
                + "  lostOther " + PassOutcomeCount(PassOutcome.PossessionLostOther) + "  missed " + PassOutcomeCount(PassOutcome.MissedTarget) + "  outOfPlay " + PassOutcomeCount(PassOutcome.OutOfPlay) + "  invalidated " + PassOutcomeCount(PassOutcome.Invalidated) + "  returnedToPasser " + PassOutcomeCount(PassOutcome.ReturnedToPasser) + "  unknown " + PassOutcomeCount(PassOutcome.Unknown) + "  notObserved " + PassOutcomeCount(PassOutcome.NotObserved));
            foreach (FootballActionKind k in new[] { FootballActionKind.ShortPass, FootballActionKind.LongPass, FootballActionKind.ThroughPass, FootballActionKind.WallPass, FootballActionKind.SkillLobPass, FootballActionKind.GoalkeeperDistribution })
                if (PassKindAttempts(k) > 0) sb.AppendLine("  " + k + " attempts " + PassKindAttempts(k) + " completed " + PassKindOutcome(k, PassOutcome.Completed) + " intercepted " + PassKindOutcome(k, PassOutcome.Intercepted) + " lostOther " + PassKindOutcome(k, PassOutcome.PossessionLostOther) + " missed " + PassKindOutcome(k, PassOutcome.MissedTarget) + " other " + (PassKindAttempts(k) - PassKindOutcome(k, PassOutcome.Completed) - PassKindOutcome(k, PassOutcome.Intercepted) - PassKindOutcome(k, PassOutcome.PossessionLostOther) - PassKindOutcome(k, PassOutcome.MissedTarget)));
            sb.AppendLine("RETURNED TO PASSER within <0.1s/<0.25s/<0.5s/later: " + ReturnedBucket(0) + "/" + ReturnedBucket(1) + "/" + ReturnedBucket(2) + "/" + ReturnedBucket(3));
            var chase = new StringBuilder("PASSER CHASED HIS OWN PASS (MoveToBall decided before the pass settled), by outcome:");
            foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) if (PassOutcomeCount(o) > 0) chase.Append(" " + o + " " + PassesChasedByPasser(o) + "/" + PassOutcomeCount(o));
            sb.AppendLine(chase.ToString());
            sb.AppendLine("ENDED IN AN OPPONENT KEEPERS HANDS (all passes / into space): intercepted " + PassesEndingWithKeeper(PassOutcome.Intercepted, false) + "/" + PassesEndingWithKeeper(PassOutcome.Intercepted, true) + "  lostOther " + PassesEndingWithKeeper(PassOutcome.PossessionLostOther, false) + "/" + PassesEndingWithKeeper(PassOutcome.PossessionLostOther, true));
            var times = new StringBuilder("PASS TIME to settle (s):");
            foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) if (PassOutcomeCount(o) > 0) times.Append(" " + o + " " + F(PassOutcomeSeconds(o)));
            sb.AppendLine(times.ToString());
            foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) if (PassOutcomeCount(o) > 0) sb.AppendLine("  " + o + " passerSpeed " + F(PassFeature(o, 0)) + "  ballSpeed " + F(PassFeature(o, 1)) + "  targetDist " + F(PassFeature(o, 2)) + "  power " + F(PassFeature(o, 3)) + "  quality " + F(PassFeature(o, 4)) + "  opponentNearPasser " + F(PassFeature(o, 5)));
            sb.AppendLine("SPACE   attempts " + SpaceAttempts + "  logged " + SpaceCount + "  dropped " + SpaceDropped);
            foreach (PassOutcome o in new[] { PassOutcome.Completed, PassOutcome.Intercepted, PassOutcome.MissedTarget, PassOutcome.Invalidated, PassOutcome.PossessionLostOther, PassOutcome.OutOfPlay, PassOutcome.ReturnedToPasser, PassOutcome.Unknown, PassOutcome.NotObserved })
                if (SpaceOutcomeCount(o) > 0) sb.AppendLine("  " + o + " " + SpaceOutcomeCount(o) + " (" + P(SpaceOutcomeCount(o), SpaceAttempts) + ")  avg runner->target " + F(SpaceFeature(o, 0)) + "  opponent->target " + F(SpaceFeature(o, 1)) + "  passer->target " + F(SpaceFeature(o, 2)) + "  finalBall->target " + F(SpaceFeature(o, 3)) + "  runnerSpeed " + F(SpaceFeature(o, 4)) + "  runnerArrival " + F(SpaceFeature(o, 5)) + "s  rivalArrival " + F(SpaceFeature(o, 6)) + "s  keeper->target " + F(SpaceFeature(o, 7)) + "  target->goal " + F(SpaceFeature(o, 8)));
            sb.AppendLine("SHOTS   opportunities(decisions with a shot candidate) " + Available(CandidateType.Shoot) + "  chosen " + Chosen(CandidateType.Shoot) + "  attempts(actions) " + ShotAttempts + "  goal " + ShotOutcomeCount(ShotOutcome.Goal) + "  opponentPossession " + ShotOutcomeCount(ShotOutcome.OpponentPossession) + "  teamPossession " + ShotOutcomeCount(ShotOutcome.TeamPossession) + "  outOfPlay " + ShotOutcomeCount(ShotOutcome.OutOfPlay) + "  unknown " + ShotOutcomeCount(ShotOutcome.Unknown));
            var shotDist = new StringBuilder("  shots by distance (<4/<7/<9/<11/<13/more): attempts, goals ");
            string[] edges = { "<4", "<7", "<9", "<11", "<13", "13+" };
            for (int b = 0; b < 6; b++)
            {
                double n = 0.0; foreach (ShotOutcome o in Enum.GetValues(typeof(ShotOutcome))) if (o != ShotOutcome.Pending) n += ShotsAtDistance(b, o);
                shotDist.Append(" " + edges[b] + ":" + n + "/" + ShotsAtDistance(b, ShotOutcome.Goal));
            }
            sb.AppendLine(shotDist.ToString());
            var shotScore = new StringBuilder("  shots by the score they had when chosen (<1.5/<2/<2.5/<3/more): attempts/goals ");
            for (int b = 0; b < 5; b++)
            {
                double n = 0.0; foreach (ShotOutcome o in Enum.GetValues(typeof(ShotOutcome))) if (o != ShotOutcome.Pending) n += ShotsAtScore(b, o);
                shotScore.Append(" " + n + "/" + ShotsAtScore(b, ShotOutcome.Goal));
            }
            sb.AppendLine(shotScore.ToString());
            string[] duelNames = { "Tackle", "Sliding", "Challenge" };
            sb.AppendLine("DEFENCE interceptions(events) " + InterceptionEvents + "  blocks " + BlockEvents + "  contacts " + TotalContacts + "  fouls(actions) " + TotalFouls + "  foul/contact " + P(TotalFouls, TotalContacts));
            for (int d = 0; d < DuelKinds; d++)
                sb.AppendLine("  " + duelNames[d].PadRight(10) + "requested " + DuelRequests(d) + "  contacts " + DuelContacts(d) + "  won " + DuelWon(d) + "  loose " + DuelLooseWon(d) + "  failed " + DuelFailed(d) + "  fouls " + DuelFouls(d) + "  foul/contact " + P(DuelFouls(d), DuelContacts(d)) + "  noContact " + DuelNoContact(d)
                    + "  | aggressive " + AggressiveFouls(d, true) + "/" + AggressiveContacts(d, true) + "  not " + AggressiveFouls(d, false) + "/" + AggressiveContacts(d, false) + "  | from behind: contacts " + BehindContacts(d, true) + " failed " + BehindFailed(d, true) + " fouls " + BehindFouls(d, true) + "  front: contacts " + BehindContacts(d, false) + " failed " + BehindFailed(d, false));
            sb.AppendLine("  carrier standing/moving contacts " + MovingContacts(false) + "/" + MovingContacts(true) + " fouls " + MovingFouls(false) + "/" + MovingFouls(true) + "  shielding contacts " + ShieldContacts(true) + " fouls " + ShieldFouls(true) + "  quality foul " + F(FoulQualityAverage) + " failed-no-foul " + F(FailedNonFoulQualityAverage));
            var kinds = new StringBuilder("  by what the carrier had decided (contacts/fouls):");
            for (int k = 0; k <= DecisionKinds; k++) if (CarrierKindContacts(k) > 0) kinds.Append(" " + (k == DecisionKinds ? "none" : ((DecisionKind)k).ToString()) + " " + CarrierKindContacts(k) + "/" + CarrierKindFouls(k));
            sb.AppendLine(kinds.ToString());
            var arch = new StringBuilder("  by defender archetype (contacts/fouls):");
            foreach (PlayerArchetype pa in Enum.GetValues(typeof(PlayerArchetype))) if ((int)pa < Archetypes && ArchetypeContacts((int)pa) > 0) arch.Append(" " + pa + " " + ArchetypeContacts((int)pa) + "/" + ArchetypeFouls((int)pa));
            sb.AppendLine(arch.ToString());
            var roleLine = new StringBuilder("  by defender position (contacts/fouls):");
            foreach (PlayerRole pr in Enum.GetValues(typeof(PlayerRole))) if (RoleContacts(pr) > 0) roleLine.Append(" " + pr + " " + RoleContacts(pr) + "/" + RoleFouls(pr));
            sb.AppendLine(roleLine.ToString());
            sb.AppendLine("ACTIONS (requested/accepted/rejected/completed/failed, stamina)");
            foreach (FootballActionKind k in Enum.GetValues(typeof(FootballActionKind)))
                if (ActionRequested(k) > 0) sb.AppendLine("  " + k.ToString().PadRight(20) + ActionRequested(k) + "/" + ActionAccepted(k) + "/" + ActionRejected(k) + "/" + ActionCompleted(k) + "/" + ActionFailed(k) + "  stamina " + F(ActionStamina(k)));
            var why = new StringBuilder("  rejections:");
            foreach (ActionRejection r in Enum.GetValues(typeof(ActionRejection))) if (ActionRejectionCount(r) > 0) why.Append(" " + r + " " + ActionRejectionCount(r));
            sb.AppendLine(why.ToString());
            return sb.ToString();
        }

        /// <summary>The per-player table (this match only).</summary>
        public string PlayersReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("player team role arch decisions carrier passes completed space dribble protect hold shots goals possessions foulsCommitted foulsSuffered contactsMade contactsSuffered tackles duelsWon interceptions");
            for (int i = 0; i < players.Length; i++)
            {
                PlayerStats p = players[i];
                if (p.PlayerId == null) continue;
                sb.AppendLine(p.PlayerId + " " + p.Team + " " + p.Role + " " + (p.ArchetypeIndex >= 0 ? ((PlayerArchetype)p.ArchetypeIndex).ToString() : "-") + " " + p.Decisions + " " + p.CarrierDecisions + " " + p.Passes + " " + p.PassesCompleted + " " + p.SpacePasses + " " + p.Dribbles + " " + p.Protects + " " + p.Holds + " " + p.Shots + " " + p.Goals + " " + p.Possessions + " " + p.FoulsCommitted + " " + p.FoulsSuffered + " " + p.ContactsMade + " " + p.ContactsSuffered + " " + p.Tackles + " " + p.DuelsWon + " " + p.Interceptions);
            }
            return sb.ToString();
        }
    }
}
