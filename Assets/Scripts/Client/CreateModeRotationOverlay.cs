using System.Collections;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class CreateModeRotationOverlay : MonoBehaviour
    {
        [SerializeField] Color lineColor = new Color(1f, 0.85f, 0.2f, 0.9f);

        KaplaoCursorServer cursorServer;
        KaplaoCursorClient cursorClient;
        LineRenderer line;

        void OnEnable()
        {
            StartCoroutine(WatchLocalKaplao());
        }

        void OnDisable()
        {
            ClearKaplaoBinding();
            if (line != null)
                line.enabled = false;
        }

        IEnumerator WatchLocalKaplao()
        {
            while (enabled)
            {
                if (!KaplaoLocalBindingUtility.TryGetLocalKaplao(
                        out _,
                        out _,
                        out KaplaoCursorClient nextClient,
                        out KaplaoCursorServer nextServer))
                {
                    ClearKaplaoBinding();
                    if (line != null)
                        line.enabled = false;
                    yield return null;
                    continue;
                }

                if (cursorClient != nextClient || cursorServer != nextServer)
                {
                    cursorClient = nextClient;
                    cursorServer = nextServer;
                    EnsureLineVisual();
                }

                yield return null;
            }
        }

        void ClearKaplaoBinding()
        {
            cursorServer = null;
            cursorClient = null;
        }

        void EnsureLineVisual()
        {
            if (line != null)
                return;

            GameObject lineObj = new GameObject("CreateRotationLine");
            lineObj.transform.SetParent(transform, false);
            line = lineObj.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.sortingOrder = 300;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        void Update()
        {
            if (line == null || cursorServer == null || cursorClient == null)
                return;

            bool show = KaplaoLocalGameModePredictor.IsCreate(cursorServer) &&
                        cursorServer.SelectedCount > 0 &&
                        (KaplaoLocalTransformSession.IsActive ||
                         cursorServer.ActiveCreateTransform != CreateTransformMode.None);
            if (!show)
            {
                line.enabled = false;
                return;
            }

            Vector2 center;
            // Follow predicted tile positions (especially G-move). Fixed begin-pivot stays put and looks "stuck".
            if (!TryGetSelectionCenter(out center))
            {
                if (KaplaoLocalTransformSession.IsActive)
                    center = KaplaoLocalTransformSession.Pivot;
                else
                {
                    line.enabled = false;
                    return;
                }
            }

            line.enabled = true;
            Vector2 cursor = cursorClient.LocalWorldPosition;
            line.SetPosition(0, new Vector3(center.x, center.y, 0f));
            line.SetPosition(1, new Vector3(cursor.x, cursor.y, 0f));
        }

        bool TryGetSelectionCenter(out Vector2 center)
        {
            center = Vector2.zero;
            if (cursorServer == null || cursorServer.SelectedTiles.Count == 0)
                return false;

            int count = 0;
            for (int i = 0; i < cursorServer.SelectedTiles.Count; i++)
            {
                uint id = cursorServer.SelectedTiles[i];
                if (NetworkClient.spawned.TryGetValue(id, out NetworkIdentity identity) &&
                    identity != null &&
                    identity.TryGetComponent(out LevelTileServer tile))
                {
                    center += KaplaoLocalEditPredictor.GetEffectivePosition(tile);
                    count++;
                }
            }

            if (count == 0)
                return false;

            center /= count;
            return true;
        }
    }
}
