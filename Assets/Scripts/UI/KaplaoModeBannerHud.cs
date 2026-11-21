using System.Collections;
using KaplaoGames.Client;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using KaplaoGames.UI.Theme;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Displays a sleek pop-down pill banner whenever the local player's role or host game mode changes.
    /// (Usual: #10B981, Create: #38BDF8, Code: #818CF8, Runner: #F59E0B)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Mode Banner HUD")]
    public class KaplaoModeBannerHud : MonoBehaviour
    {
        [SerializeField] RectTransform bannerRoot;
        [SerializeField] Image bannerBackground;
        [SerializeField] Image bannerAccentDot;
        [SerializeField] TextMeshProUGUI bannerText;

        string lastReportedKey;
        Coroutine hideRoutine;
        int activeTweenId = -1;

        static readonly Color ColorUsual = new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f); // #10B981
        static readonly Color ColorCreate = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f); // #38BDF8
        static readonly Color ColorCode = new Color(0x81 / 255f, 0x8C / 255f, 0xF8 / 255f); // #818CF8
        static readonly Color ColorRunner = new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f); // #F59E0B

        void Awake()
        {
            EnsureUi();
            if (bannerBackground != null) bannerBackground.raycastTarget = false;
            if (bannerAccentDot != null) bannerAccentDot.raycastTarget = false;
            if (bannerText != null) bannerText.raycastTarget = false;
            if (bannerRoot != null)
                bannerRoot.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            KaplaoLocalGameModePredictor.Changed += OnModeChanged;
            StartCoroutine(WatchPlayerRoleRoutine());
        }

        void OnDisable()
        {
            KaplaoLocalGameModePredictor.Changed -= OnModeChanged;
            CancelActiveTweens();
        }

        void OnModeChanged(HostGameMode mode)
        {
            CheckAndDisplayMode();
        }

        IEnumerator WatchPlayerRoleRoutine()
        {
            while (enabled)
            {
                CheckAndDisplayMode();
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        static HostGameMode ResolveKaplaoGameMode()
        {
            if (NetworkClient.localPlayer != null &&
                NetworkClient.localPlayer.TryGetComponent(out KaplaoCursorServer cursorServer))
            {
                return KaplaoLocalGameModePredictor.GetEffective(cursorServer);
            }
            return HostGameMode.Usual;
        }

        void CheckAndDisplayMode()
        {
            PlayerRole? role = LocalPlayerRole.GetOrNull();
            if (!role.HasValue) return;

            string key;
            string label;
            Color color;

            if (role.Value == PlayerRole.Runner)
            {
                key = "Runner";
                label = "MODE: RUNNER";
                color = ColorRunner;
            }
            else
            {
                HostGameMode mode = ResolveKaplaoGameMode();

                switch (mode)
                {
                    case HostGameMode.Create:
                        key = "Create";
                        label = "MODE: CREATE";
                        color = ColorCreate;
                        break;
                    case HostGameMode.Code:
                        key = "Code";
                        label = "MODE: CODE";
                        color = ColorCode;
                        break;
                    default:
                        key = "Usual";
                        label = "MODE: USUAL";
                        color = ColorUsual;
                        break;
                }
            }

            if (key == lastReportedKey)
                return;

            lastReportedKey = key;
            ShowBanner(label, color);
        }

        public void ShowBanner(string text, Color accentColor)
        {
            EnsureUi();
            if (bannerRoot == null) return;

            if (bannerText != null)
                bannerText.text = text;

            if (bannerAccentDot != null)
                bannerAccentDot.color = accentColor;

            CancelActiveTweens();
            bannerRoot.gameObject.SetActive(true);

            // Animate pop-down from top
            bannerRoot.anchoredPosition = new Vector2(0f, 60f);
            activeTweenId = LeanTween.value(bannerRoot.gameObject, 60f, -36f, 0.28f)
                .setEaseOutBack()
                .setIgnoreTimeScale(true)
                .setOnUpdate((float y) =>
                {
                    if (bannerRoot != null)
                        bannerRoot.anchoredPosition = new Vector2(0f, y);
                }).id;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);

            hideRoutine = StartCoroutine(AutoDismissRoutine());
        }

        IEnumerator AutoDismissRoutine()
        {
            yield return new WaitForSecondsRealtime(1.4f);

            CancelActiveTweens();
            if (bannerRoot != null && bannerRoot.gameObject.activeInHierarchy)
            {
                activeTweenId = LeanTween.value(bannerRoot.gameObject, bannerRoot.anchoredPosition.y, 60f, 0.22f)
                    .setEaseInQuad()
                    .setIgnoreTimeScale(true)
                    .setOnUpdate((float y) =>
                    {
                        if (bannerRoot != null)
                            bannerRoot.anchoredPosition = new Vector2(0f, y);
                    })
                    .setOnComplete(() =>
                    {
                        if (bannerRoot != null)
                            bannerRoot.gameObject.SetActive(false);
                    }).id;
            }
        }

        void CancelActiveTweens()
        {
            if (activeTweenId >= 0)
            {
                LeanTween.cancel(activeTweenId);
                activeTweenId = -1;
            }
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }
        }

        void EnsureUi()
        {
            if (bannerRoot != null) return;

            Canvas canvas = GetComponent<Canvas>() ?? GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("KaplaoModeBanner");
            GameObject rootObj;
            if (existing != null)
            {
                rootObj = existing.gameObject;
            }
            else
            {
                rootObj = new GameObject("KaplaoModeBanner", typeof(RectTransform));
                rootObj.transform.SetParent(canvas.transform, false);
            }

            bannerRoot = rootObj.GetComponent<RectTransform>();
            bannerRoot.anchorMin = new Vector2(0.5f, 1f);
            bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            bannerRoot.pivot = new Vector2(0.5f, 1f);
            bannerRoot.anchoredPosition = new Vector2(0f, 60f);
            bannerRoot.sizeDelta = new Vector2(220f, 38f);

            bannerBackground = rootObj.GetComponent<Image>() ?? rootObj.AddComponent<Image>();
            bannerBackground.sprite = KaplaoUiSprites.Pill();
            bannerBackground.type = Image.Type.Sliced;
            bannerBackground.color = new Color(0.07f, 0.11f, 0.18f, 0.94f); // #121C2E
            bannerBackground.raycastTarget = false;

            // Dot
            Transform dotTrans = rootObj.transform.Find("Dot");
            GameObject dotObj = dotTrans != null ? dotTrans.gameObject : new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotObj.transform.SetParent(rootObj.transform, false);
            RectTransform dotRt = dotObj.GetComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0f, 0.5f);
            dotRt.anchorMax = new Vector2(0f, 0.5f);
            dotRt.pivot = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = new Vector2(20f, 0f);
            dotRt.sizeDelta = new Vector2(10f, 10f);
            bannerAccentDot = dotObj.GetComponent<Image>();
            bannerAccentDot.sprite = KaplaoUiSprites.Circle();
            bannerAccentDot.color = ColorUsual;
            bannerAccentDot.raycastTarget = false;

            // Text
            Transform textTrans = rootObj.transform.Find("Text");
            GameObject textObj = textTrans != null ? textTrans.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(rootObj.transform, false);
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(34f, 0f);
            textRt.offsetMax = new Vector2(-16f, 0f);

            bannerText = textObj.GetComponent<TextMeshProUGUI>();
            bannerText.alignment = TextAlignmentOptions.MidlineLeft;
            bannerText.fontSize = 13.5f;
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f);
            bannerText.raycastTarget = false;
        }
    }
}
