using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Put on Mode / Panel (or any folder) buttons. Uses shared ON/OFF colors
    /// from KaplaoToggleButtonStyle. If children are toggled elsewhere, leave
    /// Targets empty and fill On When Any Active instead.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Folder Toggle Button")]
    public class KaplaoFolderToggleButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [Tooltip("Objects shown while this folder button is ON. Click toggles them.")]
        [SerializeField] GameObject[] targets;
        [Tooltip("If set, ON color follows whether any of these is active (does not steal OnClick).")]
        [SerializeField] GameObject[] onWhenAnyActive;
        [SerializeField] bool startOn;

        bool on;

        void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
            on = startOn;
            if (onWhenAnyActive == null || onWhenAnyActive.Length == 0)
                ApplyFolderState();
        }

        void OnEnable()
        {
            if (button != null && (onWhenAnyActive == null || onWhenAnyActive.Length == 0))
                button.onClick.AddListener(Toggle);
        }

        void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Toggle);
        }

        void LateUpdate()
        {
            if (onWhenAnyActive == null || onWhenAnyActive.Length == 0)
                return;

            bool any = false;
            for (int i = 0; i < onWhenAnyActive.Length; i++)
            {
                if (onWhenAnyActive[i] != null && onWhenAnyActive[i].activeSelf)
                {
                    any = true;
                    break;
                }
            }

            KaplaoToggleButtonStyle.Apply(button, any);
        }

        void Toggle()
        {
            on = !on;
            ApplyFolderState();
        }

        void ApplyFolderState()
        {
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                        targets[i].SetActive(on);
                }
            }

            KaplaoToggleButtonStyle.Apply(button, on);
        }
    }
}
