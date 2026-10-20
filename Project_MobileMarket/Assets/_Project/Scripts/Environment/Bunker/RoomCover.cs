using System.Collections;
using UnityEngine;
using MobileMarket.Player;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Bunker fog of war: a black lid over one room (or corridor) hides what is inside until the player first walks in.
    /// The trigger on this object is the room's floor volume; the lid fades out on entry and stays gone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class RoomCover : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("The black lid on top of the room's walls.")]
        [SerializeField] private Renderer cover;
        [SerializeField] private float fadeTime = 0.5f;
        [Tooltip("Already visible when the scene starts (the room the player starts in).")]
        [SerializeField] private bool startRevealed;

        private MaterialPropertyBlock block;

        public bool IsRevealed { get; private set; }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            block = new MaterialPropertyBlock();
            if (startRevealed) Reveal(instant: true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsRevealed || !other.TryGetComponent(out PlayerHealth _)) return;
            Reveal(instant: false);
        }

        [ContextMenu("Reveal (test)")]
        private void RevealFromMenu() => Reveal(instant: false);

        public void Reveal(bool instant)
        {
            if (IsRevealed) return;
            IsRevealed = true;
            if (cover == null) return;
            if (instant || !isActiveAndEnabled) cover.enabled = false;
            else StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            Color c = cover.sharedMaterial.GetColor(BaseColorId);
            float startAlpha = c.a;
            for (float t = 0f; t < fadeTime; t += Time.deltaTime)
            {
                c.a = Mathf.Lerp(startAlpha, 0f, t / fadeTime);
                block.SetColor(BaseColorId, c);
                cover.SetPropertyBlock(block);
                yield return null;
            }
            cover.enabled = false;
        }
    }
}
