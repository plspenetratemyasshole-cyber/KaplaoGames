using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Local-only red pulse at the immortality spike-save contact point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ImmortalitySpikePulse : MonoBehaviour
    {
        const float DurationSeconds = 0.5f;
        const float EndScaleMultiplier = 0.42f / 0.18f;

        static Sprite softCircleSprite;

        SpriteRenderer spriteRenderer;
        float age;
        float startScale;
        float endScale;

        public static void Spawn(Vector2 worldPosition)
        {
            float size = Mathf.Max(0.01f, RunnerCheatsState.SpikePulseSize);

            var go = new GameObject("ImmortalitySpikePulse");
            go.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            go.transform.localScale = Vector3.one * size;

            var pulse = go.AddComponent<ImmortalitySpikePulse>();
            pulse.startScale = size;
            pulse.endScale = size * EndScaleMultiplier;
            pulse.Build();
        }

        void Build()
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetSoftCircleSprite();
            spriteRenderer.color = new Color(1f, 0.15f, 0.15f, 0.9f);
            spriteRenderer.sortingOrder = 50;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / DurationSeconds);

            // Soft pulse: expand and fade out.
            float scale = Mathf.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, t));
            transform.localScale = Vector3.one * scale;

            if (spriteRenderer != null)
            {
                Color c = spriteRenderer.color;
                c.a = Mathf.Lerp(0.9f, 0f, t * t);
                spriteRenderer.color = c;
            }

            if (t >= 1f)
                Destroy(gameObject);
        }

        static Sprite GetSoftCircleSprite()
        {
            if (softCircleSprite != null)
                return softCircleSprite;

            const int size = 48;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply(false, true);
            softCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return softCircleSprite;
        }
    }
}
