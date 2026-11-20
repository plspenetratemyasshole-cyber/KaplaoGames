using KaplaoGames.Shared;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Assign localizationKey on any TMP label — text updates when language changes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string localizationKey;

        TextMeshProUGUI label;

        void Awake()
        {
            label = GetComponent<TextMeshProUGUI>();
        }

        void OnEnable()
        {
            LanguagePreference.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            LanguagePreference.Changed -= Refresh;
        }

        public void SetKey(string key)
        {
            localizationKey = key;
            Refresh();
        }

        void Refresh()
        {
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();

            if (label == null || string.IsNullOrWhiteSpace(localizationKey))
                return;

            label.text = GameLocalization.Get(localizationKey);
        }
    }
}
