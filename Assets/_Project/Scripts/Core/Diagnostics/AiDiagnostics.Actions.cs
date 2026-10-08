using System;

namespace FS27.Core
{
    public sealed partial class AiDiagnostics
    {
        private static int DuelIndex(FootballActionKind k)
        {
            switch (k)
            {
                case FootballActionKind.Tackle: return 0;
                case FootballActionKind.SlidingTackle: return 1;
                case FootballActionKind.PhysicalChallenge: return 2;
                default: return -1;
            }
        }

        /// <summary>
        /// One action an AI player asked for and what the Core answered. Counted by kind (requested, accepted, rejected and why, completed, failed, stamina); passes and shots are followed to their end; duels
        /// for the ball are recorded with everything around them (and the fouls with what led to them). Reads only.
        /// </summary>
        public void OnAction(int stepNumber, WorldView view, int idx, in PlayerDecision d, in ActionResult r, BasicDecisionDriver driver)
        {
            if (Twin != null) Twin.OnAction(stepNumber, view, idx, d, r, driver);
            if (!Config.Enabled || match == null || Skip(view[idx].Team)) return;
            int k = (int)r.Kind;
            a[oActRequested + k] += 1.0;
            switch (r.Outcome)
            {
                case ActionOutcome.Rejected: a[oActRejected + k] += 1.0; a[oActReason + (int)r.Rejection] += 1.0; break;
                case ActionOutcome.Accepted: a[oActAccepted + k] += 1.0; break;
                case ActionOutcome.Completed: a[oActCompleted + k] += 1.0; break;
                case ActionOutcome.Failed: a[oActFailed + k] += 1.0; break;
            }
            a[oActStamina + k] += r.StaminaCost;
            if (r.GainedPossession) a[oMatch + mRecoveries] += 1.0;
            if (r.Kind == FootballActionKind.Intercept && r.Detail == ActionDetail.Intercepted) players[idx].Interceptions++;

            if (IsPass(r.Kind) && r.Outcome == ActionOutcome.Completed) { ReleaseWindows(idx); RegisterPass(stepNumber, view, idx, d, r); }
            else if (IsShot(r.Kind) && r.Outcome == ActionOutcome.Completed) { ReleaseWindows(idx); RegisterShot(stepNumber, view, idx); }
            int duel = DuelIndex(r.Kind);
            if (duel >= 0) RecordDuel(stepNumber, view, idx, duel, r, driver);
        }

        private void ReleaseWindows(int idx)
        {
            for (int i = 0; i < windows.Length; i++) if (windows[i].Active && windows[i].PlayerIdx == idx) SettleWindow(ref windows[i], WindowOutcome.Released);
        }

        // ------------------------------------------------------------------ passes and shots

        private void RegisterPass(int stepNumber, WorldView view, int idx, in PlayerDecision d, in ActionResult r)
        {
            a[oPassAttempts] += 1.0; a[oPassKind + (int)r.Kind] += 1.0;
            players[idx].Passes++;
            int spaceIndex = -1;
            if (d.HasAction && d.Action.Target.Kind == PassTargetKind.Position)
            {
                a[oSpaceAttempts] += 1.0; players[idx].SpacePasses++;
                if (SpaceCount >= space.Length) SpaceDropped++;
                else
                {
                    WorldPlayer me = view[idx];
                    Vec2 target = d.Action.Target.Position;
                    var s = new SpaceAttempt { Step = stepNumber, PasserId = me.PlayerId, RunnerId = d.TargetPlayerId, Team = me.Team, Kind = r.Kind, Target = target, PasserToTarget = (target - me.Position).Magnitude, BallToTarget = (target - view.Ball.Ground).Magnitude,
                        BallSpeed = (float)Math.Sqrt(r.BallVelocity.X * r.BallVelocity.X + r.BallVelocity.Z * r.BallVelocity.Z), Power = r.Intensity, SkillId = r.SkillId, Outcome = PassOutcome.Pending };
                    s.RunnerArrival = float.PositiveInfinity;
                    if (d.TargetPlayerId != null && view.TryGetPlayer(d.TargetPlayerId, out WorldPlayer runner)) { s.RunnerToTarget = (target - runner.Position).Magnitude; s.RunnerSpeed = runner.Speed; if (runner.MaxSpeed > 0f) s.RunnerArrival = s.RunnerToTarget / (runner.MaxSpeed * BallCarrierDefaults.ThroughSpeedShare); }
                    s.OpponentToTarget = float.PositiveInfinity; s.RivalArrival = float.PositiveInfinity; s.KeeperDistance = float.PositiveInfinity;
                    for (int i = 0; i < view.PlayerCount; i++)
                    {
                        WorldPlayer o = view[i];
                        if (!o.IsOnPitch || o.Team == me.Team) continue;
                        float dist = (o.Position - target).Magnitude;
                        if (dist < s.OpponentToTarget) { s.OpponentToTarget = dist; s.NearestOpponentId = o.PlayerId; }
                        if (o.MaxSpeed > 0f) { float arrival = dist / (o.MaxSpeed * BallCarrierDefaults.ThroughSpeedShare); if (arrival < s.RivalArrival) s.RivalArrival = arrival; }
                        if (o.IsGoalkeeper) s.KeeperDistance = dist;
                    }
                    s.TargetToGoal = (WorldGeometry.AttackGoal(view.Match, view.Field, me.Team) - target).Magnitude;
                    spaceIndex = SpaceCount;
                    space[SpaceCount++] = s;
                }
            }
            int slot = -1;
            for (int i = 0; i < passes.Length; i++) if (!passes[i].Active) { slot = i; break; }
            if (slot < 0)
            {
                slot = 0;
                for (int i = 1; i < passes.Length; i++) if (passes[i].Step < passes[slot].Step) slot = i;
                SettlePass(ref passes[slot], PassOutcome.NotObserved, null);
            }
            float tdist = 0f;
            if (d.TargetPlayerId != null && view.TryGetPlayer(d.TargetPlayerId, out WorldPlayer tp)) tdist = (tp.Position - view[idx].Position).Magnitude; else if (d.HasAction && d.Action.Target.Kind == PassTargetKind.Position) tdist = (d.Action.Target.Position - view[idx].Position).Magnitude;
            passes[slot] = new PendingPass { OpponentDistance = WorldGeometry.NearestOpponentDistanceTo(view, view[idx].Team, view[idx].Position), PasserSpeed = view[idx].Speed, BallSpeed = (float)Math.Sqrt(r.BallVelocity.X * r.BallVelocity.X + r.BallVelocity.Z * r.BallVelocity.Z), TargetDistance = tdist, Power = r.Intensity, Quality = r.Quality, Active = true, Step = stepNumber, PasserIdx = idx, SpaceIndex = spaceIndex, PasserId = view[idx].PlayerId, IntendedId = d.TargetPlayerId ?? r.TargetPlayerId, Team = view[idx].Team, Kind = r.Kind };
        }

        private void RegisterShot(int stepNumber, WorldView view, int idx)
        {
            a[oShotAttempts] += 1.0; players[idx].Shots++;
            TeamSide team = view[idx].Team;
            int slot = -1;
            for (int i = 0; i < shots.Length; i++) if (!shots[i].Active) { slot = i; break; }
            if (slot < 0) { slot = 0; for (int i = 1; i < shots.Length; i++) if (shots[i].Step < shots[slot].Step) slot = i; SettleShot(ref shots[slot], ShotOutcome.Unknown); }
            shots[slot] = new PendingShot { Score = lastShotScore[idx], Distance = (WorldGeometry.AttackGoal(view.Match, view.Field, team) - view[idx].Position).Magnitude, Active = true, Step = stepNumber, PlayerIdx = idx, Team = team, GoalsFor = team == TeamSide.Home ? match.State.Score.Home : match.State.Score.Away };
        }

        // ------------------------------------------------------------------ duels for the ball and fouls

        private void RecordDuel(int stepNumber, WorldView view, int idx, int duel, in ActionResult r, BasicDecisionDriver driver)
        {
            a[oDuelRequested + duel] += 1.0;
            int carrierIdx = r.OtherPlayerId != null ? view.IndexOf(r.OtherPlayerId) : -1;
            if (carrierIdx < 0) { if (r.Outcome != ActionOutcome.Rejected) a[oNoContact + duel] += 1.0; return; }

            WorldPlayer def = view[idx], car = view[carrierIdx];
            bool foul = r.Detail == ActionDetail.Foul, failed = r.Outcome == ActionOutcome.Failed, won = r.Detail == ActionDetail.Won, loose = r.Detail == ActionDetail.Loose;
            Vec2 toDefender = def.Position - car.Position;
            bool behind = car.Facing.X * toDefender.X + car.Facing.Y * toDefender.Y < 0f;
            bool aggressive = def.Skills != null && def.Skills.Has(SkillEffect.AggressiveTackle);
            bool stationary = car.Speed < Config.StationarySpeed;
            bool shielding = car.IsShielding;
            DecisionKind lastKind = default(DecisionKind); bool hasLast = false;
            if (driver != null && driver.TryGetLast(car.PlayerId, out PlayerDecision last)) { lastKind = last.Kind; hasLast = true; }
            int kindSlot = hasLast ? (int)lastKind : DecisionKinds;

            a[oContacts + duel] += 1.0;
            if (won) a[oWon + duel] += 1.0; else if (loose) a[oLooseWin + duel] += 1.0; else if (failed) a[oFailed + duel] += 1.0;
            if (foul) a[oFouls + duel] += 1.0;
            a[oAggrContacts + duel * 2 + (aggressive ? 1 : 0)] += 1.0; if (foul) a[oAggrFouls + duel * 2 + (aggressive ? 1 : 0)] += 1.0;
            a[oBehindContacts + duel * 2 + (behind ? 1 : 0)] += 1.0; if (failed) a[oBehindFailed + duel * 2 + (behind ? 1 : 0)] += 1.0; if (foul) a[oBehindFouls + duel * 2 + (behind ? 1 : 0)] += 1.0;
            a[oCarrierKindContacts + kindSlot] += 1.0; if (foul) a[oCarrierKindFouls + kindSlot] += 1.0;
            a[oMovingContacts + (stationary ? 0 : 1)] += 1.0; if (foul) a[oMovingFouls + (stationary ? 0 : 1)] += 1.0;
            a[oShieldContacts + (shielding ? 1 : 0)] += 1.0; if (foul) a[oShieldFouls + (shielding ? 1 : 0)] += 1.0;
            if (foul) { a[oQuality] += r.Quality; a[oQuality + 1] += 1.0; } else if (failed) { a[oQuality + 2] += r.Quality; a[oQuality + 3] += 1.0; }

            int arch = players[idx].ArchetypeIndex;
            if (arch >= 0 && arch < Archetypes) { a[oArchContacts + arch] += 1.0; if (foul) a[oArchFouls + arch] += 1.0; }
            int carrierArch = players[carrierIdx].ArchetypeIndex;
            if (foul && carrierArch >= 0 && carrierArch < Archetypes) a[oArchSuffered + carrierArch] += 1.0;
            int role = (int)def.Role;
            if (role >= 0 && role < Roles) { a[oRoleContacts + role] += 1.0; if (foul) a[oRoleFouls + role] += 1.0; }

            players[idx].ContactsMade++; players[carrierIdx].ContactsSuffered++;
            if (duel < 2) players[idx].Tackles++;
            if (won) players[idx].DuelsWon++;
            if (foul) { players[idx].FoulsCommitted++; players[carrierIdx].FoulsSuffered++; }

            if (ContactCount >= contacts.Length) { ContactsDropped++; return; }
            contacts[ContactCount++] = new ContactRecord
            {
                Step = stepNumber, Kind = r.Kind, DefenderId = def.PlayerId, CarrierId = car.PlayerId, DefenderTeam = def.Team, Outcome = r.Outcome, Detail = r.Detail, Foul = foul, FromBehind = behind, DefenderAggressive = aggressive,
                CarrierShielding = shielding, CarrierEvading = car.IsEvading, Distance = toDefender.Magnitude, RelativeSpeed = (def.Velocity - car.Velocity).Magnitude, DefenderStamina = def.Stamina, CarrierStamina = car.Stamina,
                DefenderSpeed = def.Speed, CarrierSpeed = car.Speed, Quality = r.Quality, DefenderToOwnGoal = WorldGeometry.DistanceToOwnGoal(view, def), Zone = def.Zone, CarrierLastKind = lastKind, HasCarrierLastKind = hasLast,
                DefenderRole = def.Role, CarrierRole = car.Role
            };
        }

        // ------------------------------------------------------------------ read: duels and fouls
        public double DuelRequests(int duel) { return a[oDuelRequested + duel]; }
        public double DuelContacts(int duel) { return a[oContacts + duel]; }
        public double DuelWon(int duel) { return a[oWon + duel]; }
        public double DuelLooseWon(int duel) { return a[oLooseWin + duel]; }
        public double DuelFailed(int duel) { return a[oFailed + duel]; }
        public double DuelFouls(int duel) { return a[oFouls + duel]; }
        public double DuelNoContact(int duel) { return a[oNoContact + duel]; }
        public double TotalContacts { get { return a[oContacts] + a[oContacts + 1] + a[oContacts + 2]; } }
        public double TotalFouls { get { return a[oFouls] + a[oFouls + 1] + a[oFouls + 2]; } }
        public double AggressiveContacts(int duel, bool aggressive) { return a[oAggrContacts + duel * 2 + (aggressive ? 1 : 0)]; }
        public double AggressiveFouls(int duel, bool aggressive) { return a[oAggrFouls + duel * 2 + (aggressive ? 1 : 0)]; }
        public double BehindContacts(int duel, bool behind) { return a[oBehindContacts + duel * 2 + (behind ? 1 : 0)]; }
        public double BehindFailed(int duel, bool behind) { return a[oBehindFailed + duel * 2 + (behind ? 1 : 0)]; }
        public double BehindFouls(int duel, bool behind) { return a[oBehindFouls + duel * 2 + (behind ? 1 : 0)]; }
        public double CarrierKindContacts(int decisionKind) { return a[oCarrierKindContacts + decisionKind]; }
        public double CarrierKindFouls(int decisionKind) { return a[oCarrierKindFouls + decisionKind]; }
        public double CarrierNoDecisionContacts { get { return a[oCarrierKindContacts + DecisionKinds]; } }
        public double CarrierNoDecisionFouls { get { return a[oCarrierKindFouls + DecisionKinds]; } }
        public double MovingContacts(bool moving) { return a[oMovingContacts + (moving ? 1 : 0)]; }
        public double MovingFouls(bool moving) { return a[oMovingFouls + (moving ? 1 : 0)]; }
        public double ShieldContacts(bool shielding) { return a[oShieldContacts + (shielding ? 1 : 0)]; }
        public double ShieldFouls(bool shielding) { return a[oShieldFouls + (shielding ? 1 : 0)]; }
        public double FoulQualityAverage { get { return Div(a[oQuality], a[oQuality + 1]); } }
        public double FailedNonFoulQualityAverage { get { return Div(a[oQuality + 2], a[oQuality + 3]); } }
        public double ArchetypeContacts(int archetype) { return a[oArchContacts + archetype]; }
        public double ArchetypeFouls(int archetype) { return a[oArchFouls + archetype]; }
        public double ArchetypeFoulsSuffered(int archetype) { return a[oArchSuffered + archetype]; }
        public double RoleContacts(PlayerRole role) { return a[oRoleContacts + (int)role]; }
        public double RoleFouls(PlayerRole role) { return a[oRoleFouls + (int)role]; }
        public double InterceptionEvents { get { return a[oMatch + mInterceptions]; } }
        public double BlockEvents { get { return a[oMatch + mBlocks]; } }

        // ------------------------------------------------------------------ read: actions, passes, shots
        public double ActionRequested(FootballActionKind k) { return a[oActRequested + (int)k]; }
        public double ActionAccepted(FootballActionKind k) { return a[oActAccepted + (int)k]; }
        public double ActionRejected(FootballActionKind k) { return a[oActRejected + (int)k]; }
        public double ActionCompleted(FootballActionKind k) { return a[oActCompleted + (int)k]; }
        public double ActionFailed(FootballActionKind k) { return a[oActFailed + (int)k]; }
        public double ActionStamina(FootballActionKind k) { return a[oActStamina + (int)k]; }
        public double ActionRejectionCount(ActionRejection why) { return a[oActReason + (int)why]; }
        public double PassAttempts { get { return a[oPassAttempts]; } }
        public double PassKindAttempts(FootballActionKind k) { return a[oPassKind + (int)k]; }
        public double PassOutcomeCount(PassOutcome o) { return a[oPassOutcome + (int)o]; }
        /// <summary>Mean of a feature of the passes that ended this way: 0 passer speed, 1 ball speed after the kick, 2 distance to the target, 3 power, 4 quality of the kick, 5 nearest opponent to the passer when he kicked.</summary>
        public double PassFeature(PassOutcome o, int feature) { return Div(a[oPassFeat + (int)o * 7 + 1 + feature], a[oPassFeat + (int)o * 7]); }
        public double PassOutcomeSeconds(PassOutcome o) { return Div(a[oPassTime + (int)o * 2], a[oPassTime + (int)o * 2 + 1]); }
        public double PassKindOutcome(FootballActionKind k, PassOutcome o) { return a[oPassKindOutcome + (int)k * 10 + (int)o]; }
        public double CompletedToIntended { get { return a[oPassReceiver]; } }
        public double CompletedToOther { get { return a[oPassReceiver + 1]; } }
        public double SpaceAttempts { get { return a[oSpaceAttempts]; } }
        public double SpaceOutcomeCount(PassOutcome o) { return a[oSpaceOutcome + (int)o]; }
        public double SpaceFeature(PassOutcome o, int feature) { return Div(a[oSpaceFeatures + (int)o * 6 + 1 + feature], a[oSpaceFeatures + (int)o * 6]); }
        public double ShotAttempts { get { return a[oShotAttempts]; } }
        /// <summary>Shots by the distance to goal at which they were taken (below 4, 7, 9, 11, 13 m, and beyond) and what became of them.</summary>
        public double ShotsAtDistance(int bucket, ShotOutcome o) { return a[oShotDistance + bucket * ShotOutcomes + (int)o]; }
        /// <summary>Shots by the score the carrier gave the shot when he chose it (below 1.5, 2, 2.5, 3, and beyond) and what became of them.</summary>
        public double ShotsAtScore(int bucket, ShotOutcome o) { return a[oShotScore + bucket * ShotOutcomes + (int)o]; }
        public double ShotOutcomeCount(ShotOutcome o) { return a[oShotOutcome + (int)o]; }
        public double GainsFor(TeamSide t) { return a[oMatch + (t == TeamSide.Home ? mGainsHome : mGainsAway)]; }
        public double PassesFollowing { get { int n = 0; for (int i = 0; i < passes.Length; i++) if (passes[i].Active) n++; return n; } }
    }
}
