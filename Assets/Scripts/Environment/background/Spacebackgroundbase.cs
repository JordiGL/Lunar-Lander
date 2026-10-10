using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Base de los fondos espaciales. Contiene lo que comparten todos los estilos:
    /// cámara, nodo raíz, capas con parallax, estrella fugaz y el audio reactivo
    /// (bombo y caja). Cada clase hija decide QUÉ capas dibujar en BuildLayers().
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public abstract class SpaceBackgroundBase : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Configuración común
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

        [Header("Estrellas fugaces")]
        [SerializeField] private bool shootingStars = true;
        [SerializeField, Min(0.5f)] private float shootingMinInterval = 3f;
        [SerializeField, Min(0.5f)] private float shootingMaxInterval = 9f;

        [Header("Audio Reactivo (Estrellas)")]
        [Tooltip("Música del stage. Si no se asigna, se analiza todo lo que oye el AudioListener.")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField, Range(1.05f, 3f)] private float beatSensitivity = 1.35f;
        [Tooltip("Energía mínima de graves para considerar un beat (evita falsos beats en silencio).")]
        [SerializeField, Min(0f)] private float minBeatEnergy = 0.0005f;
        [SerializeField, Range(0.08f, 0.5f)] private float beatCooldown = 0.15f;
        [Tooltip("Brillo de las estrellas entre beats (el beat las lleva a 1).")]
        [SerializeField, Range(0f, 1f)] private float idleBrightness = 0.45f;
        [SerializeField, Min(1f)] private float pulseDecay = 5f;
        [Tooltip("Cuánto reaccionan las estrellas brillantes frente a las lejanas.")]
        [SerializeField, Range(0f, 1f)] private float pulseStrength = 1f;

        [Header("Caja (snare)")]
        [SerializeField] private bool reactToSnare = true;
        [Tooltip("Cuánto debe subir la banda de la caja sobre su media para contar como golpe.")]
        [SerializeField, Range(1.05f, 3f)] private float snareSensitivity = 1.6f;
        [Tooltip("Energía mínima de la banda de la caja (evita falsos golpes en silencio).")]
        [SerializeField, Min(0f)] private float minSnareEnergy = 0.0003f;
        [SerializeField, Range(0.08f, 0.5f)] private float snareCooldown = 0.12f;
        [Tooltip("Banda de frecuencias del chasquido de la caja. Subir el máximo mete más hi-hats.")]
        [SerializeField, Min(200f)] private float snareLowHz = 1500f;
        [SerializeField, Min(500f)] private float snareHighHz = 4500f;
        [Tooltip("Intensidad del pulso de la caja respecto al del bombo (1 = igual).")]
        [SerializeField, Range(0f, 1f)] private float snarePulseStrength = 0.8f;

        // ------------------------------------------------------------------
        // Tipos internos
        // ------------------------------------------------------------------

        protected sealed class Layer
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
            public float driftY;               // Desplazamiento vertical continuo (lluvia, humo...). Positivo = el contenido cae.
            public float parallaxYScale = 1f;  // 0 = la capa no sigue a la cámara en vertical (horizontes, ciudades).
            public float alpha = 1f;
            public bool twinkle;
            public float twinkleSpeed;
            public float twinklePhase;
            public float twinkleDepth = 0.45f;
            public bool reactsToAudio; // Reacciona a los pulsos de la música
        }

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------

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

        // Audio reactivo
        private readonly float[] spectrumData = new float[1024];
        private float audioPulse = 0f;
        private float bassAverage = 0f;
        private float lastBeatTime = -10f;
        private float snareAverage = 0f;
        private float lastSnareTime = -10f;

        // ------------------------------------------------------------------
        // Contrato para las clases hijas
        // ------------------------------------------------------------------

        /// <summary>
        /// Crea las capas visuales (cielo, nebulosa, estrellas...) usando CreateLayer().
        /// Se llama una vez desde Awake, cuando cámara, raíz y rng ya existen.
        /// </summary>
        protected abstract void BuildLayers();

        /// <summary>Color de fondo de la cámara mientras este fondo está activo.</summary>
        protected virtual Color CameraBackgroundColor => Color.black;

        protected int SortingOrderBase => sortingOrderBase;
        protected System.Random Rng => rng;
        protected Camera Cam => cam;

        /// <summary>Pulso actual del audio (0..1): 1 justo en un golpe de bombo o caja y decae después.</summary>
        protected float AudioPulse => audioPulse;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        protected virtual void Awake()
        {
            cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogWarning($"{GetType().Name}: no se encontró ninguna cámara.");
                enabled = false;
                return;
            }

            rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);

            BuildRoot();
            BuildLayers();
            if (shootingStars) BuildShootingStar();

            shootTimer = Mathf.Lerp(shootingMinInterval, shootingMaxInterval, Rand01());
            UpdateFrame();
        }

        protected virtual void OnEnable()
        {
            if (root != null) root.gameObject.SetActive(true);

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = CameraBackgroundColor;
            }
        }

        protected virtual void OnDisable()
        {
            if (root != null) root.gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            UpdateShooting(Time.deltaTime);
            UpdateAudioPulse();
        }

        protected virtual void LateUpdate()
        {
            if (cam == null || root == null) return;
            UpdateFrame();
        }

        protected virtual void OnDestroy()
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
        // Audio Reactivo
        // ------------------------------------------------------------------

        private void UpdateAudioPulse()
        {
            // Decaimiento exponencial del pulso
            audioPulse *= Mathf.Exp(-pulseDecay * Time.deltaTime);

            bool hasSource = musicSource != null;
            if (hasSource && !musicSource.isPlaying) return;

            if (hasSource) musicSource.GetSpectrumData(spectrumData, 0, FFTWindow.Blackman);
            else AudioListener.GetSpectrumData(spectrumData, 0, FFTWindow.Blackman);

            // Hz por bin según la frecuencia de muestreo real (≈21 Hz a 44.1 kHz).
            float binHz = AudioSettings.outputSampleRate * 0.5f / spectrumData.Length;

            // ---- Bombo: graves (~43-190 Hz) ----
            float bass = 0f;
            for (int i = 2; i <= 8; i++) bass += spectrumData[i];
            bass /= 7f;

            bool kick = bass > minBeatEnergy
                        && bass > bassAverage * beatSensitivity
                        && Time.time - lastBeatTime > beatCooldown;
            if (kick)
            {
                lastBeatTime = Time.time;
                audioPulse = 1f;
            }
            bassAverage = Mathf.Lerp(bassAverage, bass, Time.deltaTime * 4f);

            // ---- Caja: chasquido (~1.5-4.5 kHz) ----
            if (!reactToSnare) return;

            int lo = Mathf.Clamp(Mathf.RoundToInt(snareLowHz / binHz), 2, spectrumData.Length - 2);
            int hi = Mathf.Clamp(Mathf.RoundToInt(snareHighHz / binHz), lo + 1, spectrumData.Length - 1);

            float snare = 0f;
            for (int i = lo; i <= hi; i++) snare += spectrumData[i];
            snare /= (hi - lo + 1);

            bool snareHit = snare > minSnareEnergy
                            && snare > snareAverage * snareSensitivity
                            && Time.time - lastSnareTime > snareCooldown;
            if (snareHit)
            {
                lastSnareTime = Time.time;
                audioPulse = Mathf.Max(audioPulse, snarePulseStrength);
            }
            snareAverage = Mathf.Lerp(snareAverage, snare, Time.deltaTime * 6f);
        }

        // ------------------------------------------------------------------
        // Construcción (utilidades para las hijas)
        // ------------------------------------------------------------------

        protected float Rand01() => (float)rng.NextDouble();

        protected static Shader SpriteShader()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s;
        }

        private void BuildRoot()
        {
            var go = new GameObject($"SpaceBackground_Root_{gameObject.name}");
            root = go.transform;
            root.SetParent(cam.transform, false);

            float depth = Mathf.Clamp(cam.farClipPlane * 0.9f, 5f, 100f);
            root.localPosition = new Vector3(0f, 0f, depth);
            root.localRotation = Quaternion.identity;

            spriteMaterial = new Material(SpriteShader()) { name = "SpaceBackground Sprite" };
            root.gameObject.SetActive(isActiveAndEnabled);
        }

        private static Mesh BuildQuadMesh(string name)
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

        /// <summary>Crea una capa a pantalla completa con la textura dada. La base la actualiza cada frame.</summary>
        protected Layer CreateLayer(string name, Texture2D tex, int order, float parallax, float tilesY, bool scrolls)
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

        /// <summary>Activa el parpadeo de una capa.</summary>
        protected void SetTwinkle(Layer l, bool enabled, float speed, float depth)
        {
            l.twinkle = enabled;
            l.twinkleSpeed = speed;
            l.twinkleDepth = depth;
            l.twinklePhase = Rand01() * 6.2831f;
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
                    oy = Mathf.Repeat(camPos.y * l.parallax * tpu * l.parallaxYScale + l.driftY * t, 1f);
                    hx = 0.5f * tilesX * ViewMargin;
                    hy = 0.5f * tilesYv * ViewMargin;
                }

                l.uv[0] = new Vector2(ox - hx, oy - hy);
                l.uv[1] = new Vector2(ox - hx, oy + hy);
                l.uv[2] = new Vector2(ox + hx, oy + hy);
                l.uv[3] = new Vector2(ox + hx, oy - hy);
                l.mesh.uv = l.uv;

                float currentAlpha = l.alpha;

                if (l.twinkle)
                {
                    float s = 0.65f * Mathf.Sin(t * l.twinkleSpeed + l.twinklePhase)
                            + 0.35f * Mathf.Sin(t * l.twinkleSpeed * 2.7f + l.twinklePhase * 1.7f);
                    s = 0.5f + 0.5f * s;
                    currentAlpha = l.alpha * (1f - l.twinkleDepth + l.twinkleDepth * s);
                }

                if (l.reactsToAudio)
                {
                    // Brillo base atenuado; el beat lo lleva a pleno brillo.
                    float pulse = audioPulse * pulseStrength;
                    currentAlpha *= Mathf.Lerp(idleBrightness, 1f, pulse);
                }

                if (l.twinkle || l.reactsToAudio || !Mathf.Approximately(currentAlpha, 1f))
                {
                    l.mat.color = new Color(1f, 1f, 1f, Mathf.Clamp01(currentAlpha));
                }
            }
        }

        // ------------------------------------------------------------------
        // Utilidades matemáticas compartidas
        // ------------------------------------------------------------------

        protected static Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);

        protected static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        protected static float Smooth(float e0, float e1, float x)
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

        /// <summary>Ruido fractal que se repite sin costuras (para texturas en mosaico).</summary>
        protected static float PeriodicFbm(float u, float v, int basePeriod, int octaves, int s)
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