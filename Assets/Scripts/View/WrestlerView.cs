using UnityEngine;

namespace SumoGame
{
    /// <summary>
    /// Shows one fighter. All animation is procedural, driven by the sim state, so a real
    /// rigged model can replace the placeholder puppet by rewriting only this class.
    /// </summary>
    public class WrestlerView : MonoBehaviour
    {
        const float ThighLength = 0.38f;
        const float SmoothRate = 16f;
        const float IntroStepIn = 1.5f;
        const float FallRate = 2.2f;
        const float FallAngle = 88f;
        const float LegLength = 0.74f;
        // How high up the body the weight sits, for keeping a standing wrestler balanced over their feet.
        const float WeightHeight = 0.6f;
        const float WeightForward = 0.03f;
        const float TurnShuffleRate = 120f;
        // A grab thrown into a slap sits back this far while the arm passes, then drives in.
        const float DuckStepBack = 0.55f;
        const float DuckBackRate = 14f;
        const float DuckInRate = 22f;

        [Header("Puppet joints")]
        public Transform Pelvis;
        public Transform Spine;
        public Transform Head;
        public Transform ArmL, ArmR, ForearmL, ForearmR;
        public Transform ThighL, ThighR, ShinL, ShinR, FootL, FootR;

        [Header("Dohyo")]
        public float RingRadius = 4.2f;
        public float DropBeyondRing = 0.7f;

        struct Pose
        {
            public float Crouch, Lean, Roll, Twist;
            public float ArmL, ArmR, SpreadL, SpreadR, Elbow;
            public float Stride;
            // 1 stands balanced over the feet, so turning happens on the spot. 0 lets the
            // weight lean out past the feet, as when driving into a grab or a shove.
            public float Balance;
        }

        Pose pose;
        float clock;
        float fall;
        float lastYaw;
        float turnRate;
        float duckBack;

        public void Sync(SumoSim sim, int index, float dt)
        {
            var self = sim.Fighters[index];
            clock += dt;

            fall = self.State == FighterState.Falling ? Mathf.MoveTowards(fall, 1f, dt * FallRate) : 0f;

            // The sim turns at fixed steps, so the turning speed is smoothed across frames.
            var yaw = Mathf.Atan2(self.Facing.x, self.Facing.y) * Mathf.Rad2Deg;
            var rate = dt > 0f ? Mathf.Abs(Mathf.DeltaAngle(lastYaw, yaw)) / dt : 0f;
            turnRate = dt > 0f ? Mathf.Lerp(turnRate, rate, 1f - Mathf.Exp(-8f * dt)) : 0f;
            lastYaw = yaw;

            // Shown only: the sim keeps the grabber where it was, so reach and timing are unchanged.
            var sittingBack = SitsBackFromSlap(sim, index, self);
            var duckRate = sittingBack ? DuckBackRate : DuckInRate;
            duckBack = dt <= 0f ? (sittingBack ? 1f : 0f) : Mathf.Lerp(duckBack, sittingBack ? 1f : 0f, 1f - Mathf.Exp(-duckRate * dt));

            var target = PoseFor(sim, index, self);
            pose = dt <= 0f ? target : Blend(pose, target, 1f - Mathf.Exp(-SmoothRate * dt));
            Apply(sim, self, target.Stride, dt);
        }

        Pose PoseFor(SumoSim sim, int index, Fighter self)
        {
            var breathe = Mathf.Sin(clock * 3f);
            var p = new Pose
            {
                Crouch = 0.55f + breathe * 0.04f,
                Lean = 12f,
                ArmL = 25f,
                ArmR = 25f,
                SpreadL = 22f,
                SpreadR = 22f,
                Elbow = 30f,
                Balance = 1f,
            };

            var shiver = Mathf.Sin(clock * 34f);
            if (IntroWalk(sim) > 0f && IntroWalk(sim) < 1f) p.Stride = Mathf.Sin(clock * 11f);

            // Turning on the spot to follow someone circling shuffles the feet round.
            if (self.State == FighterState.Idle)
                p.Stride = Mathf.Sin(clock * 11f) * Mathf.Clamp01(turnRate / TurnShuffleRate) * 0.6f;

            switch (self.State)
            {
                case FighterState.Moving:
                    p.Stride = Mathf.Sin(clock * 11f);
                    p.Roll = p.Stride * 6f + self.Lateral * 8f;
                    p.Lean = 18f;
                    p.ArmL = p.ArmR = 45f;
                    break;

                case FighterState.GrabStartup when SitsBackFromSlap(sim, index, self):
                    // As in the original, the grabber sits back low under a slap and waits
                    // for the arm to pass before driving in.
                    p.Crouch = 1f;
                    p.Lean = 20f;
                    p.ArmL = p.ArmR = 55f;
                    p.SpreadL = p.SpreadR = 14f;
                    p.Elbow = 35f;
                    p.Stride = -0.5f;
                    break;

                case FighterState.GrabStartup:
                    // Drives in low, so a strike visibly passes overhead.
                    p.Balance = 0f;
                    p.Crouch = 1f;
                    p.Lean = 42f;
                    p.ArmL = p.ArmR = 88f;
                    p.SpreadL = p.SpreadR = 4f;
                    p.Elbow = 5f;
                    break;

                case FighterState.Pushing:
                case FighterState.Grabbed:
                    p.Balance = 0f;
                    // The struggle has no meter on screen, so the bodies have to show who is winning.
                    var winning = (sim.Struggle - 0.5f) * 2f * (self.State == FighterState.Pushing ? 1f : -1f);
                    p.Crouch = 0.8f;
                    p.Lean = 8f + winning * 12f;
                    if (sim.Shoving && self.State == FighterState.Pushing) p.Lean += 14f;
                    // Arms reach round the other body, so the two read as locked together.
                    p.ArmL = p.ArmR = 74f;
                    p.SpreadL = p.SpreadR = 2f;
                    p.Elbow = 58f;
                    p.Stride = Mathf.Sin(clock * 16f) * 0.6f;
                    p.Roll = p.Stride * 5f;
                    break;

                case FighterState.Recovering:
                    p.Balance = 0f;
                    p.Crouch = 0.75f;
                    p.Lean = 38f;
                    p.ArmL = p.ArmR = 55f;
                    p.Roll = Mathf.Sin(clock * 15f) * 8f;
                    break;

                case FighterState.Stunned:
                    p.Crouch = 0.35f;
                    p.Lean = -18f;
                    p.Roll = shiver * 7f;
                    p.ArmL = p.ArmR = 10f;
                    p.SpreadL = p.SpreadR = 45f;
                    break;

                case FighterState.Driving:
                    p.Balance = 0f;
                    p.Crouch = 0.8f;
                    p.Lean = 34f;
                    p.ArmL = p.ArmR = 88f;
                    p.SpreadL = p.SpreadR = 6f;
                    p.Elbow = 5f;
                    p.Stride = Mathf.Sin(clock * 14f);
                    break;

                case FighterState.Reeling:
                    // Balanced, so leaning back pushes the hips forward over the heels.
                    // Arms flung wide and teetering backwards, as in the original's finishes:
                    // elbows a little bent and hands below the shoulders, not a stiff T.
                    p.Crouch = 0.25f;
                    p.Lean = -38f + Mathf.Sin(clock * 7f) * 8f;
                    p.Roll = Mathf.Sin(clock * 7f) * 7f;
                    p.ArmL = 18f + Mathf.Sin(clock * 12f) * 22f;
                    p.ArmR = 18f - Mathf.Sin(clock * 12f) * 22f;
                    p.SpreadL = p.SpreadR = 66f;
                    p.Elbow = 32f;
                    p.Stride = Mathf.Sin(clock * 12f) * 0.7f;
                    break;

                case FighterState.Striking:
                    PoseStrike(ref p, sim.Rules, self.StateTime, self.StrikeDucked);
                    break;

                case FighterState.Recoiling:
                    p.Balance = 0f;
                    // Thrown back with arms flung out, as when two grabs meet in the original.
                    p.Crouch = 0.3f;
                    p.Lean = -22f;
                    p.ArmL = p.ArmR = 45f;
                    p.SpreadL = p.SpreadR = 72f;
                    p.Elbow = 12f;
                    break;

                case FighterState.Victory:
                    ApplyVictory(ref p, breathe);
                    break;

                case FighterState.Falling:
                    ApplyDefeat(ref p);
                    break;
            }

            return p;
        }

        /// <summary>
        /// Winds up, swings across, then follows through off balance. A swing that went over a
        /// ducking grab holds a plain over-reach for the whole recovery, so the opening reads.
        /// </summary>
        static void PoseStrike(ref Pose p, SumoRules rules, float time, bool ducked)
        {
            if (ducked && time >= rules.StrikeStartup)
            {
                // High and across, so it shows above the shoulders from behind as well. The arm
                // keeps carrying across and down, and the free arm paddles for balance, so the
                // over-reach is a stumble rather than a frozen pose.
                var end = rules.StrikeStartup + rules.StrikeActive + rules.StrikeRecovery + rules.DuckedStrikeLag;
                var carry = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rules.StrikeStartup, end, time));
                p.ArmR = 118f - carry * 34f;
                p.SpreadR = -28f - carry * 20f;
                p.Elbow = carry * 12f;
                p.ArmL = -25f + Mathf.Sin(time * 9f) * 18f;
                p.SpreadL = 40f + Mathf.Sin(time * 7f) * 12f;
                p.Twist = -34f - carry * 12f;
                p.Lean = 32f + carry * 8f;
                p.Roll = 8f + Mathf.Sin(time * 6f) * 4f;
                p.Crouch = 0.4f;
                p.Stride = 0.6f;
                return;
            }

            if (time < rules.StrikeStartup)
            {
                p.ArmR = 135f;
                p.SpreadR = 40f;
                p.Elbow = 50f;
                p.Twist = 26f;
                p.Lean = 2f;
            }
            else if (time < rules.StrikeStartup + rules.StrikeActive)
            {
                p.ArmR = 95f;
                p.SpreadR = 0f;
                p.Elbow = 0f;
                p.Twist = -24f;
                p.Lean = 18f;
            }
            else
            {
                p.ArmR = 112f;
                p.SpreadR = 32f;
                p.ArmL = 15f;
                p.Elbow = 0f;
                p.Twist = -38f;
                p.Lean = 22f;
            }
        }

        /// <summary>A grab under way while the other wrestler's slap is still in the air.</summary>
        static bool SitsBackFromSlap(SumoSim sim, int index, Fighter self) =>
            self.State == FighterState.GrabStartup && sim.Fighters[1 - index].IsStriking;

        /// <summary>0 while waiting at the ring edge in the intro, 1 once at the start line.</summary>
        static float IntroWalk(SumoSim sim) =>
            sim.Phase == MatchPhase.Intro ? Mathf.InverseLerp(1f, 2f, sim.PhaseTime) : 1f;

        static void ApplyVictory(ref Pose p, float breathe)
        {
            p.Crouch = 0.2f;
            p.Lean = -6f;
            p.Roll = 0f;
            p.ArmL = p.ArmR = 165f + breathe * 8f;
            p.SpreadL = p.SpreadR = 12f;
            p.Elbow = 15f;
            p.Stride = 0f;
        }

        static void ApplyDefeat(ref Pose p)
        {
            p.Crouch = 0.2f;
            p.Lean = -8f;
            p.Roll = 0f;
            p.ArmL = p.ArmR = 20f;
            p.SpreadL = p.SpreadR = 70f;
            p.Stride = 0f;
        }

        static Pose Blend(Pose a, Pose b, float t) => new Pose
        {
            Crouch = Mathf.Lerp(a.Crouch, b.Crouch, t),
            Lean = Mathf.Lerp(a.Lean, b.Lean, t),
            Roll = Mathf.Lerp(a.Roll, b.Roll, t),
            Twist = Mathf.Lerp(a.Twist, b.Twist, t),
            ArmL = Mathf.Lerp(a.ArmL, b.ArmL, t),
            ArmR = Mathf.Lerp(a.ArmR, b.ArmR, t),
            SpreadL = Mathf.Lerp(a.SpreadL, b.SpreadL, t),
            SpreadR = Mathf.Lerp(a.SpreadR, b.SpreadR, t),
            Elbow = Mathf.Lerp(a.Elbow, b.Elbow, t),
            Stride = Mathf.Lerp(a.Stride, b.Stride, t),
            Balance = Mathf.Lerp(a.Balance, b.Balance, t),
        };

        void Apply(SumoSim sim, Fighter self, float stride, float dt)
        {
            var past = Mathf.Max(0f, self.Position.magnitude - RingRadius);
            var waiting = self.Position.normalized * ((1f - IntroWalk(sim)) * IntroStepIn);
            var ground = self.Position + waiting;
            var world = new Vector3(ground.x, -Mathf.Min(past, DropBeyondRing), ground.y)
                - new Vector3(self.Facing.x, 0f, self.Facing.y) * (duckBack * DuckStepBack);
            var facing = Quaternion.LookRotation(new Vector3(self.Facing.x, 0f, self.Facing.y))
                * Quaternion.Euler(-Mathf.SmoothStep(0f, FallAngle, fall), 0f, 0f);
            var follow = dt <= 0f ? 1f : 1f - Mathf.Exp(-30f * dt);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, world, follow),
                Quaternion.Slerp(transform.rotation, facing, follow));

            var bend = Mathf.Lerp(8f, 62f, pose.Crouch);
            var drop = 2f * ThighLength * (1f - Mathf.Cos(bend * Mathf.Deg2Rad));

            // The lean carries the belly, chest and head out past the toes. Standing, the hips
            // sit back by that much and the legs angle forward to the feet, so the weight is over
            // the spot the wrestler turns on rather than swinging round it.
            var legHeight = LegLength - drop;
            // Leaning back works the other way round: the hips go forward over the heels.
            var shift = pose.Balance * (WeightHeight * Mathf.Sin(pose.Lean * Mathf.Deg2Rad) + WeightForward);
            var tilt = Mathf.Asin(Mathf.Clamp(shift / legHeight, -0.6f, 0.6f));
            Pelvis.localPosition = new Vector3(0f, LegLength - drop - legHeight * (1f - Mathf.Cos(tilt)), -shift);
            tilt *= Mathf.Rad2Deg;
            Spine.localRotation = Quaternion.Euler(pose.Lean, pose.Twist, pose.Roll);
            Head.localRotation = Quaternion.Euler(-pose.Lean * 0.7f, 0f, -pose.Roll * 0.5f);

            ArmL.localRotation = Quaternion.Euler(-pose.ArmL, 0f, -pose.SpreadL);
            ArmR.localRotation = Quaternion.Euler(-pose.ArmR, 0f, pose.SpreadR);
            ForearmL.localRotation = Quaternion.Euler(-pose.Elbow, 0f, 0f);
            ForearmR.localRotation = Quaternion.Euler(-pose.Elbow, 0f, 0f);

            Leg(ThighL, ShinL, FootL, bend, tilt, stride, -1f);
            Leg(ThighR, ShinR, FootR, bend, tilt, -stride, 1f);
        }

        static void Leg(Transform thigh, Transform shin, Transform foot, float bend, float tilt, float stride, float side)
        {
            var swing = stride * 16f;
            thigh.localRotation = Quaternion.Euler(-bend - tilt - swing, 0f, side * 16f);
            shin.localRotation = Quaternion.Euler(bend * 2f + Mathf.Max(0f, stride) * 18f, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-bend + tilt, 0f, -side * 16f);
        }
    }
}
