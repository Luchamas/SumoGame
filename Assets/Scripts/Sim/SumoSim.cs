using UnityEngine;

namespace SumoGame
{
    public struct FighterInput
    {
        public float Lateral;   // -1 to 1, circling left or right around the opponent
        public bool Grab;       // grab, push or resist depending on context
        public bool Strike;
    }

    public enum FighterState
    {
        Idle, Moving, GrabStartup, Recovering, Recoiling, Striking, Stunned, Pushing, Grabbed, Driving, Reeling, Falling, Victory,
    }

    public enum MatchPhase { Intro, Countdown, Fighting, RoundOver, MatchOver }

    public enum SumoImpact { Strike, Grab, Shove, Drive, Whiff, Ducked, Clash, RingOut }

    public class Fighter
    {
        public Vector2 Position;
        public Vector2 Facing;
        public FighterState State;
        public float StateTime;
        public Vector2 LungeDirection;

        public bool StrikeSpent;
        public bool StrikeDucked;
        public float RecoveryTime;
        public float StunImmunity;
        public float StruggleCooldown;
        public float RegrabCooldown;
        public bool StruggleQueued;
        public Vector2 Knockback;
        public float Lateral;

        public int Score;
        public FighterInput Previous;

        public bool IsStriking => State == FighterState.Striking;
        public bool IsLocked => State == FighterState.Pushing || State == FighterState.Grabbed;
        public bool IsSidewalking => State == FighterState.Moving && Mathf.Abs(Lateral) > 0.5f;

        public void Enter(FighterState state)
        {
            State = state;
            StateTime = 0f;
        }

        public void ResetForRound(Vector2 position, Vector2 facing)
        {
            Position = position;
            Facing = facing;
            Enter(FighterState.Idle);
            StunImmunity = 0f;
            StruggleCooldown = 0f;
            RegrabCooldown = 0f;
            StruggleQueued = false;
            Knockback = Vector2.zero;
            Lateral = 0f;
            Previous = default;
        }
    }

    /// <summary>Tunable numbers, from spec.md. Distances are metres on the dohyo, times are seconds.</summary>
    public class SumoRules
    {
        public float RingRadius = 4.2f;
        public float StartDistance = 1.75f;
        public int RoundsToWin = 2;
        public float IntroTime = 4f;
        public float CountdownTime = 1.2f;
        public float RoundOverTime = 2.4f;

        public float MoveSpeed = 2.2f;
        public float BodyRadius = 0.5f;
        public float EngageDistance = 1.75f;
        public float EngageSlack = 0.2f;
        public float CentreTie = 0.1f;
        public float RingMargin = 0.05f;
        public float TurnSpeed = 540f;

        public float GrabRange = 2.3f;
        public float GrabStartup = 0.25f;
        public float LungeSpeed = 4f;
        public float GrabReach = 1.4f;
        public float WhiffRecovery = 0.75f;
        public float WhiffStumble = 5f;
        public float WhiffVeer = 25f;
        public float RecoilTime = 0.7f;
        public float RecoilSpeed = 3.2f;

        public float LockDistance = 1.35f;
        public float MinHoldBeforeEscape = 0.5f;
        public float MaxHold = 3f;
        public float PushCooldown = 0.7f;
        public float RegrabDelay = 1.2f;
        public float ShoveTime = 0.25f;
        public float ResistInterval = 0.25f;
        public float EscapeRecovery = 0.6f;
        public float ShoveStep = 0.22f;
        public float ResistStep = 0.05f;
        public float PushSpeed = 2.5f;
        public float ResistedPush = 0.6f;
        public float PushSteering = 25f;
        public float FatigueStart = 45f;
        public float FatiguePerSecond = 0.06f;
        public float DriveTime = 1.25f;
        public float DriveSpeed = 0.9f;

        public float StrikeStartup = 0.2f;
        public float StrikeActive = 0.15f;
        public float StrikeRecovery = 0.35f;
        public float StrikeReach = 2f;
        public float StrikeReachVsSidewalk = 2.8f;
        public float StunTime = 0.8f;
        public float StunImmunity = 0.5f;
        public float StrikeKnockback = 2.4f;
        public float DuckedStrikeLag = 0.3f;
        public float KnockbackDecay = 8f;
    }

    /// <summary>
    /// The whole match as a state machine, independent of rendering and input so it can
    /// be stepped from a fixed update or a test. Index 0 is P1, index 1 is P2.
    ///
    /// The three actions are rock-paper-scissors: walking sideways slips a grab, a grab
    /// shrugs off a strike and catches its recovery, and a strike catches a sidewalker.
    /// </summary>
    public class SumoSim
    {
        public readonly SumoRules Rules;
        public readonly Fighter[] Fighters = { new Fighter(), new Fighter() };

        public MatchPhase Phase { get; private set; }
        public float PhaseTime { get; private set; }
        public int RoundWinner { get; private set; } = -1;
        public int MatchWinner { get; private set; } = -1;

        /// <summary>0 to 1. Starts at a half, the attacker wins at 1 and the defender escapes at 0.</summary>
        public float Struggle { get; private set; }

        /// <summary>Raised for anything the player should see or hear land, with where it happened.</summary>
        public event System.Action<SumoImpact, Vector2> Impact;

        /// <summary>True for the moment the attacker's latest shove is driving forward.</summary>
        public bool Shoving => pushFor > 0f;

        /// <summary>Seconds the current grab has lasted.</summary>
        public float HoldTime { get; private set; }

        float pushFor, resistFor;

        public SumoSim(SumoRules rules = null)
        {
            Rules = rules ?? new SumoRules();
            StartMatch();
        }

        public void StartMatch()
        {
            Fighters[0].Score = 0;
            Fighters[1].Score = 0;
            MatchWinner = -1;
            ResetFighters();
            SetPhase(MatchPhase.Intro);
        }

        void StartRound()
        {
            ResetFighters();
            SetPhase(MatchPhase.Countdown);
        }

        void ResetFighters()
        {
            RoundWinner = -1;
            var half = Rules.StartDistance * 0.5f;
            Fighters[0].ResetForRound(new Vector2(-half, 0f), Vector2.right);
            Fighters[1].ResetForRound(new Vector2(half, 0f), Vector2.left);
        }

        void SetPhase(MatchPhase phase)
        {
            Phase = phase;
            PhaseTime = 0f;
        }

        /// <summary>Confirm cuts the wait after a round short.</summary>
        public void SkipRoundDelay()
        {
            if (Phase == MatchPhase.RoundOver) PhaseTime = Rules.RoundOverTime;
        }

        public void Step(float dt, FighterInput p1, FighterInput p2)
        {
            PhaseTime += dt;
            switch (Phase)
            {
                case MatchPhase.Intro:
                    if (PhaseTime >= Rules.IntroTime) SetPhase(MatchPhase.Fighting);
                    break;

                case MatchPhase.Countdown:
                    if (PhaseTime >= Rules.CountdownTime) SetPhase(MatchPhase.Fighting);
                    break;

                case MatchPhase.Fighting:
                    StepFight(dt, p1, p2);
                    break;

                case MatchPhase.RoundOver:
                    if (PhaseTime >= Rules.RoundOverTime)
                    {
                        if (Fighters[RoundWinner].Score >= Rules.RoundsToWin)
                        {
                            MatchWinner = RoundWinner;
                            SetPhase(MatchPhase.MatchOver);
                        }
                        else
                        {
                            StartRound();
                        }
                    }
                    break;
            }
        }

        void StepFight(float dt, FighterInput p1, FighterInput p2)
        {
            var a = Fighters[0];
            var b = Fighters[1];

            Tick(a, dt);
            Tick(b, dt);

            HandleInput(a, b, p1);
            HandleInput(b, a, p2);

            ResolveStrikes(a, b);
            ResolveLunges(a, b);

            if (a.IsLocked)
            {
                StepLock(a.State == FighterState.Pushing ? a : b, a.State == FighterState.Pushing ? b : a, dt);
            }
            else if (a.State == FighterState.Driving)
            {
                StepDrive(a, b, dt);
            }
            else if (b.State == FighterState.Driving)
            {
                StepDrive(b, a, dt);
            }
            else
            {
                Move(a, b, dt);
                Move(b, a, dt);
                SeparateBodies(a, b);
            }

            CheckRingOut();
        }

        void Tick(Fighter f, float dt)
        {
            f.StateTime += dt;
            f.StunImmunity = Mathf.Max(0f, f.StunImmunity - dt);
            f.StruggleCooldown = Mathf.Max(0f, f.StruggleCooldown - dt);
            f.RegrabCooldown = Mathf.Max(0f, f.RegrabCooldown - dt);
        }

        void HandleInput(Fighter self, Fighter other, FighterInput input)
        {
            var grabPressed = input.Grab && !self.Previous.Grab;
            var strikePressed = input.Strike && !self.Previous.Strike;
            self.Previous = input;
            self.Lateral = Mathf.Clamp(input.Lateral, -1f, 1f);

            if (self.IsLocked)
            {
                if (self.State == FighterState.Pushing)
                {
                    // One shove, then a cooldown. Presses during it do nothing, so spamming cannot win.
                    if (grabPressed && self.StruggleCooldown <= 0f) Shove(self);
                    return;
                }

                // The defender's extra presses wait for the interval, so mashing costs them nothing.
                self.StruggleQueued |= grabPressed;
                if (self.StruggleQueued && self.StruggleCooldown <= 0f) Resist(self);
                return;
            }

            if (self.State != FighterState.Idle && self.State != FighterState.Moving) return;

            if (strikePressed)
            {
                self.StrikeSpent = false;
                self.StrikeDucked = false;
                self.Enter(FighterState.Striking);
            }
            else if (grabPressed && self.RegrabCooldown <= 0f && Vector2.Distance(self.Position, other.Position) <= Rules.GrabRange)
            {
                self.LungeDirection = (other.Position - self.Position).normalized;
                self.Enter(FighterState.GrabStartup);
            }
            else
            {
                // Once walking in, finish the step. The slack stops small drifts flickering the state.
                var slack = self.State == FighterState.Moving ? 0.02f : Rules.EngageSlack;
                var closing = HoldsTheCentre(self, other)
                    && Vector2.Distance(self.Position, other.Position) > Rules.EngageDistance + slack;
                var state = closing || Mathf.Abs(self.Lateral) > 0.1f ? FighterState.Moving : FighterState.Idle;
                if (self.State != state) self.Enter(state);
            }
        }

        /// <summary>
        /// Only the wrestler nearer the middle walks in, so ground lost to a shove or strike
        /// stays lost until you strafe around the opponent to win the middle back.
        /// </summary>
        bool HoldsTheCentre(Fighter self, Fighter other) =>
            self.Position.magnitude <= other.Position.magnitude + Rules.CentreTie;

        void Shove(Fighter self)
        {
            self.StruggleCooldown = Rules.PushCooldown;
            Struggle += Rules.ShoveStep;
            pushFor = Rules.ShoveTime;
            Raise(SumoImpact.Shove);
        }

        void Resist(Fighter self)
        {
            self.StruggleQueued = false;
            self.StruggleCooldown = Rules.ResistInterval;
            Struggle -= Rules.ResistStep;
            resistFor = Rules.ResistInterval;
        }

        void ResolveStrikes(Fighter a, Fighter b)
        {
            // Decide both before applying either, so a trade does not favour P1.
            var aLands = StrikeLands(a, b);
            var bLands = StrikeLands(b, a);
            if (aLands) ApplyStrike(a, b);
            if (bLands) ApplyStrike(b, a);
        }

        bool StrikeLands(Fighter self, Fighter other)
        {
            if (!self.IsStriking || self.StrikeSpent) return false;
            if (self.StateTime < Rules.StrikeStartup || self.StateTime >= Rules.StrikeStartup + Rules.StrikeActive) return false;
            if (other.IsLocked) return false;

            // The grab drives in low, so the strike passes overhead.
            if (other.State == FighterState.GrabStartup)
            {
                self.StrikeSpent = true;
                self.StrikeDucked = true;
                Raise(SumoImpact.Ducked);
                return false;
            }

            var reach = other.IsSidewalking ? Rules.StrikeReachVsSidewalk : Rules.StrikeReach;
            return Vector2.Distance(self.Position, other.Position) <= reach;
        }

        void ApplyStrike(Fighter self, Fighter other)
        {
            self.StrikeSpent = true;
            var push = (other.Position - self.Position).normalized * Rules.StrikeKnockback;
            other.Knockback = push;
            Raise(SumoImpact.Strike);
            if (other.State != FighterState.Stunned && other.StunImmunity <= 0f) other.Enter(FighterState.Stunned);
        }

        void Recover(Fighter f, float time)
        {
            f.RecoveryTime = time;
            f.Enter(FighterState.Recovering);
        }

        void ResolveLunges(Fighter a, Fighter b)
        {
            var aDone = a.State == FighterState.GrabStartup && a.StateTime >= Rules.GrabStartup;
            var bDone = b.State == FighterState.GrabStartup && b.StateTime >= Rules.GrabStartup;

            // Two grabs meeting are a draw, whichever landed first.
            var bothLunging = a.State == FighterState.GrabStartup && b.State == FighterState.GrabStartup;
            if (bothLunging && (aDone || bDone))
            {
                ThrowApart(a, b);
                Raise(SumoImpact.Clash);
            }
            else if (aDone && !b.IsStriking)
            {
                FinishLunge(a, b);
            }
            else if (bDone && !a.IsStriking)
            {
                FinishLunge(b, a);
            }
        }

        void ThrowApart(Fighter a, Fighter b)
        {
            var apart = (b.Position - a.Position).normalized * Rules.RecoilSpeed;
            Recoil(a, -apart);
            Recoil(b, apart);
        }

        void Recoil(Fighter f, Vector2 push)
        {
            f.RecoveryTime = Rules.RecoilTime;
            f.Enter(FighterState.Recoiling);
            f.Knockback = push;
        }

        void FinishLunge(Fighter self, Fighter other)
        {
            var inReach = Vector2.Distance(self.Position, other.Position) <= Rules.GrabReach;
            self.RegrabCooldown = Rules.RegrabDelay;
            if (!inReach || other.IsSidewalking)
            {
                Recover(self, Rules.WhiffRecovery);
                self.Knockback = StumbleDirection(self, other) * Rules.WhiffStumble;
                Raise(SumoImpact.Whiff);
                return;
            }

            if (other.State == FighterState.Stunned) other.StunImmunity = Rules.StunImmunity;
            self.Enter(FighterState.Pushing);
            other.Enter(FighterState.Grabbed);
            self.Knockback = Vector2.zero;
            other.Knockback = Vector2.zero;
            self.StruggleCooldown = 0f;
            other.StruggleCooldown = 0f;
            self.StruggleQueued = false;
            other.StruggleQueued = false;
            Struggle = 0.5f;
            HoldTime = 0f;
            pushFor = 0f;
            resistFor = 0f;

            self.Facing = (other.Position - self.Position).normalized;
            other.Facing = -self.Facing;

            // The attacker stops where the bodies meet. Moving the defender here could
            // put them over the rope without a single shove.
            self.Position = other.Position - self.Facing * Rules.LockDistance;
            Raise(SumoImpact.Grab);
        }

        /// <summary>
        /// A grab that a sidestep slipped veers off away from the sidestepper, so the two end up
        /// clearly apart rather than brushing past each other.
        /// </summary>
        Vector2 StumbleDirection(Fighter self, Fighter other)
        {
            var lunge = self.LungeDirection;
            if (!other.IsSidewalking) return lunge;
            var toOther = other.Position - self.Position;
            var side = Mathf.Sign(lunge.x * toOther.y - lunge.y * toOther.x);
            var radians = -side * Rules.WhiffVeer * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(lunge.x * cos - lunge.y * sin, lunge.x * sin + lunge.y * cos);
        }

        void StepLock(Fighter attacker, Fighter defender, float dt)
        {
            HoldTime += dt;
            pushFor = Mathf.Max(0f, pushFor - dt);
            resistFor = Mathf.Max(0f, resistFor - dt);

            if (HoldTime < Rules.MinHoldBeforeEscape) Struggle = Mathf.Max(Struggle, Rules.ResistStep);

            // The cooldown runs from when the grab ends, however it ended.
            attacker.RegrabCooldown = Rules.RegrabDelay;

            if (Struggle >= 1f)
            {
                attacker.Enter(FighterState.Driving);
                defender.Enter(FighterState.Reeling);
                Raise(SumoImpact.Drive);
                return;
            }

            if (Struggle <= 0f)
            {
                defender.Enter(FighterState.Idle);
                Recover(attacker, Rules.EscapeRecovery);
                return;
            }

            if (HoldTime >= Rules.MaxHold)
            {
                ThrowApart(attacker, defender);
                return;
            }

            Steer(attacker, dt);
            var speed = pushFor > 0f ? Rules.PushSpeed : 0f;
            if (resistFor > 0f) speed *= Rules.ResistedPush;
            speed *= Fatigue;

            attacker.Position += attacker.Facing * (speed * dt);
            defender.Position = attacker.Position + attacker.Facing * Rules.LockDistance;
            defender.Facing = -attacker.Facing;
        }

        /// <summary>The final shove: the defender reels back with their arms out, then is left stunned.</summary>
        void StepDrive(Fighter attacker, Fighter defender, float dt)
        {
            attacker.Position += attacker.Facing * (Rules.DriveSpeed * dt);
            defender.Position = attacker.Position + attacker.Facing * Rules.LockDistance;

            if (attacker.StateTime < Rules.DriveTime) return;

            attacker.RegrabCooldown = Rules.RegrabDelay;
            attacker.Enter(FighterState.Idle);
            defender.Enter(FighterState.Stunned);
        }

        void Raise(SumoImpact impact) =>
            Impact?.Invoke(impact, (Fighters[0].Position + Fighters[1].Position) * 0.5f);

        /// <summary>Wrestlers tire in a long round, so pushes carry further and every round ends.</summary>
        float Fatigue => 1f + Mathf.Max(0f, PhaseTime - Rules.FatigueStart) * Rules.FatiguePerSecond;

        void Steer(Fighter attacker, float dt)
        {
            var radians = -attacker.Lateral * Rules.PushSteering * dt * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var f = attacker.Facing;
            attacker.Facing = new Vector2(f.x * cos - f.y * sin, f.x * sin + f.y * cos).normalized;
        }

        void Move(Fighter self, Fighter other, float dt)
        {
            // A lunge is committed: it keeps facing the way it went until the recovery is over.
            var thrown = self.State == FighterState.Recovering || self.State == FighterState.Recoiling;
            var committed = self.State == FighterState.GrabStartup || thrown;
            var toOther = other.Position - self.Position;
            if (toOther.sqrMagnitude > 0.0001f && !committed) Turn(self, toOther.normalized, dt);

            switch (self.State)
            {
                case FighterState.Moving:
                    StepMove(self, other, dt);
                    break;

                case FighterState.GrabStartup:
                    if (toOther.magnitude > Rules.LockDistance) self.Position += self.LungeDirection * (Rules.LungeSpeed * dt);
                    break;

                case FighterState.Striking:
                    var lag = self.StrikeDucked ? Rules.DuckedStrikeLag : 0f;
                    if (self.StateTime >= Rules.StrikeStartup + Rules.StrikeActive + Rules.StrikeRecovery + lag)
                        self.Enter(FighterState.Idle);
                    break;

                case FighterState.Stunned:
                    if (self.StateTime >= Rules.StunTime)
                    {
                        self.StunImmunity = Rules.StunImmunity;
                        self.Enter(FighterState.Idle);
                    }
                    break;

                case FighterState.Recovering:
                case FighterState.Recoiling:
                    if (self.StateTime >= self.RecoveryTime) self.Enter(FighterState.Idle);
                    break;
            }

            var before = self.Position.magnitude;
            self.Position += self.Knockback * dt;
            self.Knockback = Vector2.MoveTowards(self.Knockback, Vector2.zero, Rules.KnockbackDecay * dt);

            // Stumbling past after a miss stops at the rope, so a dodge is not an instant win.
            if (thrown) KeepInsideRing(self, before);
        }

        void Turn(Fighter self, Vector2 target, float dt)
        {
            var angle = Vector2.SignedAngle(self.Facing, target);
            var step = Mathf.Clamp(angle, -Rules.TurnSpeed * dt, Rules.TurnSpeed * dt);
            var radians = step * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var f = self.Facing;
            self.Facing = new Vector2(f.x * cos - f.y * sin, f.x * sin + f.y * cos).normalized;
        }

        /// <summary>
        /// Stops a stumble inside the rope's margin, or where it started if that was already
        /// further out. It never ends exactly on the rope, where rounding could tip it over.
        /// </summary>
        void KeepInsideRing(Fighter self, float before)
        {
            var limit = Mathf.Max(Rules.RingRadius - Rules.RingMargin, Mathf.Min(before, Rules.RingRadius - 0.001f));
            self.Position = Vector2.ClampMagnitude(self.Position, limit);
        }

        /// <summary>Circles the opponent and closes to fighting distance automatically.</summary>
        void StepMove(Fighter self, Fighter other, float dt)
        {
            var right = new Vector2(self.Facing.y, -self.Facing.x);
            var next = self.Position + right * (self.Lateral * Rules.MoveSpeed * dt);

            // Walking sideways goes round the opponent on a circle. A straight sidestep would
            // open the gap a little every frame and keep setting off the walk-in below.
            var distance = Vector2.Distance(self.Position, other.Position);
            if (distance > 0.0001f) next = other.Position + (next - other.Position).normalized * distance;

            var gap = distance - Rules.EngageDistance;
            if (gap > 0f && HoldsTheCentre(self, other)) next += self.Facing * Mathf.Min(gap, Rules.MoveSpeed * dt);

            // Walking never crosses the rope. Only being shoved does.
            var limit = Mathf.Max(Rules.RingRadius - Rules.RingMargin, self.Position.magnitude);
            self.Position = Vector2.ClampMagnitude(next, limit);
        }

        void SeparateBodies(Fighter a, Fighter b)
        {
            var delta = b.Position - a.Position;
            var minimum = Rules.BodyRadius * 2f;
            var distance = delta.magnitude;
            if (distance >= minimum || distance < 0.0001f) return;

            // Walking into someone stops you. Only a grab can move them.
            var overlap = delta / distance * (minimum - distance);
            var aWalking = a.State == FighterState.Moving;
            var bWalking = b.State == FighterState.Moving;
            var aShare = aWalking == bWalking ? 0.5f : aWalking ? 1f : 0f;
            a.Position -= overlap * aShare;
            b.Position += overlap * (1f - aShare);
        }

        void CheckRingOut()
        {
            var aDistance = Fighters[0].Position.magnitude;
            var bDistance = Fighters[1].Position.magnitude;
            var aOut = aDistance > Rules.RingRadius;
            var bOut = bDistance > Rules.RingRadius;
            if (!aOut && !bOut) return;

            RoundWinner = aOut && bOut ? (aDistance > bDistance ? 1 : 0) : aOut ? 1 : 0;
            Fighters[RoundWinner].Score++;
            Fighters[RoundWinner].Enter(FighterState.Victory);
            Fighters[1 - RoundWinner].Enter(FighterState.Falling);
            Impact?.Invoke(SumoImpact.RingOut, Fighters[1 - RoundWinner].Position);
            SetPhase(MatchPhase.RoundOver);
        }
    }
}
