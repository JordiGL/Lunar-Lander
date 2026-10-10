using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Fondo espacial procedural clásico: degradado de cielo, nebulosa y cuatro
    /// capas de estrellas con parallax y parpadeo. Para otro estilo de estrellas
    /// crea otra clase que herede de SpaceBackgroundBase e implemente BuildLayers().
    /// </summary>
    public sealed class SpaceBackground : SpaceBackgroundBase
    {
        private const int StarTexSize = 1024;

        [Header("Cielo")]
        [SerializeField] private Color skyTop = new Color(0.005f, 0.01f, 0.04f);
        [SerializeField] private Color skyBottom = new Color(0.05f, 0.02f, 0.09f);
        [SerializeField] private Color horizonGlow = new Color(0.10f, 0.05f, 0.16f);

        [Header("Estrellas")]
        [SerializeField, Range(0.1f, 3f)] private float starDensity = 1f;
        [SerializeField] private bool twinkle = true;
        [SerializeField, Range(0f, 1f)] private float twinkleDepth = 0.45f;
        [Tooltip("Deriva lenta y constante de las estrellas (unidades de textura por segundo).")]
        [SerializeField, Min(0f)] private float starDrift = 0.0015f;

        [Header("Nebulosa")]
        [SerializeField] private bool showNebula = true;
        [SerializeField] private Color nebulaColorA = new Color(0.50f, 0.15f, 0.80f);
        [SerializeField] private Color nebulaColorB = new Color(0.08f, 0.50f, 0.85f);
        [SerializeField, Range(0f, 1f)] private float nebulaIntensity = 0.55f;

        protected override Color CameraBackgroundColor => skyTop;

        protected override void BuildLayers()
        {
            BuildSky();
            if (showNebula) BuildNebula();
            BuildStars();
        }

        // ---- Cielo -------------------------------------------------------

        private void BuildSky()
        {
            const int w = 128, h = 256;
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = Color.Lerp(skyBottom, skyTop, Smooth(0f, 1f, t));
                float glow = Mathf.Pow(1f - t, 3f) * 0.7f;
                c += horizonGlow * glow;

                for (int x = 0; x < w; x++)
                {
                    float n = (Rand01() - 0.5f) * 1.5f / 255f;
                    px[y * w + x] = new Color32(
                        ToByte(c.r + n), ToByte(c.g + n), ToByte(c.b + n), 255);
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "BG_Sky",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);

            CreateLayer("Sky", tex, SortingOrderBase, 0f, 1f, false);
        }

        // ---- Nebulosa ----------------------------------------------------

        private void BuildNebula()
        {
            const int size = 512;
            int s1 = Rng.Next(), s2 = Rng.Next(), s3 = Rng.Next();
            Vector3 ca = V(nebulaColorA), cb = V(nebulaColorB);
            var px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;

                    float d = PeriodicFbm(u, v, 3, 5, s1);
                    d = Mathf.Clamp01((d - 0.40f) / 0.45f);
                    d = d * d * (3f - 2f * d);
                    d = Mathf.Pow(d, 1.35f);

                    float hue = PeriodicFbm(u, v, 2, 3, s2);
                    float dust = PeriodicFbm(u, v, 6, 4, s3);
                    float detail = 0.6f + 0.8f * dust;

                    Vector3 col = Vector3.Lerp(ca, cb, Smooth(0.35f, 0.65f, hue)) * detail;
                    float a = Mathf.Clamp01(d * nebulaIntensity * 0.55f * detail);

                    px[y * size + x] = new Color32(ToByte(col.x), ToByte(col.y), ToByte(col.z), ToByte(a));
                }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BG_Nebula",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);

            Layer l = CreateLayer("Nebula", tex, SortingOrderBase + 5, 0.012f, 0.7f, true);
            l.drift = 0.0006f;
        }

        // ---- Estrellas ---------------------------------------------------

        private void BuildStars()
        {
            int d(int n) => Mathf.Max(1, Mathf.RoundToInt(n * starDensity));

            Layer far = CreateLayer("StarsFar",
                MakeStarTexture(StarTexSize, d(1500), 0.55f, 0.8f, 0.25f, 2.2f, false),
                SortingOrderBase + 10, 0.010f, 1.1f, true);
            far.alpha = 0.9f;
            far.drift = starDrift * 0.5f;
            far.reactsToAudio = true;

            Layer mid = CreateLayer("StarsMid",
                MakeStarTexture(StarTexSize, d(520), 0.8f, 1.1f, 0.35f, 1.8f, false),
                SortingOrderBase + 11, 0.025f, 1.5f, true);
            mid.drift = starDrift * 0.8f;
            SetTwinkle(mid, twinkle, 0.9f, twinkleDepth);
            mid.reactsToAudio = true;

            Layer b1 = CreateLayer("StarsBright1",
                MakeStarTexture(StarTexSize, d(45), 1.1f, 1.6f, 0.6f, 1.2f, true),
                SortingOrderBase + 12, 0.045f, 2.0f, true);
            b1.drift = starDrift;
            SetTwinkle(b1, twinkle, 1.7f, twinkleDepth);
            b1.reactsToAudio = true;

            Layer b2 = CreateLayer("StarsBright2",
                MakeStarTexture(StarTexSize, d(40), 1.1f, 1.6f, 0.6f, 1.2f, true),
                SortingOrderBase + 13, 0.060f, 2.3f, true);
            b2.drift = starDrift * 1.2f;
            SetTwinkle(b2, twinkle, 2.6f, twinkleDepth);
            b2.reactsToAudio = true;
        }

        private Vector3 StarColor()
        {
            float r = Rand01();
            if (r < 0.55f) return new Vector3(0.80f, 0.88f, 1.00f);
            if (r < 0.80f) return new Vector3(1.00f, 0.97f, 0.92f);
            if (r < 0.92f) return new Vector3(1.00f, 0.85f, 0.55f);
            return new Vector3(1.00f, 0.60f, 0.45f);
        }

        private Texture2D MakeStarTexture(int size, int count, float minSigma, float maxSigma,
                                          float minBright, float brightPow, bool spikes)
        {
            var r = new float[size * size];
            var g = new float[size * size];
            var b = new float[size * size];

            void Add(int x, int y, Vector3 c)
            {
                x = ((x % size) + size) % size;
                y = ((y % size) + size) % size;
                int i = y * size + x;
                r[i] += c.x; g[i] += c.y; b[i] += c.z;
            }

            for (int s = 0; s < count; s++)
            {
                float cx = Rand01() * size;
                float cy = Rand01() * size;
                float sigma = Mathf.Lerp(minSigma, maxSigma, Rand01());
                float bright = Mathf.Lerp(minBright, 1f, Mathf.Pow(Rand01(), brightPow));
                Vector3 col = StarColor();

                int ix = Mathf.FloorToInt(cx);
                int iy = Mathf.FloorToInt(cy);
                int rad = Mathf.CeilToInt(sigma * 3f);

                for (int dy = -rad; dy <= rad; dy++)
                {
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        float ddx = ix + dx + 0.5f - cx;
                        float ddy = iy + dy + 0.5f - cy;
                        float v = bright * Mathf.Exp(-(ddx * ddx + ddy * ddy) / (2f * sigma * sigma));
                        if (v < 0.004f) continue;
                        Add(ix + dx, iy + dy, col * v);
                    }
                }

                if (spikes)
                {
                    int len = Mathf.RoundToInt(6f + 10f * bright);
                    for (int k = 1; k <= len; k++)
                    {
                        float f = bright * 0.55f * Mathf.Exp(-k / (len * 0.4f));
                        Vector3 c = col * f;
                        Add(ix + k, iy, c);
                        Add(ix - k, iy, c);
                        Add(ix, iy + k, c);
                        Add(ix, iy - k, c);
                    }
                }
            }

            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float m = Mathf.Max(r[i], Mathf.Max(g[i], b[i]));
                if (m < 0.003f) { px[i] = new Color32(0, 0, 0, 0); continue; }

                float a = Mathf.Min(1f, m);
                px[i] = new Color32(ToByte(r[i] / a), ToByte(g[i] / a), ToByte(b[i] / a), ToByte(a));
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BG_Stars",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}