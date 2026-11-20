using System.Collections.Generic;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Soft dust bursts for runner landing / walking. Color matches the block underfoot.
    /// </summary>
    public sealed class RunnerDustBurst : MonoBehaviour
    {
        public enum Kind : byte
        {
            Landing = 0,
            Walk = 1
        }

        sealed class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public Vector3 StartScale;
            public float Age;
            public float Lifetime;
            public Color BaseColor;
        }

        readonly List<Particle> particles = new();
        float gravity;
        static Sprite softBlobSprite;

        public static void SpawnLanding(Vector2 worldPosition, Color32 color, int count = 10)
        {
            Spawn(worldPosition, color, count, Kind.Landing, moveSign: 0f);
        }

        public static void SpawnWalk(Vector2 worldPosition, Color32 color, float moveSign, int count = 2)
        {
            Spawn(worldPosition, color, count, Kind.Walk, moveSign);
        }

        static void Spawn(Vector2 worldPosition, Color32 color, int count, Kind kind, float moveSign)
        {
            var root = new GameObject(kind == Kind.Landing ? "RunnerLandingDust" : "RunnerWalkDust");
            root.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);
            var burst = root.AddComponent<RunnerDustBurst>();
            burst.Build(color, count, kind, moveSign);
        }

        void Build(Color32 color, int count, Kind kind, float moveSign)
        {
            Sprite sprite = GetSoftBlobSprite();
            Color baseColor = color;
            baseColor.a = 1f;

            bool landing = kind == Kind.Landing;
            gravity = landing ? 9f : 14f;
            count = landing ? Mathf.Clamp(count, 6, 14) : Mathf.Clamp(count, 1, 3);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("P");
                go.transform.SetParent(transform, false);

                float side;
                float speedX;
                float speedY;
                float size;

                if (landing)
                {
                    side = Random.value < 0.5f ? -1f : 1f;
                    speedX = side * Random.Range(3.0f, 6.5f);
                    speedY = Random.Range(2.0f, 5.0f);
                    size = Random.Range(0.38f, 0.72f);
                }
                else
                {
                    // Kick mostly opposite to run direction.
                    float away = Mathf.Abs(moveSign) > 0.01f ? -Mathf.Sign(moveSign) : (Random.value < 0.5f ? -1f : 1f);
                    speedX = away * Random.Range(1.2f, 2.8f) + Random.Range(-0.4f, 0.4f);
                    speedY = Random.Range(0.8f, 2.0f);
                    size = Random.Range(0.18f, 0.36f);
                }

                Vector3 startScale = new Vector3(size, size * Random.Range(0.75f, 1.1f), 1f);
                float spreadX = landing ? 0.45f : 0.2f;
                go.transform.localPosition = new Vector3(Random.Range(-spreadX, spreadX), Random.Range(0f, 0.08f), 0f);
                go.transform.localScale = startScale;
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f));

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = baseColor;
                renderer.sortingOrder = -1;

                particles.Add(new Particle
                {
                    Transform = go.transform,
                    Renderer = renderer,
                    Velocity = new Vector2(speedX, speedY),
                    StartScale = startScale,
                    Age = 0f,
                    Lifetime = landing ? Random.Range(0.32f, 0.5f) : Random.Range(0.22f, 0.36f),
                    BaseColor = baseColor
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Age += dt;
                if (p.Age >= p.Lifetime || p.Transform == null)
                {
                    if (p.Transform != null)
                        Destroy(p.Transform.gameObject);
                    particles.RemoveAt(i);
                    continue;
                }

                p.Velocity.y -= gravity * dt;
                p.Velocity.x *= 1f - 1.6f * dt;
                p.Transform.position += (Vector3)(p.Velocity * dt);

                float t = Mathf.Clamp01(p.Age / p.Lifetime);
                Color c = p.BaseColor;
                c.a = Mathf.Lerp(p.BaseColor.a, 0f, t * t);
                p.Renderer.color = c;
                p.Transform.localScale = Vector3.Lerp(p.StartScale, p.StartScale * 0.2f, t);
            }

            if (particles.Count == 0)
                Destroy(gameObject);
        }

        static Sprite GetSoftBlobSprite()
        {
            if (softBlobSprite != null)
                return softBlobSprite;

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
            softBlobSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return softBlobSprite;
        }
    }
}
