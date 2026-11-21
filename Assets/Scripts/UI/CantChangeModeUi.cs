using KaplaoGames.UI.Animation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Simple deny dialog with Ok. Default: "You can't change this".
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Can't Change Mode UI")]
    public class CantChangeModeUi : MonoBehaviour
    {
        public const string DefaultMessage = "You can't change this";
        public const string CantDoMessage = "You can't do this";

        [SerializeField] GameObject panelRoot;
        [SerializeField] Button okButton;
        [SerializeField] TextMeshProUGUI messageText;

        static CantChangeModeUi instance;

        void Awake()
        {
            instance = this;
            EnsureMessageText();
            Hide();
        }

        void OnEnable()
        {
            if (okButton != null)
                okButton.onClick.AddListener(Hide);
        }

        void OnDisable()
        {
            if (okButton != null)
                okButton.onClick.RemoveListener(Hide);
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public static void Show() => Show(DefaultMessage);

        public static void ShowCantDo() => Show(CantDoMessage);

        public static void Show(string message)
        {
            if (instance == null)
                instance = FindFirstObjectByType<CantChangeModeUi>();

            instance?.ShowInternal(message);
        }

        public void ShowInternal(string message = null)
        {
            EnsureMessageText();
            if (messageText != null)
                messageText.text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;

            if (panelRoot != null)
            {
                KaplaoUiAnimator.ShowPanel(panelRoot, PanelAnimStyle.PopScale, 0.18f);
                KaplaoUiAnimator.ShakeError(panelRoot);
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Hide()
        {
            if (panelRoot != null && panelRoot.activeSelf)
                KaplaoUiAnimator.HidePanel(panelRoot, PanelAnimStyle.Fade, 0.14f);
        }

        void EnsureMessageText()
        {
            if (messageText != null || panelRoot == null)
                return;

            Transform t = panelRoot.transform.Find("MessageText");
            if (t != null)
                messageText = t.GetComponent<TextMeshProUGUI>();
        }
    }
}
