using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Network & performance HUD displaying smoothed FPS and round-trip ping time
    /// with color thresholds (green / yellow / red) and dark pill styling.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Network Ping Display")]
    public class NetworkPingDisplay : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI pingLabel;
        [SerializeField] float refreshInterval = 0.25f;

        float nextRefreshTime;
        int frameCount;
        float timeAccumulator;
        int currentFps = 60;

        void Awake()
        {
            EnsureLabel();
        }

        void Update()
        {
            frameCount++;
            timeAccumulator += Time.unscaledDeltaTime;

            if (Time.unscaledTime < nextRefreshTime)
                return;

            if (timeAccumulator > 0f)
            {
                currentFps = Mathf.RoundToInt(frameCount / timeAccumulator);
            }
            frameCount = 0;
            timeAccumulator = 0f;
            nextRefreshTime = Time.unscaledTime + refreshInterval;

            if (pingLabel != null)
                pingLabel.text = BuildPerfText(currentFps);
        }

        static string BuildPerfText(int fps)
        {
            string fpsColor = fps >= 50 ? "#34D399" : (fps >= 30 ? "#FBBF24" : "#F87171");
            string fpsPart = $"<color=#FFFFFF>FPS</color> <color={fpsColor}>{fps}</color>  <color=#64748B>|</color>  <color=#FFFFFF>PING</color>";

            if (!NetworkClient.active || !NetworkClient.isConnected)
                return $"{fpsPart} <color=#94A3B8>--</color>";

            double rttSeconds = NetworkTime.rtt;
            if (rttSeconds <= 0.0 || double.IsNaN(rttSeconds) || double.IsInfinity(rttSeconds))
                return $"{fpsPart} <color=#94A3B8>--</color>";

            int pingMs = Mathf.Max(0, Mathf.RoundToInt((float)(rttSeconds * 1000.0)));
            string pingColor = pingMs < 80 ? "#34D399" : (pingMs < 160 ? "#FBBF24" : "#F87171");
            return $"{fpsPart} <color={pingColor}>{pingMs}ms</color>";
        }

        public void RebuildPill()
        {
            EnsureLabel();
        }

        public void EnsureLabel()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            Transform root = canvas.transform.Find("NetworkPingDisplay");
            if (root == null)
            {
                var rootObject = new GameObject("NetworkPingDisplay", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                root = rootObject.transform;
            }

            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(1f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(1f, 1f);
                rootRect.anchoredPosition = new Vector2(-16f, -14f);
                rootRect.sizeDelta = new Vector2(230f, 34f);
            }

            Image bg = root.GetComponent<Image>();
            if (bg == null)
                bg = root.gameObject.AddComponent<Image>();
            bg.sprite = KaplaoUiSprites.RoundedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.04f, 0.06f, 0.12f, 0.90f);
            bg.raycastTarget = false;

            Transform borderTrans = root.Find("UI_Border");
            GameObject borderObj;
            if (borderTrans == null)
            {
                borderObj = new GameObject("UI_Border", typeof(RectTransform), typeof(Image));
                borderObj.transform.SetParent(root, false);
            }
            else
            {
                borderObj = borderTrans.gameObject;
            }

            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = KaplaoUiSprites.RoundedBorder(14, 2);
            borderImg.type = Image.Type.Sliced;
            borderImg.pixelsPerUnitMultiplier = 1f;
            borderImg.raycastTarget = false;
            borderImg.color = new Color(0x38 / 255f, 0xBD / 255f, 0xF8 / 255f, 0.35f);

            Transform labelTransform = root.Find("PingLabel");
            GameObject labelObject;
            if (labelTransform == null)
            {
                labelObject = new GameObject("PingLabel", typeof(RectTransform));
                labelObject.transform.SetParent(root, false);
            }
            else
            {
                labelObject = labelTransform.gameObject;
            }

            labelObject.transform.SetAsLastSibling();

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            pingLabel = labelObject.GetComponent<TextMeshProUGUI>();
            if (pingLabel == null)
                pingLabel = labelObject.AddComponent<TextMeshProUGUI>();

            pingLabel.alignment = TextAlignmentOptions.Center;
            pingLabel.fontSize = 15.5f;
            pingLabel.fontStyle = FontStyles.Bold;
            pingLabel.color = Color.white;
            pingLabel.raycastTarget = false;
            pingLabel.textWrappingMode = TextWrappingModes.NoWrap;

            Shadow shadow = labelObject.GetComponent<Shadow>();
            if (shadow == null)
                shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1f, -1f);

            pingLabel.text = "<color=#E2E8F0>FPS</color> <color=#34D399>60</color>";
        }
    }
}
