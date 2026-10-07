using System;

namespace FS27.Core
{
    /// <summary>
    /// The FIRST level of decision (Phase 8): one player looks at the world through a <see cref="WorldView"/>, recognises his situation (has the ball or not, is he the nearest, is he under pressure, is there
    /// a team-mate to pass to, is there room to run, is there a shot) and answers with a <see cref="PlayerDecision"/> carrying a <see cref="MovementIntent"/> and/or an <see cref="ActionIntent"/>.
    /// It is a short, fixed hierarchy, NOT a tactical AI: no formations as tactics, no marking, no cover, no pressing, no transitions, no scoring of futures.
    /// It is a pure function of (view, player): it reads only the view (never a definition, an attribute or the match), changes nothing, uses no chance and no time. The same view and the same
    /// player always give the same decision. The Core stays the authority: the intents it returns go through locomotion and the action resolver like any other.
    /// </summary>
    public sealed class BasicDecisionEngine
    {
        private readonly PitchBounds bounds;
        private readonly BasicDecisionTuning t;
        private readonly PlayerObservation obs = new PlayerObservation();

        public BasicDecisionTuning Tuning => t;

        public BasicDecisionEngine(PitchBounds bounds, BasicDecisionTuning tuning = null)
        {
            this.bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
            t = tuning ?? new BasicDecisionTuning();
        }

        public PlayerDecision Decide(WorldView view, string playerId)
        {
            int idx = view.IndexOf(playerId);
            if (idx < 0) return PlayerDecision.Idle(playerId, DecisionReason.NotOnPitch);
            WorldPlayer me = view[idx];
            if (!me.IsOnPitch) return PlayerDecision.Idle(playerId, DecisionReason.NotOnPitch);
            if (!view.Match.BallLive) return PlayerDecision.Idle(playerId, DecisionReason.NotPlaying);
            if (me.ActionLocked) return PlayerDecision.Idle(playerId, DecisionReason.Locked);
            view.Observe(idx, obs);
            if (me.IsGoalkeeper) return Keeper(view, idx, me);
            return me.HasBall ? WithBall(view, idx, me) : WithoutBall(view, idx, me);
        }

        // ------------------------------------------------------------------ geometry shared by the rules

        private static Vec2 AttackDirection(WorldView view, TeamSide team)
        {
            Vec2 d = WorldGeometry.AttackGoal(view.Match, view.Field, team) - WorldGeometry.OwnGoal(view.Match, view.Field, team);
            return d.Normalized;
        }

        private static Vec2 Lerp(Vec2 a, Vec2 b, float k) { return a + (b - a) * k; }

        private PlayerDecision MoveTo(string playerId, Vec2 me, Vec2 target, float magnitude, DecisionKind kind, DecisionReason reason)
        {
            Vec2 goal = bounds.ClampPlayer(target);
            if ((goal - me).Magnitude <= t.IdleRadius) return new PlayerDecision { PlayerId = playerId, Kind = DecisionKind.Idle, Reason = DecisionReason.ArrivedAtPoint, HasMovement = true, Movement = MovementIntent.Stand };
            return new PlayerDecision { PlayerId = playerId, Kind = kind, Reason = reason, HasMovement = true, Movement = MovementIntent.TowardsTarget(me, goal, magnitude, t.ArriveRadius) };
        }

        /// <summary>Am I the field player of my team nearest to the ball? (the goalkeeper in goal does not count; ties go to the earlier in the squad)</summary>
        private static bool IsNearestFieldPlayerToBall(WorldView view, int idx, in WorldPlayer me)
        {
            float mine = WorldGeometry.DistanceToBall(me, view.Ball);
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team != me.Team || o.IsGoalkeeper) continue;
                float d = WorldGeometry.DistanceToBall(o, view.Ball);
                if (d < mine || (d == mine && i < idx)) return false;
            }
            return true;
        }

        /// <summary>How many team-mates (field players, the carrier and me excluded) are nearer to the carrier than I am.</summary>
        private static int TeammatesNearerToCarrier(WorldView view, int idx, in WorldPlayer me, in WorldPlayer carrier, int carrierIdx)
        {
            float mine = (me.Position - carrier.Position).Magnitude;
            int n = 0;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx || i == carrierIdx) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team != me.Team || o.IsGoalkeeper) continue;
                float d = (o.Position - carrier.Position).Magnitude;
                if (d < mine || (d == mine && i < idx)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ without the ball

        private PlayerDecision WithoutBall(WorldView view, int idx, in WorldPlayer me)
        {
            WorldBall ball = view.Ball;
            Vec2 home = me.HasHome ? me.Home : me.Position;
            bool nearest = IsNearestFieldPlayerToBall(view, idx, me);

            if (!ball.HasPossessor)
            {
                if (nearest) return MoveTo(me.PlayerId, me.Position, ball.Ground, t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.ClosestToLooseBall);
                return Position(view, me, home, t.PositionBallPull, DecisionReason.LooseBallNotClosest);
            }

            int carrierIdx = view.IndexOf(ball.PossessorId);
            WorldPlayer carrier = view[carrierIdx];
            if (carrier.Team != me.Team)
            {
                if (nearest) return MoveTo(me.PlayerId, me.Position, ball.Ground, t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.ChaseOpponent);
                return Position(view, me, home, t.DefendBallPull, DecisionReason.OpponentHasBall);
            }

            // a team-mate has the ball
            if ((me.Position - home).Magnitude > t.FarFromHomeDistance) return MoveTo(me.PlayerId, me.Position, home, t.ChaseMagnitude, DecisionKind.MoveToPoint, DecisionReason.FarFromHome);
            float toCarrier = (me.Position - carrier.Position).Magnitude;
            if (toCarrier <= t.SupportRadius && TeammatesNearerToCarrier(view, idx, me, carrier, carrierIdx) < t.SupportCount)
                return MoveTo(me.PlayerId, me.Position, SupportPoint(view, me, carrier, home), t.PositionMagnitude, DecisionKind.Support, DecisionReason.SupportRole);
            return Position(view, me, home, t.PositionBallPull, DecisionReason.TeammateHasBall);
        }

        /// <summary>A place near his home, drawn towards the ball: the basic "keep your position and follow the play".</summary>
        private PlayerDecision Position(WorldView view, in WorldPlayer me, Vec2 home, float pull, DecisionReason reason)
        {
            if ((me.Position - home).Magnitude > t.FarFromHomeDistance) return MoveTo(me.PlayerId, me.Position, home, t.ChaseMagnitude, DecisionKind.MoveToPoint, DecisionReason.FarFromHome);
            return MoveTo(me.PlayerId, me.Position, Lerp(home, view.Ball.Ground, pull), t.PositionMagnitude, DecisionKind.MoveToPoint, reason);
        }

        /// <summary>Where a supporting player stands: between his home and the carrier, a little further up the pitch, never too close to him nor too far.</summary>
        private Vec2 SupportPoint(WorldView view, in WorldPlayer me, in WorldPlayer carrier, Vec2 home)
        {
            Vec2 attack = AttackDirection(view, me.Team);
            Vec2 p = Lerp(home, carrier.Position, t.SupportBallPull) + attack * t.SupportAdvance;
            Vec2 off = p - carrier.Position;
            float d = off.Magnitude;
            Vec2 dir = d > 1e-4f ? off * (1f / d) : new Vec2(0f, home.Y >= carrier.Position.Y ? 1f : -1f);
            if (d < t.SupportMinDistance) p = carrier.Position + dir * t.SupportMinDistance;
            else if (d > t.SupportMaxDistance) p = carrier.Position + dir * t.SupportMaxDistance;
            return p;
        }

        // ------------------------------------------------------------------ with the ball

        private PlayerDecision WithBall(WorldView view, int idx, in WorldPlayer me)
        {
            bool canKick = !me.KickCooldown;
            float nearestOpponent = obs.NearestOpponentDistance;
            bool pressed = nearestOpponent < t.PressureRadius;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);

            if (canKick && TryShoot(view, me, goal, nearestOpponent, out PlayerDecision shot)) return shot;

            if (canKick)
            {
                int receiver = BestReceiver(view, idx, me, pressed, false, out float gain);
                if (receiver < 0 && pressed) receiver = BestReceiver(view, idx, me, true, true, out gain);
                if (receiver >= 0) return PassTo(view, me, view[receiver], pressed ? DecisionReason.UnderPressurePass : DecisionReason.FreePass);
            }

            if (TryDribble(view, me, goal, canKick, pressed, out PlayerDecision dribble)) return dribble;
            if (pressed) return Protect(view, me);

            // nothing applies: keep moving towards the goal
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.NoActionFallback, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, bounds.ClampPlayer(goal), t.DribbleMagnitude, 0f)
            };
        }

        private bool TryShoot(WorldView view, in WorldPlayer me, Vec2 goal, float nearestOpponent, out PlayerDecision decision)
        {
            decision = default(PlayerDecision);
            float distance = (goal - me.Position).Magnitude;
            if (distance > t.ShootRange || nearestOpponent < t.ShootMinOpponentDistance) return false;
            if (Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, goal)) > t.ShootMaxAngleDegrees) return false;
            if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, goal, t.ShootLaneClearance, true)) return false;

            // aim for the side of the goal the goalkeeper is not on
            float side = 1f;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (o.IsOnPitch && o.Team != me.Team && o.IsGoalkeeper) { side = o.Position.Y >= goal.Y ? -1f : 1f; break; }
            }
            Vec2 aim = new Vec2(goal.X, goal.Y + side * t.ShotAimOffset);
            Vec2 dir = ActionModel.DirectionOr(aim - me.Position, ActionModel.DirectionOr(goal - me.Position, me.Facing));
            var intent = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Shot, dir, t.ShotPower);
            DecisionReason reason = DecisionReason.ClearShot;
            if (distance >= t.CurveMinDistance && view.CanUse(me.PlayerId, FootballActionKind.CurvedShot).Allowed)
            {
                intent.Kind = FootballActionKind.CurvedShot;
                intent.Curve = (me.PreferredFoot == PreferredFoot.Right ? 1f : -1f) * t.CurveAmount;
                reason = DecisionReason.CurvedShotAvailable;
            }
            decision = new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Shoot, Reason = reason, HasAction = true, Action = intent, HasMovement = true, Movement = MovementIntent.Stand };
            return true;
        }

        /// <summary>
        /// The best team-mate to pass to, or -1: on the pitch, not too near nor too far, with a clear lane, with space around him and (unless the carrier is under pressure) further forward than the carrier.
        /// The score weighs how far forward he is, how much space he has, how close he is to a comfortable distance and how much the carrier has to turn. The goalkeeper is only considered when asked.
        /// </summary>
        private int BestReceiver(WorldView view, int idx, in WorldPlayer me, bool pressed, bool allowKeeper, out float bestGain)
        {
            bestGain = 0f;
            int best = -1;
            float bestScore = float.NegativeInfinity;
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            float mineToGoal = (goal - me.Position).Magnitude;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer r = view[i];
                if (!r.IsOnPitch || r.Team != me.Team || (r.IsGoalkeeper && !allowKeeper)) continue;
                float d = (r.Position - me.Position).Magnitude;
                if (d < t.PassMinDistance || d > t.PassMaxDistance) continue;
                if (!WorldGeometry.IsLaneClear(view, me.Team, me.Position, r.Position, view.Tuning.PassLaneClearance)) continue;
                float space = WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position);
                if (space < t.ReceiverMinSpace) continue;
                float gain = (mineToGoal - (goal - r.Position).Magnitude) / view.Field.Length;
                if (pressed ? gain < -t.PassMaxBackwardGain : gain < t.PassMinForwardGain) continue;
                float turn = Math.Abs(WorldGeometry.RelativeAngle(me.Facing, me.Position, r.Position)) / t.FullTurnDegrees;
                float score = t.PassForwardWeight * gain + t.PassSpaceWeight * Math.Min(space, t.PassSpaceCap) / t.PassSpaceCap
                              - t.PassDistanceWeight * Math.Abs(d - t.PassPreferredDistance) / t.PassMaxDistance + t.PassAngleWeight * (1f - turn);
                if (score > bestScore) { bestScore = score; best = i; bestGain = gain; }
            }
            return best;
        }

        private PlayerDecision PassTo(WorldView view, in WorldPlayer me, in WorldPlayer receiver, DecisionReason reason)
        {
            Vec2 dir = ActionModel.DirectionOr(receiver.Position - me.Position, me.Facing);
            float d = (receiver.Position - me.Position).Magnitude;
            FootballActionKind kind = FootballActionKind.ShortPass;
            if (d >= t.LobPassDistance && view.CanUse(me.PlayerId, FootballActionKind.SkillLobPass).Allowed) { kind = FootballActionKind.SkillLobPass; reason = DecisionReason.LobPassAvailable; }
            else if (d >= t.LongPassDistance) kind = FootballActionKind.LongPass;
            float power = Math.Min(1f, t.PassPowerBase + (1f - t.PassPowerBase) * d / t.PassPowerRefDistance);
            var intent = new ActionIntent(me.Team, me.PlayerId, kind, dir, power) { Target = PassTarget.ToPlayer(receiver.PlayerId) };
            return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = reason, HasAction = true, Action = intent, TargetPlayerId = receiver.PlayerId, HasMovement = true, Movement = MovementIntent.Stand };
        }

        /// <summary>Is there room ahead in this direction? (no opponent inside the cone within the clear distance)</summary>
        private bool Clear(WorldView view, in WorldPlayer me, Vec2 dir, out int blocker)
        {
            blocker = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == me.Team) continue;
                Vec2 off = o.Position - me.Position;
                float d = off.Magnitude;
                if (d > t.DribbleClearDistance || d <= 0f) continue;
                if (WorldGeometry.AngleBetween(dir, off) > t.DribbleConeDegrees) continue;
                if (d < best) { best = d; blocker = i; }
            }
            return blocker < 0;
        }

        private bool TryDribble(WorldView view, in WorldPlayer me, Vec2 goal, bool canKick, bool pressed, out PlayerDecision decision)
        {
            decision = default(PlayerDecision);
            Vec2 toGoal = ActionModel.DirectionOr(goal - me.Position, me.Facing);
            Vec2 dir = toGoal;
            bool space = Clear(view, me, toGoal, out int blocker);
            if (!space)
            {
                // try the side away from whoever blocks the way
                Vec2 off = view[blocker].Position - me.Position;
                float sign = ActionModel.SignedAngle(toGoal, off) >= 0f ? -1f : 1f;
                Vec2 alt = ActionModel.Rotate(toGoal, sign * t.DribbleAvoidDegrees);
                if (Clear(view, me, alt, out _)) { dir = ActionModel.DirectionOr(toGoal + (alt - toGoal) * t.AvoidWeight, alt); space = true; }
                else if (canKick && (off.Magnitude <= t.SkillMoveTriggerDistance) && view.CanUse(me.PlayerId, FootballActionKind.SkillMove).Allowed)
                {
                    var move = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.SkillMove, alt, ActionDefaults.DefaultPower);
                    decision = new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.SkillMoveAvailable, HasAction = true, Action = move, HasMovement = true, Movement = MovementIntent.Move(alt, t.DribbleMagnitude) };
                    return true;
                }
            }
            if (!space) return false;
            Vec2 ahead = bounds.ClampPlayer(me.Position + dir * t.DribbleLookahead);
            decision = new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Dribble, Reason = DecisionReason.SpaceToDribble, HasMovement = true,
                Movement = MovementIntent.TowardsTarget(me.Position, ahead, t.DribbleMagnitude, 0f)
            };
            return true;
        }

        private PlayerDecision Protect(WorldView view, in WorldPlayer me)
        {
            Vec2 away = me.Facing * -1f;
            if (obs.OpponentCount > 0) away = ActionModel.DirectionOr(me.Position - view[obs.OpponentIndex(0)].Position, away);
            return new PlayerDecision
            {
                PlayerId = me.PlayerId, Kind = DecisionKind.Protect, Reason = DecisionReason.UnderPressureProtect,
                HasAction = true, Action = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.Shield, me.Facing, ActionDefaults.DefaultPower),
                HasMovement = true, Movement = MovementIntent.Move(away, t.ProtectAwayMagnitude)
            };
        }

        // ------------------------------------------------------------------ the goalkeeper in goal (a minimal rule)

        private PlayerDecision Keeper(WorldView view, int idx, in WorldPlayer me)
        {
            bool attacksPlusX = view.Match.HomeAttacksPositiveX;
            if (me.HasBall)
            {
                int receiver = BestReceiver(view, idx, me, true, false, out _);
                Vec2 dir; string targetId = null;
                if (receiver >= 0) { dir = ActionModel.DirectionOr(view[receiver].Position - me.Position, me.Facing); targetId = view[receiver].PlayerId; }
                else dir = AttackDirection(view, me.Team);
                float d = receiver >= 0 ? (view[receiver].Position - me.Position).Magnitude : t.LongPassDistance;
                var intent = new ActionIntent(me.Team, me.PlayerId, FootballActionKind.GoalkeeperDistribution, dir, t.KeeperDistributePower) { Long = d >= t.LongPassDistance };
                if (targetId != null) intent.Target = PassTarget.ToPlayer(targetId);
                return new PlayerDecision { PlayerId = me.PlayerId, Kind = DecisionKind.Pass, Reason = DecisionReason.KeeperDistribute, HasAction = true, Action = intent, TargetPlayerId = targetId, HasMovement = true, Movement = MovementIntent.Stand };
            }

            Vec2 own = WorldGeometry.OwnGoal(view.Match, view.Field, me.Team);
            Vec2 ball = view.Ball.Ground;
            if ((ball - me.Position).Magnitude <= t.KeeperReactDistance)
                return MoveTo(me.PlayerId, me.Position, bounds.ClampToGoalZone(ball, attacksPlusX, me.Team), t.ChaseMagnitude, DecisionKind.MoveToBall, DecisionReason.KeeperReact);
            Vec2 toBall = ActionModel.DirectionOr(ball - own, AttackDirection(view, me.Team));
            return MoveTo(me.PlayerId, me.Position, bounds.ClampToGoalZone(own + toBall * t.KeeperLineDepth, attacksPlusX, me.Team), t.PositionMagnitude, DecisionKind.MoveToPoint, DecisionReason.KeeperHold);
        }
    }
}
