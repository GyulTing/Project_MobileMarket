using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Turns a wall turret (or security camera) toward a point: Yaw spins around its local Y, Pitch tilts around its local
    /// X, each at a limited speed and within limits so wall mounts never swing into the wall. The barrel points along -Z
    /// at rest (the prefab convention). Call AimAt every frame while tracking.
    /// </summary>
    public class TurretAim : MonoBehaviour
    {
        [SerializeField] private Transform yaw;
        [SerializeField] private Transform pitch;
        [Tooltip("Where shots leave (barrel tip / lens), pointing along the barrel.")]
        [SerializeField] private Transform muzzle;

        [SerializeField] private float yawSpeed = 90f;
        [SerializeField] private float pitchSpeed = 60f;
        [Tooltip("Max swing either side of straight ahead (degrees).")]
        [SerializeField] private float yawLimit = 75f;
        [SerializeField] private float pitchMin = -65f;
        [SerializeField] private float pitchMax = 20f;

        [Header("Motor sound (optional)")]
        [Tooltip("Looping servo whine; its volume follows how fast the turret is turning.")]
        [SerializeField] private AudioSource servoSource;
        [SerializeField] private float servoMaxVolume = 0.5f;
        [Tooltip("How fast the motor sound follows the turning speed (volume per second).")]
        [SerializeField] private float servoFade = 4f;
        [Tooltip("Winding-down whine when the turret dies (PowerDown).")]
        [SerializeField] private SoundEffect powerDownSound = new SoundEffect();

        public Transform Muzzle => muzzle != null ? muzzle : transform;

        private Vector3 target;
        private bool hasTarget;
        private float yawAngle, pitchAngle;

        public void AimAt(Vector3 point)
        {
            target = point;
            hasTarget = true;
        }

        public void ClearTarget() => hasTarget = false;

        /// <summary>Lifeless: the barrel slowly sags to its lowest tilt and stays there.</summary>
        public void PowerDown()
        {
            yawSpeed = 0f;
            pitchSpeed = 25f;
            AimAt(Muzzle.position + Vector3.down * 50f);
            SoundManager.PlayAt(powerDownSound, Muzzle.position);
        }

        /// <summary>Angle (degrees) between where the barrel points and the target.</summary>
        public float AimError(Vector3 point) => Vector3.Angle(Muzzle.forward, point - Muzzle.position);

        private void Update()
        {
            float dt = Time.deltaTime;
            float prevYaw = yawAngle, prevPitch = pitchAngle;
            if (hasTarget) Turn(dt);
            UpdateServoSound(prevYaw, prevPitch, dt);
        }

        /// <summary>Motor whine: louder the faster the barrel is turning, silent when it holds still.</summary>
        private void UpdateServoSound(float prevYaw, float prevPitch, float dt)
        {
            if (servoSource == null || dt <= 0f) return;
            float turned = Mathf.Abs(Mathf.DeltaAngle(prevYaw, yawAngle)) + Mathf.Abs(Mathf.DeltaAngle(prevPitch, pitchAngle));
            float fullSpeed = Mathf.Max(1f, Mathf.Max(yawSpeed, pitchSpeed));
            float target = Mathf.Clamp01(turned / dt / fullSpeed) * servoMaxVolume;
            servoSource.volume = Mathf.MoveTowards(servoSource.volume, target, servoFade * dt);

            bool audible = servoSource.volume > 0.01f;
            if (audible && !servoSource.isPlaying) servoSource.Play();
            else if (!audible && servoSource.isPlaying) servoSource.Pause();
        }

        private void Turn(float dt)
        {
            if (yaw != null)
            {
                // Front is -Z: rotating -Z by angle a around Y gives (-sin a, 0, -cos a).
                Vector3 d = yaw.parent.InverseTransformDirection(target - yaw.position);
                float want = Mathf.Clamp(Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
                yawAngle = Mathf.MoveTowardsAngle(yawAngle, want, yawSpeed * dt);
                yaw.localRotation = Quaternion.Euler(0f, yawAngle, 0f);
            }
            if (pitch != null)
            {
                // Rotating -Z by angle p around X gives (0, sin p, -cos p): positive p lifts the barrel.
                Vector3 d = pitch.parent.InverseTransformDirection(target - pitch.position);
                float want = Mathf.Clamp(Mathf.Atan2(d.y, -d.z) * Mathf.Rad2Deg, pitchMin, pitchMax);
                pitchAngle = Mathf.MoveTowardsAngle(pitchAngle, want, pitchSpeed * dt);
                pitch.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
            }
        }
    }
}
