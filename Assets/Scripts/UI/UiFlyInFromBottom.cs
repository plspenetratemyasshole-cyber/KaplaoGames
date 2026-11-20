using System.Collections;
using UnityEngine;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Flies this RectTransform up from below the screen into its current layout position.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Kaplao/UI/Fly In From Bottom")]
    public class UiFlyInFromBottom : MonoBehaviour
    {
        [SerializeField] float flyOffsetY;
        [SerializeField] float duration = 0.55f;
        [SerializeField] float delay;
        [SerializeField] LeanTweenType ease = LeanTweenType.easeOutCubic;
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool waitOneFrameForLayout = true;

        RectTransform rectTransform;
        bool hasPlayed;

        void Awake()
        {
            rectTransform = transform as RectTransform;
        }

        void Start()
        {
            if (!playOnStart)
                return;

            if (waitOneFrameForLayout)
                StartCoroutine(PlayAfterLayout());
            else
                Play();
        }

        IEnumerator PlayAfterLayout()
        {
            yield return null;
            Play();
        }

        public void Play()
        {
            if (rectTransform == null || hasPlayed)
                return;

            hasPlayed = true;
            UiFlyInUtility.Play(rectTransform, flyOffsetY, duration, delay, ease);
        }

        void OnDisable()
        {
            if (rectTransform != null)
                LeanTween.cancel(rectTransform.gameObject);
        }
    }
}
