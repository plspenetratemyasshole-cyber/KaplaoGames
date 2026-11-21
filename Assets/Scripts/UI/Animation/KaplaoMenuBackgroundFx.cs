using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KaplaoGames.UI.Theme;

namespace KaplaoGames.UI.Animation
{
    /// <summary>
    /// Atmospheric animated background for MainMenu.
    /// Spawns soft glowing colored orbs and drifting firefly particles behind all menus.
    /// Frame-rate independent and non-blocking for raycasts.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Kaplao/UI/Menu Background FX")]
    public class KaplaoMenuBackgroundFx : MonoBehaviour
    {
        [Header("Atmosphere Settings")]
        [SerializeField] bool transparentForParallax = true;
        [SerializeField] int fireflyCount = 20;

        readonly List<OrbState> orbs = new();
        readonly List<FireflyState> fireflies = new();
        RectTransform canvasRect;
        Vector2 currentMouseOffset;

        struct OrbState
        {
            public RectTransform transform;
            public Vector2 center;
            public Vector2 radius;
            public float speed;
            public float phase;
        }

        struct FireflyState
        {
            public RectTransform transform;
            public Image image;
            public float x;
            public float y;
            public float speedY;
            public float freqX;
            public float ampX;
            public float baseAlpha;
            public float pulseSpeed;
            public float phase;
        }

        void Awake()
        {
            canvasRect = transform as RectTransform;
            transform.SetAsFirstSibling();
            BuildFx();
        }

        public void BuildFx()
        {
            // Clear any old generated objects
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("FX_"))
                    Destroy(child.gameObject);
            }

            orbs.Clear();
            fireflies.Clear();

            // 1. Dark Vignette / Base Tint
            Image baseImg = GetComponent<Image>();
            if (baseImg == null)
                baseImg = gameObject.AddComponent<Image>();

            baseImg.sprite = null;
            baseImg.color = transparentForParallax
                ? new Color(0x07 / 255f, 0x0B / 255f, 0x14 / 255f, 0.45f)
                : new Color(0x07 / 255f, 0x0B / 255f, 0x14 / 255f, 1f);
            baseImg.raycastTarget = false;

            // 2. Glowing Orbs
            CreateOrb("FX_Orb_Cyan", new Color(0x0E / 255f, 0xA5 / 255f, 0xE9 / 255f, 0.08f), new Vector2(-280f, 120f), 520f, 0.35f, 0f);
            CreateOrb("FX_Orb_Indigo", new Color(0x63 / 255f, 0x66 / 255f, 0xF1 / 255f, 0.08f), new Vector2(320f, -140f), 580f, 0.28f, 2.1f);
            CreateOrb("FX_Orb_Emerald", new Color(0x10 / 255f, 0xB9 / 255f, 0x81 / 255f, 0.05f), new Vector2(0f, 220f), 460f, 0.42f, 4.3f);

            // 3. Fireflies
            for (int i = 0; i < fireflyCount; i++)
            {
                CreateFirefly(i);
            }
        }

        void CreateOrb(string name, Color color, Vector2 center, float size, float speed, float phase)
        {
            GameObject orbObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            orbObj.transform.SetParent(transform, false);

            RectTransform rt = orbObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = center;

            Image img = orbObj.GetComponent<Image>();
            img.sprite = KaplaoUiSprites.SoftShadow(18, 16);
            img.type = Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;

            orbs.Add(new OrbState
            {
                transform = rt,
                center = center,
                radius = new Vector2(90f, 60f),
                speed = speed,
                phase = phase
            });
        }

        void CreateFirefly(int index)
        {
            GameObject ffObj = new GameObject($"FX_Firefly_{index}", typeof(RectTransform), typeof(Image));
            ffObj.transform.SetParent(transform, false);

            RectTransform rt = ffObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float s = UnityEngine.Random.Range(3f, 6.5f);
            rt.sizeDelta = new Vector2(s, s);

            Image img = ffObj.GetComponent<Image>();
            img.sprite = KaplaoUiSprites.Circle();
            img.color = new Color(0.22f, 0.74f, 0.97f, 0.5f);
            img.raycastTarget = false;

            fireflies.Add(new FireflyState
            {
                transform = rt,
                image = img,
                x = UnityEngine.Random.Range(-550f, 550f),
                y = UnityEngine.Random.Range(-450f, 450f),
                speedY = UnityEngine.Random.Range(18f, 42f),
                freqX = UnityEngine.Random.Range(0.8f, 2.2f),
                ampX = UnityEngine.Random.Range(15f, 35f),
                baseAlpha = UnityEngine.Random.Range(0.25f, 0.65f),
                pulseSpeed = UnityEngine.Random.Range(1.2f, 3.2f),
                phase = UnityEngine.Random.Range(0f, 6.28f)
            });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            // Live mouse parallax response (opposite to cursor, ±18..35px)
            Vector2 targetOffset = Vector2.zero;
            if (Screen.width > 0 && Screen.height > 0)
            {
                Vector2 mouseNorm = new Vector2(
                    Mathf.Clamp((Input.mousePosition.x / Screen.width) - 0.5f, -0.5f, 0.5f),
                    Mathf.Clamp((Input.mousePosition.y / Screen.height) - 0.5f, -0.5f, 0.5f)
                );
                targetOffset = new Vector2(-mouseNorm.x * 32f, -mouseNorm.y * 24f);
            }
            currentMouseOffset = Vector2.Lerp(currentMouseOffset, targetOffset, 1f - Mathf.Exp(-4.5f * dt));

            // Move Orbs
            for (int i = 0; i < orbs.Count; i++)
            {
                OrbState o = orbs[i];
                if (o.transform != null)
                {
                    float angle = t * o.speed + o.phase;
                    Vector2 offset = new Vector2(Mathf.Sin(angle) * o.radius.x, Mathf.Cos(angle * 0.8f) * o.radius.y);
                    o.transform.anchoredPosition = o.center + offset + currentMouseOffset * 1.15f;
                }
            }

            // Move Fireflies
            for (int i = 0; i < fireflies.Count; i++)
            {
                FireflyState f = fireflies[i];
                if (f.transform != null)
                {
                    f.y += f.speedY * dt;
                    if (f.y > 520f)
                    {
                        f.y = -520f;
                        f.x = UnityEngine.Random.Range(-550f, 550f);
                    }

                    float offsetX = Mathf.Sin(t * f.freqX + f.phase) * f.ampX;
                    f.transform.anchoredPosition = new Vector2(f.x + offsetX, f.y) + currentMouseOffset * 0.75f;

                    float alpha = f.baseAlpha * (0.6f + 0.4f * Mathf.Sin(t * f.pulseSpeed + f.phase));
                    Color c = f.image.color;
                    c.a = alpha;
                    f.image.color = c;

                    fireflies[i] = f;
                }
            }
        }
    }
}
