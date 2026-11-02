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
        }

        Pose pose;
        float clock;
        float fall;

        public void Sync(SumoSim sim, int index, float dt)
        {
            var self = sim.Fighters[index];
            clock += dt;

            fall = self.State == FighterState.Falling ? Mathf.MoveTowards(fall, 1f, dt * FallRate) : 0f;

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
            };

            var shiver = Mathf.Sin(clock * 34f);
            if (IntroWalk(sim) > 0f && IntroWalk(sim) < 1f) p.Stride = Mathf.Sin(clock * 11f);

            switch (self.State)
            {
                case FighterState.Moving:
                    p.Stride = Mathf.Sin(clock * 11f);
                    p.Roll = p.Stride * 6f + self.Lateral * 8f;
                    p.Lean = 18f;
                    p.ArmL = p.ArmR = 45f;
                    break;

                case FighterState.GrabStartup:
                    // Drives in low, ducking under any strike.
                    p.Crouch = 0.85f;
                    p.Lean = 30f;
                    p.ArmL = p.ArmR = 88f;
                    p.SpreadL = p.SpreadR = 4f;
                    p.Elbow = 5f;
                    break;

                case FighterState.Pushing:
                case FighterState.Grabbed:
                    // The struggle has no meter on screen, so the bodies have to show who is winning.
                    var winning = (sim.Struggle - 0.5f) * 2f * (self.State == FighterState.Pushing ? 1f : -1f);
                    p.Crouch = 0.8f;
                    p.Lean = 8f + winning * 12f;
                    if (sim.Shoving && self.State == FighterState.Pushing) p.Lean += 14f;
                    p.ArmL = p.ArmR = 82f;
                    p.SpreadL = p.SpreadR = 12f;
                    p.Elbow = 25f;
                    p.Stride = Mathf.Sin(clock * 16f) * 0.6f;
                    p.Roll = p.Stride * 5f;
                    break;

                case FighterState.Recovering:
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
                    p.Crouch = 0.8f;
                    p.Lean = 34f;
                    p.ArmL = p.ArmR = 88f;
                    p.SpreadL = p.SpreadR = 6f;
                    p.Elbow = 5f;
                    p.Stride = Mathf.Sin(clock * 14f);
                    break;

                case FighterState.Reeling:
                    // Arms flung wide and teetering backwards, as in the original's finishes.
                    p.Crouch = 0.25f;
                    p.Lean = -16f;
                    p.Roll = Mathf.Sin(clock * 7f) * 7f;
                    p.ArmL = 18f + Mathf.Sin(clock * 12f) * 22f;
                    p.ArmR = 18f - Mathf.Sin(clock * 12f) * 22f;
                    p.SpreadL = p.SpreadR = 82f;
                    p.Elbow = 10f;
                    p.Stride = Mathf.Sin(clock * 12f) * 0.7f;
                    break;

                case FighterState.Striking:
                    p.ArmR = 100f;
                    p.SpreadR = 2f;
                    p.Elbow = 0f;
                    p.Twist = -22f;
                    p.Lean += 8f;
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
        };

        void Apply(SumoSim sim, Fighter self, float stride, float dt)
        {
            var past = Mathf.Max(0f, self.Position.magnitude - RingRadius);
            var waiting = self.Position.normalized * ((1f - IntroWalk(sim)) * IntroStepIn);
            var ground = self.Position + waiting;
            var world = new Vector3(ground.x, -Mathf.Min(past, DropBeyondRing), ground.y);
            var facing = Quaternion.LookRotation(new Vector3(self.Facing.x, 0f, self.Facing.y))
                * Quaternion.Euler(-Mathf.SmoothStep(0f, FallAngle, fall), 0f, 0f);
            var follow = dt <= 0f ? 1f : 1f - Mathf.Exp(-30f * dt);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, world, follow),
                Quaternion.Slerp(transform.rotation, facing, follow));

            var bend = Mathf.Lerp(8f, 62f, pose.Crouch);
            var drop = 2f * ThighLength * (1f - Mathf.Cos(bend * Mathf.Deg2Rad));
            Pelvis.localPosition = new Vector3(0f, 0.74f - drop, 0f);
            Spine.localRotation = Quaternion.Euler(pose.Lean, pose.Twist, pose.Roll);
            Head.localRotation = Quaternion.Euler(-pose.Lean * 0.7f, 0f, -pose.Roll * 0.5f);

            ArmL.localRotation = Quaternion.Euler(-pose.ArmL, 0f, -pose.SpreadL);
            ArmR.localRotation = Quaternion.Euler(-pose.ArmR, 0f, pose.SpreadR);
            ForearmL.localRotation = Quaternion.Euler(-pose.Elbow, 0f, 0f);
            ForearmR.localRotation = Quaternion.Euler(-pose.Elbow, 0f, 0f);

            Leg(ThighL, ShinL, FootL, bend, stride, -1f);
            Leg(ThighR, ShinR, FootR, bend, -stride, 1f);
        }

        static void Leg(Transform thigh, Transform shin, Transform foot, float bend, float stride, float side)
        {
            var swing = stride * 16f;
            thigh.localRotation = Quaternion.Euler(-bend - swing, 0f, side * 16f);
            shin.localRotation = Quaternion.Euler(bend * 2f + Mathf.Max(0f, stride) * 18f, 0f, 0f);
            foot.localRotation = Quaternion.Euler(-bend, 0f, -side * 16f);
        }
    }
}
