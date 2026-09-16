using UnityEngine;
using UnityEngine.InputSystem;

namespace MobileMarket.Player
{
    /// <summary>
    /// Follows a target's position at a fixed offset, with scroll-wheel zoom. Rotation stays fixed (quarter-view).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -8f);
        [SerializeField] private float zoomSpeed = 0.05f;
        [SerializeField] private float minZoom = 0.5f;
        [SerializeField] private float maxZoom = 1.080f;
        [SerializeField] private float zoomSmoothing = 10f;

        private Vector3 baseOffset;
        private float targetZoom = 1f;
        private float zoom = 1f;

        private void Awake()
        {
            baseOffset = offset;
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
        }
    }
}
