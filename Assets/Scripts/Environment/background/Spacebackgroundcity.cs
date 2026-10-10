using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Fondo cyberpunk SIN estrellas: cielo rojizo, sol sintético con franjas,
    /// dos planos de rascacielos con ventanas encendidas y lluvia neón.
    /// El sol y las ventanas laten con el bombo y la caja (audio reactivo de la base).
    /// </summary>
    public sealed class SpaceBackgroundCity : SpaceBackgroundBase
    {
        private const int CityTexSize = 1024;
        private const int RainTexSize = 512;
        private const float AssumedAspect = 16f / 9f; // Para que el sol salga redondo en 16:9

        [Header("Cielo")]
        [SerializeField] private Color skyTop = new Color(0.02f, 0.00f, 0.05f);
        [SerializeField] private Color skyBottom = new Color(0.22f, 0.02f, 0.18f);
        [SerializeField] private Color horizonGlow = new Color(0.55f, 0.08f, 0.22f);

        [Header("Sol sintético")]
        [SerializeField] private bool showSun = true;
        [SerializeField] private Color sunTop = new Color(1.00f, 0.60f, 0.12f);
        [SerializeField] private Color sunBottom = new Color(1.00f, 0.10f, 0.55f);
        [SerializeField, Range(0.1f, 0.6f)] private float sunSize = 0.30f;
        [SerializeField, Range(0f, 1f)] private float sunX = 0.68f;
        [SerializeField, Range(0f, 1f)] private float sunY = 0.42f;
        [SerializeField, Range(0f, 1f)] private float sunGlow = 0.35f;

        [Header("Ciudad")]
        [SerializeField] private bool showCity = true;
        [SerializeField] private Color farBody = new Color(0.10f, 0.02f, 0.18f);
        [SerializeField] private Color nearBody = new Color(0.02f, 0.00f, 0.05f);
        [SerializeField] private Color neonEdgeA = new Color(0.10f, 0.90f, 1.00f);
        [SerializeField] private Color neonEdgeB = new Color(1.00f, 0.20f, 0.75f);
        [SerializeField, Range(0f, 1f)] private float windowDensity = 0.28f;

        [Header("Lluvia")]
        [SerializeField] private bool showRain = true;
        [SerializeField] private Color rainColor = new Color(0.45f, 0.85f, 1.00f);
        [SerializeField] private Color rainAccent = new Color(1.00f, 0.30f, 0.80f);
        [SerializeField, Range(0.1f, 3f)] private float rainDensity = 1f;
        [SerializeField, Range(0.2f, 3f)] private float rainSpeed = 1f;

        protected override Color CameraBackgroundColor => skyTop;

        protected override void BuildLayers()
        {
            BuildSky();
            if (showSun) BuildSun();

            if (showCity)
            {
                // Plano lejano: más bajo, estrecho y con bruma morada.
                Layer farWindows = BuildCity("CityFar", SortingOrderBase + 4, farBody,
                    0.14f, 0.34f, 40, 90, windowDensity * 0.7f, 0.020f, false);
                farWindows.reactsToAudio = true;

                // Plano cercano: más alto, oscuro y con filo de neón.
                Layer nearWindows = BuildCity("CityNear", SortingOrderBase + 7, nearBody,
                    0.20f, 0.58f, 70, 150, windowDensity, 0.045f, true);
                nearWindows.reactsToAudio = true;
                SetTwinkle(nearWindows, true, 0.7f, 0.25f);
            }

            if (showRain)
            {
                BuildRain("RainFar", SortingOrderBase + 9, Mathf.RoundToInt(260 * rainDensity),
                    14, 34, 1, 1.1f * rainSpeed, 1.5f, 0.060f, 0.55f);
                BuildRain("RainNear", SortingOrderBase + 14, Mathf.RoundToInt(120 * rainDensity),
                    30, 70, 2, 2.0f * rainSpeed, 1.0f, 0.100f, 0.9f);
            }
        }

        // ---- Utilidades ----------------------------------------------------

        private static Color32 ToColor32(Color c, float alpha = 1f) =>
            new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(alpha));

        private static Texture2D MakeTexture(string name, Color32[] px, int w, int h, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private int RandInt(int min, int max) => Rng.Next(min, max + 1);

        // ---- Cielo ---------------------------------------------------------

        private void BuildSky()
        {
            const int w = 64, h = 256;
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);                 // 0 = horizonte, 1 = cénit
                Color c = Color.Lerp(skyBottom, skyTop, Smooth(0f, 0.9f, t));
                c += horizonGlow * (Mathf.Pow(1f - t, 3f) * 0.9f);

                for (int x = 0; x < w; x++) px[y * w + x] = ToColor32(c);
            }

            CreateLayer("Sky", MakeTexture("BG_CitySky", px, w, h, TextureWrapMode.Clamp),
                SortingOrderBase, 0f, 1f, false);
        }

        // ---- Sol sintético ---------------------------------------------------

        private void BuildSun()
        {
            const int w = 1024, h = 512;
            var px = new Color32[w * h];

            float cx = sunX * w;
            float cy = sunY * h;
            float ry = sunSize * h;
            float rx = ry * (2f / AssumedAspect);             // Corrige el estiramiento de la textura

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy); // 1 = borde del disco

                    float alpha;
                    Color col;

                    if (dist <= 1f)
                    {
                        // Franjas horizontales que se ensanchan hacia abajo.
                        float below = (cy - y) / ry;          // 0 en el centro, 1 en la base
                        if (below > 0.08f)
                        {
                            float k = below * 7f;
                            float frac = k - Mathf.Floor(k);
                            if (frac < Mathf.Lerp(0.10f, 0.65f, below)) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                        }

                        float tv = Mathf.InverseLerp(cy - ry, cy + ry, y); // 0 abajo, 1 arriba
                        col = Color.Lerp(sunBottom, sunTop, tv);
                        alpha = 0.95f * Smooth(1f, 0.96f, dist);
                    }
                    else
                    {
                        col = Color.Lerp(sunBottom, sunTop, 0.4f);
                        alpha = sunGlow * Mathf.Exp(-(dist - 1f) * 5f);
                    }

                    px[y * w + x] = alpha < 0.004f ? new Color32(0, 0, 0, 0) : ToColor32(col, alpha);
                }
            }

            Layer sun = CreateLayer("Sun", MakeTexture("BG_CitySun", px, w, h, TextureWrapMode.Clamp),
                SortingOrderBase + 2, 0f, 1f, false);
            sun.reactsToAudio = true;                         // el sol late con el ritmo
        }

        // ---- Ciudad ----------------------------------------------------------

        private Color32 WindowColor()
        {
            float r = Rand01();
            if (r < 0.50f) return new Color32(255, 180, 70, 255);   // ámbar
            if (r < 0.78f) return ToColor32(neonEdgeA);
            return ToColor32(neonEdgeB);
        }

        /// <summary>
        /// Crea dos capas: la silueta de los edificios (siempre estable) y las ventanas
        /// (para que puedan parpadear y latir sin tocar la silueta). Devuelve la de ventanas.
        /// </summary>
        private Layer BuildCity(string name, int order, Color body, float minH, float maxH,
                                int minW, int maxW, float windowChance, float parallax, bool outline)
        {
            int n = CityTexSize;
            var sil = new Color32[n * n];
            var win = new Color32[n * n];
            Color32 bodyC = ToColor32(body);
            Color32 edgeA = ToColor32(neonEdgeA);
            Color32 edgeB = ToColor32(neonEdgeB);

            int x = 0;
            while (x < n)
            {
                int w = Mathf.Min(RandInt(minW, maxW), n - x);

                if (w < 8) { x += w; continue; }
                if (Rand01() < 0.12f) { x += w / 2 + 1; continue; }   // hueco entre edificios

                int h = Mathf.RoundToInt(n * Mathf.Lerp(minH, maxH, Mathf.Pow(Rand01(), 1.6f)));
                Color32 edge = Rand01() < 0.5f ? edgeA : edgeB;

                for (int xx = x; xx < x + w; xx++)
                    for (int yy = 0; yy < h; yy++)
                        sil[yy * n + xx] = bodyC;

                if (outline)
                    for (int xx = x; xx < x + w; xx++)
                        for (int t = 0; t < 2; t++)
                            sil[(h - 1 - t) * n + xx] = edge;

                if (Rand01() < 0.3f)                                   // antena con baliza
                {
                    int ax = x + w / 2;
                    int ah = Mathf.Min(RandInt(18, 46), n - 1 - h);
                    for (int yy = h; yy < h + ah; yy++)
                        sil[yy * n + ax] = (yy >= h + ah - 3) ? edge : bodyC;
                }

                for (int wy = 12; wy < h - 14; wy += 14)               // ventanas
                {
                    for (int wx = x + 5; wx + 5 < x + w - 3; wx += 10)
                    {
                        if (Rand01() >= windowChance) continue;
                        Color32 wc = WindowColor();
                        for (int dy = 0; dy < 6; dy++)
                            for (int dx = 0; dx < 5; dx++)
                                win[(wy + dy) * n + wx + dx] = wc;
                    }
                }

                x += w;
            }

            Layer body_ = CreateLayer(name + "Body", MakeTexture("BG_" + name + "Body", sil, n, n, TextureWrapMode.Repeat),
                order, parallax, 1f, true);
            body_.parallaxYScale = 0f;                                 // el horizonte no sube con la cámara

            Layer windows = CreateLayer(name + "Windows", MakeTexture("BG_" + name + "Win", win, n, n, TextureWrapMode.Repeat),
                order + 1, parallax, 1f, true);
            windows.parallaxYScale = 0f;
            return windows;
        }

        // ---- Lluvia ----------------------------------------------------------

        private void BuildRain(string name, int order, int count, int minLen, int maxLen,
                               int widthPx, float fallSpeed, float tilesY, float parallax, float opacity)
        {
            int n = RainTexSize;
            var px = new Color32[n * n];

            for (int i = 0; i < count; i++)
            {
                int x = Rng.Next(n);
                int y0 = Rng.Next(n);
                int len = RandInt(minLen, maxLen);
                float bright = Mathf.Lerp(0.4f, 1f, Rand01()) * opacity;
                Color c = Rand01() < 0.2f ? rainAccent : rainColor;

                for (int k = 0; k < len; k++)
                {
                    // La cabeza de la gota es el punto más bajo (k = 0) y la estela sube y se apaga.
                    float a = bright * (1f - k / (float)len);
                    int yy = (y0 + k) % n;
                    for (int dx = 0; dx < widthPx; dx++)
                    {
                        int xx = (x + dx) % n;
                        byte al = ToByte(a);
                        if (al > px[yy * n + xx].a) px[yy * n + xx] = ToColor32(c, a);
                    }
                }
            }

            Layer rain = CreateLayer(name, MakeTexture("BG_" + name, px, n, n, TextureWrapMode.Repeat),
                order, parallax, tilesY, true);
            rain.parallaxYScale = 0f;
            rain.driftY = fallSpeed;                                   // cae continuamente
            rain.drift = 0.012f;                                       // ligera inclinación
        }
    }
}