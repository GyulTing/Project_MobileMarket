using System;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileMarket.Environment;

namespace MobileMarket.UI
{
    /// <summary>
    /// Keypad screen for a CodeTerminal: digits by mouse (keypad buttons) or keyboard, Backspace deletes, Enter submits,
    /// Esc closes. A wrong code flashes "ACCESS DENIED"; the right one closes the panel.
    /// </summary>
    public class CodePanelUI : MonoBehaviour
    {
        public event Action<bool> OnVisibilityChanged;

        [SerializeField] private Text display;
        [SerializeField] private Text status;
        [SerializeField] private Button[] digitButtons = new Button[10];
        [SerializeField] private Button clearButton;
        [SerializeField] private Button enterButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private float closeDelay = 0.8f;

        private readonly StringBuilder entered = new StringBuilder(8);
        private CodeTerminal terminal;
        private float closeAt = -1f;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            for (int i = 0; i < digitButtons.Length; i++)
            {
                int digit = i;
                if (digitButtons[i] != null) digitButtons[i].onClick.AddListener(() => Press(digit));
            }
            if (clearButton != null) clearButton.onClick.AddListener(Backspace);
            if (enterButton != null) enterButton.onClick.AddListener(Submit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        public void Open(CodeTerminal target)
        {
            terminal = target;
            entered.Clear();
            closeAt = -1f;
            status.text = "봉쇄 해제 코드를 입력하십시오";
            Refresh();
            gameObject.SetActive(true);
            OnVisibilityChanged?.Invoke(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            terminal = null;
            OnVisibilityChanged?.Invoke(false);
        }

        private void Update()
        {
            if (closeAt > 0f)
            {
                if (Time.unscaledTime >= closeAt) Close();
                return;
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Submit(); return; }
            if (kb.backspaceKey.wasPressedThisFrame) { Backspace(); return; }
            for (int d = 0; d <= 9; d++)
            {
                if (kb[(Key)((int)Key.Digit0 + d)].wasPressedThisFrame || kb[(Key)((int)Key.Numpad0 + d)].wasPressedThisFrame) { Press(d); return; }
            }
        }

        private void Press(int digit)
        {
            if (terminal == null || closeAt > 0f || entered.Length >= terminal.CodeLength) return;
            entered.Append((char)('0' + digit));
            Refresh();
        }

        private void Backspace()
        {
            if (entered.Length > 0) entered.Length--;
            Refresh();
        }

        private void Submit()
        {
            if (terminal == null || closeAt > 0f) return;
            if (terminal.TrySubmit(entered.ToString()))
            {
                status.text = "ACCESS GRANTED";
                closeAt = Time.unscaledTime + closeDelay;
                return;
            }
            status.text = "ACCESS DENIED";
            entered.Clear();
            Refresh();
        }

        private void Refresh()
        {
            int length = terminal != null ? terminal.CodeLength : 4;
            var sb = new StringBuilder(length * 2);
            for (int i = 0; i < length; i++) { sb.Append(i < entered.Length ? entered[i] : '_'); if (i < length - 1) sb.Append(' '); }
            display.text = sb.ToString();
        }
    }
}
