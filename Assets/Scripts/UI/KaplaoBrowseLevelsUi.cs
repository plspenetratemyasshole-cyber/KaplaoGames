using System.Collections;
using System.Collections.Generic;
using KaplaoGames.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    [DisallowMultipleComponent]
    public class KaplaoBrowseLevelsUi : MonoBehaviour
    {
        [SerializeField] Button browseButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_InputField searchInput;
        [SerializeField] Transform listContent;
        [SerializeField] GameObject entryTemplate;
        [SerializeField] Button playButton;
        [SerializeField] Button copyButton;
        [SerializeField] Button deleteFromServersButton;
        [SerializeField] GameObject deleteConfirmPanel;
        [SerializeField] TextMeshProUGUI deleteConfirmLabel;
        [SerializeField] Button deleteConfirmButton;
        [SerializeField] Button deleteCancelButton;
        [SerializeField] GameObject progressRoot;
        [SerializeField] Slider progressBar;
        [SerializeField] TextMeshProUGUI progressLabel;
        [SerializeField] TextMeshProUGUI statusLabel;
        [SerializeField] Button backButton;
        [SerializeField] ScrollRect scrollRect;

        readonly List<PublicLevelEntry> loadedEntries = new();
        readonly List<GameObject> entryObjects = new();
        PublicLevelEntry selected;
        int nextOffset;
        bool loading;
        bool endReached;
        bool canModerateDelete;
        bool deleteInProgress;
        Coroutine fetchRoutine;
        Coroutine accessRoutine;
        MainMenuUI mainMenu;

        void Awake()
        {
            TryAutoWireMissingRefs();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (entryTemplate != null) entryTemplate.SetActive(false);
            if (progressRoot != null) progressRoot.SetActive(false);
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
            SetActionButtonsVisible(false);
            if (browseButton != null) browseButton.gameObject.SetActive(true);
        }

        void OnEnable()
        {
            TryAutoWireMissingRefs();
            if (browseButton != null) browseButton.onClick.AddListener(OpenPanel);
            if (backButton != null) backButton.onClick.AddListener(ClosePanel);
            if (playButton != null) playButton.onClick.AddListener(PlaySelected);
            if (copyButton != null) copyButton.onClick.AddListener(CopySelected);
            if (deleteFromServersButton != null) deleteFromServersButton.onClick.AddListener(ShowDeleteConfirm);
            if (deleteConfirmButton != null) deleteConfirmButton.onClick.AddListener(ConfirmDeleteSelected);
            if (deleteCancelButton != null) deleteCancelButton.onClick.AddListener(HideDeleteConfirm);
            if (searchInput != null) searchInput.onSubmit.AddListener(_ => RefreshSearch());
            mainMenu = GetComponent<MainMenuUI>() ?? FindFirstObjectByType<MainMenuUI>();
        }

        void OnDisable()
        {
            if (browseButton != null) browseButton.onClick.RemoveListener(OpenPanel);
            if (backButton != null) backButton.onClick.RemoveListener(ClosePanel);
            if (playButton != null) playButton.onClick.RemoveListener(PlaySelected);
            if (copyButton != null) copyButton.onClick.RemoveListener(CopySelected);
            if (deleteFromServersButton != null) deleteFromServersButton.onClick.RemoveListener(ShowDeleteConfirm);
            if (deleteConfirmButton != null) deleteConfirmButton.onClick.RemoveListener(ConfirmDeleteSelected);
            if (deleteCancelButton != null) deleteCancelButton.onClick.RemoveListener(HideDeleteConfirm);
        }

        void Update()
        {
            if (panelRoot == null || !panelRoot.activeSelf || scrollRect == null || loading || endReached)
                return;

            if (scrollRect.verticalNormalizedPosition <= 0.05f)
                FetchNextPage();
        }

        void OpenPanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(true);
            HideDeleteConfirm();
            RefreshModeratorAccess();
            RefreshSearch();
        }

        void ClosePanel()
        {
            HideDeleteConfirm();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void RefreshModeratorAccess()
        {
            canModerateDelete = false;
            if (accessRoutine != null)
            {
                StopCoroutine(accessRoutine);
                accessRoutine = null;
            }

            if (!PlayerSession.IsLoggedIn || string.IsNullOrWhiteSpace(PlayerSession.AuthToken))
                return;

            accessRoutine = StartCoroutine(RefreshModeratorAccessRoutine());
        }

        IEnumerator RefreshModeratorAccessRoutine()
        {
            var result = new KaplaoAccountVerificationService.VerifyResult();
            bool done = false;
            yield return KaplaoAccountVerificationService.VerifyPlayerCoroutine(
                PlayerSession.AuthToken,
                verifyResult =>
                {
                    result = verifyResult;
                    done = true;
                });

            while (!done)
                yield return null;

            accessRoutine = null;
            canModerateDelete = result.Valid && KaplaoAccountVerificationService.CanDeletePublicLevels(result);
            if (selected != null)
                UpdateActionButtonsVisible();
        }

        void RefreshSearch()
        {
            loadedEntries.Clear();
            nextOffset = 0;
            endReached = false;
            selected = null;
            HideDeleteConfirm();
            SetActionButtonsVisible(false);
            ClearEntries();
            FetchNextPage();
        }

        void FetchNextPage()
        {
            if (fetchRoutine != null) return;
            loading = true;
            fetchRoutine = StartCoroutine(FetchPageRoutine());
        }

        IEnumerator FetchPageRoutine()
        {
            string search = searchInput != null ? searchInput.text : string.Empty;
            List<PublicLevelEntry> page = null;
            string error = null;
            yield return PublicLevelService.FetchPage(nextOffset, search, p => page = p, e => error = e);
            loading = false;
            fetchRoutine = null;

            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error);
                yield break;
            }

            if (page == null || page.Count == 0)
            {
                endReached = true;
                yield break;
            }

            loadedEntries.AddRange(page);
            nextOffset += page.Count;
            RebuildList();
        }

        void RebuildList()
        {
            while (entryObjects.Count < loadedEntries.Count)
            {
                GameObject clone = Instantiate(entryTemplate, listContent);
                clone.SetActive(true);
                entryObjects.Add(clone);
            }

            for (int i = 0; i < entryObjects.Count; i++)
            {
                bool active = i < loadedEntries.Count;
                entryObjects[i].SetActive(active);
                if (!active) continue;

                PublicLevelEntry entry = loadedEntries[i];
                TextMeshProUGUI title = entryObjects[i].transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI author = entryObjects[i].transform.Find("Author")?.GetComponent<TextMeshProUGUI>();
                if (title != null) title.text = entry.title;
                if (author != null) author.text = "by " + entry.author_nick;

                Button button = entryObjects[i].GetComponent<Button>() ?? entryObjects[i].AddComponent<Button>();
                PublicLevelEntry captured = entry;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectEntry(captured));
            }
        }

        void ClearEntries()
        {
            for (int i = 0; i < entryObjects.Count; i++)
                entryObjects[i].SetActive(false);
        }

        void SelectEntry(PublicLevelEntry entry)
        {
            selected = entry;
            HideDeleteConfirm();
            UpdateActionButtonsVisible();
        }

        void PlaySelected()
        {
            if (selected == null) return;
            StartCoroutine(DownloadRoutine(selected, PublicLevelService.LevelInstallTarget.BrowsePlay, playAfterDownload: true));
        }

        void CopySelected()
        {
            if (selected == null) return;
            StartCoroutine(DownloadRoutine(selected, PublicLevelService.LevelInstallTarget.UserLibrary, playAfterDownload: false));
        }

        void ShowDeleteConfirm()
        {
            if (selected == null || !CanDeleteSelectedLevel() || deleteInProgress)
                return;

            if (deleteConfirmPanel == null)
            {
                SetStatus("Delete confirm UI is missing. Run Kaplao -> Setup Browse Levels UI.");
                return;
            }

            if (deleteConfirmLabel != null)
            {
                deleteConfirmLabel.text =
                    $"Permanently delete \"{selected.title}\" from public servers?\nThis cannot be undone.";
            }

            deleteConfirmPanel.SetActive(true);
        }

        void HideDeleteConfirm()
        {
            if (deleteConfirmPanel != null)
                deleteConfirmPanel.SetActive(false);
        }

        void ConfirmDeleteSelected()
        {
            if (selected == null || deleteInProgress)
                return;

            StartCoroutine(DeleteSelectedRoutine());
        }

        IEnumerator DeleteSelectedRoutine()
        {
            if (selected == null)
                yield break;

            deleteInProgress = true;
            HideDeleteConfirm();
            UpdateActionButtonsVisible();
            SetStatus("Deleting from servers...");

            PublicLevelEntry entry = selected;
            string error = null;
            bool success = false;
            yield return PublicLevelService.DeleteFromCatalog(
                entry.id,
                entry.title,
                () => success = true,
                e => error = e);

            deleteInProgress = false;

            if (!success)
            {
                SetStatus(string.IsNullOrWhiteSpace(error) ? "Delete failed." : error);
                UpdateActionButtonsVisible();
                yield break;
            }

            loadedEntries.Remove(entry);
            selected = null;
            RebuildList();
            SetStatus($"Deleted \"{entry.title}\" from servers.");
            SetActionButtonsVisible(false);
        }

        IEnumerator DownloadRoutine(
            PublicLevelEntry entry,
            PublicLevelService.LevelInstallTarget target,
            bool playAfterDownload)
        {
            HideDeleteConfirm();
            UpdateActionButtonsVisible(false);
            if (progressRoot != null) progressRoot.SetActive(true);
            string worldName = null;
            string error = null;
            yield return PublicLevelService.DownloadAndInstall(
                entry.storage_path,
                entry.title,
                p =>
                {
                    if (progressBar != null) progressBar.value = p;
                    if (progressLabel != null) progressLabel.text = $"{Mathf.RoundToInt(p * 100f)}%";
                },
                name => worldName = name,
                e => error = e,
                target);

            if (progressRoot != null) progressRoot.SetActive(false);
            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error);
                if (selected != null)
                    UpdateActionButtonsVisible();
                yield break;
            }

            ClosePanel();

            if (playAfterDownload)
                mainMenu?.PlayBrowseLevelByName(worldName);
            else
                mainMenu?.OnBrowseLevelCopied(worldName);
        }

        void UpdateActionButtonsVisible()
        {
            UpdateActionButtonsVisible(selected != null);
        }

        bool CanDeleteSelectedLevel()
        {
            if (selected == null)
                return false;

            if (canModerateDelete)
                return true;

            return PublicLevelService.IsOwnedByCurrentPlayer(selected);
        }

        void UpdateActionButtonsVisible(bool visible)
        {
            if (playButton != null) playButton.gameObject.SetActive(visible);
            if (copyButton != null) copyButton.gameObject.SetActive(visible);

            bool showDelete = visible && CanDeleteSelectedLevel();
            if (deleteFromServersButton != null)
            {
                deleteFromServersButton.gameObject.SetActive(showDelete);
                deleteFromServersButton.interactable = showDelete && !deleteInProgress;
            }
        }

        void SetActionButtonsVisible(bool visible)
        {
            UpdateActionButtonsVisible(visible);
        }

        void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        void TryAutoWireMissingRefs()
        {
            Transform canvasRoot = transform;
            if (browseButton == null)
            {
                Transform buttonTransform = FindChildRecursive(canvasRoot, "BrowseLevelsButton");
                if (buttonTransform != null)
                    browseButton = buttonTransform.GetComponent<Button>();
            }

            if (panelRoot == null || panelRoot == browseButton?.gameObject)
            {
                Transform panelTransform = FindChildRecursive(canvasRoot, "BrowseLevelsPanel");
                if (panelTransform != null)
                    panelRoot = panelTransform.gameObject;
            }

            if (playButton == null)
            {
                Transform playTransform = FindChildRecursive(canvasRoot, "BrowsePlayButton");
                if (playTransform != null)
                    playButton = playTransform.GetComponent<Button>();
            }

            if (copyButton == null)
            {
                Transform copyTransform = FindChildRecursive(canvasRoot, "BrowseCopyButton");
                if (copyTransform != null)
                    copyButton = copyTransform.GetComponent<Button>();
            }

            if (deleteFromServersButton == null)
            {
                Transform deleteTransform = FindChildRecursive(canvasRoot, "BrowseDeleteFromServersButton");
                if (deleteTransform != null)
                    deleteFromServersButton = deleteTransform.GetComponent<Button>();
            }

            if (deleteConfirmPanel == null)
            {
                Transform confirmTransform = FindChildRecursive(canvasRoot, "BrowseDeleteConfirmPanel");
                if (confirmTransform != null)
                    deleteConfirmPanel = confirmTransform.gameObject;
            }

            if (deleteConfirmLabel == null && deleteConfirmPanel != null)
            {
                Transform labelTransform = deleteConfirmPanel.transform.Find("BrowseDeleteConfirmLabel");
                if (labelTransform != null)
                    deleteConfirmLabel = labelTransform.GetComponent<TextMeshProUGUI>();
            }

            if (deleteConfirmButton == null && deleteConfirmPanel != null)
            {
                Transform confirmButtonTransform = deleteConfirmPanel.transform.Find("BrowseDeleteConfirmButton");
                if (confirmButtonTransform != null)
                    deleteConfirmButton = confirmButtonTransform.GetComponent<Button>();
            }

            if (deleteCancelButton == null && deleteConfirmPanel != null)
            {
                Transform cancelButtonTransform = deleteConfirmPanel.transform.Find("BrowseDeleteCancelButton");
                if (cancelButtonTransform != null)
                    deleteCancelButton = cancelButtonTransform.GetComponent<Button>();
            }

            // Legacy single-button setup from older scenes.
            if (playButton == null)
            {
                Transform legacyTransform = FindChildRecursive(canvasRoot, "DownloadAndPlayButton");
                if (legacyTransform != null)
                    playButton = legacyTransform.GetComponent<Button>();
            }
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
