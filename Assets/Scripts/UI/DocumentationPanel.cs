using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KaplaoGames.Shared;

public class DocumentationPanel : MonoBehaviour
{
    [System.Serializable]
    public struct DocumentationItem
    {
        public Button triggerButton;
        public string textEnglish;
        [TextArea(3, 5)]
        public string textRussian;
    }

    [SerializeField] private GameObject panelObject;
    [SerializeField] private TextMeshProUGUI labelText;
    [SerializeField] private Button okButton;
    [SerializeField] private DocumentationItem[] documentations;

    // current language and currently shown doc texts
    private bool isRussian = false;
    private string currentEnglish = string.Empty;
    private string currentRussian = string.Empty;

    // used to avoid closing+reopening when a trigger button is clicked
    public bool IgnoreNextOutsideClick { get; private set; }

    public void ClearIgnoreNextOutsideClick() => IgnoreNextOutsideClick = false;

    private void Start()
    {
        ValidateReferences();
        // set initial language from preference
        isRussian = LanguagePreference.SelectedLanguage == GameLanguage.Russian;
        SetupButtonListeners();
    }

    void OnEnable()
    {
        LanguagePreference.Changed += OnLanguageChanged;
    }

    void OnDisable()
    {
        LanguagePreference.Changed -= OnLanguageChanged;
    }

    private void ValidateReferences()
    {
        if (panelObject == null || labelText == null || okButton == null)
        {
            Debug.LogError("DocumentationPanel: Panel, Label или Button не установлены в инспекторе!");
            return;
        }

        if (documentations == null || documentations.Length == 0)
        {
            Debug.LogWarning("DocumentationPanel: Документации не добавлены!");
            return;
        }
    }

    private void SetupButtonListeners()
    {
okButton.onClick.RemoveAllListeners();
okButton.onClick.AddListener(ClosePanel);

foreach (var doc in documentations)
{
    if (doc.triggerButton != null)
    {
        Button button = doc.triggerButton;
        string engText = doc.textEnglish;
        string rusText = doc.textRussian;

        // remove existing listeners to avoid double-wiring
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => TogglePanelForDoc(engText, rusText));
    }
    else
    {
        Debug.LogWarning("DocumentationPanel: Кнопка для одной из документаций не установлена!");
    }
}
    }

    private void TogglePanelForDoc(string englishText, string russianText)
    {
        // Avoid an outside-click close from racing with button click: ignore the next outside-click handler
        IgnoreNextOutsideClick = true;

        // If the same doc is already shown, close the panel, otherwise open this doc
        bool sameDocShowing = panelObject.activeSelf && currentEnglish == englishText && currentRussian == russianText;
        if (sameDocShowing)
        {
            ClosePanel();
            return;
        }

        OpenPanel(englishText, russianText);
    }

    public void OpenPanel(string englishText, string russianText)
    {
        currentEnglish = englishText ?? string.Empty;
        currentRussian = russianText ?? string.Empty;

        string textToDisplay = isRussian ? currentRussian : currentEnglish;
        labelText.text = textToDisplay;
        panelObject.SetActive(true);
    }

    public void ClosePanel()
    {
        panelObject.SetActive(false);
        currentEnglish = string.Empty;
        currentRussian = string.Empty;
    }

    public void SetLanguage(bool russian)
    {
        isRussian = russian;
        // update visible text if any
        if (panelObject != null && panelObject.activeSelf)
            labelText.text = isRussian ? currentRussian : currentEnglish;
    }

    private void OnLanguageChanged()
    {
        isRussian = LanguagePreference.SelectedLanguage == GameLanguage.Russian;
        if (panelObject != null && panelObject.activeSelf)
            labelText.text = isRussian ? currentRussian : currentEnglish;
    }
}
