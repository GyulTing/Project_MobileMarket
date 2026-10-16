using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Security-camera targeting laser: turns the camera toward a ground point and draws a red beam from the lens to it.
    /// Used to show where the 40 mm will strike; hidden the moment it fires.
    /// </summary>
    [RequireComponent(typeof(TurretAim))]
    public class TargetLaser : MonoBehaviour
    {
        [SerializeField] private LineRenderer beam;

        private TurretAim aim;

        private void Awake()
        {
            aim = GetComponent<TurretAim>();
            if (beam != null) beam.enabled = false;
        }

        /// <summary>Points the camera and the beam at a point (call every frame while tracking).</summary>
        public void Track(Vector3 point)
        {
            aim.AimAt(point);
            if (beam == null) return;
            beam.enabled = true;
            beam.SetPosition(0, aim.Muzzle.position);
            beam.SetPosition(1, point);
        }

        public void Hide()
        {
            if (beam != null) beam.enabled = false;
        }
    }
}
