using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Shared ON/OFF colors for text toggle buttons (pause Kaplao/Mod/ModerateChat,
    /// create-mode Usual/Create/Code/EditorSettings, selection toolbar, Mode/Panel folders).
    /// Edit via Assets/Resources/KaplaoToggleButtonStyle.
    /// </summary>
    [CreateAssetMenu(fileName = "KaplaoToggleButtonStyle", menuName = "Kaplao/Toggle Button Style")]
    public class KaplaoToggleButtonStyle : ScriptableObject
    {
        public const string ResourcePath = "KaplaoToggleButtonStyle";

        static KaplaoToggleButtonStyle cached;

        [Header("Button background")]
        [SerializeField] Color offColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        [SerializeField] Color onColor = new Color(0.2f, 0.75f, 0.3f, 0.95f);

        [Header("Button text (optional)")]
        [SerializeField] Color offTextColor = Color.white;
        [SerializeField] Color onTextColor = Color.white;

        public Color OffColor => offColor;
        public Color OnColor => onColor;
        public Color OffTextColor => offTextColor;
        public Color OnTextColor => onTextColor;

        public static KaplaoToggleButtonStyle Load()
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<KaplaoToggleButtonStyle>(ResourcePath);
            return cached;
        }

        public static void ClearCache()
        {
            cached = null;
        }

        public static void Apply(Button button, bool on)
        {
            if (button == null)
                return;

            KaplaoToggleButtonStyle style = Load();
            Color background = style != null
                ? (on ? style.onColor : style.offColor)
                : (on ? new Color(0.2f, 0.75f, 0.3f, 0.95f) : new Color(0.15f, 0.15f, 0.15f, 0.9f));
            Color textColor = style != null
                ? (on ? style.onTextColor : style.offTextColor)
                : Color.white;

            Graphic graphic = button.targetGraphic;
            if (graphic == null)
                graphic = button.GetComponent<Image>();
            if (graphic != null)
                graphic.color = background;

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
                label.color = textColor;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            cached = this;
        }
#endif
    }
}
