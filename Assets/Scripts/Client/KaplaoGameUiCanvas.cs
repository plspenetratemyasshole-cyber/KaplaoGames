using UnityEngine;

namespace KaplaoGames.Client
{
  /// <summary>
  /// Keeps Screen Space - Camera UI above tiles but below Kaplao cursors and selection overlays.
  /// Tiles/nameplates: up to ~100. UI: 150. Cursors/marquee: 198+.
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(Canvas))]
  public class KaplaoGameUiCanvas : MonoBehaviour
  {
    const int RecommendedSortingOrder = 150;
    const int MaxBelowWorldCursors = 197;

    [SerializeField] int sortingOrder = RecommendedSortingOrder;

    void Awake()
    {
      ApplySorting();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
      if (sortingOrder > MaxBelowWorldCursors)
        sortingOrder = RecommendedSortingOrder;

      sortingOrder = Mathf.Clamp(sortingOrder, 0, MaxBelowWorldCursors);
      if (!Application.isPlaying)
        ApplySorting();
    }
#endif

    void ApplySorting()
    {
      Canvas canvas = GetComponent<Canvas>();
      if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        return;

      if (sortingOrder > MaxBelowWorldCursors)
        sortingOrder = RecommendedSortingOrder;

      canvas.overrideSorting = true;
      canvas.sortingOrder = sortingOrder;
    }
  }
}
