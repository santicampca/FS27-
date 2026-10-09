using System;

namespace FS27.Core
{
    public sealed partial class AiDiagnostics
    {
        private struct PendingPass { public bool Active; public bool PasserChased; public float OpponentDistance, PasserSpeed, BallSpeed, TargetDistance, Power, Quality; public int Step, PasserIdx, SpaceIndex; public string PasserId, IntendedId; public TeamSide Team; public FootballActionKind Kind; }
        private struct PendingWindow { public bool Active; public int Step, PlayerIdx; public TeamSide Team; public CandidateType Type; }
        private struct PendingShot { public float Score, Distance; public bool Active; public int Step, PlayerIdx, GoalsFor, LogIndex; public TeamSide Team; }

        private readonly PendingPass[] passes = new PendingPass[16];
        private readonly PendingWindow[] windows = new PendingWindow[48];
        private readonly PendingShot[] shots = new PendingShot[8];
        private readonly CandidateType[] lastType = new CandidateType[WorldState.MaxPlayers];
        private readonly int[] lastTypeStep = new int[WorldState.MaxPlayers];
        private readonly float[] lastShotScore = new float[WorldState.MaxPlayers];
        private readonly double[] bestOfType = new double[Types];
        private readonly int[] bestIndex = new int[Types];

        private MatchController match;
        private int step;
        private int lastInterceptStep = -1, lastGoalStep = -1, lastOutStep = -1;
        private TeamSide lastInterceptTeam;
        private bool hasPossession;
        private TeamSide lastTeam;
        private string lastPossessor;

        // ------------------------------------------------------------------ attaching to a match

        /// <summary>Starts observing a match: subscribes to its events (read-only) and learns who is who.</summary>
        public void Attach(MatchController m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            if (match != null) Detach();
            match = m;
            shotQuality.ModeIsClassic = m.State.Mode == GameMode.Classic ? 1f : 0f;
            m.Events.Published += OnEvent;
            a[oMatch + mMatches] += 1.0;
            int n = 0;
            foreach (TeamMatchState team in new[] { m.State.Home, m.State.Away })
                for (int i = 0; i < team.Players.Count && n < players.Length; i++, n++)
                {
                    MatchPlayer p = team.Players[i];
                    players[n].PlayerId = p.PlayerId; players[n].Team = team.Side; players[n].Role = p.Role; players[n].ArchetypeIndex = -1;
                    if (m.TryGetPlayingProfile(p.PlayerId, out PlayerPlayingProfile profile) && profile.Roles != null && profile.Roles.Count > 0) players[n].ArchetypeIndex = (int)profile.Roles[0].Role;
                }
        }

        public void Detach()
        {
            if (match != null) match.Events.Published -= OnEvent;
            match = null;
        }

        private void OnEvent(GameplayEvent e)
        {
            // a keeper's catch or parry while an opponent's shot is in flight: a save (the keeper's team counts it; the shooter's observer marks its shot)
            if (e.Kind == GameplayEventKind.BallTouched && (e.Detail == "GoalkeeperCatch" || e.Detail == "GoalkeeperSave") && anyShotTeam != e.Team && step - anyShotStep <= ToSteps(Config.ShotWindowSeconds))
            {
                // one save per shot: a parry that is then caught is the same save
                if (!Skip(e.Team) && !anyShotSaved) a[oShotQ + 3] += 1.0;
                anyShotSaved = true;
                for (int i = 0; i < shots.Length; i++) if (shots[i].Active && shots[i].Team != e.Team && shots[i].LogIndex >= 0) shotLog[shots[i].LogIndex].Saved = true;
            }
            if (Config.OnlyOneTeam && e.HasTeam && e.Team != Config.Team) return;
            switch (e.Kind)
            {
                case GameplayEventKind.GoalScored:
                    a[oMatch + mGoals] += 1.0; lastGoalStep = step;
                    { bool afterShot = false; for (int i = 0; i < shots.Length; i++) if (shots[i].Active && (!e.HasTeam || shots[i].Team == e.Team)) afterShot = true; a[oMatch + (afterShot ? mGoalsAfterShot : mGoalsOther)] += 1.0; }
                    break;
                case GameplayEventKind.FoulCommitted: a[oMatch + mFoulEvents] += 1.0; break;
                case GameplayEventKind.YellowCardIssued: a[oContext + 6] += 1.0; break;
                case GameplayEventKind.RedCardIssued: a[oContext + 7] += 1.0; break;
                case GameplayEventKind.InterceptionPerformed:
                    lastInterceptStep = step; lastInterceptTeam = e.Team; a[oMatch + mInterceptEvents] += 1.0;
                    if (e.Detail == "Blocked") a[oMatch + mBlocks] += 1.0; else a[oMatch + mInterceptions] += 1.0;
                    break;
                case GameplayEventKind.BallOutOfBounds: case GameplayEventKind.CornerAwarded: case GameplayEventKind.ThrowInAwarded: case GameplayEventKind.GoalKickAwarded:
                    lastOutStep = step; a[oMatch + mOutEvents] += 1.0; break;
            }
        }

        private int IndexOf(TeamSide team, string id)
        {
            if (id == null) return -1;
            for (int i = 0; i < players.Length; i++) if (players[i].Team == team && players[i].PlayerId == id) return i;
            return -1;
        }

        // ------------------------------------------------------------------ every step: possession and the things being followed

        /// <summary>Called at the start of every driver step, before anything is decided: follows possession and settles what can be settled. Reads the match only.</summary>
        public void BeginStep(int stepNumber)
        {
            if (Twin != null) Twin.BeginStep(stepNumber);
            if (!Config.Enabled || match == null) return;
            step = stepNumber;
            a[oMatch + mSteps] += 1.0;
            MatchBall ball = match.State.Ball;
            if (ball.HasPossession && ball.PossessingPlayerId != null)
            {
                if (!hasPossession || ball.PossessingTeam != lastTeam)
                {
                    if (hasPossession && ball.PossessingTeam != lastTeam) { a[oMatch + mSwitches] += 1.0; if (!Skip(lastTeam)) a[oLost] += 1.0; }
                    a[oMatch + (ball.PossessingTeam == TeamSide.Home ? mGainsHome : mGainsAway)] += 1.0;
                    if (!Skip(ball.PossessingTeam)) a[oProgress + 5] += 1.0;
                    inFinalThird = false; inDanger = false; possessionWasDangerous = false;
                }
                if (!Skip(ball.PossessingTeam)) TrackProgress(ball);
                if (ball.PossessingPlayerId != lastPossessor) { int who = IndexOf(ball.PossessingTeam, ball.PossessingPlayerId); if (who >= 0) players[who].Possessions++; }
                hasPossession = true; lastTeam = ball.PossessingTeam; lastPossessor = ball.PossessingPlayerId;
            }
            if (step % ToSteps(10f) == 0)
            {
                int slot = Math.Min(11, step / ToSteps(10f));
                foreach (TeamMatchState team in new[] { match.State.Home, match.State.Away })
                    for (int i = 0; i < team.Players.Count; i++)
                    {
                        MatchPlayer p = team.Players[i];
                        if (!p.IsActive || p.InGoal || Skip(team.Side)) continue;
                        float st = p.Locomotion.CurrentStamina;
                        a[oStamina + slot * 3] += st; if (st < 20f) a[oStamina + slot * 3 + 1] += 1.0; a[oStamina + slot * 3 + 2] += 1.0;
                    }
            }
            FollowPasses(ball);
            FollowWindows(ball);
            FollowShots(ball);
        }

        /// <summary>Called when the simulation ends: whatever is still being followed is closed as not observed.</summary>
        public void Finish()
        {
            if (match == null) return;
            for (int i = 0; i < passes.Length; i++) if (passes[i].Active) SettlePass(ref passes[i], PassOutcome.NotObserved, null);
            for (int i = 0; i < windows.Length; i++) if (windows[i].Active) SettleWindow(ref windows[i], WindowOutcome.Unknown);
            for (int i = 0; i < shots.Length; i++) if (shots[i].Active) SettleShot(ref shots[i], ShotOutcome.Unknown);
        }

        private int ToSteps(float seconds) { return Math.Max(1, (int)Math.Round(seconds / LocomotionDefaults.FixedDeltaTime)); }

        // ------------------------------------------------------------------ passes

        private static bool IsPass(FootballActionKind k)
        {
            switch (k)
            {
                case FootballActionKind.ShortPass: case FootballActionKind.LongPass: case FootballActionKind.ThroughPass: case FootballActionKind.WallPass:
                case FootballActionKind.SkillLobPass: case FootballActionKind.Cross: case FootballActionKind.GoalkeeperDistribution: return true;
                default: return false;
            }
        }

        private static bool IsShot(FootballActionKind k)
        {
            switch (k)
            {
                case FootballActionKind.Shot: case FootballActionKind.CurvedShot: case FootballActionKind.PlacedShot: case FootballActionKind.Chip: case FootballActionKind.Header: return true;
                default: return false;
            }
        }

        private bool inFinalThird, inDanger, possessionWasDangerous;
        private bool anyShotSaved;
        private int anyShotStep = -100000;
        private TeamSide anyShotTeam;

        /// <summary>The ball in the hands of the observed team: did it enter the final third, did it reach the dangerous zone (counted once per possession).</summary>
        private void TrackProgress(MatchBall ball)
        {
            bool positive = (ball.PossessingTeam == TeamSide.Home) == match.State.HomeAttacksPositiveX;
            float x = positive ? ball.Physics.Position.X : -ball.Physics.Position.X;
            float half = match.Field.Length * 0.5f;
            bool third = x > half - match.Field.Length * Config.FinalThirdFraction;
            bool danger = half - x < Config.DangerDistance;
            if (third && !inFinalThird) a[oProgress] += 1.0;
            if (danger && !inDanger) { a[oProgress + 1] += 1.0; if (!possessionWasDangerous) { a[oProgress + 2] += 1.0; possessionWasDangerous = true; } }
            inFinalThird = third; inDanger = danger;
        }

        /// <summary>A controlled mistake of a level (or a late reaction / late contest), and the metres the player was off his place. Counts for the team given.</summary>
        public void OnMistake(int stepNumber, TeamSide team, in DecisionError error, float driftMetres)
        {
            if (Twin != null) Twin.OnMistake(stepNumber, team, error, driftMetres);
            if (!Config.Enabled || match == null || Skip(team)) return;
            if (error.Occurred)
            {
                a[oMistake + (int)error.Kind] += 1.0; a[oMistakeGap + (int)error.Kind] += error.Gap;
                if (error.Kind != DecisionErrorKind.LateContest && error.Kind != DecisionErrorKind.LateReaction)
                {
                    if (error.Pressure >= Config.PressedLevel) a[oContext + 3] += 1.0;
                    if (!error.FreeWay) a[oContext + 4] += 1.0;
                    if (error.Stamina <= Config.TiredLevel) a[oContext + 5] += 1.0;
                }
            }
            if (driftMetres > 0f) { a[oDrift] += 1.0; a[oDrift + 1] += driftMetres; }
        }

        private void FollowPasses(MatchBall ball)
        {
            int window = ToSteps(Config.PassWindowSeconds);
            for (int i = 0; i < passes.Length; i++)
            {
                if (!passes[i].Active || step <= passes[i].Step) continue;
                PendingPass p = passes[i];
                if (ball.HasPossession && ball.PossessingPlayerId != null)
                {
                    if (ball.PossessingTeam == p.Team)
                    {
                        if (ball.PossessingPlayerId == p.PasserId) SettlePass(ref passes[i], PassOutcome.ReturnedToPasser, ball.PossessingPlayerId);
                        else SettlePass(ref passes[i], PassOutcome.Completed, ball.PossessingPlayerId);
                    }
                    else SettlePass(ref passes[i], lastInterceptStep >= p.Step && lastInterceptTeam == ball.PossessingTeam ? PassOutcome.Intercepted : PassOutcome.PossessionLostOther, ball.PossessingPlayerId);
                }
                else if (!match.State.IsBallLive)
                    SettlePass(ref passes[i], lastGoalStep >= p.Step ? PassOutcome.Invalidated : (lastOutStep >= p.Step ? PassOutcome.OutOfPlay : PassOutcome.Invalidated), null);
                else if (step - p.Step > window) SettlePass(ref passes[i], PassOutcome.MissedTarget, null);
            }
        }

        private void SettlePass(ref PendingPass p, PassOutcome outcome, string receiver)
        {
            p.Active = false;
            a[oPassOutcome + (int)outcome] += 1.0;
            a[oPassKindOutcome + (int)p.Kind * 10 + (int)outcome] += 1.0;
            if (p.PasserChased) a[oChase + (int)outcome] += 1.0;
            if (receiver != null && outcome != PassOutcome.Completed && outcome != PassOutcome.ReturnedToPasser)
            {
                int who = IndexOf(p.Team.Opponent(), receiver);
                if (who >= 0 && players[who].Role == PlayerRole.Goalkeeper) { a[oToKeeper + (int)outcome * 2] += 1.0; if (p.SpaceIndex >= 0) a[oToKeeper + (int)outcome * 2 + 1] += 1.0; }
            }
            if (outcome == PassOutcome.ReturnedToPasser) { float secs = (step - p.Step) * LocomotionDefaults.FixedDeltaTime; a[oReturnTime + (secs < 0.1f ? 0 : (secs < 0.25f ? 1 : (secs < 0.5f ? 2 : 3)))] += 1.0; }
            int pf = oPassFeat + (int)outcome * 7;
            a[pf] += 1.0; a[pf + 1] += p.PasserSpeed; a[pf + 2] += p.BallSpeed; a[pf + 3] += p.TargetDistance; a[pf + 4] += p.Power; a[pf + 5] += p.Quality; a[pf + 6] += p.OpponentDistance;
            a[oPassTime + (int)outcome * 2] += (step - p.Step) * LocomotionDefaults.FixedDeltaTime; a[oPassTime + (int)outcome * 2 + 1] += 1.0;
            if (outcome == PassOutcome.Completed)
            {
                a[oPassReceiver + (receiver == p.IntendedId ? 0 : 1)] += 1.0;
                if (p.PasserIdx >= 0) players[p.PasserIdx].PassesCompleted++;
            }
            if (p.SpaceIndex >= 0)
            {
                a[oSpaceOutcome + (int)outcome] += 1.0;
                ref SpaceAttempt s = ref space[p.SpaceIndex];
                s.Outcome = outcome; s.ReceiverId = receiver; s.ResolvedStep = step; s.ReceivedByRunner = receiver != null && receiver == s.RunnerId;
                Vec3 b = match.State.Ball.Physics.Position;
                s.FinalBallToTarget = (new Vec2(b.X, b.Z) - s.Target).Magnitude;
                int f = oSpaceFeatures + (int)outcome * 10;
                a[f] += 1.0; a[f + 1] += s.RunnerToTarget; a[f + 2] += s.OpponentToTarget; a[f + 3] += s.PasserToTarget; a[f + 4] += s.FinalBallToTarget; a[f + 5] += s.RunnerSpeed; a[f + 6] += s.RunnerArrival; a[f + 7] += s.RivalArrival; a[f + 8] += s.KeeperDistance; a[f + 9] += s.TargetToGoal;
            }
        }

        // ------------------------------------------------------------------ possession after a dribble, a protect or a hold

        private void FollowWindows(MatchBall ball)
        {
            int window = ToSteps(Config.PossessionWindowSeconds);
            for (int i = 0; i < windows.Length; i++)
            {
                if (!windows[i].Active || step <= windows[i].Step) continue;
                PendingWindow w = windows[i];
                if (ball.HasPossession && ball.PossessingTeam != w.Team) SettleWindow(ref windows[i], WindowOutcome.Lost);
                else if (!match.State.IsBallLive) SettleWindow(ref windows[i], WindowOutcome.Unknown);
                else if (step - w.Step >= window) SettleWindow(ref windows[i], ball.HasPossession ? WindowOutcome.Retained : WindowOutcome.Loose);
            }
        }

        private void SettleWindow(ref PendingWindow w, WindowOutcome outcome)
        {
            w.Active = false;
            a[oWindow + (int)w.Type * 6 + (int)outcome] += 1.0;
        }

        // ------------------------------------------------------------------ shots

        private void FollowShots(MatchBall ball)
        {
            int window = ToSteps(Config.ShotWindowSeconds);
            for (int i = 0; i < shots.Length; i++)
            {
                if (!shots[i].Active || step <= shots[i].Step) continue;
                PendingShot s = shots[i];
                int goalsNow = s.Team == TeamSide.Home ? match.State.Score.Home : match.State.Score.Away;
                if (goalsNow > s.GoalsFor) { SettleShot(ref shots[i], ShotOutcome.Goal); players[s.PlayerIdx].Goals++; }
                else if (ball.HasPossession && ball.PossessingPlayerId != null) SettleShot(ref shots[i], ball.PossessingTeam == s.Team ? ShotOutcome.TeamPossession : ShotOutcome.OpponentPossession);
                else if (lastOutStep >= s.Step) SettleShot(ref shots[i], ShotOutcome.OutOfPlay);
                else if (!match.State.IsBallLive || step - s.Step >= window) SettleShot(ref shots[i], ShotOutcome.Unknown);
            }
        }

        private void SettleShot(ref PendingShot s, ShotOutcome outcome)
        {
            s.Active = false;
            if (s.LogIndex >= 0) { shotLog[s.LogIndex].Outcome = outcome; shotLog[s.LogIndex].Settled = true; if (outcome == ShotOutcome.Goal) a[oShotQ + 5] += shotLog[s.LogIndex].Quality.ExpectedGoal; }
            a[oShotOutcome + (int)outcome] += 1.0;
            int bucket = s.Distance < 4f ? 0 : (s.Distance < 7f ? 1 : (s.Distance < 9f ? 2 : (s.Distance < 11f ? 3 : (s.Distance < 13f ? 4 : 5))));
            a[oShotDistance + bucket * ShotOutcomes + (int)outcome] += 1.0;
            a[oShotScore + (s.Score < 1.5f ? 0 : (s.Score < 2f ? 1 : (s.Score < 2.5f ? 2 : (s.Score < 3f ? 3 : 4)))) * ShotOutcomes + (int)outcome] += 1.0;
        }

        // ------------------------------------------------------------------ the carrier's decision

        /// <summary>One decision of one player (any player): counted by kind, by player and, for a carrier, explained.</summary>
        public void OnDecision(int stepNumber, WorldView view, int idx, in PlayerDecision d, BallCarrierEvaluator carrier, bool wasCarrierDecision)
        {
            if (Twin != null) Twin.OnDecision(stepNumber, view, idx, d, carrier, wasCarrierDecision);
            if (!Config.Enabled || match == null || Skip(view[idx].Team)) return;
            a[oDecisions] += 1.0; a[oKinds + (int)d.Kind] += 1.0;
            { WorldPlayer who = view[idx]; if (!who.IsGoalkeeper && who.HasHome) { bool attacking = view.Match.HasPossession && view.Match.PossessingTeam == who.Team; int o = oHome + (attacking ? 0 : 2); a[o] += (who.Position - who.Home).Magnitude; a[o + 1] += 1.0; } a[oDrift + 2] += 1.0; }
            players[idx].Decisions++;
            { float m = d.HasMovement ? d.Movement.Magnitude : 0f; a[oEffort + (int)d.Kind * 3 + (m < LocomotionDefaults.WalkThreshold ? 0 : (m < LocomotionDefaults.SprintThreshold ? 1 : 2))] += 1.0; }
            if (d.Kind == DecisionKind.MoveToBall) for (int i = 0; i < passes.Length; i++) if (passes[i].Active && passes[i].PasserIdx == idx) passes[i].PasserChased = true;
            if (!wasCarrierDecision || carrier == null) return;
            players[idx].CarrierDecisions++;
            lastShotScore[idx] = carrier.Summary.ShootValid ? carrier.Summary.ShootScore : 0f;
            switch (d.Kind)
            {
                case DecisionKind.Dribble: players[idx].Dribbles++; break;
                case DecisionKind.Protect: players[idx].Protects++; break;
                case DecisionKind.Hold: players[idx].Holds++; break;
            }
            ExplainCarrier(stepNumber, view, idx, d, carrier);
        }

        private void ExplainCarrier(int stepNumber, WorldView view, int idx, in PlayerDecision d, BallCarrierEvaluator ev)
        {
            CarrierSummary s = ev.Summary;
            int win = (int)s.Best;
            a[oCarrier] += 1.0;
            if (s.Pressure >= Config.PressedLevel) a[oContext] += 1.0;
            if (!s.DribbleFree) a[oContext + 1] += 1.0;
            if (s.Stamina <= Config.TiredLevel) a[oContext + 2] += 1.0;
            if (s.ShootValid && s.ShootScore >= Config.ClearChanceScore) { a[oProgress + 3] += 1.0; if (s.Best == CandidateType.Shoot) a[oProgress + 4] += 1.0; }
            a[oChosen + win] += 1.0; a[oChosenScore + win] += s.BestScore; a[oChosenPressure + win] += s.Pressure; a[oChosenStamina + win] += s.Stamina; a[oChosenGoal + win] += s.DistanceToGoal;
            if (s.RunnerUpType != CandidateType.None) { a[oRunnerUp + (int)s.RunnerUpType] += 1.0; a[oSecond + win * Types + (int)s.RunnerUpType] += 1.0; }
            double margin = s.Margin;
            if (double.IsInfinity(margin)) a[oMarginBuckets + 4] += 1.0;
            else
            {
                a[oChosenMargin + win] += margin; a[oChosenMarginN + win] += 1.0; a[oMargin] += margin; a[oMargin + 1] += 1.0;
                if (margin < MarginMin) MarginMin = margin;
                if (margin > MarginMax) MarginMax = margin;
                a[oMarginBuckets + (margin < 0.10 ? 0 : (margin < 0.25 ? 1 : (margin < 0.50 ? 2 : 3)))] += 1.0;
            }
            for (int k = 1; k < 5; k++) if ((s.RunMask & (1 << k)) != 0) a[oRunPresent + k] += 1.0;
            if (s.DribbleFree)
            {
                a[oDribble] += 1.0;
                int bucket = s.DribbleFreeDistance < 1.75f ? 0 : (s.DribbleFreeDistance < 3.5f ? 1 : (s.DribbleFreeDistance < 5f ? 2 : (s.DribbleFreeDistance < 7f ? 3 : (s.DribbleFreeDistance < 10f ? 4 : 5))));
                a[oFreeHist + bucket] += 1.0; if (s.Best == CandidateType.Dribble) a[oFreeHist + 6 + bucket] += 1.0;
            }
            if (s.Best == CandidateType.Dribble) { a[oDribble + 1] += s.DribbleSpace; a[oDribble + 2] += s.DribbleProgress; }

            // the best candidate of every kind (winner or not): how often it existed, its score and what it was made of; which kinds were not candidates and why
            for (int t = 0; t < Types; t++) { bestOfType[t] = double.NegativeInfinity; bestIndex[t] = -1; }
            for (int k = 0; k < ev.Count; k++)
            {
                ActionCandidate c = ev.At(k);
                int t = (int)c.Type;
                if (c.Score > bestOfType[t]) { bestOfType[t] = c.Score; bestIndex[t] = k; }
            }
            for (int t = 1; t < Types; t++)
            {
                if (bestIndex[t] >= 0)
                {
                    a[oAvailable + t] += 1.0; a[oBestN + t] += 1.0; a[oBestScore + t] += bestOfType[t];
                    ActionCandidate c = ev.At(bestIndex[t]);
                    for (int p = 0; p < Parts; p++) a[oBestParts + t * Parts + p] += c.Parts[p];
                }
                for (int r = 1; r < Rejections; r++) a[oRejected + t * Rejections + r] += ev.RejectionCount((CandidateType)t, (CandidateRejection)r);
            }
            if (bestIndex[win] >= 0) { ActionCandidate w = ev.At(bestIndex[win]); for (int p = 0; p < Parts; p++) a[oWinnerParts + win * Parts + p] += w.Parts[p]; }

            // what the executed decision led to (possession a moment later), counted once per episode of the same kind
            CandidateType executed = d.Kind == DecisionKind.Dribble ? CandidateType.Dribble : (d.Kind == DecisionKind.Protect ? CandidateType.Protect : (d.Kind == DecisionKind.Hold ? CandidateType.Hold : CandidateType.None));
            if (executed != CandidateType.None)
            {
                bool episode = lastType[idx] != executed || stepNumber - lastTypeStep[idx] > Config.EpisodeGapSteps;
                if (episode) { a[oEpisodes + (int)executed] += 1.0; AddWindow(stepNumber, idx, view[idx].Team, executed); }
            }
            lastType[idx] = executed; lastTypeStep[idx] = stepNumber;

            if (Config.TraceEnabled && stepNumber >= Config.TraceStartStep && stepNumber <= Config.TraceEndStep && (Config.TracePlayerId == null || Config.TracePlayerId == view[idx].PlayerId))
            {
                if (TraceCount >= trace.Length) TraceDropped++;
                else
                {
                    CarrierTrace tr = new CarrierTrace { Step = stepNumber, PlayerId = view[idx].PlayerId, Team = view[idx].Team, Best = s.Best, BestScore = s.BestScore, RunnerUp = s.RunnerUpType, RunnerUpScore = s.RunnerUpScore, Margin = s.Margin, Pressure = s.Pressure, Stamina = s.Stamina, DistanceToGoal = s.DistanceToGoal, AvailableMask = s.AvailableMask };
                    if (bestIndex[win] >= 0) tr.BestParts = ev.At(bestIndex[win]).Parts;
                    if (s.RunnerUpType != CandidateType.None && bestIndex[(int)s.RunnerUpType] >= 0) tr.RunnerUpParts = ev.At(bestIndex[(int)s.RunnerUpType]).Parts;
                    trace[TraceCount++] = tr;
                }
            }
        }

        private void AddWindow(int stepNumber, int idx, TeamSide team, CandidateType type)
        {
            for (int i = 0; i < windows.Length; i++)
                if (!windows[i].Active) { windows[i] = new PendingWindow { Active = true, Step = stepNumber, PlayerIdx = idx, Team = team, Type = type }; return; }
            // no room: the oldest is closed as unknown to make room
            int oldest = 0;
            for (int i = 1; i < windows.Length; i++) if (windows[i].Step < windows[oldest].Step) oldest = i;
            SettleWindow(ref windows[oldest], WindowOutcome.Unknown);
            windows[oldest] = new PendingWindow { Active = true, Step = stepNumber, PlayerIdx = idx, Team = team, Type = type };
        }
    }
}
