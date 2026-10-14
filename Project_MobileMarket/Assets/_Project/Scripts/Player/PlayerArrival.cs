using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Player
{
    /// <summary>Lets SceneLoader find this scene's player and put it beside the parked truck after travelling.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerArrival : MonoBehaviour
    {
        private CharacterController controller;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            SceneLoader.RegisterPlayer(this);
        }

        private void OnDestroy() => SceneLoader.UnregisterPlayer(this);

        /// <summary>
        /// Teleports the player so its feet stand on groundPoint (the CharacterController has to be off while its transform
        /// is moved; its capsule is centred on the transform, so the transform goes half a capsule above the ground).
        /// </summary>
        public void PlaceAt(Vector3 groundPoint, Quaternion rotation)
        {
            controller.enabled = false;
            float feet = controller.height * 0.5f - controller.center.y + controller.skinWidth;
            transform.SetPositionAndRotation(groundPoint + Vector3.up * feet, rotation);
            controller.enabled = true;
        }
    }
}
