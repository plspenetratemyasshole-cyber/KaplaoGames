using KaplaoGames.Client;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Full-screen transparent click catcher behind Create-mode modal panels.
    /// Click outside a panel to close it.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreateModePanelDismissOverlay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] KaplaoCreateModeUi createModeUi;
        Image image;

        void Awake()
        {
            if (createModeUi == null)
                createModeUi = GetComponentInParent<KaplaoCreateModeUi>() ??
                               FindFirstObjectByType<KaplaoCreateModeUi>(FindObjectsInactive.Include);

            image = GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.01f);
                image.raycastTarget = true;
            }

            gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                transform.SetAsFirstSibling();
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (createModeUi == null)
                return;

            createModeUi.DismissOpenPanelsFromOverlay();
        }
    }
}
