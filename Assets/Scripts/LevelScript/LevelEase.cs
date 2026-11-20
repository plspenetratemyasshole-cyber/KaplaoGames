using System;
using UnityEngine;

namespace KaplaoGames.LevelScript
{
    public enum LevelEase
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    public static class LevelEaseUtility
    {
        public static bool TryParse(string value, out LevelEase ease)
        {
            ease = LevelEase.Linear;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "linear":
                    ease = LevelEase.Linear;
                    return true;
                case "easein":
                    ease = LevelEase.EaseIn;
                    return true;
                case "easeout":
                    ease = LevelEase.EaseOut;
                    return true;
                case "easeinout":
                    ease = LevelEase.EaseInOut;
                    return true;
                default:
                    return false;
            }
        }

        public static float Evaluate(LevelEase ease, float t)
        {
            t = Mathf.Clamp01(t);
            return ease switch
            {
                LevelEase.EaseIn => t * t,
                LevelEase.EaseOut => 1f - (1f - t) * (1f - t),
                LevelEase.EaseInOut => t < 0.5f
                    ? 2f * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f,
                _ => t
            };
        }
    }
}
