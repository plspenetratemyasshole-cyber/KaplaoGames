using System;
using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Horizontally scrolling parallax background. Each layer follows the camera on X
    /// with its own speed (0 = fixed in world, 1 = moves with the camera).
    /// Vertically, layers stay fixed relative to the camera (they move with camera Y).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/Client/Parallax Background")]
    public class ParallaxBackground : MonoBehaviour
    {
        [Serializable]
        public class Layer
        {
            public string name = "Layer";
            public Sprite sprite;
            [Range(0f, 1f)]
            public float followSpeed = 0.5f;
            public int sortingOrder = -100;
            public string sortingLayerName = "Default";
            [Tooltip("Vertical offset from the camera center. 0 = centered on camera.")]
            public float yPosition = 0f;
            public float zPosition = 0f;
            [Min(0.01f)]
            public float scale = 1f;
            public Color color = Color.white;
        }

        [SerializeField] List<Layer> layers = new();
        [SerializeField] Camera targetCamera;
        [SerializeField] bool useMainCameraIfUnset = true;
        [SerializeField] int minimumSegmentCopies = 3;
        [SerializeField] float segmentMargin = 2f;
        [SerializeField] bool rebuildOnEnable = true;
        [SerializeField] bool rebuildInEditMode = true;

        readonly List<LayerRuntime> runtimes = new();
        float referenceCameraX;
        bool hasReferenceCameraX;

        public static ParallaxBackground Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            if (rebuildOnEnable)
                RebuildLayers();
        }

        void OnDisable()
        {
            ClearRuntime();
        }

        void LateUpdate()
        {
            Camera cam = ResolveCamera();
            if (cam == null || runtimes.Count == 0)
                return;

            if (!hasReferenceCameraX)
            {
                referenceCameraX = cam.transform.position.x;
                hasReferenceCameraX = true;
            }

            float cameraX = cam.transform.position.x;
            float cameraY = cam.transform.position.y;
            float halfViewWidth = cam.orthographic
                ? cam.orthographicSize * cam.aspect
                : 10f;

            for (int i = 0; i < runtimes.Count; i++)
                runtimes[i].Update(cameraX, cameraY, referenceCameraX, halfViewWidth, segmentMargin);
        }

        [ContextMenu("Rebuild Layers")]
        public void RebuildLayers()
        {
            ClearRuntime();

            Camera cam = ResolveCamera();
            if (cam != null)
            {
                referenceCameraX = cam.transform.position.x;
                hasReferenceCameraX = true;
            }
            else
            {
                hasReferenceCameraX = false;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                Layer definition = layers[i];
                if (definition.sprite == null)
                    continue;

                runtimes.Add(new LayerRuntime(
                    transform,
                    definition,
                    minimumSegmentCopies,
                    referenceCameraX,
                    cam != null ? cam.transform.position.y : 0f));
            }

            if (runtimes.Count == 0)
            {
                Debug.LogWarning("[ParallaxBackground] No layers built. Assign Sprite on each layer and press Rebuild Layers.");
                return;
            }

            Debug.Log($"[ParallaxBackground] Built {runtimes.Count} layer(s) around camera X={referenceCameraX:0.##}.");
        }

#if UNITY_EDITOR
        bool pendingEditModeRebuild;

        void OnValidate()
        {
            if (!rebuildInEditMode || Application.isPlaying)
                return;

            if (UnityEditor.BuildPipeline.isBuildingPlayer)
                return;

            ScheduleEditModeRebuild();
        }

        void ScheduleEditModeRebuild()
        {
            if (pendingEditModeRebuild)
                return;

            pendingEditModeRebuild = true;
            UnityEditor.EditorApplication.delayCall += DeferredRebuildInEditMode;
        }

        void DeferredRebuildInEditMode()
        {
            UnityEditor.EditorApplication.delayCall -= DeferredRebuildInEditMode;
            pendingEditModeRebuild = false;

            if (this == null || !rebuildInEditMode || Application.isPlaying)
                return;

            RebuildLayers();
        }
#endif

        Camera ResolveCamera()
        {
            if (targetCamera != null)
                return targetCamera;

            return useMainCameraIfUnset ? Camera.main : null;
        }

        public int LayerCount => runtimes.Count;

        public void ClearBackground() => ClearRuntime();

        public void HideLayer(int index)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetVisible(false);
        }

        public void ShowLayer(int index)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetVisible(true);
        }

        public void SetLayerColor(int index, Color color)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetColor(color);
        }

        public void SetLayerSpeed(int index, float speed)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetSpeed(speed);
        }

        public void SetLayerOffset(int index, float yOffset)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetOffset(yOffset);
        }

        public void SetLayerAlpha(int index, float alpha)
        {
            if (index >= 0 && index < runtimes.Count)
                runtimes[index].SetAlpha(alpha);
        }

        public void ResetBackground() => RebuildLayers();

        void ClearRuntime()
        {
            for (int i = 0; i < runtimes.Count; i++)
                runtimes[i].Destroy();

            runtimes.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        sealed class LayerRuntime
        {
            readonly Transform root;
            readonly Transform[] segments;
            readonly float segmentWidth;
            float followSpeed;
            float yOffsetFromCamera;
            readonly float z;
            readonly float worldAnchorX;
            readonly int segmentCount;

            public LayerRuntime(
                Transform parent,
                Layer definition,
                int minimumSegments,
                float worldAnchorX,
                float cameraY)
            {
                followSpeed = definition.followSpeed;
                yOffsetFromCamera = definition.yPosition;
                z = definition.zPosition;
                this.worldAnchorX = worldAnchorX;
                segmentWidth = definition.sprite.bounds.size.x * definition.scale;
                segmentCount = Mathf.Max(minimumSegments, 3);

                string layerName = string.IsNullOrWhiteSpace(definition.name)
                    ? definition.sprite.name
                    : definition.name;

                GameObject rootObject = new GameObject(layerName);
                rootObject.transform.SetParent(parent, false);
                root = rootObject.transform;
                root.position = new Vector3(worldAnchorX, cameraY + yOffsetFromCamera, z);

                Color tint = definition.color;
                if (tint.a <= 0.001f)
                {
                    tint.a = 1f;
                    Debug.LogWarning($"[ParallaxBackground] Layer '{layerName}' color alpha was 0. Using alpha=1 so it is visible.");
                }

                segments = new Transform[segmentCount];
                float totalWidth = segmentCount * segmentWidth;
                float startLocalX = -totalWidth * 0.5f + segmentWidth * 0.5f;

                for (int i = 0; i < segmentCount; i++)
                {
                    GameObject segmentObject = new GameObject($"Segment_{i}");
                    segmentObject.transform.SetParent(root, false);
                    segmentObject.transform.localScale = Vector3.one * definition.scale;
                    segmentObject.transform.localPosition = new Vector3(startLocalX + i * segmentWidth, 0f, 0f);

                    SpriteRenderer renderer = segmentObject.AddComponent<SpriteRenderer>();
                    renderer.sprite = definition.sprite;
                    renderer.sortingOrder = definition.sortingOrder;
                    renderer.color = tint;

                    if (!string.IsNullOrWhiteSpace(definition.sortingLayerName))
                    {
                        SortingLayer[] sortingLayers = SortingLayer.layers;
                        bool found = false;
                        for (int s = 0; s < sortingLayers.Length; s++)
                        {
                            if (sortingLayers[s].name == definition.sortingLayerName)
                            {
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            renderer.sortingLayerName = definition.sortingLayerName;
                        else
                            Debug.LogWarning($"[ParallaxBackground] Sorting layer '{definition.sortingLayerName}' not found. Using Default.");
                    }

                    segments[i] = segmentObject.transform;
                }
            }

            public void Update(
                float cameraX,
                float cameraY,
                float referenceCameraX,
                float halfViewWidth,
                float margin)
            {
                float parallaxX = worldAnchorX + (cameraX - referenceCameraX) * followSpeed;
                root.position = new Vector3(parallaxX, cameraY + yOffsetFromCamera, z);

                float viewLeft = cameraX - halfViewWidth - margin;
                float viewRight = cameraX + halfViewWidth + margin;
                float halfSegment = segmentWidth * 0.5f;
                float wrapStep = segmentWidth * segmentCount;

                for (int i = 0; i < segments.Length; i++)
                {
                    Transform segment = segments[i];
                    float localX = segment.localPosition.x;
                    float worldX = root.position.x + localX;

                    while (worldX + halfSegment < viewLeft)
                    {
                        localX += wrapStep;
                        worldX += wrapStep;
                    }

                    while (worldX - halfSegment > viewRight)
                    {
                        localX -= wrapStep;
                        worldX -= wrapStep;
                    }

                    segment.localPosition = new Vector3(localX, 0f, 0f);
                }
            }

            public void SetVisible(bool visible)
            {
                if (root != null)
                    root.gameObject.SetActive(visible);
            }

            public void SetSpeed(float speed)
            {
                followSpeed = Mathf.Clamp01(speed);
            }

            public void SetOffset(float yOffset)
            {
                yOffsetFromCamera = yOffset;
            }

            public void SetColor(Color color)
            {
                if (segments == null)
                    return;

                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] != null && segments[i].TryGetComponent(out SpriteRenderer sr))
                        sr.color = color;
                }
            }

            public void SetAlpha(float alpha)
            {
                if (segments == null)
                    return;

                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i] != null && segments[i].TryGetComponent(out SpriteRenderer sr))
                    {
                        Color c = sr.color;
                        c.a = Mathf.Clamp01(alpha);
                        sr.color = c;
                    }
                }
            }

            public void Destroy()
            {
                if (root == null)
                    return;

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(root.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}
