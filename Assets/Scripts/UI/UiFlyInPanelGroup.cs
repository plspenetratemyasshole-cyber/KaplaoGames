using System.Collections;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Flies whole UI panels from below into place (one tween per panel).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Fly In Panel Group")]
    public class UiFlyInPanelGroup : MonoBehaviour
    {
        [SerializeField] RectTransform[] panels;
        [SerializeField] float flyOffsetY;
        [SerializeField] float duration = 0.6f;
        [SerializeField] float stagger = 0.08f;
        [SerializeField] LeanTweenType ease = LeanTweenType.easeOutCubic;

        void Start()
        {
            StartCoroutine(PlayAfterLayout());
        }

        IEnumerator PlayAfterLayout()
        {
            yield return null;
            Play();
        }

        public void Play()
        {
            if (panels == null)
                return;

            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null)
                    continue;

                UiFlyInUtility.Play(panels[i], flyOffsetY, duration, i * stagger, ease);
            }
        }

        public void SetPanels(RectTransform[] newPanels) => panels = newPanels;
    }
}
