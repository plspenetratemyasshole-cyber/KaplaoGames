using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class KaplaoSelectionSettingsUi : MonoBehaviour
    {
        const bool HideOpenButtonForNow = true;

        [SerializeField] Button openButton;
        [SerializeField] Button resetButton;
        [SerializeField] GameObject panelRoot;
        [SerializeField] TMP_Dropdown criterionDropdown;
        [SerializeField] GameObject typePanel;
        [SerializeField] Toggle typeBlockToggle;
        [SerializeField] Toggle typeSpikeToggle;
        [SerializeField] Toggle typeTriggerToggle;
        [SerializeField] Toggle typeParticleToggle;
        [SerializeField] Toggle typeKeyframeToggle;
        [SerializeField] Toggle typeExecutorToggle;
        [SerializeField] GameObject colorPanel;
        [SerializeField] TMP_InputField colorHexInput;
        [SerializeField] Button colorAddButton;
        [SerializeField] RectTransform colorGridRoot;
        [SerializeField] GameObject groupPanel;
        [SerializeField] TMP_InputField groupInput;
        [SerializeField] Button groupAddButton;
        [SerializeField] RectTransform groupGridRoot;
        [SerializeField] GameObject comparePanel;
        [SerializeField] TMP_Dropdown compareDropdown;
        [SerializeField] TMP_InputField valueInput;
        [SerializeField] TMP_InputField valueXInput;
        [SerializeField] TMP_InputField valueYInput;
        [SerializeField] TMP_InputField dateHoursInput;
        [SerializeField] TMP_InputField dateMinutesInput;
        [SerializeField] TMP_InputField dateDaysInput;
        [SerializeField] TMP_InputField dateMonthsInput;
        [SerializeField] TMP_InputField dateYearsInput;
        [SerializeField] Button applyButton;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject kaplaoOnlyRoot;

        KaplaoCursorClient cursorClient;
        KaplaoCursorServer cursorServer;
        readonly List<Button> colorChipButtons = new();
        readonly List<Button> groupChipButtons = new();
        readonly List<string> colorList = new();
        readonly List<int> groupList = new();
        bool suppress;

        void Awake()
        {
            TryAutoWireMissingRefs();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (resetButton != null) resetButton.gameObject.SetActive(false);
            if (HideOpenButtonForNow && openButton != null) openButton.gameObject.SetActive(false);
            if (applyButton != null) applyButton.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            TryAutoWireMissingRefs();
            if (openButton != null) openButton.onClick.AddListener(TogglePanel);
            if (resetButton != null) resetButton.onClick.AddListener(ResetFilter);
            if (colorAddButton != null) colorAddButton.onClick.AddListener(AddColor);
            if (groupAddButton != null) groupAddButton.onClick.AddListener(AddGroup);
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
            if (criterionDropdown != null) criterionDropdown.onValueChanged.AddListener(OnCriterionChanged);
            WireFilterToggles(true);
            WireFilterInputs(true);
            StartCoroutine(WatchKaplao());
        }

        void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(TogglePanel);
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetFilter);
            if (colorAddButton != null) colorAddButton.onClick.RemoveListener(AddColor);
            if (groupAddButton != null) groupAddButton.onClick.RemoveListener(AddGroup);
            if (closeButton != null) closeButton.onClick.RemoveListener(ClosePanel);
            if (criterionDropdown != null) criterionDropdown.onValueChanged.RemoveListener(OnCriterionChanged);
            WireFilterToggles(false);
            WireFilterInputs(false);
            ClearBinding();
        }

        IEnumerator WatchKaplao()
        {
            while (enabled)
            {
                if (NetworkClient.localPlayer != null &&
                    NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer np) &&
                    np.role == PlayerRole.Kaplao)
                {
                    KaplaoCursorClient c = NetworkClient.localPlayer.GetComponent<KaplaoCursorClient>();
                    KaplaoCursorServer s = NetworkClient.localPlayer.GetComponent<KaplaoCursorServer>();
                    if (c != cursorClient || s != cursorServer)
                    {
                        cursorClient = c;
                        cursorServer = s;
                    }

                    bool create = s != null && KaplaoLocalGameModePredictor.IsCreate(s);
                    bool showOpen = create && !HideOpenButtonForNow;
                    if (openButton != null) openButton.gameObject.SetActive(showOpen);
                    RefreshResetButton();
                }
                else
                {
                    ClearBinding();
                    if (openButton != null) openButton.gameObject.SetActive(false);
                }

                yield return null;
            }
        }

        void ClearBinding()
        {
            cursorClient = null;
            cursorServer = null;
        }

        void TryAutoWireMissingRefs()
        {
            if (panelRoot == null)
                return;

            Transform root = panelRoot.transform;
            if (criterionDropdown == null)
                criterionDropdown = FindDropdown(root, "CriterionDropdown");
            if (compareDropdown == null)
                compareDropdown = FindDropdown(root, "CompareDropdown");
            if (typePanel == null)
                typePanel = FindGo(root, "TypePanel");
            if (colorPanel == null)
                colorPanel = FindGo(root, "ColorPanel");
            if (groupPanel == null)
                groupPanel = FindGo(root, "GroupPanel");
            if (comparePanel == null)
                comparePanel = FindGo(root, "ComparePanel");
        }

        static TMP_Dropdown FindDropdown(Transform root, string name)
        {
            Transform t = FindChildRecursive(root, name);
            return t != null ? t.GetComponent<TMP_Dropdown>() : null;
        }

        static GameObject FindGo(Transform root, string name)
        {
            Transform t = FindChildRecursive(root, name);
            return t != null ? t.gameObject : null;
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

        void TogglePanel()
        {
            if (panelRoot == null) return;
            bool open = !panelRoot.activeSelf;
            panelRoot.SetActive(open);
            if (open) LoadFromServer();
        }

        void ClosePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        void LoadFromServer()
        {
            CreateModeSelectionFilter filter = cursorServer?.SelectionFilter ?? CreateModeSelectionFilter.CreateDefault();
            suppress = true;
            if (criterionDropdown != null) criterionDropdown.value = (int)filter.criterion;
            if (typeBlockToggle != null) typeBlockToggle.isOn = filter.allowBlock;
            if (typeSpikeToggle != null) typeSpikeToggle.isOn = filter.allowSpike;
            if (typeTriggerToggle != null) typeTriggerToggle.isOn = filter.allowTrigger;
            if (typeParticleToggle != null) typeParticleToggle.isOn = filter.allowParticle;
            if (typeKeyframeToggle != null) typeKeyframeToggle.isOn = filter.allowKeyframe;
            if (typeExecutorToggle != null) typeExecutorToggle.isOn = filter.allowExecutor;
            colorList.Clear();
            if (filter.allowedColorsHex != null) colorList.AddRange(filter.allowedColorsHex);
            groupList.Clear();
            if (filter.allowedGroups != null) groupList.AddRange(filter.allowedGroups);
            if (compareDropdown != null) compareDropdown.value = filter.criterion == SelectionFilterCriterion.Date ? (int)filter.dateOp : (int)filter.numericOp;
            if (valueInput != null) valueInput.text = filter.numericValue.ToString(CultureInfo.InvariantCulture);
            if (valueXInput != null) valueXInput.text = filter.numericValueX.ToString(CultureInfo.InvariantCulture);
            if (valueYInput != null) valueYInput.text = filter.numericValueY.ToString(CultureInfo.InvariantCulture);
            if (dateHoursInput != null) dateHoursInput.text = filter.dateThreshold.hours.ToString();
            if (dateMinutesInput != null) dateMinutesInput.text = filter.dateThreshold.minutes.ToString();
            if (dateDaysInput != null) dateDaysInput.text = filter.dateThreshold.days.ToString();
            if (dateMonthsInput != null) dateMonthsInput.text = filter.dateThreshold.months.ToString();
            if (dateYearsInput != null) dateYearsInput.text = filter.dateThreshold.years.ToString();
            suppress = false;
            RefreshColorChips();
            RefreshGroupChips();
            RefreshCriterionPanels();
        }

        void RefreshCriterionPanels()
        {
            SelectionFilterCriterion criterion = criterionDropdown != null
                ? (SelectionFilterCriterion)criterionDropdown.value
                : SelectionFilterCriterion.None;

            SetActive(typePanel, criterion == SelectionFilterCriterion.Type);
            SetActive(colorPanel, criterion == SelectionFilterCriterion.Color);
            SetActive(groupPanel, criterion == SelectionFilterCriterion.Group);
            bool showCompare = criterion is SelectionFilterCriterion.Date or SelectionFilterCriterion.Alpha
                or SelectionFilterCriterion.Rotation or SelectionFilterCriterion.Position
                or SelectionFilterCriterion.Scale or SelectionFilterCriterion.Order;
            SetActive(comparePanel, showCompare);

            bool dualAxis = criterion is SelectionFilterCriterion.Position or SelectionFilterCriterion.Scale;
            if (valueInput != null) valueInput.gameObject.SetActive(!dualAxis && criterion != SelectionFilterCriterion.Date);
            if (valueXInput != null) valueXInput.gameObject.SetActive(dualAxis);
            if (valueYInput != null) valueYInput.gameObject.SetActive(dualAxis);
            bool showDate = criterion == SelectionFilterCriterion.Date;
            SetActive(dateHoursInput?.transform.parent?.gameObject, showDate);
            SetActive(dateMinutesInput?.transform.parent?.gameObject, showDate);
            SetActive(dateDaysInput?.transform.parent?.gameObject, showDate);
            SetActive(dateMonthsInput?.transform.parent?.gameObject, showDate);
            SetActive(dateYearsInput?.transform.parent?.gameObject, showDate);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        void AddColor()
        {
            if (colorHexInput == null) return;
            if (!CreateModeSelectionFilter.TryParseHexColor(colorHexInput.text, out Color32 parsed))
            {
                return;
            }

            string hex = ColorUtility.ToHtmlStringRGB(parsed);
            if (!colorList.Contains(hex)) colorList.Add(hex);
            RefreshColorChips();
            AutoApplyFilter();
        }

        void AddGroup()
        {
            if (groupInput == null || !TileGroups.TryParseNumber(groupInput.text, out int group)) return;
            if (!groupList.Contains(group)) groupList.Add(group);
            RefreshGroupChips();
            AutoApplyFilter();
        }

        void RefreshColorChips()
        {
            if (colorGridRoot == null) return;
            while (colorChipButtons.Count < colorList.Count)
                colorChipButtons.Add(CreateChip(colorGridRoot, RemoveColorAt));
            for (int i = 0; i < colorChipButtons.Count; i++)
            {
                bool active = i < colorList.Count;
                colorChipButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                int captured = i;
                string hex = colorList[i];
                Image img = colorChipButtons[i].targetGraphic as Image;
                if (img != null && CreateModeSelectionFilter.TryParseHexColor(hex, out Color32 c))
                    img.color = c;
                colorChipButtons[i].onClick.RemoveAllListeners();
                colorChipButtons[i].onClick.AddListener(() => RemoveColorAt(captured));
            }
        }

        void RefreshGroupChips()
        {
            if (groupGridRoot == null) return;
            while (groupChipButtons.Count < groupList.Count)
                groupChipButtons.Add(CreateChip(groupGridRoot, RemoveGroupAt));
            for (int i = 0; i < groupChipButtons.Count; i++)
            {
                bool active = i < groupList.Count;
                groupChipButtons[i].gameObject.SetActive(active);
                if (!active) continue;
                int captured = i;
                TextMeshProUGUI label = groupChipButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = groupList[i].ToString();
                groupChipButtons[i].onClick.RemoveAllListeners();
                groupChipButtons[i].onClick.AddListener(() => RemoveGroupAt(captured));
            }
        }

        Button CreateChip(RectTransform parent, Action<int> _)
        {
            GameObject go = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(56f, 36f);
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            RectTransform lr = labelGo.GetComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center; label.fontSize = 16f; label.color = Color.white;
            return go.GetComponent<Button>();
        }

        void RemoveColorAt(int index)
        {
            if (index < 0 || index >= colorList.Count) return;
            colorList.RemoveAt(index);
            RefreshColorChips();
            AutoApplyFilter();
        }

        void RemoveGroupAt(int index)
        {
            if (index < 0 || index >= groupList.Count) return;
            groupList.RemoveAt(index);
            RefreshGroupChips();
            AutoApplyFilter();
        }

        CreateModeSelectionFilter BuildFilterFromUi()
        {
            var filter = new CreateModeSelectionFilter
            {
                criterion = criterionDropdown != null ? (SelectionFilterCriterion)criterionDropdown.value : SelectionFilterCriterion.None,
                allowBlock = typeBlockToggle == null || typeBlockToggle.isOn,
                allowSpike = typeSpikeToggle == null || typeSpikeToggle.isOn,
                allowTrigger = typeTriggerToggle == null || typeTriggerToggle.isOn,
                allowParticle = typeParticleToggle == null || typeParticleToggle.isOn,
                allowKeyframe = typeKeyframeToggle == null || typeKeyframeToggle.isOn,
                allowExecutor = typeExecutorToggle == null || typeExecutorToggle.isOn,
                allowedColorsHex = new List<string>(colorList),
                allowedGroups = new List<int>(groupList)
            };

            if (filter.CountEnabledTypes() == 0)
                filter.allowBlock = true;

            SelectionCompareOp op = compareDropdown != null ? (SelectionCompareOp)compareDropdown.value : SelectionCompareOp.Equal;
            if (filter.criterion == SelectionFilterCriterion.Date)
            {
                filter.dateOp = op;
                filter.dateThreshold = new SelectionFilterDate
                {
                    hours = ParseInt(dateHoursInput),
                    minutes = ParseInt(dateMinutesInput),
                    days = ParseInt(dateDaysInput),
                    months = ParseInt(dateMonthsInput),
                    years = ParseInt(dateYearsInput)
                };
            }
            else
            {
                filter.numericOp = op;
                filter.numericValue = ParseFloat(valueInput);
                filter.numericValueX = ParseFloat(valueXInput);
                filter.numericValueY = ParseFloat(valueYInput);
            }

            return filter;
        }

        static int ParseInt(TMP_InputField field) => field != null && int.TryParse(field.text, out int v) ? v : 0;
        static float ParseFloat(TMP_InputField field) =>
            field != null && KaplaoNumericInputUtility.TryParseFloat(field.text, out float v) ? v : 0f;

        void ApplyFilter()
        {
            if (suppress)
                return;

            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null) return;
            CreateModeSelectionFilter filter = BuildFilterFromUi();
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdSetSelectionFilter(SelectionFilterPayload.Serialize(filter));
            RefreshResetButton();
        }

        void AutoApplyFilter() => ApplyFilter();

        void OnCriterionChanged(int _)
        {
            RefreshCriterionPanels();
            AutoApplyFilter();
        }

        void WireFilterToggles(bool bind)
        {
            WireToggle(typeBlockToggle, bind);
            WireToggle(typeSpikeToggle, bind);
            WireToggle(typeTriggerToggle, bind);
            WireToggle(typeParticleToggle, bind);
            WireToggle(typeKeyframeToggle, bind);
            WireToggle(typeExecutorToggle, bind);
        }

        void WireToggle(Toggle toggle, bool bind)
        {
            if (toggle == null)
                return;
            toggle.onValueChanged.RemoveListener(OnFilterToggleChanged);
            if (bind)
                toggle.onValueChanged.AddListener(OnFilterToggleChanged);
        }

        void OnFilterToggleChanged(bool _) => AutoApplyFilter();

        void WireFilterInputs(bool bind)
        {
            WireInput(valueInput, bind);
            WireInput(valueXInput, bind);
            WireInput(valueYInput, bind);
            WireInput(dateHoursInput, bind);
            WireInput(dateMinutesInput, bind);
            WireInput(dateDaysInput, bind);
            WireInput(dateMonthsInput, bind);
            WireInput(dateYearsInput, bind);
            if (compareDropdown != null)
            {
                compareDropdown.onValueChanged.RemoveListener(OnFilterDropdownChanged);
                if (bind)
                    compareDropdown.onValueChanged.AddListener(OnFilterDropdownChanged);
            }
        }

        void WireInput(TMP_InputField field, bool bind)
        {
            if (field == null)
                return;
            field.onEndEdit.RemoveListener(OnFilterTextChanged);
            if (bind)
                field.onEndEdit.AddListener(OnFilterTextChanged);
        }

        void OnFilterTextChanged(string _) => AutoApplyFilter();
        void OnFilterDropdownChanged(int _) => AutoApplyFilter();

        void ResetFilter()
        {
            KaplaoCursorClient client = KaplaoLocalBindingUtility.ResolveLocalCursorClient() ?? cursorClient;
            if (client == null) return;
            KaplaoCursorClient.EnsureClientReadyForCommands();
            client.CmdSetSelectionFilter(string.Empty);
            LoadFromServer();
            RefreshResetButton();
        }

        void RefreshResetButton()
        {
            if (resetButton == null) return;
            bool active = cursorServer != null && cursorServer.HasActiveSelectionFilter;
            resetButton.gameObject.SetActive(active);
        }
    }
}
