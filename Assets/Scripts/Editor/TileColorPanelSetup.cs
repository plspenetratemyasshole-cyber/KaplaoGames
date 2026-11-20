#if UNITY_EDITOR
using KaplaoGames.Client;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Editor
{
  public static class TileColorPanelSetup
  {
    [MenuItem("Kaplao/Bind Existing Tile Color Panel")]
    public static void BindExistingFromMenu()
    {
      Canvas canvas = Object.FindFirstObjectByType<Canvas>();
      if (canvas == null)
      {
        Debug.LogWarning("[Kaplao] Canvas not found in open scene.");
        return;
      }

      Transform createModeRoot = canvas.transform.Find("CreateModeUI");
      if (createModeRoot == null)
      {
        Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
        return;
      }

      KaplaoTileColorUi colorUi = canvas.GetComponent<KaplaoTileColorUi>();
      if (colorUi == null)
      {
        Debug.LogWarning("[Kaplao] KaplaoTileColorUi not found on Canvas.");
        return;
      }

      if (!TryBindExistingPanel(colorUi, createModeRoot))
        Debug.LogWarning("[Kaplao] TileColorPanel not found. Use Kaplao/Setup Tile Color Panel to create a new one.");
    }

    [MenuItem("Kaplao/Setup Tile Color Panel")]
    public static void SetupFromMenu()
    {
      Canvas canvas = Object.FindFirstObjectByType<Canvas>();
      if (canvas == null)
      {
        Debug.LogWarning("[Kaplao] Canvas not found in open scene.");
        return;
      }

      Transform createModeRoot = canvas.transform.Find("CreateModeUI");
      if (createModeRoot == null)
      {
        Debug.LogWarning("[Kaplao] CreateModeUI not found. Run Kaplao/Setup Create Mode UI first.");
        return;
      }

      KaplaoTileColorUi colorUi = canvas.GetComponent<KaplaoTileColorUi>();
      if (colorUi == null)
        colorUi = canvas.gameObject.AddComponent<KaplaoTileColorUi>();

      // Safe behavior: if panel already exists, only re-bind references and keep layout untouched.
      if (TryBindExistingPanel(colorUi, createModeRoot))
      {
        Debug.Log("[Kaplao] Bound existing TileColorPanel references (layout preserved).");
        return;
      }

      BuildPanel(colorUi, createModeRoot);
      Debug.Log("[Kaplao] Tile color panel created under CreateModeUI/TileColorPanel.");
    }

    public static bool TryBindExistingPanel(KaplaoTileColorUi colorUi, Transform parent)
    {
      Transform panel = parent.Find("TileColorPanel");
      if (panel == null)
        return false;

      SerializedObject serializedUi = new SerializedObject(colorUi);
      serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;

      RectTransform panelRoot = panel.GetComponent<RectTransform>();
      Image previewFill = panel.Find("PickerRow/ColorPreview/Fill")?.GetComponent<Image>();
      RawImage svImage = panel.Find("PickerRow/SvPicker")?.GetComponent<RawImage>();
      RawImage hueImage = panel.Find("PickerRow/HuePicker")?.GetComponent<RawImage>();
      RectTransform svPickerRoot = panel.Find("PickerRow/SvPicker") as RectTransform;
      RectTransform huePickerRoot = panel.Find("PickerRow/HuePicker") as RectTransform;
      RectTransform swatchGrid = panel.Find("SwatchGrid") as RectTransform;
      Slider red = panel.Find("RgbRow/RRow/Slider")?.GetComponent<Slider>();
      Slider green = panel.Find("RgbRow/GRow/Slider")?.GetComponent<Slider>();
      Slider blue = panel.Find("RgbRow/BRow/Slider")?.GetComponent<Slider>();
      TMP_InputField hexField = panel.Find("HexRow/HexField")?.GetComponent<TMP_InputField>();

      var references = new KaplaoTileColorUi.PanelReferences
      {
        PanelRoot = panelRoot,
        PreviewFill = previewFill,
        SvImage = svImage,
        HueImage = hueImage,
        SvPickerRoot = svPickerRoot,
        HuePickerRoot = huePickerRoot,
        SwatchGridRoot = swatchGrid,
        RedSlider = red,
        GreenSlider = green,
        BlueSlider = blue,
        HexField = hexField
      };

      colorUi.AssignPanelReferences(references);

      serializedUi.FindProperty("panelRoot").objectReferenceValue = panelRoot;
      serializedUi.FindProperty("previewFill").objectReferenceValue = previewFill;
      serializedUi.FindProperty("svImage").objectReferenceValue = svImage;
      serializedUi.FindProperty("hueImage").objectReferenceValue = hueImage;
      serializedUi.FindProperty("svPickerRoot").objectReferenceValue = svPickerRoot;
      serializedUi.FindProperty("huePickerRoot").objectReferenceValue = huePickerRoot;
      serializedUi.FindProperty("swatchGridRoot").objectReferenceValue = swatchGrid;
      serializedUi.FindProperty("redSlider").objectReferenceValue = red;
      serializedUi.FindProperty("greenSlider").objectReferenceValue = green;
      serializedUi.FindProperty("blueSlider").objectReferenceValue = blue;
      serializedUi.FindProperty("hexField").objectReferenceValue = hexField;
      serializedUi.ApplyModifiedPropertiesWithoutUndo();

      EditorUtility.SetDirty(colorUi);
      return true;
    }

    public static void BuildPanel(KaplaoTileColorUi colorUi, Transform parent)
    {
      SerializedObject serializedUi = new SerializedObject(colorUi);
      serializedUi.FindProperty("kaplaoOnlyRoot").objectReferenceValue = parent.gameObject;

      GameObject panelObject = new GameObject("TileColorPanel", typeof(RectTransform));
      panelObject.transform.SetParent(parent, false);
      RectTransform panelRoot = panelObject.GetComponent<RectTransform>();
      panelRoot.anchorMin = new Vector2(0f, 0f);
      panelRoot.anchorMax = new Vector2(0f, 0f);
      panelRoot.pivot = new Vector2(0f, 0f);
      panelRoot.anchoredPosition = new Vector2(16f, 16f);
      panelRoot.sizeDelta = new Vector2(392f, 292f);

      Image panelBackground = panelObject.AddComponent<Image>();
      panelBackground.color = new Color(0.07f, 0.08f, 0.11f, 0.94f);

      VerticalLayoutGroup rootLayout = panelObject.AddComponent<VerticalLayoutGroup>();
      rootLayout.padding = new RectOffset(10, 10, 10, 10);
      rootLayout.spacing = 8f;
      rootLayout.childAlignment = TextAnchor.UpperLeft;
      rootLayout.childControlWidth = true;
      rootLayout.childControlHeight = false;
      rootLayout.childForceExpandWidth = true;

      Image previewFill = BuildPickerRow(panelObject.transform, out RawImage svImage, out RawImage hueImage,
        out RectTransform svPickerRoot, out RectTransform huePickerRoot);
      Slider red = BuildRgbRow(panelObject.transform, out Slider green, out Slider blue);
      TMP_InputField hexField = BuildHexRow(panelObject.transform);
      RectTransform swatchGrid = BuildSwatchGrid(panelObject.transform);

      var references = new KaplaoTileColorUi.PanelReferences
      {
        PanelRoot = panelRoot,
        PreviewFill = previewFill,
        SvImage = svImage,
        HueImage = hueImage,
        SvPickerRoot = svPickerRoot,
        HuePickerRoot = huePickerRoot,
        SwatchGridRoot = swatchGrid,
        RedSlider = red,
        GreenSlider = green,
        BlueSlider = blue,
        HexField = hexField
      };

      colorUi.AssignPanelReferences(references);

      serializedUi.FindProperty("panelRoot").objectReferenceValue = panelRoot;
      serializedUi.FindProperty("previewFill").objectReferenceValue = previewFill;
      serializedUi.FindProperty("svImage").objectReferenceValue = svImage;
      serializedUi.FindProperty("hueImage").objectReferenceValue = hueImage;
      serializedUi.FindProperty("svPickerRoot").objectReferenceValue = svPickerRoot;
      serializedUi.FindProperty("huePickerRoot").objectReferenceValue = huePickerRoot;
      serializedUi.FindProperty("swatchGridRoot").objectReferenceValue = swatchGrid;
      serializedUi.FindProperty("redSlider").objectReferenceValue = red;
      serializedUi.FindProperty("greenSlider").objectReferenceValue = green;
      serializedUi.FindProperty("blueSlider").objectReferenceValue = blue;
      serializedUi.FindProperty("hexField").objectReferenceValue = hexField;
      serializedUi.ApplyModifiedPropertiesWithoutUndo();

      EditorUtility.SetDirty(colorUi);
      EditorUtility.SetDirty(panelObject);
    }

    static Image BuildPickerRow(Transform parent, out RawImage svImage, out RawImage hueImage,
      out RectTransform svPickerRoot, out RectTransform huePickerRoot)
    {
      GameObject rowObject = new GameObject("PickerRow", typeof(RectTransform));
      rowObject.transform.SetParent(parent, false);
      LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
      rowLayout.preferredHeight = 148f;

      HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 10f;
      row.childAlignment = TextAnchor.UpperLeft;
      row.childControlWidth = false;
      row.childControlHeight = false;

      Image previewFill = BuildPreviewColumn(rowObject.transform);

      GameObject svRoot = new GameObject("SvPicker", typeof(RectTransform));
      svRoot.transform.SetParent(rowObject.transform, false);
      svPickerRoot = svRoot.GetComponent<RectTransform>();
      svPickerRoot.sizeDelta = new Vector2(140f, 140f);
      svImage = svRoot.AddComponent<RawImage>();
      svImage.raycastTarget = true;

      GameObject hueRoot = new GameObject("HuePicker", typeof(RectTransform));
      hueRoot.transform.SetParent(rowObject.transform, false);
      huePickerRoot = hueRoot.GetComponent<RectTransform>();
      huePickerRoot.sizeDelta = new Vector2(24f, 140f);
      hueImage = hueRoot.AddComponent<RawImage>();
      hueImage.raycastTarget = true;

      return previewFill;
    }

    static Image BuildPreviewColumn(Transform parent)
    {
      GameObject previewRoot = new GameObject("ColorPreview", typeof(RectTransform));
      previewRoot.transform.SetParent(parent, false);
      RectTransform previewRect = previewRoot.GetComponent<RectTransform>();
      previewRect.sizeDelta = new Vector2(64f, 140f);

      VerticalLayoutGroup column = previewRoot.AddComponent<VerticalLayoutGroup>();
      column.spacing = 4f;
      column.childAlignment = TextAnchor.UpperCenter;
      column.childControlWidth = true;
      column.childControlHeight = false;

      GameObject labelObject = new GameObject("Label", typeof(RectTransform));
      labelObject.transform.SetParent(previewRoot.transform, false);
      LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
      labelLayout.preferredHeight = 18f;
      TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
      label.text = "Color";
      label.fontSize = 12f;
      label.alignment = TextAlignmentOptions.Center;
      label.color = new Color(1f, 1f, 1f, 0.75f);

      GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
      fillObject.transform.SetParent(previewRoot.transform, false);
      LayoutElement fillLayout = fillObject.AddComponent<LayoutElement>();
      fillLayout.flexibleHeight = 1f;
      fillLayout.preferredHeight = 110f;

      Image fill = fillObject.AddComponent<Image>();
      fill.color = Color.white;
      return fill;
    }

    static Slider BuildRgbRow(Transform parent, out Slider greenSlider, out Slider blueSlider)
    {
      GameObject rowObject = new GameObject("RgbRow", typeof(RectTransform));
      rowObject.transform.SetParent(parent, false);
      LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
      rowLayout.preferredHeight = 72f;

      VerticalLayoutGroup column = rowObject.AddComponent<VerticalLayoutGroup>();
      column.spacing = 4f;
      column.childControlHeight = true;
      column.childControlWidth = true;
      column.childForceExpandWidth = true;

      Slider redSlider = CreateChannelSlider(rowObject.transform, "R");
      greenSlider = CreateChannelSlider(rowObject.transform, "G");
      blueSlider = CreateChannelSlider(rowObject.transform, "B");
      return redSlider;
    }

    static TMP_InputField BuildHexRow(Transform parent)
    {
      GameObject rowObject = new GameObject("HexRow", typeof(RectTransform));
      rowObject.transform.SetParent(parent, false);
      LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
      rowLayout.preferredHeight = 28f;

      HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 8f;
      row.childAlignment = TextAnchor.MiddleLeft;
      row.childControlWidth = false;
      row.childControlHeight = true;

      GameObject labelObject = new GameObject("HexLabel", typeof(RectTransform));
      labelObject.transform.SetParent(rowObject.transform, false);
      LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
      labelLayout.preferredWidth = 36f;
      TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
      label.text = "#";
      label.fontSize = 14f;
      label.color = new Color(1f, 1f, 1f, 0.85f);

      return CreateHexField(rowObject.transform);
    }

    static RectTransform BuildSwatchGrid(Transform parent)
    {
      GameObject gridObject = new GameObject("SwatchGrid", typeof(RectTransform));
      gridObject.transform.SetParent(parent, false);
      LayoutElement gridLayout = gridObject.AddComponent<LayoutElement>();
      gridLayout.preferredHeight = 60f;

      GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
      grid.cellSize = new Vector2(26f, 26f);
      grid.spacing = new Vector2(4f, 4f);
      grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
      grid.constraintCount = 9;
      return gridObject.GetComponent<RectTransform>();
    }

    static Slider CreateChannelSlider(Transform parent, string labelText)
    {
      GameObject rowObject = new GameObject($"{labelText}Row", typeof(RectTransform));
      rowObject.transform.SetParent(parent, false);
      LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
      rowLayout.preferredHeight = 22f;

      HorizontalLayoutGroup row = rowObject.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 8f;
      row.childAlignment = TextAnchor.MiddleLeft;
      row.childControlWidth = false;
      row.childControlHeight = true;

      GameObject labelObject = new GameObject("Label", typeof(RectTransform));
      labelObject.transform.SetParent(rowObject.transform, false);
      LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
      labelLayout.preferredWidth = 16f;
      TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
      label.text = labelText;
      label.fontSize = 13f;
      label.color = new Color(1f, 1f, 1f, 0.85f);

      GameObject sliderObject = new GameObject("Slider", typeof(RectTransform));
      sliderObject.transform.SetParent(rowObject.transform, false);
      LayoutElement sliderLayout = sliderObject.AddComponent<LayoutElement>();
      sliderLayout.flexibleWidth = 1f;
      sliderLayout.preferredHeight = 18f;

      Slider slider = sliderObject.AddComponent<Slider>();
      slider.minValue = 0f;
      slider.maxValue = 255f;
      slider.wholeNumbers = true;

      GameObject background = new GameObject("Background", typeof(RectTransform));
      background.transform.SetParent(sliderObject.transform, false);
      RectTransform backgroundRect = background.GetComponent<RectTransform>();
      backgroundRect.anchorMin = Vector2.zero;
      backgroundRect.anchorMax = Vector2.one;
      backgroundRect.offsetMin = Vector2.zero;
      backgroundRect.offsetMax = Vector2.zero;
      Image backgroundImage = background.AddComponent<Image>();
      backgroundImage.color = new Color(0.12f, 0.13f, 0.16f, 1f);

      GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
      fillArea.transform.SetParent(sliderObject.transform, false);
      RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
      fillAreaRect.anchorMin = Vector2.zero;
      fillAreaRect.anchorMax = Vector2.one;
      fillAreaRect.offsetMin = new Vector2(6f, 4f);
      fillAreaRect.offsetMax = new Vector2(-6f, -4f);

      GameObject fill = new GameObject("Fill", typeof(RectTransform));
      fill.transform.SetParent(fillArea.transform, false);
      Image fillImage = fill.AddComponent<Image>();
      fillImage.color = new Color(0.35f, 0.55f, 0.95f, 1f);
      slider.fillRect = fill.GetComponent<RectTransform>();

      GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
      handleArea.transform.SetParent(sliderObject.transform, false);
      RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
      handleAreaRect.anchorMin = Vector2.zero;
      handleAreaRect.anchorMax = Vector2.one;
      handleAreaRect.offsetMin = new Vector2(6f, 0f);
      handleAreaRect.offsetMax = new Vector2(-6f, 0f);

      GameObject handle = new GameObject("Handle", typeof(RectTransform));
      handle.transform.SetParent(handleArea.transform, false);
      RectTransform handleRect = handle.GetComponent<RectTransform>();
      handleRect.sizeDelta = new Vector2(12f, 0f);
      Image handleImage = handle.AddComponent<Image>();
      handleImage.color = Color.white;
      slider.handleRect = handleRect;
      slider.targetGraphic = handleImage;

      return slider;
    }

    static TMP_InputField CreateHexField(Transform parent)
    {
      GameObject fieldObject = new GameObject("HexField", typeof(RectTransform));
      fieldObject.transform.SetParent(parent, false);
      LayoutElement fieldLayout = fieldObject.AddComponent<LayoutElement>();
      fieldLayout.preferredWidth = 96f;
      fieldLayout.preferredHeight = 26f;

      Image fieldBackground = fieldObject.AddComponent<Image>();
      fieldBackground.color = new Color(0.12f, 0.13f, 0.16f, 1f);

      GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
      textArea.transform.SetParent(fieldObject.transform, false);
      RectTransform textAreaRect = textArea.GetComponent<RectTransform>();
      textAreaRect.anchorMin = Vector2.zero;
      textAreaRect.anchorMax = Vector2.one;
      textAreaRect.offsetMin = new Vector2(8f, 2f);
      textAreaRect.offsetMax = new Vector2(-8f, -2f);

      GameObject textObject = new GameObject("Text", typeof(RectTransform));
      textObject.transform.SetParent(textArea.transform, false);
      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;

      TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
      text.fontSize = 14f;
      text.color = Color.white;

      TMP_InputField inputField = fieldObject.AddComponent<TMP_InputField>();
      inputField.textViewport = textAreaRect;
      inputField.textComponent = text;
      inputField.characterLimit = 6;
      inputField.contentType = TMP_InputField.ContentType.Alphanumeric;
      return inputField;
    }
  }
}
#endif
