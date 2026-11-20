using System.Collections;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    public class KaplaoPublishLevelUi : MonoBehaviour
    {
        [SerializeField] Button publishButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_InputField titleInput;
        [SerializeField] Button confirmPublishButton;
        [SerializeField] Button cancelButton;
        [SerializeField] GameObject progressRoot;
        [SerializeField] Slider progressBar;
        [SerializeField] TextMeshProUGUI progressLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] KaplaoAccountClient accountClient;

        PauseMenuUI pauseMenu;
        Coroutine publishRoutine;

        void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (progressRoot != null) progressRoot.SetActive(false);
        }

        void OnEnable()
        {
            if (publishButton != null) publishButton.onClick.AddListener(OpenPanel);
            if (confirmPublishButton != null) confirmPublishButton.onClick.AddListener(StartPublish);
            if (cancelButton != null) cancelButton.onClick.AddListener(ClosePanel);
            pauseMenu = GetComponent<PauseMenuUI>() ?? FindFirstObjectByType<PauseMenuUI>();
        }

        void OnDisable()
        {
            if (publishButton != null) publishButton.onClick.RemoveListener(OpenPanel);
            if (confirmPublishButton != null) confirmPublishButton.onClick.RemoveListener(StartPublish);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(ClosePanel);
        }

        void Update()
        {
            bool host = NetworkServer.active && NetworkClient.isConnected;
            if (publishButton != null)
                publishButton.gameObject.SetActive(host);
        }

        void OpenPanel()
        {
            if (panelRoot == null) return;

            if (pauseMenu == null)
                pauseMenu = GetComponent<PauseMenuUI>() ?? FindFirstObjectByType<PauseMenuUI>();

            if (pauseMenu != null)
                pauseMenu.ShowExternalOverlay(panelRoot);
            else
                panelRoot.SetActive(true);

            if (progressRoot != null) progressRoot.SetActive(false);
            if (titleInput != null && WorldSession.HasActiveWorld)
                titleInput.text = WorldSession.ActiveWorldName;
            SetStatus(string.Empty);
        }

        void ClosePanel()
        {
            if (pauseMenu != null && panelRoot != null)
                pauseMenu.HideExternalOverlay(panelRoot);
            else if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        void StartPublish()
        {
            if (accountClient == null) accountClient = KaplaoAccountClient.Instance;
            if (!PlayerSession.IsLoggedIn)
            {
                SetStatus("Log in to account to publish levels.");
                return;
            }

            string title = titleInput != null ? titleInput.text.Trim() : WorldSession.ActiveWorldName;
            if (string.IsNullOrWhiteSpace(title))
            {
                SetStatus("Enter a level title.");
                return;
            }

            if (publishRoutine != null) StopCoroutine(publishRoutine);
            if (progressRoot != null) progressRoot.SetActive(true);
            publishRoutine = StartCoroutine(PublishRoutine(title, PlayerSession.Nickname));
        }

        IEnumerator PublishRoutine(string title, string authorNick)
        {
            yield return PublicLevelService.PublishActiveWorld(
                title,
                authorNick,
                p =>
                {
                    if (progressBar != null) progressBar.value = p;
                    if (progressLabel != null) progressLabel.text = $"{Mathf.RoundToInt(p * 100f)}%";
                },
                msg =>
                {
                    SetStatus(msg);
                    ClosePanel();
                    if (progressRoot != null) progressRoot.SetActive(false);
                    pauseMenu?.ResumeFromExternal();
                },
                err => SetStatus(err));
            publishRoutine = null;
        }

        void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }
    }
}
