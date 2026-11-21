using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.Client;
using KaplaoGames.Shared;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Player profile badge widget in MainMenu (Top-Right of MainPanel).
    /// Displays current nickname, avatar / skin icon, and account sign-in status.
    /// Clicking opens the SettingsPanel.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Profile Badge UI")]
    public class KaplaoProfileBadgeUi : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] TextMeshProUGUI avatarLetterText;
        [SerializeField] TextMeshProUGUI nicknameLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] Image statusIndicatorDot;
        [SerializeField] Button badgeButton;

        string lastNickname = string.Empty;
        bool lastSignedIn;

        void Awake()
        {
            if (badgeButton == null)
                badgeButton = GetComponent<Button>();

            if (badgeButton != null)
                badgeButton.onClick.AddListener(OnBadgeClicked);
        }

        void OnEnable()
        {
            Refresh();
        }

        void Update()
        {
            string currentNick = PlayerSession.Nickname ?? string.Empty;
            bool signedIn = IsSignedIn();

            if (currentNick != lastNickname || signedIn != lastSignedIn)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            string nick = PlayerSession.Nickname;
            if (string.IsNullOrWhiteSpace(nick))
                nick = "Guest";

            lastNickname = nick;
            bool signedIn = IsSignedIn();
            lastSignedIn = signedIn;

            if (nicknameLabel != null)
                nicknameLabel.text = nick;

            if (avatarLetterText != null)
                avatarLetterText.text = nick.Length > 0 ? nick.Substring(0, 1).ToUpperInvariant() : "G";

            if (statusLabel != null)
            {
                statusLabel.text = signedIn ? "Signed in" : "Guest • Sign in";
                statusLabel.color = signedIn
                    ? new Color(0.20f, 0.83f, 0.60f, 1f) // #34D399 Emerald
                    : new Color(0.58f, 0.64f, 0.72f, 1f); // #94A3B8 Slate
            }

            if (statusIndicatorDot != null)
            {
                statusIndicatorDot.color = signedIn
                    ? new Color(0.20f, 0.83f, 0.60f, 1f)
                    : new Color(0.39f, 0.45f, 0.55f, 1f);
            }
        }

        bool IsSignedIn()
        {
            return PlayerSession.IsLoggedIn;
        }

        void OnBadgeClicked()
        {
            MainMenuUI menu = FindFirstObjectByType<MainMenuUI>();
            if (menu != null)
            {
                menu.ShowSettingsPanel();
            }
            else
            {
                Transform settings = transform.root.Find("SettingsPanel");
                if (settings != null)
                    settings.gameObject.SetActive(true);
            }
        }
    }
}
