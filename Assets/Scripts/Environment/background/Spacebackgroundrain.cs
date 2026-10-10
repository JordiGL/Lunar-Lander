using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Fondo de lluvia pura: cielo oscuro y gotas simuladas en el mundo del juego.
    /// Cada gota detecta colisiones con Physics2D, así que salpica al tocar el terreno,
    /// la nave o cualquier collider. Las gotas y las salpicaduras laten con el audio.
    /// Todo se dibuja en una sola malla dinámica (un solo draw call).
    /// </summary>
    public sealed class SpaceBackgroundRain : SpaceBackgroundBase
    {
        private const int RippleSegments = 10;
        private const float SpawnSpread = 8f;   // Altura sobre la pantalla en la que reaparecen las gotas

        [Header("Cielo")]
        [SerializeField] private Color skyTop = new Color(0.01f, 0.02f, 0.05f);
        [SerializeField] private Color skyBottom = new Color(0.05f, 0.10f, 0.15f);

        [Header("Lluvia")]
        [SerializeField, Range(50, 1500)] private int dropCount = 450;
        [SerializeField, Min(1f)] private float fallSpeedMin = 18f;
        [SerializeField, Min(1f)] private float fallSpeedMax = 30f;
        [SerializeField, Min(0.005f)] private float dropWidth = 0.035f;
        [Tooltip("Segundos de recorrido que se dibujan como estela. Más alto = gotas más largas.")]
        [SerializeField, Range(0.01f, 0.1f)] private float streakTime = 0.035f;
        [Tooltip("Viento horizontal medio (unidades/segundo). Negativo = hacia la izquierda.")]
        [SerializeField] private float wind = 3f;
        [Tooltip("Cuánto varía el viento a ráfagas.")]
        [SerializeField, Min(0f)] private float gust = 2f;
        [SerializeField] private Color rainColor = new Color(0.55f, 0.85f, 1.00f);
        [SerializeField] private Color rainAccent = new Color(1.00f, 0.30f, 0.80f);
        [SerializeField, Range(0f, 1f)] private float accentChance = 0.15f;
        [Tooltip("Brillo de la lluvia entre golpes de música (el beat la lleva a 1).")]
        [SerializeField, Range(0f, 1f)] private float rainIdleBrightness = 0.45f;
        [Tooltip("Debe quedar por encima del terreno si quieres ver las salpicaduras sobre él.")]
        [SerializeField] private int sortingOrder = 20;

        [Header("Reacción al ritmo")]
        [Tooltip("Fracción de gotas que solo aparecen con el beat: cada golpe de bombo o caja suelta una ráfaga extra de lluvia.")]
        [SerializeField, Range(0f, 0.8f)] private float reactiveFraction = 0.3f;
        [Tooltip("Cuánto se engrosan las gotas en el golpe (0 = nada, 1 = el doble).")]
        [SerializeField, Range(0f, 3f)] private float beatWidthBoost = 1f;
        [Tooltip("Cuánto se alargan las estelas en el golpe.")]
        [SerializeField, Range(0f, 3f)] private float beatStreakBoost = 0.8f;
        [Tooltip("Cuánto se acelera la caída en el golpe.")]
        [SerializeField, Range(0f, 1f)] private float beatSpeedBoost = 0.25f;
        [Tooltip("Cuánto viran las gotas hacia blanco en el golpe.")]
        [SerializeField, Range(0f, 1f)] private float beatWhiten = 0.6f;

        [Header("Salpicaduras")]
        [SerializeField] private bool splashes = true;
        [Tooltip("Capas contra las que choca la lluvia. Limítalo (Terreno, Nave) para ahorrar CPU.")]
        [SerializeField] private LayerMask splashLayers = ~0;
        [SerializeField, Range(0, 10)] private int dropletsPerSplash = 4;
        [SerializeField, Min(0.5f)] private float dropletSpeed = 3.5f;
        [SerializeField] private float dropletGravity = -14f;
        [SerializeField, Range(16, 1024)] private int maxDroplets = 300;
        [SerializeField] private bool ripples = true;
        [Tooltip("Si está activo, las ondas solo salen al impactar contra un VectorTerrain (no contra la nave ni otros colliders).")]
        [SerializeField] private bool rippleOnlyOnTerrain = true;
        [SerializeField, Min(0.05f)] private float rippleMaxRadius = 0.45f;
        [SerializeField, Min(0.1f)] private float rippleLife = 0.4f;
        [SerializeField, Range(8, 200)] private int maxRipples = 60;

        private struct Drop
        {
            public Vector2 pos, vel;
            public float speed, windFactor, bright;
            public bool accent;
            public bool reactive;   // Solo es visible durante el beat
        }

        private struct Droplet
        {
            public Vector2 pos, vel;
            public float life, maxLife;
            public bool accent;
        }

        private struct Ripple
        {
            public Vector2 center;
            public float age, life, maxR;
            public bool accent;
        }

        private Drop[] drops;
        private Droplet[] droplets;
        private Ripple[] rippleList;
        private int dropletHead, rippleHead;

        private GameObject rainGO;
        private Mesh mesh;
        private Material rainMat;
        private Vector3[] verts;
        private Color32[] cols;
        private int capacityQuads, quad, prevQuad;

        private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[4];
        private ContactFilter2D contactFilter;
        private readonly Dictionary<Collider2D, bool> terrainCache = new Dictionary<Collider2D, bool>();

        protected override Color CameraBackgroundColor => skyTop;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        protected override void BuildLayers() => BuildSky();

        protected override void Awake()
        {
            base.Awake();
            if (!enabled || Cam == null) return;

            BuildRainObject();
            InitDrops();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (rainGO != null) rainGO.SetActive(true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (rainGO != null) rainGO.SetActive(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (mesh != null) Destroy(mesh);
            if (rainMat != null) Destroy(rainMat);
            if (rainGO != null) Destroy(rainGO);
        }

        protected override void Update()
        {
            base.Update();
            if (rainGO == null || !rainGO.activeInHierarchy) return;
            Simulate(Mathf.Min(Time.deltaTime, 0.05f));
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (rainGO == null || !rainGO.activeInHierarchy) return;
            RebuildMesh();
        }

        // ------------------------------------------------------------------
        // Cielo
        // ------------------------------------------------------------------

        private void BuildSky()
        {
            const int w = 64, h = 256;
            var px = new Color32[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);                 // 0 = abajo, 1 = arriba
                Color c = Color.Lerp(skyBottom, skyTop, Smooth(0f, 1f, t));
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), 255);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "BG_RainSky",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);

            CreateLayer("Sky", tex, SortingOrderBase, 0f, 1f, false);
        }

        // ------------------------------------------------------------------
        // Malla dinámica
        // ------------------------------------------------------------------

        private void BuildRainObject()
        {
            droplets = new Droplet[maxDroplets];
            rippleList = new Ripple[maxRipples];

            capacityQuads = dropCount + maxDroplets + maxRipples * RippleSegments;
            verts = new Vector3[capacityQuads * 4];
            cols = new Color32[capacityQuads * 4];

            var tris = new int[capacityQuads * 6];
            for (int q = 0; q < capacityQuads; q++)
            {
                int v = q * 4, t = q * 6;
                tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            mesh = new Mesh { name = "RainMesh" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000f, 100000f, 10f)); // nunca se recorta

            rainMat = new Material(SpriteShader()) { name = "RainMaterial" };

            // Sin padre: los vértices están en coordenadas de mundo.
            rainGO = new GameObject($"SpaceBackground_Rain_{gameObject.name}");
            rainGO.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = rainGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rainMat;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            rainGO.SetActive(isActiveAndEnabled);
        }

        private void GetView(out Vector2 center, out float halfW, out float halfH)
        {
            center = Cam.transform.position;
            halfH = Cam.orthographicSize;
            halfW = halfH * Cam.aspect;
        }

        private Color32 Tint(bool accent, float alpha, float whiten = 0f)
        {
            Color c = accent ? rainAccent : rainColor;
            if (whiten > 0f) c = Color.Lerp(c, Color.white, whiten);
            return new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(alpha));
        }

        private void AddQuad(Vector2 a, Vector2 b, float widthA, float widthB, Color32 colA, Color32 colB)
        {
            if (quad >= capacityQuads) return;

            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f) return;

            Vector2 perp = new Vector2(-dir.y, dir.x) / len * 0.5f;
            int v = quad * 4;
            verts[v] = a + perp * widthA;
            verts[v + 1] = a - perp * widthA;
            verts[v + 2] = b - perp * widthB;
            verts[v + 3] = b + perp * widthB;
            cols[v] = colA; cols[v + 1] = colA;
            cols[v + 2] = colB; cols[v + 3] = colB;
            quad++;
        }

        private void RebuildMesh()
        {
            quad = 0;
            float pulse = AudioPulse;
            float mult = Mathf.Lerp(rainIdleBrightness, 1f, pulse);

            // Gotas: estela transparente en la cola y opaca en la cabeza.
            float width = dropWidth * (1f + pulse * beatWidthBoost);
            float streak = streakTime * (1f + pulse * beatStreakBoost);
            float whiten = pulse * beatWhiten;

            for (int i = 0; i < drops.Length; i++)
            {
                Drop d = drops[i];

                // Las gotas "reactivas" solo existen con el beat; el resto cambia de brillo.
                float a = d.reactive ? d.bright * pulse : d.bright * mult;
                if (a < 0.01f) continue;

                Vector2 tail = d.pos - d.vel * streak;
                AddQuad(tail, d.pos, width * 0.25f, width, Tint(d.accent, 0f, whiten), Tint(d.accent, a, whiten));
            }

            // Gotitas de salpicadura.
            for (int i = 0; i < droplets.Length; i++)
            {
                Droplet p = droplets[i];
                if (p.life <= 0f) continue;
                float t = p.life / p.maxLife;
                Vector2 tail = p.pos - p.vel * 0.03f;
                AddQuad(tail, p.pos, dropWidth * 0.3f, dropWidth * 0.9f, Tint(p.accent, 0f),
                        Tint(p.accent, Mathf.Clamp01(t * (0.75f + 0.25f * pulse))));
            }

            // Ondas en el suelo: elipses aplastadas que se expanden y se desvanecen.
            for (int i = 0; i < rippleList.Length; i++)
            {
                Ripple r = rippleList[i];
                if (r.age >= r.life) continue;

                float k = r.age / r.life;
                float radius = r.maxR * (1f - (1f - k) * (1f - k));
                float alpha = Mathf.Pow(1f - k, 1.5f) * 0.9f * mult;
                Color32 c = Tint(r.accent, alpha);

                Vector2 prev = r.center + new Vector2(radius, 0f);
                for (int s = 1; s <= RippleSegments; s++)
                {
                    float ang = s * Mathf.PI * 2f / RippleSegments;
                    Vector2 cur = r.center + new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius * 0.28f);
                    AddQuad(prev, cur, dropWidth * 0.8f, dropWidth * 0.8f, c, c);
                    prev = cur;
                }
            }

            // Limpia los cuadrados que se usaron el frame anterior y ya no hacen falta.
            for (int q = quad; q < prevQuad; q++)
            {
                int v = q * 4;
                for (int j = 0; j < 4; j++) { verts[v + j] = Vector3.zero; cols[v + j] = default; }
            }
            prevQuad = quad;

            mesh.vertices = verts;
            mesh.colors32 = cols;
        }

        // ------------------------------------------------------------------
        // Simulación
        // ------------------------------------------------------------------

        private void InitDrops()
        {
            drops = new Drop[dropCount];
            GetView(out Vector2 c, out float hw, out float hh);
            for (int i = 0; i < drops.Length; i++) Respawn(ref drops[i], c, hw, hh, 2f, true);
        }

        private void Respawn(ref Drop d, Vector2 c, float hw, float hh, float margin, bool anywhere)
        {
            float x = Random.Range(c.x - hw - margin, c.x + hw + margin);
            float y = anywhere
                ? Random.Range(c.y - hh, c.y + hh + SpawnSpread)
                : c.y + hh + Random.Range(0.3f, SpawnSpread);

            d.pos = new Vector2(x, y);
            d.speed = Random.Range(fallSpeedMin, Mathf.Max(fallSpeedMin, fallSpeedMax));
            d.windFactor = Random.Range(0.7f, 1.1f);
            d.bright = Random.Range(0.35f, 1f);
            d.accent = Random.value < accentChance;
            d.reactive = Random.value < reactiveFraction;
        }

        private bool Cast(Vector2 a, Vector2 b, out RaycastHit2D best)
        {
            best = default;
            int n = Physics2D.Linecast(a, b, contactFilter, hitBuffer);
            if (n <= 0) return false;

            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (hitBuffer[i].collider == null || hitBuffer[i].distance >= bestDist) continue;
                bestDist = hitBuffer[i].distance;
                best = hitBuffer[i];
            }
            return bestDist < float.MaxValue;
        }

        /// <summary>¿Pertenece este collider a un VectorTerrain? Se cachea para no llamar a GetComponent en cada gota.</summary>
        private bool IsTerrain(Collider2D col)
        {
            if (col == null) return false;
            if (terrainCache.TryGetValue(col, out bool isTerrain)) return isTerrain;

            if (terrainCache.Count > 256) terrainCache.Clear();   // evita acumular colliders de restos ya destruidos
            isTerrain = col.GetComponentInParent<TerrainBase>() != null;
            terrainCache[col] = isTerrain;
            return isTerrain;
        }

        private void Simulate(float dt)
        {
            contactFilter.useTriggers = false;
            contactFilter.SetLayerMask(splashLayers);

            float t = Time.time;
            float windNow = wind + Mathf.Sin(t * 0.6f) * gust + Mathf.Sin(t * 1.7f + 1.3f) * gust * 0.4f;

            GetView(out Vector2 c, out float hw, out float hh);
            float margin = Mathf.Abs(windNow) * 0.8f + 2f;
            float pulse = AudioPulse;
            float speedMult = 1f + pulse * beatSpeedBoost;   // la lluvia se acelera con el golpe

            for (int i = 0; i < drops.Length; i++)
            {
                ref Drop d = ref drops[i];
                d.vel = new Vector2(windNow * d.windFactor, -d.speed * speedMult);
                Vector2 next = d.pos + d.vel * dt;

                if (splashes && Cast(d.pos, next, out RaycastHit2D hit))
                {
                    // Una gota reactiva invisible no debe salpicar: solo si es visible (hay beat).
                    if (!d.reactive || pulse > 0.25f)
                        SpawnSplash(hit.point, hit.normal, d.accent, IsTerrain(hit.collider));
                    Respawn(ref d, c, hw, hh, margin, false);
                    continue;
                }

                d.pos = next;

                bool outside = d.pos.y < c.y - hh - 2f
                               || d.pos.y > c.y + hh + SpawnSpread + 3f
                               || d.pos.x < c.x - hw - margin
                               || d.pos.x > c.x + hw + margin;
                if (outside) Respawn(ref d, c, hw, hh, margin, false);
            }

            for (int i = 0; i < droplets.Length; i++)
            {
                ref Droplet p = ref droplets[i];
                if (p.life <= 0f) continue;
                p.vel.y += dropletGravity * dt;
                p.pos += p.vel * dt;
                p.life -= dt;
            }

            for (int i = 0; i < rippleList.Length; i++)
            {
                if (rippleList[i].age < rippleList[i].life) rippleList[i].age += dt;
            }
        }

        private void SpawnSplash(Vector2 point, Vector2 normal, bool accent, bool onTerrain)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector2.up;
            normal.Normalize();

            float pulse = AudioPulse;
            Vector2 tangent = new Vector2(normal.y, -normal.x);

            int count = Mathf.RoundToInt(dropletsPerSplash * Random.Range(0.6f, 1.4f) * (1f + pulse * 0.8f));
            for (int k = 0; k < count; k++)
            {
                Vector2 dir = (normal * Random.Range(0.5f, 1.2f) + tangent * (Random.Range(-1f, 1f) * 0.9f)).normalized;
                float life = Random.Range(0.25f, 0.5f);

                droplets[dropletHead] = new Droplet
                {
                    pos = point + normal * 0.02f,
                    vel = dir * dropletSpeed * Random.Range(0.5f, 1f) * (1f + pulse * 0.5f),
                    life = life,
                    maxLife = life,
                    accent = accent
                };
                dropletHead = (dropletHead + 1) % droplets.Length;
            }

            // Onda solo si la superficie es más o menos horizontal (suelo, no pared)
            // y, opcionalmente, solo si es un VectorTerrain (no la nave).
            if (ripples && normal.y > 0.5f && (onTerrain || !rippleOnlyOnTerrain))
            {
                rippleList[rippleHead] = new Ripple
                {
                    center = point + normal * 0.01f,
                    age = 0f,
                    life = rippleLife * Random.Range(0.8f, 1.2f),
                    maxR = rippleMaxRadius * Random.Range(0.6f, 1f) * (1f + pulse * 0.5f),
                    accent = accent
                };
                rippleHead = (rippleHead + 1) % rippleList.Length;
            }
        }
    }
}