using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.UI
{
    /// <summary>
    /// Flies each interactive UI element (buttons, inputs, labels) from below into place.
    /// Intended for Main Menu style scenes.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Fly In Children")]
    public class UiFlyInChildren : MonoBehaviour
    {
        [SerializeField] RectTransform searchRoot;
        [SerializeField] float flyOffsetY;
        [SerializeField] float duration = 0.55f;
        [SerializeField] float stagger = 0.04f;
        [SerializeField] LeanTweenType ease = LeanTweenType.easeOutCubic;
        [SerializeField] bool includeTexts = true;
        [SerializeField] bool onlyAnimateActiveHierarchy = true;
        [SerializeField] string[] excludeNameContains = { "Template", "Placeholder" };

        readonly List<RectTransform> animatedTargets = new();

        void Start()
        {
            StartCoroutine(PlayAfterLayout());
        }

        IEnumerator PlayAfterLayout()
        {
            yield return null;
            yield return WaitForStableLayout();
            Play();
        }

        IEnumerator WaitForStableLayout()
        {
            const int requiredStableFrames = 3;
            const int maxFrames = 90;

            Vector2 lastSize = Vector2.zero;
            int stableFrames = 0;
            int frames = 0;

            while (stableFrames < requiredStableFrames && frames < maxFrames)
            {
                if (Application.isMobilePlatform)
                {
                    while (Screen.width < Screen.height && frames < maxFrames)
                    {
                        frames++;
                        yield return null;
                    }
                }

                ForceLayoutRefresh();

                Vector2 size = GetLayoutReferenceSize();
                if (size.x > 1f && size.y > 1f && size == lastSize)
                    stableFrames++;
                else
                    stableFrames = 0;

                lastSize = size;
                frames++;
                yield return null;
            }
        }

        void ForceLayoutRefresh()
        {
            RectTransform root = searchRoot != null ? searchRoot : transform as RectTransform;
            if (root != null)
                RobloxStyleRect.ApplyAllUnder(root);

            Canvas.ForceUpdateCanvases();
        }

        Vector2 GetLayoutReferenceSize()
        {
            RectTransform root = searchRoot != null ? searchRoot : transform as RectTransform;
            return root != null ? root.rect.size : Vector2.zero;
        }

        public void Play()
        {
            ForceLayoutRefresh();

            RectTransform root = searchRoot != null ? searchRoot : transform as RectTransform;
            if (root == null)
                return;

            List<RectTransform> targets = CollectTargets(root);
            SortBottomToTop(targets);
            animatedTargets.Clear();
            animatedTargets.AddRange(targets);

            for (int i = 0; i < targets.Count; i++)
            {
                float delay = i * stagger;
                UiFlyInUtility.Play(targets[i], flyOffsetY, duration, delay, ease);
            }
        }

        public void SnapAllToRest()
        {
            ForceLayoutRefresh();

            for (int i = 0; i < animatedTargets.Count; i++)
                UiFlyInUtility.SnapToRest(animatedTargets[i]);
        }

        public void RefreshRestFromCurrentPositions()
        {
            ForceLayoutRefresh();

            for (int i = 0; i < animatedTargets.Count; i++)
                UiFlyInUtility.SyncRestFromCurrentPosition(animatedTargets[i]);
        }

        List<RectTransform> CollectTargets(RectTransform root)
        {
            var results = new List<RectTransform>();
            var seen = new HashSet<RectTransform>();

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || ShouldExclude(buttons[i].gameObject))
                    continue;

                TryAddTarget(seen, results, buttons[i].transform as RectTransform);
            }

            TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] == null || ShouldExclude(inputs[i].gameObject))
                    continue;

                TryAddTarget(seen, results, inputs[i].transform as RectTransform);
            }

            if (includeTexts)
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] == null || ShouldExclude(texts[i].gameObject))
                        continue;

                    if (texts[i].GetComponentInParent<Button>() != null)
                        continue;

                    TryAddTarget(seen, results, texts[i].transform as RectTransform);
                }
            }

            return results;
        }

        void TryAddTarget(HashSet<RectTransform> seen, List<RectTransform> results, RectTransform rect)
        {
            if (rect == null || seen.Contains(rect))
                return;

            if (onlyAnimateActiveHierarchy && !rect.gameObject.activeInHierarchy)
                return;

            seen.Add(rect);
            results.Add(rect);
        }

        bool ShouldExclude(GameObject target)
        {
            if (target == null)
                return true;

            string name = target.name;
            for (int i = 0; i < excludeNameContains.Length; i++)
            {
                if (string.IsNullOrEmpty(excludeNameContains[i]))
                    continue;

                if (name.IndexOf(excludeNameContains[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static void SortBottomToTop(List<RectTransform> targets)
        {
            targets.Sort((a, b) =>
            {
                float ay = GetWorldY(a);
                float by = GetWorldY(b);
                return ay.CompareTo(by);
            });
        }

        static float GetWorldY(RectTransform rect)
        {
            return rect != null ? rect.position.y : 0f;
        }
    }
}
