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
        [SerializeField] Color offColor = new Color(0x1E / 255f, 0x29 / 255f, 0x3B / 255f, 0.94f);
        [SerializeField] Color onColor = new Color(0x0E / 255f, 0xA5 / 255f, 0xE9 / 255f, 1f);

        [Header("Button text (optional)")]
        [SerializeField] Color offTextColor = new Color(0xCB / 255f, 0xD5 / 255f, 0xE1 / 255f, 1f);
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
                : (on ? new Color(0x0E / 255f, 0xA5 / 255f, 0xE9 / 255f, 1f) : new Color(0x1E / 255f, 0x29 / 255f, 0x3B / 255f, 0.94f));
            Color textColor = style != null
                ? (on ? style.onTextColor : style.offTextColor)
                : (on ? Color.white : new Color(0xCB / 255f, 0xD5 / 255f, 0xE1 / 255f, 1f));

            Graphic graphic = button.targetGraphic;
            if (graphic == null)
                graphic = button.GetComponent<Image>();

            if (graphic != null)
            {
                if (button.gameObject.activeInHierarchy && Application.isPlaying)
                {
                    LeanTween.cancel(graphic.gameObject);
                    Color startBg = graphic.color;
                    LeanTween.value(graphic.gameObject, startBg, background, 0.15f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad)
                        .setOnUpdate((Color c) =>
                        {
                            if (graphic != null)
                                graphic.color = c;
                        });
                }
                else
                {
                    graphic.color = background;
                }
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                if (button.gameObject.activeInHierarchy && Application.isPlaying)
                {
                    Color startText = label.color;
                    LeanTween.value(label.gameObject, startText, textColor, 0.15f)
                        .setIgnoreTimeScale(true)
                        .setEase(LeanTweenType.easeOutQuad)
                        .setOnUpdate((Color c) =>
                        {
                            if (label != null)
                                label.color = c;
                        });
                }
                else
                {
                    label.color = textColor;
                }
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            cached = this;
        }
#endif
    }
}
