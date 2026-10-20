using System;
using UnityEngine;

namespace MobileMarket.Common
{
    /// <summary>
    /// Short world messages from interactables ("전원이 들어오지 않는다", "잠겨 있다"). Anything that wants to show them
    /// (a HUD line, a toast) subscribes to OnMessage; nothing here knows about UI.
    /// </summary>
    public static class InteractionFeedback
    {
        public static event Action<string> OnMessage;

        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Debug.Log("[InteractionFeedback] " + message);
            OnMessage?.Invoke(message);
        }
    }
}
