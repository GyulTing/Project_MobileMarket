using System.Collections;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The 1F main power lever (P2). Pulled while emergency power is off it does nothing but tell the player why; with
    /// emergency power on it throws the handle down and restores main power.
    /// </summary>
    public class PowerLever : MonoBehaviour, IInteractable, IResolvable
    {
        [SerializeField] private BunkerPower power;
        [Tooltip("Optional handle that swings down when the lever is thrown.")]
        [SerializeField] private Transform handle;
        [SerializeField] private float throwAngle = 90f;
        [SerializeField] private float throwTime = 0.4f;
        [SerializeField] private string noPowerText = "레버가 꿈쩍도 하지 않는다. 계통 전원이 끊겨 있다 — 2층 전원실을 확인하자.";
        [SerializeField] private string restoredText = "주전원이 복구되었다.";

        private bool thrown;

        public bool IsResolved => thrown;

        public void Interact(GameObject interactor)
        {
            if (thrown || power == null) return;
            if (!power.RestoreMain())
            {
                InteractionFeedback.Show(noPowerText);
                return;
            }
            thrown = true;
            InteractionFeedback.Show(restoredText);
            if (handle != null) StartCoroutine(Throw());
        }

        private IEnumerator Throw()
        {
            Quaternion from = handle.localRotation, to = from * Quaternion.Euler(throwAngle, 0f, 0f);
            for (float t = 0f; t < throwTime; t += Time.deltaTime)
            {
                handle.localRotation = Quaternion.Slerp(from, to, t / throwTime);
                yield return null;
            }
            handle.localRotation = to;
        }
    }
}
