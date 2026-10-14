using UnityEngine;
using MobileMarket.Player;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Enterable building: while the player is inside this trigger (the building's interior volume), the roof renderers are
    /// hidden so the top-down camera can see in; they come back when the player walks out.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RoofHider : MonoBehaviour
    {
        [Tooltip("Renderers hidden while the player is inside (the roof, and any upper floor).")]
        [SerializeField] private Renderer[] roofRenderers;

        private int playerInside;

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent(out PlayerHealth _)) return;
            if (playerInside++ == 0) SetRoofVisible(false);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.TryGetComponent(out PlayerHealth _)) return;
            if (--playerInside <= 0)
            {
                playerInside = 0;
                SetRoofVisible(true);
            }
        }

        private void SetRoofVisible(bool visible)
        {
            foreach (var r in roofRenderers) if (r != null) r.enabled = visible;
        }
    }
}
