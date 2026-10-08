using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Fondo espacial 100% procedural: degradado de cielo, nebulosa, estrellas con
    /// parallax y parpadeo, y estrellas fugaces (sin planetas).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class SpaceBackground : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Configuración
        // ------------------------------------------------------------------

        [Header("General")]
        [Tooltip("Si se deja vacío usa la cámara principal.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("0 = aleatoria en cada partida.")]
        [SerializeField] private int seed = 0;

        [Tooltip("Mitad del alto (en unidades) de la pantalla de referencia. Solo escala el fondo.")]
        [SerializeField, Min(2f)] private float referenceSize = 14f;

        [Tooltip("Orden de dibujado del fondo. Debe ser menor que el del terreno.")]
        [SerializeField] private int sortingOrderBase = -200;

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

        [Header("Estrellas fugaces")]
        [SerializeField] private bool shootingStars = true;
        [SerializeField, Min(0.5f)] private float shootingMinInterval = 3f;
        [SerializeField, Min(0.5f)] private float shootingMaxInterval = 9f;

        // ------------------------------------------------------------------
        // Tipos internos
        // ------------------------------------------------------------------

        private sealed class Layer
        {
            public GameObject go;
            public Mesh mesh;
            public Material mat;
            public Texture2D tex;
            public readonly Vector2[] uv = new Vector2[4];
            public bool scrolls = true;
            public float parallax;
            public float tilesY = 1f;
            public float drift;
            public float alpha = 1f;
            public bool twinkle;
            public float twinkleSpeed;
            public float twinklePhase;
        }

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------

        private const int StarTexSize = 1024;
        private const float ViewMargin = 1.03f;

        private Camera cam;
        private System.Random rng;
        private Transform root;
        private Material spriteMaterial;

        private readonly List<Layer> layers = new List<Layer>();

        // Estrella fugaz
        private LineRenderer shootLine;
        private bool shootActive;
        private float shootTimer, shootT, shootDur, shootSpeed, shootLen;
        private Vector2 shootStart, shootDir;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning("SpaceBackground: no se encontró ninguna cámara.");
                enabled = false;
                return;
            }

            rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = skyTop;

            BuildRoot();
            BuildSky();
            if (showNebula) BuildNebula();
            BuildStars();
            if (shootingStars) BuildShootingStar();

            shootTimer = Mathf.Lerp(shootingMinInterval, shootingMaxInterval, Rand01());
            UpdateFrame();
        }

        private void Update()
        {
            UpdateShooting(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (cam == null || root == null) return;
            UpdateFrame();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].mesh != null) Destroy(layers[i].mesh);
                if (layers[i].mat != null) Destroy(layers[i].mat);
                if (layers[i].tex != null) Destroy(layers[i].tex);
            }

            if (spriteMaterial != null) Destroy(spriteMaterial);
            if (root != null) Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------

        private float Rand01() => (float)rng.NextDouble();

        private static Shader SpriteShader()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s;
        }

        private void BuildRoot()
        {
            var go = new GameObject("SpaceBackground_Root");
            root = go.transform;
            root.SetParent(cam.transform, false);

            float depth = Mathf.Clamp(cam.farClipPlane * 0.9f, 5f, 100f);
            root.localPosition = new Vector3(0f, 0f, depth);
            root.localRotation = Quaternion.identity;

            spriteMaterial = new Material(SpriteShader()) { name = "SpaceBackground Sprite" };
        }

        private Mesh BuildQuadMesh(string name)
        {
            var mesh = new Mesh { name = "SpaceBG_" + name };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Layer CreateLayer(string name, Texture2D tex, int order, float parallax, float tilesY, bool scrolls)
        {
            var layer = new Layer
            {
                tex = tex,
                parallax = parallax,
                tilesY = tilesY,
                scrolls = scrolls
            };

            layer.go = new GameObject("BG_" + name);
            layer.go.transform.SetParent(root, false);

            layer.mesh = BuildQuadMesh(name);
            layer.go.AddComponent<MeshFilter>().sharedMesh = layer.mesh;

            layer.mat = new Material(SpriteShader()) { name = "BG_" + name, mainTexture = tex };

            var mr = layer.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = layer.mat;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            layers.Add(layer);
            return layer;
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

            CreateLayer("Sky", tex, sortingOrderBase, 0f, 1f, false);
        }

        // ---- Nebulosa ----------------------------------------------------

        private void BuildNebula()
        {
            const int size = 512;
            int s1 = rng.Next(), s2 = rng.Next(), s3 = rng.Next();
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

            Layer l = CreateLayer("Nebula", tex, sortingOrderBase + 5, 0.012f, 0.7f, true);
            l.drift = 0.0006f;
        }

        // ---- Estrellas ---------------------------------------------------

        private void BuildStars()
        {
            int d(int n) => Mathf.Max(1, Mathf.RoundToInt(n * starDensity));

            Layer far = CreateLayer("StarsFar",
                MakeStarTexture(StarTexSize, d(1500), 0.55f, 0.8f, 0.25f, 2.2f, false),
                sortingOrderBase + 10, 0.010f, 1.1f, true);
            far.alpha = 0.9f;
            far.drift = starDrift * 0.5f;

            Layer mid = CreateLayer("StarsMid",
                MakeStarTexture(StarTexSize, d(520), 0.8f, 1.1f, 0.35f, 1.8f, false),
                sortingOrderBase + 11, 0.025f, 1.5f, true);
            mid.drift = starDrift * 0.8f;
            SetTwinkle(mid, 0.9f);

            Layer b1 = CreateLayer("StarsBright1",
                MakeStarTexture(StarTexSize, d(45), 1.1f, 1.6f, 0.6f, 1.2f, true),
                sortingOrderBase + 12, 0.045f, 2.0f, true);
            b1.drift = starDrift;
            SetTwinkle(b1, 1.7f);

            Layer b2 = CreateLayer("StarsBright2",
                MakeStarTexture(StarTexSize, d(40), 1.1f, 1.6f, 0.6f, 1.2f, true),
                sortingOrderBase + 13, 0.060f, 2.3f, true);
            b2.drift = starDrift * 1.2f;
            SetTwinkle(b2, 2.6f);
        }

        private void SetTwinkle(Layer l, float speed)
        {
            l.twinkle = twinkle;
            l.twinkleSpeed = speed;
            l.twinklePhase = Rand01() * 6.2831f;
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

        // ---- Estrella fugaz ----------------------------------------------

        private void BuildShootingStar()
        {
            var go = new GameObject("BG_ShootingStar");
            go.transform.SetParent(root, false);

            shootLine = go.AddComponent<LineRenderer>();
            shootLine.useWorldSpace = false;
            shootLine.positionCount = 2;
            shootLine.startWidth = 0f;
            shootLine.endWidth = 0.14f;
            shootLine.numCapVertices = 2;
            shootLine.sharedMaterial = spriteMaterial;
            shootLine.sortingOrder = sortingOrderBase + 50;
            shootLine.shadowCastingMode = ShadowCastingMode.Off;
            shootLine.receiveShadows = false;
            shootLine.enabled = false;
        }

        private void StartShooting()
        {
            float halfH = referenceSize;
            float halfW = referenceSize * Mathf.Max(cam.aspect, 1.6f);

            float side = Rand01() < 0.5f ? -1f : 1f;
            float angle = Mathf.Lerp(15f, 35f, Rand01()) * Mathf.Deg2Rad;

            shootDir = new Vector2(side * Mathf.Cos(angle), -Mathf.Sin(angle));
            shootStart = new Vector2(
                Mathf.Lerp(-0.9f, 0.9f, Rand01()) * halfW - side * halfW * 0.2f,
                Mathf.Lerp(0.3f, 1.0f, Rand01()) * halfH);

            shootSpeed = Mathf.Lerp(35f, 60f, Rand01());
            shootLen = Mathf.Lerp(4f, 8f, Rand01());
            shootDur = Mathf.Lerp(0.7f, 1.2f, Rand01());
            shootT = 0f;
            shootActive = true;
            shootLine.enabled = true;
        }

        private void UpdateShooting(float dt)
        {
            if (!shootingStars || shootLine == null) return;

            if (!shootActive)
            {
                shootTimer -= dt;
                if (shootTimer <= 0f) StartShooting();
                return;
            }

            shootT += dt;
            float k = shootT / shootDur;
            if (k >= 1f)
            {
                shootActive = false;
                shootLine.enabled = false;
                shootTimer = Mathf.Lerp(shootingMinInterval, shootingMaxInterval, Rand01());
                return;
            }

            float pulse = Mathf.Sin(Mathf.PI * k);
            Vector2 head = shootStart + shootDir * (shootSpeed * shootT);
            Vector2 tail = head - shootDir * (shootLen * (0.35f + 0.65f * pulse));

            shootLine.SetPosition(0, new Vector3(tail.x, tail.y, 0f));
            shootLine.SetPosition(1, new Vector3(head.x, head.y, 0f));
            shootLine.startColor = new Color(0.6f, 0.8f, 1f, 0f);
            shootLine.endColor = new Color(1f, 1f, 1f, pulse);
        }

        // ------------------------------------------------------------------
        // Actualización por frame
        // ------------------------------------------------------------------

        private void UpdateFrame()
        {
            float aspect = cam.aspect;
            float k = cam.orthographicSize / referenceSize;
            root.localScale = new Vector3(k, k, 1f);

            Vector2 camPos = cam.transform.position;
            float t = Time.time;

            float quadW = 2f * referenceSize * aspect * ViewMargin;
            float quadH = 2f * referenceSize * ViewMargin;

            for (int i = 0; i < layers.Count; i++)
            {
                Layer l = layers[i];
                l.go.transform.localScale = new Vector3(quadW, quadH, 1f);

                float tilesX = l.tilesY * aspect;
                float tilesYv = l.tilesY;
                float ox = 0.5f, oy = 0.5f;
                float hx = 0.5f, hy = 0.5f;

                if (l.scrolls)
                {
                    float tpu = l.tilesY / (2f * referenceSize);
                    ox = Mathf.Repeat(camPos.x * l.parallax * tpu + l.drift * t, 1f);
                    oy = Mathf.Repeat(camPos.y * l.parallax * tpu, 1f);
                    hx = 0.5f * tilesX * ViewMargin;
                    hy = 0.5f * tilesYv * ViewMargin;
                }

                l.uv[0] = new Vector2(ox - hx, oy - hy);
                l.uv[1] = new Vector2(ox - hx, oy + hy);
                l.uv[2] = new Vector2(ox + hx, oy + hy);
                l.uv[3] = new Vector2(ox + hx, oy - hy);
                l.mesh.uv = l.uv;

                if (l.twinkle)
                {
                    float s = 0.65f * Mathf.Sin(t * l.twinkleSpeed + l.twinklePhase)
                            + 0.35f * Mathf.Sin(t * l.twinkleSpeed * 2.7f + l.twinklePhase * 1.7f);
                    s = 0.5f + 0.5f * s;
                    float a = l.alpha * (1f - twinkleDepth + twinkleDepth * s);
                    l.mat.color = new Color(1f, 1f, 1f, a);
                }
                else if (!Mathf.Approximately(l.alpha, 1f))
                {
                    l.mat.color = new Color(1f, 1f, 1f, l.alpha);
                }
            }
        }

        // ------------------------------------------------------------------
        // Utilidades matemáticas
        // ------------------------------------------------------------------

        private static Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash3(int x, int y, int z, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791) ^ (uint)(s * 668265263);
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        private static float PeriodicNoise(float u, float v, int period, int s)
        {
            float px = u * period, py = v * period;
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float fx = px - ix, fy = py - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int x0 = ((ix % period) + period) % period;
            int y0 = ((iy % period) + period) % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;

            float a = Hash3(x0, y0, 0, s);
            float b = Hash3(x1, y0, 0, s);
            float c = Hash3(x0, y1, 0, s);
            float d = Hash3(x1, y1, 0, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float PeriodicFbm(float u, float v, int basePeriod, int octaves, int s)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * PeriodicNoise(u, v, period, s + o * 31);
                norm += amp;
                amp *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }
    }
}