using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>A note, log or scribble left behind in the bunker. F shows its text (puzzle hints live in these).</summary>
    public class ReadableNote : MonoBehaviour, IInteractable, IResolvable
    {
        [SerializeField, TextArea(2, 6)] private string text;

        /// <summary>Read at least once (its highlight stops; it stays readable).</summary>
        public bool IsResolved { get; private set; }

        public void Interact(GameObject interactor)
        {
            IsResolved = true;
            InteractionFeedback.Show(text);
        }
    }
}
