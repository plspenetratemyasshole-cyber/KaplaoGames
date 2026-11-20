using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local runner client draws their own sprite above other runners to avoid overlap flicker.
    /// Remote runners and host view keep default sorting.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerLocalSortingClient : NetworkBehaviour
    {
        const int DefaultSortingOrder = 0;
        const int LocalPlayerSortingOrder = 20;

        [SerializeField] Transform visualRoot;
        [SerializeField] int localPlayerSortingOrder = LocalPlayerSortingOrder;
        [SerializeField] int remoteSortingOrder = DefaultSortingOrder;

        SpriteRenderer spriteRenderer;
        TrailRenderer trailRenderer;

        void Awake()
        {
            ResolveVisualReferences();
        }

        public override void OnStartClient()
        {
            ApplySorting(isLocalPlayer ? localPlayerSortingOrder : remoteSortingOrder);
        }

        public override void OnStartLocalPlayer()
        {
            ApplySorting(localPlayerSortingOrder);
        }

        void ResolveVisualReferences()
        {
            if (visualRoot == null)
            {
                Transform child = transform.Find("Visual");
                if (child != null)
                    visualRoot = child;
            }

            if (visualRoot == null)
                return;

            if (spriteRenderer == null)
                spriteRenderer = visualRoot.GetComponent<SpriteRenderer>();

            if (trailRenderer == null)
                trailRenderer = visualRoot.GetComponent<TrailRenderer>();
        }

        void ApplySorting(int sortingOrder)
        {
            ResolveVisualReferences();

            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = sortingOrder;

            if (trailRenderer != null)
                trailRenderer.sortingOrder = sortingOrder - 1;
        }

#if UNITY_EDITOR
        protected new void OnValidate()
        {
            ResolveVisualReferences();
        }
#endif
    }
}
