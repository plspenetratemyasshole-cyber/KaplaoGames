using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KaplaoGames.Shared
{
    [Serializable]
    public class KaplaoNicknamePresetCatalogDto
    {
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("presets")] public List<KaplaoNicknamePresetDto> Presets;
    }

    [Serializable]
    public class KaplaoNicknamePresetDto
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("style")] public KaplaoNicknameStyleDto Style;
    }

    [Serializable]
    public class KaplaoNicknameStyleDto
    {
        [JsonProperty("fontName")] public string FontName;
        [JsonProperty("baseColor")] public string BaseColor;
        [JsonProperty("outlineColor")] public string OutlineColor;
        [JsonProperty("outlineWidth")] public float OutlineWidth;
        [JsonProperty("effects")] public List<KaplaoNicknameEffectDto> Effects;
    }

    [Serializable]
    public class KaplaoNicknameGradientStopDto
    {
        [JsonProperty("t")] public float T;
        [JsonProperty("hex")] public string Hex;
    }

    [Serializable]
    public class KaplaoNicknameVec2Dto
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
    }

    [Serializable]
    public class KaplaoNicknameEasingKeyDto
    {
        [JsonProperty("time")] public float Time;
        [JsonProperty("value")] public float Value;
    }

    /// <summary>
    /// One Easy Text Effects instance. Type selects which fields are used.
    /// Nested lists support composite / per-vertex.
    /// </summary>
    [Serializable]
    public class KaplaoNicknameEffectDto
    {
        [JsonProperty("type")] public string Type = "color";

        [JsonProperty("triggerWhen")] public string TriggerWhen = "OnStart";
        [JsonProperty("overrideTagEffects")] public bool OverrideTagEffects;

        [JsonProperty("animationType")] public string AnimationType = "PingPong";
        [JsonProperty("fixedDuration")] public float FixedDuration;
        [JsonProperty("durationPerChar")] public float DurationPerChar = 0.5f;
        [JsonProperty("timeBetweenChars")] public float TimeBetweenChars = 0.05f;
        [JsonProperty("noDelayForChars")] public bool NoDelayForChars;
        [JsonProperty("reverseCharOrder")] public bool ReverseCharOrder;
        [JsonProperty("clampBetween0And1")] public bool ClampBetween0And1;
        [JsonProperty("timeType")] public string TimeType = "UnscaledTime";
        [JsonProperty("easing")] public string Easing = "linear";
        [JsonProperty("easingKeys")] public List<KaplaoNicknameEasingKeyDto> EasingKeys;

        // color
        [JsonProperty("colorType")] public string ColorType = "Gradient";
        [JsonProperty("startColor")] public string StartColor;
        [JsonProperty("endColor")] public string EndColor;
        [JsonProperty("gradientStops")] public List<KaplaoNicknameGradientStopDto> GradientStops;
        [JsonProperty("orientation")] public string Orientation = "Horizontal";
        [JsonProperty("stride")] public int Stride = 10;
        [JsonProperty("startAlpha")] public float StartAlpha;
        [JsonProperty("endAlpha")] public float EndAlpha = 1f;

        // move
        [JsonProperty("startOffset")] public KaplaoNicknameVec2Dto StartOffset;
        [JsonProperty("endOffset")] public KaplaoNicknameVec2Dto EndOffset;

        // rotate
        [JsonProperty("centerOffset")] public KaplaoNicknameVec2Dto CenterOffset;
        [JsonProperty("startAngle")] public float StartAngle = 90f;
        [JsonProperty("endAngle")] public float EndAngle;

        // scale
        [JsonProperty("startScale")] public float StartScale;
        [JsonProperty("endScale")] public float EndScale = 1f;

        // composite
        [JsonProperty("effects")] public List<KaplaoNicknameEffectDto> Effects;

        // per-vertex
        [JsonProperty("topLeft")] public List<KaplaoNicknameEffectDto> TopLeft;
        [JsonProperty("topRight")] public List<KaplaoNicknameEffectDto> TopRight;
        [JsonProperty("bottomLeft")] public List<KaplaoNicknameEffectDto> BottomLeft;
        [JsonProperty("bottomRight")] public List<KaplaoNicknameEffectDto> BottomRight;
    }
}
