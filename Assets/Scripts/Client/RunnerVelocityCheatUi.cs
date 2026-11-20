using KaplaoGames.Network;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// World-space velocity readout pinned to the local runner's top-left.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerVelocityCheatUi : MonoBehaviour
    {
        [SerializeField] Vector2 canvasSize = new Vector2(2.4f, 0.55f);
        [SerializeField] float canvasScale = 0.01f;
        [SerializeField] int fontSize = 28;
        [SerializeField] int sortingOrder = 210;
        [SerializeField] float updateMinIntervalSeconds = 0.05f;
        [SerializeField] Vector2 screenPadding = new Vector2(0.04f, 0.04f);

        Rigidbody2D rb;
        Collider2D col;
        TextMeshProUGUI label;
        RectTransform canvasRect;
        GameObject root;
        float nextUpdateTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            EnsureLabel();
            SetVisible(RunnerVelocityCheat.ShowVelocity);
        }

        void OnEnable()
        {
            RunnerVelocityCheat.Changed += OnCheatChanged;
        }

        void OnDisable()
        {
            RunnerVelocityCheat.Changed -= OnCheatChanged;
        }

        void OnCheatChanged()
        {
            if (!IsLocalRunner())
                return;

            SetVisible(RunnerVelocityCheat.ShowVelocity);
        }

        public void SetVisible(bool visible)
        {
            EnsureLabel();
            if (root != null)
                root.SetActive(visible);

            if (!visible)
                nextUpdateTime = 0f;
        }

        void EnsureLabel()
        {
            if (root != null)
                return;

            root = new GameObject("RunnerVelocityLabel");
            root.transform.SetParent(transform, false);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = sortingOrder;
            NicknameStyle.EnsureWorldSpaceTmpCanvas(canvas);

            canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = canvasSize;
            canvasRect.anchorMin = new Vector2(0f, 1f);
            canvasRect.anchorMax = new Vector2(0f, 1f);
            canvasRect.pivot = new Vector2(0f, 1f);
            canvasRect.localScale = Vector3.one * canvasScale;

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            label = textObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.text = string.Empty;

            if (TMP_Settings.defaultFontAsset != null)
                NicknameStyle.AssignFont(label, TMP_Settings.defaultFontAsset);

            root.SetActive(false);
        }

        void LateUpdate()
        {
            if (root == null || !root.activeInHierarchy)
                return;

            if (!IsLocalRunner())
            {
                SetVisible(false);
                return;
            }

            if (rb == null)
                rb = GetComponent<Rigidbody2D>();
            if (col == null)
                col = GetComponent<Collider2D>();

            UpdateAnchorPosition();

            if (Camera.main != null)
            {
                Canvas canvas = root.GetComponent<Canvas>();
                if (canvas != null && canvas.worldCamera == null)
                    canvas.worldCamera = Camera.main;

                canvasRect.rotation = Camera.main.transform.rotation;
            }

            if (Time.time < nextUpdateTime || label == null || rb == null)
                return;

            nextUpdateTime = Time.time + Mathf.Max(0.01f, updateMinIntervalSeconds);

            Vector2 velocity = rb.linearVelocity;
            label.text = $"vX: {velocity.x:0.00}\n|v|: {velocity.magnitude:0.00}";
        }

        void UpdateAnchorPosition()
        {
            if (col != null)
            {
                Bounds bounds = col.bounds;
                root.transform.position = new Vector3(
                    bounds.min.x - screenPadding.x,
                    bounds.max.y + screenPadding.y,
                    transform.position.z);
                return;
            }

            root.transform.position = transform.position + new Vector3(-0.25f, 1.05f, 0f);
        }

        static bool IsLocalRunner()
        {
            if (!NetworkClient.active || NetworkClient.localPlayer == null)
                return false;

            return NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) &&
                   player.role == PlayerRole.Runner;
        }
    }
}
