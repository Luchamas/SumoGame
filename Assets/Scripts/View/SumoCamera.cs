using UnityEngine;

namespace SumoGame
{
    /// <summary>
    /// Camera manager for one viewport. Receives the match state and fighter positions, plays
    /// the scripted intro, then sits directly behind one wrestler with them in the centre of
    /// the view. It never zooms or swings during a grab.
    /// </summary>
    public class SumoCamera : MonoBehaviour
    {
        public float BehindDistance = 3.3f;
        public float BehindHeight = 2.9f;
        public float LookAhead = 1.3f;
        public float LookHeight = 1.1f;
        public float TurnRate = 7f;
        public float FollowRate = 6f;
        public float ShakeDecay = 7f;
        public Vector3 WideShot = new Vector3(0f, 5.2f, -10.5f);

        Vector3 focus;
        Vector3 direction = Vector3.right;
        float shake;

        public void Shake(float amount)
        {
            if (GameSettings.CameraShake) shake = Mathf.Max(shake, amount);
        }

        public void SetView(bool visible, Rect viewport, float fieldOfView)
        {
            var view = GetComponent<Camera>();
            view.enabled = visible;
            view.rect = viewport;
            view.fieldOfView = fieldOfView;
        }

        public void ShowWideShot()
        {
            transform.position = WideShot;
            transform.LookAt(Vector3.up * 0.7f);
        }

        /// <summary>Updates the camera for a fighter's view. A dt of zero snaps straight into place.</summary>
        public void Sync(SumoSim sim, int index, float dt)
        {
            var self = sim.Fighters[index];
            var target = new Vector3(self.Position.x, 0f, self.Position.y);
            var facing = new Vector3(self.Facing.x, 0f, self.Facing.y);

            var follow = dt <= 0f ? 1f : 1f - Mathf.Exp(-FollowRate * dt);
            var turn = dt <= 0f ? 1f : 1f - Mathf.Exp(-TurnRate * dt);
            focus = Vector3.Lerp(focus, target, follow);
            direction = Vector3.Slerp(direction, facing, turn).normalized;
            shake = Mathf.MoveTowards(shake, 0f, ShakeDecay * dt * Mathf.Max(shake, 0.05f));

            var position = focus - direction * BehindDistance + Vector3.up * BehindHeight;
            var look = focus + direction * LookAhead + Vector3.up * LookHeight;
            var rotation = Quaternion.LookRotation(look - position);

            if (sim.Phase == MatchPhase.Intro)
            {
                var blend = IntroBlend(sim.PhaseTime);
                var wide = Quaternion.LookRotation(Vector3.up * 0.7f - WideShot);
                position = Vector3.Lerp(WideShot, position, blend);
                rotation = Quaternion.Slerp(wide, rotation, blend);
            }

            transform.SetPositionAndRotation(position + Random.insideUnitSphere * shake, rotation);
        }

        /// <summary>Wide until 1s, part of the way by 2s, holds, then lands on the gameplay view at 4s.</summary>
        static float IntroBlend(float time)
        {
            var first = Mathf.SmoothStep(0f, 0.5f, Mathf.InverseLerp(1f, 2f, time));
            var last = Mathf.SmoothStep(0f, 0.5f, Mathf.InverseLerp(3f, 4f, time));
            return first + last;
        }
    }
}
