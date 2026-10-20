using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The name shown in the "[F] ..." prompt when the player stands next to this interactable. Interactables without
    /// one still get a generic prompt.
    /// </summary>
    public class InteractionPrompt : MonoBehaviour
    {
        public const string DefaultLabel = "상호작용";

        [SerializeField] private string label = DefaultLabel;
        [Tooltip("Hide the prompt once the interactable is resolved (an opened door). Off for things worth using again (notes).")]
        [SerializeField] private bool hideWhenResolved = true;

        private IResolvable resolvable;

        public string Label => label;
        public bool IsShown => !(hideWhenResolved && resolvable != null && resolvable.IsResolved);

        private void Awake() => resolvable = GetComponent<IResolvable>();
    }
}
