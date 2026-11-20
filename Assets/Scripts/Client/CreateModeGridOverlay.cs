using System.Collections.Generic;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Client
{
    [DisallowMultipleComponent]
    public class CreateModeGridOverlay : MonoBehaviour
    {
        [SerializeField] Color gridColor = new Color(0.55f, 0.55f, 0.55f, 0.35f);
        [SerializeField] float gridLineOffset = 0.5f;
        [SerializeField] float lineWidth = 0.02f;
        [SerializeField] int sortingOrder = -100;
        [SerializeField] float z = 0f;

        KaplaoCursorServer cursorServer;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Mesh gridMesh;
        readonly List<Vector3> vertices = new();
        readonly List<Color> colors = new();
        readonly List<int> triangles = new();
        Camera cachedCamera;
        Vector3 lastCameraPosition;
        float lastOrthographicSize;
        float lastAspect;

        void Awake()
        {
            EnsureGridRenderer();
        }

        void LateUpdate()
        {
            if (meshRenderer == null)
                EnsureGridRenderer();

            RefreshCursorBinding();

            if (cursorServer == null || !KaplaoLocalGameModePredictor.IsCreate(cursorServer))
            {
                if (meshRenderer != null && meshRenderer.enabled)
                    meshRenderer.enabled = false;
                return;
            }

            if (!CreateModeEditorSettings.ShowGrid)
            {
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
                return;
            }

            Camera camera = Camera.main;
            if (camera == null || !camera.isActiveAndEnabled)
            {
                if (meshRenderer != null)
                    meshRenderer.enabled = false;
                return;
            }

            meshRenderer.enabled = true;

            bool cameraChanged = camera != cachedCamera ||
                camera.transform.position != lastCameraPosition ||
                !Mathf.Approximately(camera.orthographicSize, lastOrthographicSize) ||
                !Mathf.Approximately(camera.aspect, lastAspect);

            if (cameraChanged || gridMesh.vertexCount == 0)
            {
                cachedCamera = camera;
                lastCameraPosition = camera.transform.position;
                lastOrthographicSize = camera.orthographicSize;
                lastAspect = camera.aspect;
                RebuildGridMesh(camera);
            }
        }

        void RefreshCursorBinding()
        {
            NetworkIdentity local = NetworkClient.localPlayer;
            if (local == null)
            {
                cursorServer = null;
                return;
            }

            if (!local.TryGetComponent(out NetworkPlayer player) || player.role != PlayerRole.Kaplao)
            {
                cursorServer = null;
                return;
            }

            KaplaoCursorServer next = local.GetComponent<KaplaoCursorServer>();
            if (next != cursorServer)
                cursorServer = next;
        }

        void OnEnable()
        {
            cachedCamera = null;
            CreateModeEditorSettings.Changed += OnEditorSettingsChanged;
        }

        void OnDisable()
        {
            CreateModeEditorSettings.Changed -= OnEditorSettingsChanged;
        }

        void OnEditorSettingsChanged()
        {
            cachedCamera = null;
            if (meshRenderer != null && !CreateModeEditorSettings.ShowGrid)
                meshRenderer.enabled = false;
        }

        void EnsureGridRenderer()
        {
            if (meshFilter != null)
                return;

            GameObject gridObject = new GameObject("CreateModeGridMesh");
            gridObject.transform.SetParent(null, false);

            meshFilter = gridObject.AddComponent<MeshFilter>();
            meshRenderer = gridObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

            if (shader != null)
            {
                Material material = new Material(shader);
                meshRenderer.sharedMaterial = material;
            }

            meshRenderer.sortingOrder = sortingOrder;
            meshRenderer.enabled = false;

            gridMesh = new Mesh { name = "CreateModeGridLines" };
            meshFilter.sharedMesh = gridMesh;
        }

        void RebuildGridMesh(Camera camera)
        {
            if (gridMesh == null)
                return;

            Vector3 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, -camera.transform.position.z));
            Vector3 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, -camera.transform.position.z));
            int minX = Mathf.FloorToInt(min.x);
            int maxX = Mathf.CeilToInt(max.x);
            int minY = Mathf.FloorToInt(min.y);
            int maxY = Mathf.CeilToInt(max.y);

            vertices.Clear();
            colors.Clear();
            triangles.Clear();

            for (int x = minX; x <= maxX; x++)
            {
                float lineX = x + gridLineOffset;
                AddLineQuad(
                    new Vector3(lineX, minY + gridLineOffset, z),
                    new Vector3(lineX, maxY + gridLineOffset, z));
            }

            for (int y = minY; y <= maxY; y++)
            {
                float lineY = y + gridLineOffset;
                AddLineQuad(
                    new Vector3(minX + gridLineOffset, lineY, z),
                    new Vector3(maxX + gridLineOffset, lineY, z));
            }

            gridMesh.Clear();
            gridMesh.SetVertices(vertices);
            gridMesh.SetColors(colors);
            gridMesh.SetTriangles(triangles, 0, true);
            gridMesh.RecalculateBounds();
        }

        void AddLineQuad(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude <= Mathf.Epsilon)
                return;

            Vector3 tangent = delta.normalized;
            Vector3 perpendicular = new Vector3(-tangent.y, tangent.x, 0f) * (lineWidth * 0.5f);

            int baseIndex = vertices.Count;
            vertices.Add(start - perpendicular);
            vertices.Add(start + perpendicular);
            vertices.Add(end + perpendicular);
            vertices.Add(end - perpendicular);

            for (int i = 0; i < 4; i++)
                colors.Add(gridColor);

            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
        }

        void OnDestroy()
        {
            if (gridMesh != null)
                Destroy(gridMesh);

            if (meshFilter != null)
                Destroy(meshFilter.gameObject);
        }
    }
}
