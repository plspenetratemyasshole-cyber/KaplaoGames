using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI.Animation
{
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Global animated toast notification banner.
    /// Appears at top-center, holds for 2.2 seconds, and smoothly fades out.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Toast")]
    public class KaplaoUiToast : MonoBehaviour
    {
        static KaplaoUiToast instance;

        [SerializeField] RectTransform container;
        [SerializeField] Image background;
        [SerializeField] Image border;
        [SerializeField] TextMeshProUGUI messageLabel;
        [SerializeField] CanvasGroup canvasGroup;

        Coroutine activeRoutine;

        public static void Show(string message, ToastType type = ToastType.Info, float duration = 2.2f)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            EnsureInstance();
            if (instance != null)
                instance.Display(message, type, duration);
        }

        public static void ShowSuccess(string message) => Show(message, ToastType.Success, 2.2f);
        public static void ShowWarning(string message) => Show(message, ToastType.Warning, 2.8f);
        public static void ShowError(string message) => Show(message, ToastType.Error, 3.2f);

        static void EnsureInstance()
        {
            if (instance != null)
                return;

            instance = FindFirstObjectByType<KaplaoUiToast>();
            if (instance != null)
                return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            GameObject root = new GameObject("KaplaoUiToastRoot", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            instance = root.AddComponent<KaplaoUiToast>();
            instance.BuildHierarchy();
        }

        void Awake()
        {
            if (instance == null)
                instance = this;

            if (container == null)
                BuildHierarchy();

            if (background != null)
                background.raycastTarget = false;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        void BuildHierarchy()
        {
            RectTransform rootRect = GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -24f);
            rootRect.sizeDelta = new Vector2(460f, 48f);

            container = rootRect;

            background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.sprite = KaplaoUiSprites.RoundedRect(14);
            background.type = Image.Type.Sliced;
            background.color = new Color(0x0F / 255f, 0x15 / 255f, 0x24 / 255f, 0.98f);
            background.raycastTarget = false;

            GameObject borderObj = new GameObject("UI_Border", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            borderObj.transform.SetParent(transform, false);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            border = borderObj.GetComponent<Image>();
            border.sprite = KaplaoUiSprites.RoundedBorder(14, 2);
            border.type = Image.Type.Sliced;
            border.raycastTarget = false;
            border.color = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.9f);
            borderObj.GetComponent<LayoutElement>().ignoreLayout = true;

            GameObject textObj = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 0f);
            textRect.offsetMax = new Vector2(-18f, 0f);

            messageLabel = textObj.GetComponent<TextMeshProUGUI>();
            messageLabel.fontSize = 15f;
            messageLabel.alignment = TextAlignmentOptions.Center;
            messageLabel.color = Color.white;
            messageLabel.raycastTarget = false;

            canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            gameObject.SetActive(false);
        }

        public void Display(string message, ToastType type, float duration)
        {
            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            if (messageLabel != null)
                messageLabel.text = message;

            Color borderColor = type switch
            {
                ToastType.Success => new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f, 0.95f),
                ToastType.Warning => new Color(0xF5 / 255f, 0x9E / 255f, 0x0B / 255f, 0.95f),
                ToastType.Error => new Color(0xEF / 255f, 0x44 / 255f, 0x44 / 255f, 0.95f),
                _ => new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.95f)
            };

            if (border != null)
                border.color = borderColor;

            activeRoutine = StartCoroutine(ToastRoutine(duration));
        }

        IEnumerator ToastRoutine(float holdSeconds)
        {
            if (container == null || canvasGroup == null)
                yield break;

            LeanTween.cancel(container.gameObject);

            Vector3 restPos = new Vector3(0f, -24f, 0f);
            container.anchoredPosition3D = restPos + new Vector3(0f, 30f, 0f);
            canvasGroup.alpha = 0f;

            LeanTween.move(container, restPos, 0.22f)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeOutBack);
            LeanTween.alphaCanvas(canvasGroup, 1f, 0.18f)
                .setIgnoreTimeScale(true);

            yield return new WaitForSecondsRealtime(holdSeconds);

            LeanTween.move(container, restPos + new Vector3(0f, 20f, 0f), 0.18f)
                .setIgnoreTimeScale(true)
                .setEase(LeanTweenType.easeInQuad);
            yield return new WaitForSecondsRealtime(
                LeanTween.alphaCanvas(canvasGroup, 0f, 0.18f).setIgnoreTimeScale(true).time);

            gameObject.SetActive(false);
            activeRoutine = null;
        }
    }
}
