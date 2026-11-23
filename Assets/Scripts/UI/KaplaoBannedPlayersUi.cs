using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Banned players list (left side) for host and protected developer account.
    /// Shows nickname bans and IP bans with matching Unban buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class KaplaoBannedPlayersUi : MonoBehaviour
    {
        const float RefreshIntervalSeconds = 0.5f;
        const float NameColWidth = 140f;
        const float UnbanColWidth = 110f;
        const float RowHeight = 36f;

        enum BanEntryKind
        {
            Name,
            Ip
        }

        [SerializeField] GameObject panelRoot;
        [SerializeField] Transform listContent;

        [Header("Unban buttons")]
        [Tooltip("Sprite for the Unban name button background. Text stays on top.")]
        [SerializeField] Sprite unbanNameButtonSprite;

        [Tooltip("Sprite for the Unban IP button background. Text stays on top.")]
        [SerializeField] Sprite unbanIpButtonSprite;

        [Tooltip("Width of each Unban button column.")]
        [SerializeField] float unbanButtonWidth = UnbanColWidth;

        [Tooltip("Height of each Unban button row.")]
        [SerializeField] float unbanButtonHeight = RowHeight - 4f;

        float nextRefreshTime;
        LevelSessionServer subscribedSession;
        string lastBansFingerprint;

        void OnDisable()
        {
            lastBansFingerprint = null;
            UnsubscribeSession();
        }

        void Update()
        {
            TryAutoBind();

            bool isHost = NetworkServer.active && NetworkClient.isConnected;
            bool isProtected = NetworkPlayer.IsLocalAdmin();
            bool canManageBans = isHost || isProtected;
            bool pauseOpen = PauseCursorState.IsPauseOpen && IsAnyPauseMenuRootActive();
            bool canShow = canManageBans && pauseOpen && panelRoot != null && NetworkClient.isConnected;

            if (panelRoot != null && panelRoot.activeSelf != canShow)
            {
                panelRoot.SetActive(canShow);
                if (canShow)
                    panelRoot.transform.SetAsLastSibling();
            }

            if (!canShow)
                return;

            EnsureSessionSubscription();

            if (Time.time < nextRefreshTime)
                return;

            nextRefreshTime = Time.time + RefreshIntervalSeconds;
            RefreshBannedList();
        }

        void TryAutoBind()
        {
            if (panelRoot != null && listContent != null)
            {
                CleanBannedScrollMasks(panelRoot.transform);
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            Transform panel = canvas.transform.Find("BannedPlayersPanel");
            if (panel == null)
                panel = canvas.transform.Find("PauseMenuRoot/BannedPlayersPanel");

            if (panel == null)
                return;

            if (panel.parent != canvas.transform)
                panel.SetParent(canvas.transform, true);

            if (panelRoot == null)
                panelRoot = panel.gameObject;

            if (listContent == null)
                listContent = panel.Find("BannedScroll/Viewport/Content");

            CleanBannedScrollMasks(panel);
        }

        void CleanBannedScrollMasks(Transform panel)
        {
            if (panel == null) return;
            Transform scroll = panel.Find("BannedScroll");
            if (scroll != null)
            {
                Mask scrollMask = scroll.GetComponent<Mask>();
                if (scrollMask != null)
                    Destroy(scrollMask);

                Transform viewport = scroll.Find("Viewport");
                if (viewport != null)
                {
                    Mask vpMask = viewport.GetComponent<Mask>();
                    if (vpMask != null)
                        Destroy(vpMask);

                    if (viewport.GetComponent<RectMask2D>() == null)
                        viewport.gameObject.AddComponent<RectMask2D>();
                }
            }
        }

        void EnsureSessionSubscription()
        {
            LevelSessionServer session = LevelSessionServer.Instance;
            if (session == subscribedSession)
                return;

            UnsubscribeSession();
            subscribedSession = session;
            if (subscribedSession != null)
                subscribedSession.BannedIdsChanged += OnBannedIdsChanged;
        }

        void UnsubscribeSession()
        {
            if (subscribedSession != null)
                subscribedSession.BannedIdsChanged -= OnBannedIdsChanged;
            subscribedSession = null;
        }

        void OnBannedIdsChanged()
        {
            nextRefreshTime = 0f;
        }

        static bool IsAnyPauseMenuRootActive()
        {
            if (IsHostPauseMenuRootActive())
                return true;

            RunnerPauseMenuUI runnerPause = Object.FindFirstObjectByType<RunnerPauseMenuUI>();
            if (runnerPause == null)
                return false;

            Transform runnerRoot = runnerPause.transform.Find("RunnerPauseMenuRoot");
            if (runnerRoot == null)
            {
                Canvas canvas = runnerPause.GetComponentInParent<Canvas>();
                if (canvas != null)
                    runnerRoot = canvas.transform.Find("RunnerPauseMenuRoot");
            }

            return runnerRoot != null && runnerRoot.gameObject.activeSelf;
        }

        static bool IsHostPauseMenuRootActive()
        {
            PauseMenuUI pauseUi = Object.FindFirstObjectByType<PauseMenuUI>();
            if (pauseUi == null)
                return false;

            Transform pauseRoot = pauseUi.transform.Find("PauseMenuRoot");
            if (pauseRoot == null)
            {
                Canvas canvas = pauseUi.GetComponentInParent<Canvas>();
                if (canvas != null)
                    pauseRoot = canvas.transform.Find("PauseMenuRoot");
            }

            return pauseRoot != null && pauseRoot.gameObject.activeSelf;
        }

        void RefreshBannedList()
        {
            if (listContent == null || LevelSessionServer.Instance == null)
                return;

            var nameBans = new List<string>(LevelSessionServer.Instance.BannedIds);
            var ipBans = new List<string>(LevelSessionServer.Instance.BannedIps);
            nameBans.Sort(System.StringComparer.OrdinalIgnoreCase);
            ipBans.Sort(System.StringComparer.OrdinalIgnoreCase);

            string fingerprint = string.Join(";", nameBans) + "|" + string.Join(";", ipBans);
            if (fingerprint == lastBansFingerprint && listContent.childCount > 0)
                return;

            lastBansFingerprint = fingerprint;

            for (int i = listContent.childCount - 1; i >= 0; i--)
                Destroy(listContent.GetChild(i).gameObject);

            if (nameBans.Count == 0 && ipBans.Count == 0)
            {
                CreateEmptyLabel();
                return;
            }

            for (int i = 0; i < nameBans.Count; i++)
                CreateBannedRow(BanEntryKind.Name, nameBans[i]);

            for (int i = 0; i < ipBans.Count; i++)
                CreateBannedRow(BanEntryKind.Ip, ipBans[i]);

            KaplaoGames.UI.Animation.KaplaoUiAnimator.AnimateListStagger(listContent, 0.03f, 0.2f);
        }

        void CreateEmptyLabel()
        {
            GameObject go = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(listContent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, RowHeight);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.minHeight = RowHeight;
            le.preferredHeight = RowHeight;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = "No banned players";
            label.fontSize = 14f;
            label.color = new Color(1f, 1f, 1f, 0.65f);
            label.alignment = TextAlignmentOptions.Center;
        }

        void CreateBannedRow(BanEntryKind kind, string value)
        {
            bool isIp = kind == BanEntryKind.Ip;
            string display = isIp ? $"IP: {value}" : value;
            string buttonLabel = isIp ? "Unban IP" : "Unban name";
            Sprite buttonSprite = isIp ? unbanIpButtonSprite : unbanNameButtonSprite;
            bool useSpriteButton = buttonSprite != null;

            GameObject row = new GameObject($"Banned_{kind}_{value}", typeof(RectTransform));
            row.transform.SetParent(listContent, false);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, RowHeight);
            KaplaoGames.UI.Theme.KaplaoUiStyler.StyleCardRow(row, forceReplaceSprite: true);

            var rowLayout = row.GetComponent<HorizontalLayoutGroup>() ?? row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlHeight = true;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.spacing = 8f;
            rowLayout.padding = new RectOffset(8, 8, 3, 3);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(LayoutElement));
            nameGo.transform.SetParent(row.transform, false);
            LayoutElement nameLe = nameGo.GetComponent<LayoutElement>();
            nameLe.preferredWidth = NameColWidth;
            nameLe.minWidth = NameColWidth;
            nameLe.flexibleWidth = 1f;

            TextMeshProUGUI nameLabel = nameGo.AddComponent<TextMeshProUGUI>();
            nameLabel.text = display;
            nameLabel.fontSize = 15f;
            nameLabel.fontStyle = FontStyles.Bold;
            nameLabel.color = new Color(0xF8 / 255f, 0xFA / 255f, 0xFC / 255f, 1f);
            nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.overflowMode = TextOverflowModes.Ellipsis;

            GameObject buttonGo = new GameObject(
                "Unban",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonGo.transform.SetParent(row.transform, false);
            LayoutElement buttonLe = buttonGo.GetComponent<LayoutElement>();
            float buttonWidth = unbanButtonWidth > 0f ? unbanButtonWidth : UnbanColWidth;
            float buttonHeight = unbanButtonHeight > 0f ? unbanButtonHeight : RowHeight - 4f;
            buttonLe.preferredWidth = buttonWidth;
            buttonLe.minWidth = buttonWidth;
            buttonLe.preferredHeight = buttonHeight;
            buttonLe.minHeight = buttonHeight;

            Image buttonImage = buttonGo.GetComponent<Image>();
            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;

            if (useSpriteButton)
            {
                buttonImage.sprite = buttonSprite;
                buttonImage.type = Image.Type.Sliced;
                buttonImage.preserveAspect = false;
                buttonImage.color = Color.white;
                UiButtonLeanScale scale = buttonGo.AddComponent<UiButtonLeanScale>();
                scale.SetBaseColors(Color.white, Color.clear);
            }
            else
            {
                KaplaoGames.UI.Theme.KaplaoUiStyler.StyleButton(button, KaplaoGames.UI.Theme.KaplaoButtonVariant.Danger);
            }

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(2f, 0f);
            textRect.offsetMax = new Vector2(-2f, 0f);

            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = buttonLabel;
            text.fontSize = 12f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;

            string valueCopy = value;
            BanEntryKind kindCopy = kind;
            button.onClick.AddListener(() => OnUnbanClicked(kindCopy, valueCopy));
        }

        static void OnUnbanClicked(BanEntryKind kind, string value)
        {
            if (LevelSessionServer.Instance == null)
                return;

            if (kind == BanEntryKind.Ip)
            {
                if (NetworkServer.active)
                    LevelSessionServer.Instance.ServerUnbanIp(value);
                else
                    LevelSessionServer.Instance.CmdUnbanIp(value);
                return;
            }

            if (NetworkServer.active)
                LevelSessionServer.Instance.ServerUnbanByDisplayName(value);
            else
                LevelSessionServer.Instance.CmdUnbanByDisplayName(value);
        }
    }
}
