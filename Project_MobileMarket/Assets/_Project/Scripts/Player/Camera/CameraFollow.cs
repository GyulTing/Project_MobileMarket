using UnityEngine;
using UnityEngine.InputSystem;
using MobileMarket.Common;
using MobileMarket.Items;

namespace MobileMarket.Player
{
    /// <summary>
    /// Follows a target's position at a fixed offset, with scroll-wheel zoom. Rotation stays fixed (quarter-view).
    /// Shakes slightly whenever the target's weapon fires, and harder for nearby ScreenShake requests (explosions).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -8f);
        [SerializeField] private float zoomSpeed = 0.05f;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 1.080f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("Shot shake")]
        [Tooltip("Shake distance per 1.0 weapon recoil (meters). Keep it subtle.")]
        [SerializeField] private float shakePerRecoil = 0.03f;
        [Tooltip("How fast the shake dies out (30 stops it in about 0.1s).")]
        [SerializeField] private float shakeDecay = 30f;

        [Header("Blast shake (ScreenShake requests, e.g. explosions)")]
        [Tooltip("Sources farther than this from the target don't shake the camera; closer ones scale up linearly.")]
        [SerializeField] private float blastRange = 25f;
        [Tooltip("How fast a blast shake dies out (6 lasts about half a second).")]
        [SerializeField] private float blastDecay = 6f;
        [Tooltip("Upper limit of the blast shake (meters), however many explosions stack up.")]
        [SerializeField] private float maxBlastShake = 0.35f;

        private Vector3 baseOffset;
        private float targetZoom = 1f;
        private float zoom = 1f;
        private PlayerCombat targetCombat;
        private float shake;
        private float blastShake;

        private void Awake()
        {
            baseOffset = offset;
            if (target != null) targetCombat = target.GetComponent<PlayerCombat>();
        }

        private void OnEnable()
        {
            if (targetCombat != null) targetCombat.OnFired += HandleFired;
            ScreenShake.OnRequested += HandleShakeRequested;
        }

        private void OnDisable()
        {
            if (targetCombat != null) targetCombat.OnFired -= HandleFired;
            ScreenShake.OnRequested -= HandleShakeRequested;
        }

        private void HandleFired(WeaponData weapon)
        {
            shake = Mathf.Max(shake, shakePerRecoil * weapon.recoil);
        }

        private void HandleShakeRequested(Vector3 source, float strength)
        {
            if (target == null || blastRange <= 0f) return;
            float falloff = Mathf.Clamp01(1f - Vector3.Distance(source, target.position) / blastRange);
            blastShake = Mathf.Min(maxBlastShake, Mathf.Max(blastShake, strength * falloff));
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll == 0f) return;

            targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed, minZoom, maxZoom);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            zoom = Mathf.Lerp(zoom, targetZoom, zoomSmoothing * Time.deltaTime);
            transform.position = target.position + baseOffset * zoom;

            if (shake <= 0.0005f) shake = 0f;
            if (blastShake <= 0.0005f) blastShake = 0f;
            if (shake <= 0f && blastShake <= 0f) return;

            transform.position += Random.insideUnitSphere * (shake + blastShake);
            shake *= Mathf.Exp(-shakeDecay * Time.deltaTime);
            blastShake *= Mathf.Exp(-blastDecay * Time.deltaTime);
        }
    }
}
