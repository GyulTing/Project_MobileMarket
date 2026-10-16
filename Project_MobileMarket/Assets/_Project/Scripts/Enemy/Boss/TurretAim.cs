using UnityEngine;

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
        }

        /// <summary>Angle (degrees) between where the barrel points and the target.</summary>
        public float AimError(Vector3 point) => Vector3.Angle(Muzzle.forward, point - Muzzle.position);

        private void Update()
        {
            if (!hasTarget) return;
            float dt = Time.deltaTime;

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
