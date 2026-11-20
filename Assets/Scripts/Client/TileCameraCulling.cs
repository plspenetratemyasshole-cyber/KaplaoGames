using System.Threading.Tasks;
using KaplaoGames.Server;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Culls tile SpriteRenderers outside the camera view.
    /// Bounds tests run on worker threads; enabling renderers stays on the main thread.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    [AddComponentMenu("Kaplao/Client/Tile Camera Culling")]
    public class TileCameraCulling : MonoBehaviour
    {
        const float MarginWorld = 3f;
        const int MaxTilesPerApply = 400;

        static TileCameraCulling instance;

        Camera viewCamera;
        readonly System.Collections.Generic.List<LevelTileServer> tiles = new(2048);
        Vector2[] positions;
        bool[] shouldCull;
        int applyIndex;
        float nextRebuildTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null)
                return;

            var go = new GameObject("TileCameraCulling");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TileCameraCulling>();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        void LateUpdate()
        {
            if (!NetworkClient.active)
                return;

            if (viewCamera == null)
                viewCamera = Camera.main;
            if (viewCamera == null || !viewCamera.orthographic)
                return;

            if (Time.unscaledTime >= nextRebuildTime || tiles.Count == 0)
            {
                RebuildTileList();
                nextRebuildTime = Time.unscaledTime + 0.5f;
            }

            if (tiles.Count == 0)
                return;

            EnsureBuffers(tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
            {
                LevelTileServer tile = tiles[i];
                positions[i] = tile != null ? (Vector2)tile.transform.position : Vector2.zero;
            }

            float ortho = viewCamera.orthographicSize;
            float aspect = viewCamera.aspect;
            Vector2 camPos = viewCamera.transform.position;
            float halfH = ortho + MarginWorld;
            float halfW = ortho * aspect + MarginWorld;
            float minX = camPos.x - halfW;
            float maxX = camPos.x + halfW;
            float minY = camPos.y - halfH;
            float maxY = camPos.y + halfH;

            int count = tiles.Count;
            Parallel.For(0, count, i =>
            {
                Vector2 p = positions[i];
                shouldCull[i] = p.x < minX || p.x > maxX || p.y < minY || p.y > maxY;
            });

            int applied = 0;
            while (applied < MaxTilesPerApply && applyIndex < count)
            {
                LevelTileServer tile = tiles[applyIndex];
                if (tile != null)
                    tile.SetCameraCulled(shouldCull[applyIndex]);
                applyIndex++;
                applied++;
            }

            if (applyIndex >= count)
                applyIndex = 0;
        }

        void RebuildTileList()
        {
            tiles.Clear();
            if (LevelGridServer.Instance != null)
            {
                LevelGridServer.Instance.ForEachTile(tile =>
                {
                    if (tile != null)
                        tiles.Add(tile);
                });
                return;
            }

            LevelTileServer[] found = FindObjectsByType<LevelTileServer>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                    tiles.Add(found[i]);
            }
        }

        void EnsureBuffers(int count)
        {
            if (positions == null || positions.Length < count)
            {
                positions = new Vector2[Mathf.NextPowerOfTwo(count)];
                shouldCull = new bool[positions.Length];
                applyIndex = 0;
            }
        }
    }
}
