using Mirror;
using TMPro;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Shows network round-trip time in the top-right corner for Kaplao and Runner.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Network Ping Display")]
    public class NetworkPingDisplay : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI pingLabel;
        [SerializeField] float refreshInterval = 0.25f;

        float nextRefreshTime;

        void Awake()
        {
            EnsureLabel();
        }

        void Update()
        {
            if (pingLabel == null)
                return;

            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            pingLabel.text = BuildPingText();
        }

        static string BuildPingText()
        {
            if (!NetworkClient.active || !NetworkClient.isConnected)
                return "Ping: -- ms";

            double rttSeconds = NetworkTime.rtt;
            if (rttSeconds <= 0.0 || double.IsNaN(rttSeconds) || double.IsInfinity(rttSeconds))
                return "Ping: -- ms";

            int pingMs = Mathf.Max(0, Mathf.RoundToInt((float)(rttSeconds * 1000.0)));
            return $"Ping: {pingMs} ms";
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
                rootRect.sizeDelta = new Vector2(160f, 32f);
                root = rootObject.transform;
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
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            pingLabel = labelObject.GetComponent<TextMeshProUGUI>();
            if (pingLabel == null)
                pingLabel = labelObject.AddComponent<TextMeshProUGUI>();

            pingLabel.alignment = TextAlignmentOptions.TopRight;
            pingLabel.fontSize = 18f;
            pingLabel.color = Color.white;
            pingLabel.raycastTarget = false;
            pingLabel.textWrappingMode = TextWrappingModes.NoWrap;
            pingLabel.text = "Ping: -- ms";
        }
    }
}
