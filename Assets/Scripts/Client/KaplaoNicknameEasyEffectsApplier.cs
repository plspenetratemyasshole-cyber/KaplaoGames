using System;
using System.Collections.Generic;
using EasyTextEffects;
using EasyTextEffects.Effects;
using KaplaoGames.Shared;
using TMPro;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Builds Easy Text Effects instances from website nickname preset JSON and drives TMP.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KaplaoNicknameEasyEffectsApplier : MonoBehaviour
    {
        TextMeshProUGUI label;
        TextEffect textEffect;
        readonly List<ScriptableObject> ownedAssets = new List<ScriptableObject>();
        string appliedSignature;

        public void Apply(KaplaoNicknameStyleDto style)
        {
            label = GetComponent<TextMeshProUGUI>();
            if (label == null)
                return;

            string signature = style == null ? "none" : JsonUtilityFingerprint(style);
            if (signature == appliedSignature)
            {
                ForceRefreshEffect();
                return;
            }

            ClearEffects();
            appliedSignature = signature;

            if (style == null || style.Effects == null || style.Effects.Count == 0)
            {
                ApplyPlainBase(style);
                return;
            }

            ApplyPlainBase(style);

            textEffect = GetComponent<TextEffect>();
            if (textEffect == null)
                textEffect = gameObject.AddComponent<TextEffect>();

            textEffect.text = label;
            textEffect.usePreset = false;
            textEffect.tagEffects = new List<TextEffectEntry>();
            textEffect.globalEffects = new List<GlobalTextEffectEntry>();
            textEffect.updatesPerSecond = 30;

            for (int i = 0; i < style.Effects.Count; i++)
            {
                KaplaoNicknameEffectDto dto = style.Effects[i];
                TextEffectInstance effect = BuildEffect(dto);
                if (effect == null)
                    continue;

                textEffect.globalEffects.Add(new GlobalTextEffectEntry
                {
                    effect = effect,
                    triggerWhen = ParseTrigger(dto?.TriggerWhen),
                    overrideTagEffects = dto != null && dto.OverrideTagEffects
                });
            }

            if (textEffect.globalEffects.Count == 0)
            {
                ClearEffects();
                ApplyPlainBase(style);
                return;
            }

            label.color = Color.white;
            label.enableVertexGradient = false;
            NicknameStyle.ApplyOutline(label, style);
            textEffect.Refresh();
            textEffect.StartOnStartEffects();
            enabled = true;
        }

        public void ForceRefreshEffect()
        {
            if (label == null) label = GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            label.ForceMeshUpdate(true, true);
            if (textEffect == null) textEffect = GetComponent<TextEffect>();
            if (textEffect != null)
            {
                textEffect.Refresh();
                textEffect.StartOnStartEffects();
            }
        }

        public void Clear()
        {
            ClearEffects();
            appliedSignature = null;
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();

            KaplaoNicknameFontRegistry fonts = KaplaoNicknameFontRegistry.Load();
            if (label != null)
            {
                label.enableVertexGradient = false;
                label.color = fonts != null ? fonts.FallbackColor : Color.white;
                NicknameStyle.ClearOutline(label);
            }

            enabled = false;
        }

        void OnDestroy()
        {
            ClearEffects();
        }

        void ClearEffects()
        {
            if (textEffect != null)
            {
                textEffect.StopAllEffects();
                textEffect.globalEffects = new List<GlobalTextEffectEntry>();
                textEffect.tagEffects = new List<TextEffectEntry>();
                Destroy(textEffect);
                textEffect = null;
            }

            for (int i = 0; i < ownedAssets.Count; i++)
            {
                if (ownedAssets[i] != null)
                    Destroy(ownedAssets[i]);
            }

            ownedAssets.Clear();
        }

        void ApplyPlainBase(KaplaoNicknameStyleDto style)
        {
            if (label == null)
                return;

            Color baseColor = Color.white;
            KaplaoNicknameFontRegistry fonts = KaplaoNicknameFontRegistry.Load();
            if (fonts != null)
                baseColor = fonts.FallbackColor;

            if (style != null && TryParseHex(style.BaseColor, out Color parsed))
                baseColor = parsed;

            label.enableVertexGradient = false;
            label.color = baseColor;
            NicknameStyle.ApplyOutline(label, style);
        }

        TextEffectInstance BuildEffect(KaplaoNicknameEffectDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Type))
                return null;

            string type = dto.Type.Trim().ToLowerInvariant();
            TextEffectInstance effect = null;
            if (type == "color")
                effect = BuildColor(dto);
            else if (type == "move")
                effect = BuildMove(dto);
            else if (type == "rotate")
                effect = BuildRotate(dto);
            else if (type == "scale")
                effect = BuildScale(dto);
            else if (type == "composite")
                effect = BuildComposite(dto);
            else if (type == "pervertex" || type == "per_vertex" || type == "per-vertex")
                effect = BuildPerVertex(dto);

            if (effect != null)
                ApplyTiming(effect, dto);

            return effect;
        }

        Effect_Color BuildColor(KaplaoNicknameEffectDto dto)
        {
            Effect_Color effect = Create<Effect_Color>();
            effect.colorType = ParseEnum(dto.ColorType, Effect_Color.ColorType.Gradient);
            effect.orientation = ParseEnum(dto.Orientation, Effect_Color.GradientOrientation.Horizontal);
            effect.stride = dto.Stride > 0 ? dto.Stride : 10;
            effect.startAlpha = dto.StartAlpha;
            effect.endAlpha = dto.EndAlpha;
            if (TryParseHex(dto.StartColor, out Color start))
                effect.startColor = start;
            if (TryParseHex(dto.EndColor, out Color end))
                effect.endColor = end;
            effect.gradient = BuildGradient(dto.GradientStops);
            return effect;
        }

        Effect_Move BuildMove(KaplaoNicknameEffectDto dto)
        {
            Effect_Move effect = Create<Effect_Move>();
            effect.startOffset = ToVec2(dto.StartOffset, new Vector2(0f, -10f));
            effect.endOffset = ToVec2(dto.EndOffset, Vector2.zero);
            return effect;
        }

        Effect_Rotate BuildRotate(KaplaoNicknameEffectDto dto)
        {
            Effect_Rotate effect = Create<Effect_Rotate>();
            effect.centerOffset = ToVec2(dto.CenterOffset, Vector2.zero);
            effect.startAngle = dto.StartAngle;
            effect.endAngle = dto.EndAngle;
            return effect;
        }

        Effect_Scale BuildScale(KaplaoNicknameEffectDto dto)
        {
            Effect_Scale effect = Create<Effect_Scale>();
            effect.startScale = dto.StartScale;
            effect.endScale = dto.EndScale;
            return effect;
        }

        Effect_Composite BuildComposite(KaplaoNicknameEffectDto dto)
        {
            Effect_Composite effect = Create<Effect_Composite>();
            effect.effects = new List<TextEffectInstance>();
            if (dto.Effects == null)
                return effect;

            for (int i = 0; i < dto.Effects.Count; i++)
            {
                TextEffectInstance child = BuildEffect(dto.Effects[i]);
                if (child != null)
                    effect.effects.Add(child);
            }

            return effect;
        }

        Effect_PerVertex BuildPerVertex(KaplaoNicknameEffectDto dto)
        {
            Effect_PerVertex effect = Create<Effect_PerVertex>();
            effect.topLeftEffects = BuildList(dto.TopLeft);
            effect.topRightEffects = BuildList(dto.TopRight);
            effect.bottomLeftEffects = BuildList(dto.BottomLeft);
            effect.bottomRightEffects = BuildList(dto.BottomRight);
            return effect;
        }

        List<TextEffectInstance> BuildList(List<KaplaoNicknameEffectDto> source)
        {
            var list = new List<TextEffectInstance>();
            if (source == null)
                return list;

            for (int i = 0; i < source.Count; i++)
            {
                TextEffectInstance child = BuildEffect(source[i]);
                if (child != null)
                    list.Add(child);
            }

            return list;
        }

        void ApplyTiming(TextEffectInstance effect, KaplaoNicknameEffectDto dto)
        {
            effect.animationType = ParseEnum(dto.AnimationType, TextEffectInstance.AnimationType.PingPong);
            effect.fixedDuration = dto.FixedDuration;
            effect.durationPerChar = dto.DurationPerChar > 0f ? dto.DurationPerChar : 0.5f;
            effect.timeBetweenChars = dto.TimeBetweenChars;
            effect.noDelayForChars = dto.NoDelayForChars;
            effect.reverseCharOrder = dto.ReverseCharOrder;
            effect.clampBetween0And1 = dto.ClampBetween0And1;
            effect.timeType = ParseEnum(dto.TimeType, TimeUtil.TimeType.UnscaledTime);
            effect.easingCurve = BuildCurve(dto);
        }

        T Create<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            ownedAssets.Add(asset);
            return asset;
        }

        static TextEffectEntry.TriggerWhen ParseTrigger(string value)
        {
            if (string.Equals(value, "Manual", StringComparison.OrdinalIgnoreCase))
                return TextEffectEntry.TriggerWhen.Manual;
            return TextEffectEntry.TriggerWhen.OnStart;
        }

        static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            return Enum.TryParse(value.Trim(), ignoreCase: true, out TEnum parsed) ? parsed : fallback;
        }

        static Vector2 ToVec2(KaplaoNicknameVec2Dto dto, Vector2 fallback)
        {
            return dto == null ? fallback : new Vector2(dto.X, dto.Y);
        }

        static Gradient BuildGradient(List<KaplaoNicknameGradientStopDto> stops)
        {
            var gradient = new Gradient();
            if (stops == null || stops.Count == 0)
            {
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.red, 0f),
                        new GradientColorKey(Color.yellow, 0.25f),
                        new GradientColorKey(Color.green, 0.5f),
                        new GradientColorKey(Color.cyan, 0.75f),
                        new GradientColorKey(Color.magenta, 1f)
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return gradient;
            }

            var colorKeys = new List<GradientColorKey>();
            var alphaKeys = new List<GradientAlphaKey>();
            for (int i = 0; i < stops.Count; i++)
            {
                KaplaoNicknameGradientStopDto stop = stops[i];
                if (stop == null || !TryParseHex(stop.Hex, out Color color))
                    continue;

                float t = Mathf.Clamp01(stop.T);
                colorKeys.Add(new GradientColorKey(color, t));
                alphaKeys.Add(new GradientAlphaKey(color.a, t));
            }

            if (colorKeys.Count == 0)
                return BuildGradient(null);

            if (colorKeys.Count == 1)
            {
                Color c = colorKeys[0].color;
                colorKeys.Add(new GradientColorKey(c, 1f));
                alphaKeys.Add(new GradientAlphaKey(c.a, 1f));
            }

            gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
            return gradient;
        }

        static AnimationCurve BuildCurve(KaplaoNicknameEffectDto dto)
        {
            if (dto?.EasingKeys != null && dto.EasingKeys.Count >= 2)
            {
                var keys = new Keyframe[dto.EasingKeys.Count];
                for (int i = 0; i < dto.EasingKeys.Count; i++)
                {
                    KaplaoNicknameEasingKeyDto key = dto.EasingKeys[i];
                    keys[i] = new Keyframe(key?.Time ?? 0f, key?.Value ?? 0f);
                }

                return new AnimationCurve(keys);
            }

            string easing = dto?.Easing?.Trim().ToLowerInvariant() ?? "linear";
            if (easing == "easein" || easing == "ease_in")
            {
                return new AnimationCurve(
                    new Keyframe(0f, 0f, 0f, 0f),
                    new Keyframe(1f, 1f, 2f, 2f));
            }

            if (easing == "easeout" || easing == "ease_out")
            {
                return new AnimationCurve(
                    new Keyframe(0f, 0f, 2f, 2f),
                    new Keyframe(1f, 1f, 0f, 0f));
            }

            if (easing == "easeinout" || easing == "ease_in_out")
                return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        public static bool TryParseHex(string hex, out Color color)
        {
            color = Color.white;
            if (string.IsNullOrWhiteSpace(hex))
                return false;

            string value = hex.Trim();
            if (value[0] != '#')
                value = "#" + value;

            return ColorUtility.TryParseHtmlString(value, out color);
        }

        static string JsonUtilityFingerprint(KaplaoNicknameStyleDto style)
        {
            // Stable-enough signature without serializing every nested object twice.
            try
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(style);
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }
    }
}
