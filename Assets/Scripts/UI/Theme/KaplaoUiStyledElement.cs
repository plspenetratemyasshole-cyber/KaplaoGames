using UnityEngine;

namespace KaplaoGames.UI.Theme
{
    public enum StyledElementType
    {
        Panel,
        Card,
        Button,
        InputField,
        Dropdown,
        Slider,
        ToggleSwitch,
        Backdrop
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Styled Element")]
    public class KaplaoUiStyledElement : MonoBehaviour
    {
        public StyledElementType ElementType = StyledElementType.Panel;
        public KaplaoButtonVariant ButtonVariant = KaplaoButtonVariant.Secondary;
        public int Radius = 14;
        public bool WithShadow = true;
        public bool WithHeaderGlow = true;
    }
}
