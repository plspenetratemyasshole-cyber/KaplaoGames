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
            string fpsColor = fps >= 55 ? "#10B981" : (fps >= 30 ? "#FBBF24" : "#EF4444");
            string fpsPart = $"<color=#94A3B8>FPS</color> <color={fpsColor}>{fps}</color>";

            if (!NetworkClient.active || !NetworkClient.isConnected)
                return fpsPart;

            double rttSeconds = NetworkTime.rtt;
            if (rttSeconds <= 0.0 || double.IsNaN(rttSeconds) || double.IsInfinity(rttSeconds))
                return $"{fpsPart}  <color=#94A3B8>PING</color> <color=#94A3B8>--</color>";

            int pingMs = Mathf.Max(0, Mathf.RoundToInt((float)(rttSeconds * 1000.0)));
            string pingColor = pingMs < 60 ? "#10B981" : (pingMs < 120 ? "#FBBF24" : "#EF4444");
            return $"{fpsPart}  <color=#94A3B8>PING</color> <color={pingColor}>{pingMs}ms</color>";
        }

        void EnsureLabel()
        {
            if (pingLabel != null)
                return;

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
                RectTransform rootRect = rootObject.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(1f, 1f);
                rootRect.anchorMax = new Vector2(1f, 1f);
                rootRect.pivot = new Vector2(1f, 1f);
                rootRect.anchoredPosition = new Vector2(-16f, -16f);
                rootRect.sizeDelta = new Vector2(210f, 30f);
                root = rootObject.transform;
            }

            Image bg = root.GetComponent<Image>();
            if (bg == null)
            {
                bg = root.gameObject.AddComponent<Image>();
                bg.sprite = KaplaoUiSprites.Pill();
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0.06f, 0.09f, 0.16f, 0.65f);
                bg.raycastTarget = false;
            }

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

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            pingLabel = labelObject.GetComponent<TextMeshProUGUI>();
            if (pingLabel == null)
                pingLabel = labelObject.AddComponent<TextMeshProUGUI>();

            pingLabel.alignment = TextAlignmentOptions.Center;
            pingLabel.fontSize = 13.5f;
            pingLabel.fontStyle = FontStyles.Bold;
            pingLabel.color = Color.white;
            pingLabel.raycastTarget = false;
            pingLabel.textWrappingMode = TextWrappingModes.NoWrap;
            pingLabel.text = "<color=#94A3B8>FPS</color> 60";
        }
    }
}
