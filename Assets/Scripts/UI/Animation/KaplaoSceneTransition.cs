using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Global scene transition overlay.
    /// Provides smooth cinematic fade-out / fade-in curtains with loading spinner.
    /// Self-initializes on startup via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class KaplaoSceneTransition : MonoBehaviour
    {
        static KaplaoSceneTransition instance;

        Canvas transitionCanvas;
        CanvasGroup canvasGroup;
        TextMeshProUGUI subtitleText;
        RectTransform spinnerTransform;
        Coroutine timeoutRoutine;
        bool isTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitOnLoad()
        {
            if (instance != null) return;
            GameObject go = new GameObject("[KaplaoSceneTransition]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<KaplaoSceneTransition>();
            instance.BuildHierarchy();
        }

        public static KaplaoSceneTransition Instance => instance;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                BuildHierarchy();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void BuildHierarchy()
        {
            if (transitionCanvas != null) return;

            transitionCanvas = gameObject.AddComponent<Canvas>();
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = 9999;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Fullscreen backdrop
            GameObject bgObj = new GameObject("Curtain", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            bgObj.transform.SetParent(transform, false);

            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            Image bgImg = bgObj.GetComponent<Image>();
            bgImg.color = new Color(0x05 / 255f, 0x08 / 255f, 0x11 / 255f, 1f); // #050811

            canvasGroup = bgObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;

            // Center container
            GameObject centerObj = new GameObject("CenterContent", typeof(RectTransform));
            centerObj.transform.SetParent(bgObj.transform, false);
            RectTransform centerRt = centerObj.GetComponent<RectTransform>();
            centerRt.anchorMin = new Vector2(0.5f, 0.5f);
            centerRt.anchorMax = new Vector2(0.5f, 0.5f);
            centerRt.pivot = new Vector2(0.5f, 0.5f);
            centerRt.sizeDelta = new Vector2(400f, 200f);

            // Spinner Neon Ring
            GameObject spinnerObj = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
            spinnerObj.transform.SetParent(centerObj.transform, false);
            spinnerTransform = spinnerObj.GetComponent<RectTransform>();
            spinnerTransform.anchorMin = new Vector2(0.5f, 0.65f);
            spinnerTransform.anchorMax = new Vector2(0.5f, 0.65f);
            spinnerTransform.pivot = new Vector2(0.5f, 0.5f);
            spinnerTransform.anchoredPosition = Vector2.zero;
            spinnerTransform.sizeDelta = new Vector2(48f, 48f);

            Image spinnerImg = spinnerObj.GetComponent<Image>();
            spinnerImg.sprite = KaplaoUiSprites.RoundedBorder(24, 4);
            spinnerImg.color = new Color(0.22f, 0.74f, 0.97f, 1f); // Neon Cyan #38BDF8

            // Subtitle
            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subObj.transform.SetParent(centerObj.transform, false);
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.25f);
            subRt.anchorMax = new Vector2(0.5f, 0.25f);
            subRt.pivot = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = Vector2.zero;
            subRt.sizeDelta = new Vector2(360f, 40f);

            subtitleText = subObj.GetComponent<TextMeshProUGUI>();
            subtitleText.text = "Loading...";
            subtitleText.fontSize = 20f;
            subtitleText.fontStyle = FontStyles.Bold;
            subtitleText.alignment = TextAlignmentOptions.Center;
            subtitleText.color = new Color(0.97f, 0.98f, 0.99f, 0.95f);
        }

        void Update()
        {
            if (isTransitioning && spinnerTransform != null)
            {
                spinnerTransform.Rotate(0f, 0f, -280f * Time.unscaledDeltaTime);
            }
        }

        public static void TransitionTo(string subtitle, Action loadAction)
        {
            if (instance == null)
                InitOnLoad();

            if (instance != null)
                instance.StartTransition(subtitle, loadAction);
            else
                loadAction?.Invoke();
        }

        void StartTransition(string subtitle, Action loadAction)
        {
            if (subtitleText != null)
                subtitleText.text = subtitle ?? "Loading...";

            isTransitioning = true;
            canvasGroup.blocksRaycasts = true;

            LeanTween.cancel(canvasGroup.gameObject);
            LeanTween.alphaCanvas(canvasGroup, 1f, 0.22f)
                .setIgnoreTimeScale(true)
                .setEaseOutQuad()
                .setOnComplete(() =>
                {
                    try
                    {
                        loadAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[KaplaoSceneTransition] Error during transition action: {ex}");
                        FadeOut();
                        return;
                    }

                    if (timeoutRoutine != null)
                        StopCoroutine(timeoutRoutine);
                    timeoutRoutine = StartCoroutine(TransitionSafetyTimeout());
                });
        }

        IEnumerator TransitionSafetyTimeout()
        {
            yield return new WaitForSecondsRealtime(6f);
            if (isTransitioning)
            {
                FadeOut();
                KaplaoUiToast.ShowWarning("Connection or scene load timed out.");
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (timeoutRoutine != null)
            {
                StopCoroutine(timeoutRoutine);
                timeoutRoutine = null;
            }

            if (canvasGroup != null && canvasGroup.alpha > 0f)
            {
                FadeOut();
            }
        }

        void FadeOut()
        {
            if (canvasGroup == null) return;

            LeanTween.cancel(canvasGroup.gameObject);
            LeanTween.alphaCanvas(canvasGroup, 0f, 0.35f)
                .setIgnoreTimeScale(true)
                .setEaseOutQuad()
                .setOnComplete(() =>
                {
                    canvasGroup.blocksRaycasts = false;
                    isTransitioning = false;
                });
        }
    }
}
