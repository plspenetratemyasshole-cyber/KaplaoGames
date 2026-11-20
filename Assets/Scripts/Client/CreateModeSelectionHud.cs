using System.Collections.Generic;
using System.Text;
using KaplaoGames.Network;
using KaplaoGames.Server;
using KaplaoGames.Shared;
using Mirror;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Create-mode selection info near the pause control: Objects / Layer / Groups / Order + G/R/T hint.
    /// </summary>
    [DisallowMultipleComponent]
    public class CreateModeSelectionHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI hintLabel;
        [SerializeField] TextMeshProUGUI infoLabel;

        KaplaoCursorServer cursorServer;
        readonly StringBuilder builder = new();
        readonly HashSet<int> groupsBuffer = new();
        readonly List<int> layersBuffer = new();
        readonly List<int> ordersBuffer = new();

        void Awake()
        {
            EnsureLabels();
        }

        void Update()
        {
            if (!TryResolveCursor(out KaplaoCursorServer cursor))
            {
                SetVisible(false);
                return;
            }

            if (!KaplaoLocalGameModePredictor.IsCreate(cursor) ||
                cursor.SelectedCount <= 0)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            if (hintLabel != null)
            {
                // NoWrap + explicit \n => always exactly 3 lines; Auto Size only changes font size.
                hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
                hintLabel.overflowMode = TextOverflowModes.Overflow;
                if (hintLabel.text != "G - move\nR - rotate\nT - scale")
                    hintLabel.text = "G - move\nR - rotate\nT - scale";
            }

            if (infoLabel != null)
            {
                infoLabel.textWrappingMode = TextWrappingModes.NoWrap;
                infoLabel.overflowMode = TextOverflowModes.Overflow;
                infoLabel.text = BuildInfo(cursor);
            }
        }

        string BuildInfo(KaplaoCursorServer cursor)
        {
            layersBuffer.Clear();
            ordersBuffer.Clear();
            groupsBuffer.Clear();

            int objectCount = cursor.SelectedCount;
            HashSet<int> intersection = null;

            for (int i = 0; i < cursor.SelectedTiles.Count; i++)
            {
                LevelTileServer tile = FindTile(cursor.SelectedTiles[i]);
                if (tile == null)
                    continue;

                AddUnique(layersBuffer, tile.EditorLayer);
                AddUnique(ordersBuffer, tile.SortingOrder);

                HashSet<int> tileGroups = new();
                TileGroups.AddNumbers(tileGroups, TileGroups.ParseNumbers(tile.GroupsDisplay));
                foreach (int g in tileGroups)
                    groupsBuffer.Add(g);

                if (intersection == null)
                {
                    intersection = new HashSet<int>(tileGroups);
                }
                else
                {
                    intersection.IntersectWith(tileGroups);
                }
            }

            if (intersection == null)
                intersection = new HashSet<int>();

            builder.Clear();
            builder.Append("Objects: ").Append(objectCount).Append('\n');
            builder.Append("Layer: ").Append(FormatList(layersBuffer)).Append('\n');
            builder.Append("Groups: ").Append(FormatGroups(groupsBuffer, intersection)).Append('\n');
            builder.Append("Order: ").Append(FormatList(ordersBuffer));
            return builder.ToString();
        }

        static string FormatList(List<int> values)
        {
            if (values.Count == 0)
                return "-";

            values.Sort();
            builderScratch.Clear();
            int shown = Mathf.Min(5, values.Count);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                    builderScratch.Append(',');
                builderScratch.Append(values[i]);
            }

            if (values.Count > 5)
                builderScratch.Append("...");

            return builderScratch.ToString();
        }

        static readonly StringBuilder builderScratch = new();

        static string FormatGroups(HashSet<int> all, HashSet<int> onAllTiles)
        {
            if (all.Count == 0)
                return "-";

            var list = new List<int>(all);
            list.Sort();

            builderScratch.Clear();
            int shown = Mathf.Min(5, list.Count);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0)
                    builderScratch.Append(',');

                int g = list[i];
                if (!onAllTiles.Contains(g))
                    builderScratch.Append('(').Append(g).Append(')');
                else
                    builderScratch.Append(g);
            }

            if (list.Count > 5)
                builderScratch.Append("...");

            return builderScratch.ToString();
        }

        static void AddUnique(List<int> list, int value)
        {
            if (!list.Contains(value))
                list.Add(value);
        }

        static LevelTileServer FindTile(uint netId)
        {
            if (LevelGridServer.Instance != null &&
                LevelGridServer.Instance.TryGetTileByNetId(netId, out LevelTileServer tile))
            {
                return tile;
            }

            return null;
        }

        static bool TryResolveCursor(out KaplaoCursorServer cursor)
        {
            cursor = null;
            if (NetworkClient.localPlayer == null)
                return false;

            if (!NetworkClient.localPlayer.TryGetComponent(out NetworkPlayer player) ||
                player.role != PlayerRole.Kaplao)
            {
                return false;
            }

            return NetworkClient.localPlayer.TryGetComponent(out cursor);
        }

        void SetVisible(bool visible)
        {
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(visible);
            if (infoLabel != null)
                infoLabel.gameObject.SetActive(visible);
        }

        void EnsureLabels()
        {
            if (hintLabel != null && infoLabel != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            Transform root = canvas.transform.Find("CreateSelectionHud");
            if (root == null)
            {
                var rootObject = new GameObject("CreateSelectionHud", typeof(RectTransform));
                rootObject.transform.SetParent(canvas.transform, false);
                RectTransform rect = rootObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-24f, -140f);
                rect.sizeDelta = new Vector2(300f, 200f);
                root = rootObject.transform;
            }

            if (hintLabel == null)
            {
                hintLabel = EnsureLabel(root, "HintLabel", new Vector2(0f, -8f), 18f, new Vector2(280f, 72f));
                hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
                hintLabel.overflowMode = TextOverflowModes.Overflow;
                hintLabel.enableAutoSizing = true;
                hintLabel.fontSizeMin = 10f;
                hintLabel.fontSizeMax = 22f;
                hintLabel.text = "G - move\nR - rotate\nT - scale";
            }

            if (infoLabel == null)
            {
                infoLabel = EnsureLabel(root, "InfoLabel", new Vector2(0f, -90f), 16f, new Vector2(280f, 88f));
                infoLabel.textWrappingMode = TextWrappingModes.NoWrap;
                infoLabel.overflowMode = TextOverflowModes.Overflow;
                infoLabel.enableAutoSizing = true;
                infoLabel.fontSizeMin = 10f;
                infoLabel.fontSizeMax = 18f;
            }
        }

        static TextMeshProUGUI EnsureLabel(
            Transform parent,
            string name,
            Vector2 anchoredPos,
            float fontSize,
            Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject labelObject;
            if (existing == null)
            {
                labelObject = new GameObject(name, typeof(RectTransform));
                labelObject.transform.SetParent(parent, false);
            }
            else
            {
                labelObject = existing.gameObject;
            }

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = labelObject.AddComponent<TextMeshProUGUI>();

            label.alignment = TextAlignmentOptions.TopRight;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }
    }
}
