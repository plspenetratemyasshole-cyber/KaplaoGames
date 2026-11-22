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
            EnsureVisualHierarchy();

            if (badgeButton == null)
                badgeButton = GetComponent<Button>();

            if (badgeButton != null)
            {
                badgeButton.onClick.RemoveListener(OnBadgeClicked);
                badgeButton.onClick.AddListener(OnBadgeClicked);
            }
        }

        public void EnsureVisualHierarchy()
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(1f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(1f, 1f);
                rootRect.anchoredPosition = new Vector2(-24f, -86f);
                rootRect.sizeDelta = new Vector2(230f, 54f);
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.sprite = KaplaoUiSprites.RoundedRect(12);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0x0F / 255f, 0x17 / 255f, 0x2A / 255f, 0.95f);
            bg.raycastTarget = true;

            KaplaoUiStyler.EnsureChildBorder(transform, 12, 1, new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 0.9f));

            if (badgeButton == null)
                badgeButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            badgeButton.transition = Selectable.Transition.None;

            UiButtonLeanScale scale = GetComponent<UiButtonLeanScale>();
            if (scale == null)
                scale = gameObject.AddComponent<UiButtonLeanScale>();
            scale.SetBaseColors(bg.color, new Color(0x33 / 255f, 0x41 / 255f, 0x55 / 255f, 0.9f));
            scale.SetScale(1.02f, 0.985f);

            // Avatar Container
            Transform avatarTrans = transform.Find("AvatarCircle");
            GameObject avatarObj;
            if (avatarTrans == null)
            {
                avatarObj = new GameObject("AvatarCircle", typeof(RectTransform), typeof(Image));
                avatarObj.transform.SetParent(transform, false);
            }
            else
            {
                avatarObj = avatarTrans.gameObject;
            }
            RectTransform avRect = avatarObj.GetComponent<RectTransform>();
            avRect.anchorMin = new Vector2(0f, 0.5f);
            avRect.anchorMax = new Vector2(0f, 0.5f);
            avRect.pivot = new Vector2(0f, 0.5f);
            avRect.anchoredPosition = new Vector2(10f, 0f);
            avRect.sizeDelta = new Vector2(36f, 36f);

            avatarImage = avatarObj.GetComponent<Image>();
            avatarImage.sprite = KaplaoUiSprites.Circle();
            avatarImage.type = Image.Type.Simple;
            avatarImage.color = new Color(0x1D / 255f, 0x4E / 255f, 0xD8 / 255f, 1f);
            avatarImage.raycastTarget = false;

            // Avatar Letter
            Transform letterTrans = avatarObj.transform.Find("AvatarLetter");
            GameObject letterObj;
            if (letterTrans == null)
            {
                letterObj = new GameObject("AvatarLetter", typeof(RectTransform), typeof(TextMeshProUGUI));
                letterObj.transform.SetParent(avatarObj.transform, false);
            }
            else
            {
                letterObj = letterTrans.gameObject;
            }
            RectTransform letRect = letterObj.GetComponent<RectTransform>();
            letRect.anchorMin = Vector2.zero;
            letRect.anchorMax = Vector2.one;
            letRect.offsetMin = Vector2.zero;
            letRect.offsetMax = Vector2.zero;

            avatarLetterText = letterObj.GetComponent<TextMeshProUGUI>();
            avatarLetterText.alignment = TextAlignmentOptions.Center;
            avatarLetterText.fontSize = 17f;
            avatarLetterText.fontStyle = FontStyles.Bold;
            avatarLetterText.color = Color.white;
            avatarLetterText.raycastTarget = false;

            // Nickname Label
            Transform nickTrans = transform.Find("NicknameLabel");
            GameObject nickObj;
            if (nickTrans == null)
            {
                nickObj = new GameObject("NicknameLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                nickObj.transform.SetParent(transform, false);
            }
            else
            {
                nickObj = nickTrans.gameObject;
            }
            RectTransform nickRect = nickObj.GetComponent<RectTransform>();
            nickRect.anchorMin = new Vector2(0f, 0.5f);
            nickRect.anchorMax = new Vector2(1f, 1f);
            nickRect.pivot = new Vector2(0f, 0.5f);
            nickRect.offsetMin = new Vector2(56f, 2f);
            nickRect.offsetMax = new Vector2(-10f, -6f);

            nicknameLabel = nickObj.GetComponent<TextMeshProUGUI>();
            nicknameLabel.fontSize = 15.5f;
            nicknameLabel.fontStyle = FontStyles.Bold;
            nicknameLabel.color = Color.white;
            nicknameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nicknameLabel.overflowMode = TextOverflowModes.Overflow;
            nicknameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nicknameLabel.raycastTarget = false;

            // Status Container
            Transform statusContainerTrans = transform.Find("StatusContainer");
            GameObject statusContObj;
            if (statusContainerTrans == null)
            {
                statusContObj = new GameObject("StatusContainer", typeof(RectTransform));
                statusContObj.transform.SetParent(transform, false);
            }
            else
            {
                statusContObj = statusContainerTrans.gameObject;
            }
            RectTransform scRect = statusContObj.GetComponent<RectTransform>();
            scRect.anchorMin = new Vector2(0f, 0f);
            scRect.anchorMax = new Vector2(1f, 0.5f);
            scRect.pivot = new Vector2(0f, 0.5f);
            scRect.offsetMin = new Vector2(56f, 6f);
            scRect.offsetMax = new Vector2(-10f, -2f);

            // Dot
            Transform dotTrans = statusContObj.transform.Find("StatusDot");
            GameObject dotObj;
            if (dotTrans == null)
            {
                dotObj = new GameObject("StatusDot", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(statusContObj.transform, false);
            }
            else
            {
                dotObj = dotTrans.gameObject;
            }
            RectTransform dotRect = dotObj.GetComponent<RectTransform>();
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.anchoredPosition = new Vector2(0f, 0f);
            dotRect.sizeDelta = new Vector2(8f, 8f);

            statusIndicatorDot = dotObj.GetComponent<Image>();
            statusIndicatorDot.sprite = KaplaoUiSprites.Circle();
            statusIndicatorDot.type = Image.Type.Simple;
            statusIndicatorDot.color = new Color(0.20f, 0.83f, 0.60f, 1f);
            statusIndicatorDot.raycastTarget = false;

            // Status Text
            Transform statTextTrans = statusContObj.transform.Find("StatusLabel");
            GameObject statTextObj;
            if (statTextTrans == null)
            {
                statTextObj = new GameObject("StatusLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                statTextObj.transform.SetParent(statusContObj.transform, false);
            }
            else
            {
                statTextObj = statTextTrans.gameObject;
            }
            RectTransform stRect = statTextObj.GetComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0f, 0f);
            stRect.anchorMax = new Vector2(1f, 1f);
            stRect.offsetMin = new Vector2(14f, 0f);
            stRect.offsetMax = Vector2.zero;

            statusLabel = statTextObj.GetComponent<TextMeshProUGUI>();
            statusLabel.fontSize = 12f;
            statusLabel.color = new Color(0.58f, 0.64f, 0.72f, 1f);
            statusLabel.alignment = TextAlignmentOptions.MidlineLeft;
            statusLabel.raycastTarget = false;
        }

        void OnEnable()
        {
            PlayerSession.NicknamePresetChanged += Refresh;
            KaplaoNicknamePresetCatalog.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            PlayerSession.NicknamePresetChanged -= Refresh;
            KaplaoNicknamePresetCatalog.Changed -= Refresh;
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
            {
                nicknameLabel.text = nick;
                NicknameStyle.Apply(nicknameLabel, PlayerSession.AccountEmail, PlayerSession.CachedNicknamePreset);
            }

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
