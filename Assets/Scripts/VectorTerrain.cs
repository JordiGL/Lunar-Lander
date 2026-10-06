using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>Tipo de plataforma de aterrizaje; define su entorno y su dificultad.</summary>
    public enum PadKind
    {
        Plain,   // Llanura abierta y ancha (fácil)
        Crater,  // Fondo de un cráter: paredes suaves a ambos lados
        Ledge,   // Repisa: pared por un lado y precipicio por el otro
        Peak,    // Cima de una montaña: estrecha, con caída por todos lados
        Canyon   // Cañón: estrecho entre paredes verticales (muy difícil)
    }

    /// <summary>
    /// Representa una plataforma de aterrizaje plana dentro del terreno lunar.
    /// </summary>
    [System.Serializable]
    public struct LandingPad
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
        public int multiplier; // Multiplicador de puntos (2x, 3x, 4x, 5x)
        public PadKind kind;

        public Vector2 Center => (startPoint + endPoint) * 0.5f;
        public float Width => Mathf.Abs(endPoint.x - startPoint.x);
    }

    /// <summary>
    /// Terreno lunar vectorial procedural (LineRenderer + EdgeCollider2D).
    ///
    /// - Relieve: ruido "ridged" multi-octava (montañas afiladas) + cráteres decorativos.
    /// - Plataformas: hasta 5 tipos con entornos y dificultades distintos (ver PadKind). Cada una
    ///   lleva balizas en los extremos (parpadean en las difíciles) y un color según dificultad.
    /// - Estética: capas de estratos bajo la superficie y cordilleras lejanas con eliminación
    ///   de líneas ocultas (como en los vectoriales clásicos). Todo es solo visual salvo la
    ///   línea principal, que es la única con colisión.
    /// - Bandera: escucha LanderController.OnLanded y, tras un aterrizaje correcto, planta e iza
    ///   una VectorFlag junto a la nave, en la plataforma donde ha aterrizado. Se retira al
    ///   reiniciar la nave (OnReset) o al regenerar el terreno.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public sealed class VectorTerrain : MonoBehaviour
    {
        private const string FxPrefix = "TerrainFX_";
        private const string FlagName = FxPrefix + "Flag";

        // ------------------------------------------------------------------
        // Configuración
        // ------------------------------------------------------------------

        [Header("Configuración del Terreno")]
        [SerializeField, Min(60)] private int segments = 200;
        [SerializeField] private float width = 80f;
        [SerializeField] private float minHeight = -5f;
        [SerializeField] private float maxHeight = 8f;
        [Tooltip("Cuánto detalle fino (rocas, picos pequeños) tiene el relieve.")]
        [SerializeField, Range(0f, 1f)] private float roughness = 0.5f;
        [Tooltip("Frecuencia base de las montañas (más alto = más montañas más estrechas).")]
        [SerializeField, Min(0.005f)] private float mountainFrequency = 0.045f;
        [Tooltip("0 = colinas suaves, 1 = crestas afiladas.")]
        [SerializeField, Range(0f, 1f)] private float ridgedMix = 0.65f;
        [Tooltip("Mayor = más llanuras y picos más escasos pero más altos.")]
        [SerializeField, Range(0.8f, 2.5f)] private float peakSharpness = 1.35f;
        [Tooltip("0 = semilla aleatoria cada partida; otro valor = terreno reproducible.")]
        [SerializeField] private int randomSeed = 0;
        [Tooltip("Cráteres decorativos (lejos de las plataformas).")]
        [SerializeField, Range(0, 10)] private int craterCount = 4;

        [Header("Plataformas de Aterrizaje")]
        [Tooltip("Cuántas plataformas se generan (en orden: Llanura, Cima, Cráter, Cañón, Repisa).")]
        [SerializeField, Range(2, 5)] private int padCount = 5;
        [Tooltip("Anchura base; cada tipo usa un factor. La nave mide ~1.85 con las patas.")]
        [SerializeField, Min(2.2f)] private float minPadWidth = 2.6f;
        [SerializeField, Min(0f)] private float beaconBlinkRate = 2f;

        [Header("Aspecto Visual")]
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color terrainColor = Color.green;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int sortingOrder = 5;

        [Header("Estratos bajo la superficie")]
        [SerializeField, Range(0, 6)] private int strataLines = 3;
        [SerializeField, Min(0.05f)] private float strataSpacing = 0.45f;

        [Header("Cordilleras de fondo")]
        [SerializeField, Range(0, 4)] private int backgroundLayers = 3;

        [Header("Plataformas (colores por dificultad)")]
        [SerializeField] private Color colorPad2x = Color.white;
        [SerializeField] private Color colorPad3x = new Color(1f, 0.92f, 0.2f);  // Amarillo
        [SerializeField] private Color colorPad4x = new Color(1f, 0.6f, 0.15f);  // Naranja
        [SerializeField] private Color colorPad5x = new Color(1f, 0.28f, 0.28f); // Rojo
        [SerializeField] private float padLineWidthMultiplier = 1.6f;

        [Header("Bandera de aterrizaje")]
        [Tooltip("Opcional. Si está vacío se busca un LanderController en la escena.")]
        [SerializeField] private LanderController lander;
        [SerializeField] private bool plantFlagOnLanding = true;
        [Tooltip("Si está desactivado, la tela de la bandera no ondea (queda rígida).")]
        [SerializeField] private bool flagWaves = true;
        [SerializeField, Min(0.3f)] private float flagPoleHeight = 1.6f;
        [SerializeField, Min(0.2f)] private float flagWidth = 0.9f;
        [SerializeField, Min(0.2f)] private float flagHeight = 0.55f;
        [Tooltip("Segundos que tarda en crecer el mástil y subir la tela.")]
        [SerializeField, Min(0.2f)] private float flagRaiseDuration = 1.8f;
        [Tooltip("Separación horizontal entre el centro de la nave y el mástil.")]
        [SerializeField, Min(0.5f)] private float flagOffsetFromLander = 1.2f;

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private struct PadPlan
        {
            public PadKind kind;
            public int multiplier;
            public float widthUnits;
            public float heightFrac;
            public int startIdx;
            public int widthSeg;
            public float padH;
            public int wallSide; // Solo Ledge: +1 pared a la derecha, -1 a la izquierda
        }

        private LineRenderer lineRenderer;
        private EdgeCollider2D edgeCollider;
        private MaterialPropertyBlock propertyBlock;
        private Material runtimeMaterial;
        private System.Random rng;
        private float seedOffset;

        private Vector3[] terrainPoints3D;
        private Vector2[] terrainPoints2D;
        private readonly List<LandingPad> landingPads = new List<LandingPad>();
        private readonly List<GameObject> fxObjects = new List<GameObject>();
        private readonly List<LineRenderer> blinkingBeacons = new List<LineRenderer>();
        private readonly List<VectorFlag> flags = new List<VectorFlag>();
        private float blinkTimer;
        private bool landerSubscribed;

        public IReadOnlyList<LandingPad> LandingPads => landingPads;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            GenerateTerrain();
        }

        private void OnEnable()
        {
            SubscribeLander();
        }

        private void OnDisable()
        {
            UnsubscribeLander();
        }

        private void Update()
        {
            if (blinkingBeacons.Count == 0 || beaconBlinkRate <= 0f) return;

            blinkTimer += Time.deltaTime;
            bool on = ((int)(blinkTimer * beaconBlinkRate * 2f) & 1) == 0;
            for (int i = 0; i < blinkingBeacons.Count; i++)
            {
                if (blinkingBeacons[i] != null) blinkingBeacons[i].enabled = on;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void OnValidate()
        {
            maxHeight = Mathf.Max(minHeight + 1f, maxHeight);

            // Permite activar/desactivar el ondeo de una bandera ya plantada desde el Inspector.
            for (int i = 0; i < flags.Count; i++)
            {
                if (flags[i] != null) flags[i].WaveEnabled = flagWaves;
            }

            if (Application.isPlaying && lineRenderer != null && terrainPoints3D != null)
            {
                RebuildVisuals();
            }
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>Altura del terreno (coordenadas locales) en la X dada.</summary>
        public float SampleHeight(float x)
        {
            if (terrainPoints2D == null || terrainPoints2D.Length < 2) return 0f;

            float step = width / segments;
            float f = (x + width * 0.5f) / step;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, terrainPoints2D.Length - 2);
            float t = Mathf.Clamp01(f - i);
            return Mathf.Lerp(terrainPoints2D[i].y, terrainPoints2D[i + 1].y, t);
        }

        /// <summary>Retira las banderas plantadas (p. ej. al empezar un nuevo intento).</summary>
        public void ClearFlags()
        {
            for (int i = 0; i < flags.Count; i++)
            {
                if (flags[i] == null) continue;

                if (Application.isPlaying) Destroy(flags[i].gameObject);
                else DestroyImmediate(flags[i].gameObject);
            }

            flags.Clear();
        }

        /// <summary>
        /// Genera el relieve, coloca las plataformas y reconstruye todos los visuales.
        /// </summary>
        [ContextMenu("Regenerar Terreno")]
        public void GenerateTerrain()
        {
            EnsureComponents();

            rng = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            seedOffset = (float)rng.NextDouble() * 1000f;

            int pointCount = segments + 1;
            float stepX = width / segments;
            float startX = -width * 0.5f;

            landingPads.Clear();
            ClearFlags(); // un terreno nuevo no conserva banderas del anterior

            // 1. Relieve base: crestas afiladas + colinas suaves.
            float[] heights = BuildBaseHeights(pointCount, stepX, startX);

            // 2. Planificar plataformas (tipo, posición y altura).
            List<PadPlan> plans = PlanPads(pointCount, stepX);

            // 3. Cráteres decorativos, lejos de las plataformas.
            for (int c = 0; c < craterCount; c++)
            {
                AddDecorativeCrater(heights, plans, startX, stepX);
            }

            // 4. Esculpir el entorno de cada plataforma.
            for (int p = 0; p < plans.Count; p++)
            {
                ShapePad(heights, plans[p], startX, stepX);
            }

            FlattenPads(heights, plans);
            for (int i = 0; i < pointCount; i++)
            {
                heights[i] = Mathf.Clamp(heights[i], minHeight, maxHeight);
            }
            FlattenPads(heights, plans);

            // 5. Registrar plataformas.
            for (int p = 0; p < plans.Count; p++)
            {
                PadPlan plan = plans[p];
                landingPads.Add(new LandingPad
                {
                    startPoint = new Vector2(startX + plan.startIdx * stepX, plan.padH),
                    endPoint = new Vector2(startX + (plan.startIdx + plan.widthSeg) * stepX, plan.padH),
                    multiplier = plan.multiplier,
                    kind = plan.kind
                });
            }

            // 6. Puntos finales, línea y colisionador.
            terrainPoints3D = new Vector3[pointCount];
            terrainPoints2D = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                terrainPoints3D[i] = new Vector3(x, heights[i], 0f);
                terrainPoints2D[i] = new Vector2(x, heights[i]);
            }

            UpdateLineRenderer();
            UpdateCollider();
            RebuildVisuals();
        }

        // ------------------------------------------------------------------
        // Bandera de aterrizaje
        // ------------------------------------------------------------------

        private void SubscribeLander()
        {
            if (landerSubscribed) return;

            if (lander == null) lander = FindFirstObjectByType<LanderController>();
            if (lander == null) return;

            lander.OnLanded += HandleLanded;
            lander.OnReset += HandleLanderReset;
            landerSubscribed = true;
        }

        private void UnsubscribeLander()
        {
            if (!landerSubscribed || lander == null) return;

            lander.OnLanded -= HandleLanded;
            lander.OnReset -= HandleLanderReset;
            landerSubscribed = false;
        }

        private void HandleLanderReset()
        {
            ClearFlags();
        }

        private void HandleLanded(LandingResult result)
        {
            if (!plantFlagOnLanding || !result.Success || lander == null) return;

            Vector2 landerLocal = transform.InverseTransformPoint(lander.transform.position);
            int padIndex = FindPadIndex(landerLocal.x);
            if (padIndex < 0) return;

            PlantFlag(landingPads[padIndex], landerLocal.x);
        }

        /// <summary>Plataforma sobre la que está la nave (la de centro más cercano a la X dada).</summary>
        private int FindPadIndex(float localX)
        {
            int best = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < landingPads.Count; i++)
            {
                LandingPad pad = landingPads[i];
                const float tolerance = 1f;

                if (localX < pad.startPoint.x - tolerance || localX > pad.endPoint.x + tolerance) continue;

                float dist = Mathf.Abs(localX - pad.Center.x);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Planta el mástil en la plataforma, al lado de la nave donde haya más sitio, con la tela
        /// orientada hacia fuera de la nave.
        /// </summary>
        private void PlantFlag(LandingPad pad, float landerX)
        {
            ClearFlags(); // solo una bandera a la vez

            const float margin = 0.15f;
            float minX = pad.startPoint.x + margin;
            float maxX = pad.endPoint.x - margin;

            int direction = (maxX - landerX) >= (landerX - minX) ? 1 : -1;
            float x = Mathf.Clamp(landerX + direction * flagOffsetFromLander, minX, maxX);

            var go = new GameObject(FlagName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, pad.startPoint.y, 0f);

            VectorFlag flag = go.AddComponent<VectorFlag>();
            flag.Init(ResolveMaterial(), PadColor(pad.multiplier), lineWidth * 0.8f, sortingOrder + 2,
                      flagPoleHeight, flagWidth, flagHeight, direction, flagRaiseDuration, flagWaves);

            flags.Add(flag);
        }

        // ------------------------------------------------------------------
        // Relieve
        // ------------------------------------------------------------------

        private float Rand01() => (float)rng.NextDouble();

        private float RandRange(float a, float b) => Mathf.Lerp(a, b, Rand01());

        private float[] BuildBaseHeights(int pointCount, float stepX, float startX)
        {
            float[] v = new float[pointCount];
            float persistence = Mathf.Lerp(0.35f, 0.6f, roughness);
            float lo = float.MaxValue, hi = float.MinValue;

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                float broad = Mathf.PerlinNoise((x + seedOffset) * mountainFrequency * 0.5f, 3.7f);
                float ridged = RidgeFbm(x, mountainFrequency, 5, persistence, 0f);
                v[i] = Mathf.Lerp(broad, ridged, ridgedMix);
                lo = Mathf.Min(lo, v[i]);
                hi = Mathf.Max(hi, v[i]);
            }

            float range = Mathf.Max(0.0001f, hi - lo);
            for (int i = 0; i < pointCount; i++)
            {
                float n = Mathf.Pow((v[i] - lo) / range, peakSharpness);
                v[i] = Mathf.Lerp(minHeight, maxHeight, n);
            }

            return v;
        }

        /// <summary>Ruido fractal "ridged": produce crestas y picos afilados en [0,1].</summary>
        private float RidgeFbm(float x, float frequency, int octaves, float persistence, float offset)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = frequency;
            for (int o = 0; o < octaves; o++)
            {
                float n = Mathf.PerlinNoise((x + seedOffset) * freq + offset, 5.3f + o * 7.1f);
                float r = 1f - Mathf.Abs(2f * n - 1f);
                sum += r * r * amp;
                norm += amp;
                amp *= persistence;
                freq *= 2.07f;
            }

            return sum / norm;
        }

        private void AddDecorativeCrater(float[] heights, List<PadPlan> plans, float startX, float stepX)
        {
            float radius = RandRange(1.6f, 3.6f);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float cx = RandRange(startX + radius * 2f, startX + width - radius * 2f);

                bool clear = true;
                for (int p = 0; p < plans.Count && clear; p++)
                {
                    float padCx = startX + (plans[p].startIdx + plans[p].widthSeg * 0.5f) * stepX;
                    float minDist = radius * 1.7f + plans[p].widthUnits * 0.5f + 8f;
                    if (Mathf.Abs(cx - padCx) < minDist) clear = false;
                }

                if (!clear) continue;

                float depth = radius * 0.28f;
                for (int i = 0; i < heights.Length; i++)
                {
                    float r = Mathf.Abs(startX + i * stepX - cx);
                    if (r > radius * 2f) continue;

                    float offset = 0f;
                    if (r < radius)
                    {
                        float t = r / radius;
                        offset -= depth * (1f - t * t);
                    }

                    float rim = (r - radius) / (radius * 0.22f);
                    offset += depth * 0.5f * Mathf.Exp(-rim * rim);
                    heights[i] += offset;
                }

                return;
            }
        }

        // ------------------------------------------------------------------
        // Plataformas
        // ------------------------------------------------------------------

        private static void KindData(PadKind kind, out int multiplier, out float widthFactor, out float heightFrac)
        {
            switch (kind)
            {
                case PadKind.Peak: multiplier = 4; widthFactor = 0.9f; heightFrac = 0.86f; break;
                case PadKind.Crater: multiplier = 3; widthFactor = 1.4f; heightFrac = 0.22f; break;
                case PadKind.Canyon: multiplier = 5; widthFactor = 1.0f; heightFrac = 0.12f; break;
                case PadKind.Ledge: multiplier = 4; widthFactor = 1.2f; heightFrac = 0.50f; break;
                default: multiplier = 2; widthFactor = 1.7f; heightFrac = 0.40f; break;
            }
        }

        private List<PadPlan> PlanPads(int pointCount, float stepX)
        {
            PadKind[] priority = { PadKind.Plain, PadKind.Peak, PadKind.Crater, PadKind.Canyon, PadKind.Ledge };
            var plans = new List<PadPlan>();

            for (int i = 0; i < padCount && i < priority.Length; i++)
            {
                KindData(priority[i], out int mult, out float widthFactor, out float heightFrac);
                float widthUnits = minPadWidth * widthFactor;
                plans.Add(new PadPlan
                {
                    kind = priority[i],
                    multiplier = mult,
                    widthUnits = widthUnits,
                    heightFrac = heightFrac,
                    widthSeg = Mathf.Max(2, Mathf.CeilToInt(widthUnits / stepX)),
                    wallSide = Rand01() < 0.5f ? -1 : 1
                });
            }

            // Barajar para que el orden en el mapa cambie en cada partida.
            for (int i = plans.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                PadPlan tmp = plans[i];
                plans[i] = plans[j];
                plans[j] = tmp;
            }

            // Repartir en zonas iguales a lo largo del mapa.
            int margin = 6;
            int span = pointCount - 2 * margin;
            float zoneWidth = span / (float)plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                PadPlan plan = plans[i];
                float centerIdx = margin + zoneWidth * (i + 0.3f + 0.4f * Rand01());
                int start = Mathf.RoundToInt(centerIdx - plan.widthSeg * 0.5f);
                start = Mathf.Clamp(start, margin, pointCount - 1 - margin - plan.widthSeg);

                plan.startIdx = start;
                plan.padH = Mathf.Lerp(minHeight, maxHeight, plan.heightFrac);
                plans[i] = plan;
            }

            return plans;
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Esculpe el relieve alrededor de una plataforma según su tipo.</summary>
        private void ShapePad(float[] heights, PadPlan plan, float startX, float stepX)
        {
            float padStartX = startX + plan.startIdx * stepX;
            float padEndX = startX + (plan.startIdx + plan.widthSeg) * stepX;

            for (int i = 0; i < heights.Length; i++)
            {
                float x = startX + i * stepX;
                float d;
                bool right;

                if (x < padStartX) { d = padStartX - x; right = false; }
                else if (x > padEndX) { d = x - padEndX; right = true; }
                else { heights[i] = plan.padH; continue; }

                float orig = heights[i];
                float res = orig;

                switch (plan.kind)
                {
                    case PadKind.Plain:
                        {
                            // Rampa muy suave que se funde con el terreno.
                            float target = plan.padH + d * 0.12f;
                            res = Mathf.Lerp(orig, target, 1f - Smooth(4f, 8f, d));
                            break;
                        }
                    case PadKind.Canyon:
                        {
                            // Paredes casi verticales a ambos lados.
                            float target = plan.padH + Mathf.Min(d * 3f, 6.5f);
                            res = Mathf.Lerp(orig, Mathf.Max(orig, target), 1f - Smooth(5f, 8f, d));
                            break;
                        }
                    case PadKind.Peak:
                        {
                            // Cono de montaña con la plataforma en la cima.
                            float target = plan.padH - d * 1.1f;
                            res = Mathf.Max(orig, target);
                            break;
                        }
                    case PadKind.Ledge:
                        {
                            bool onWall = (right ? 1 : -1) == plan.wallSide;
                            if (onWall)
                            {
                                float target = plan.padH + Mathf.Min(d * 1.6f, 4.5f);
                                res = Mathf.Lerp(orig, Mathf.Max(orig, target), 1f - Smooth(5f, 8f, d));
                            }
                            else
                            {
                                // Precipicio.
                                float target = plan.padH - d * 2.8f;
                                res = Mathf.Lerp(orig, Mathf.Min(orig, target), 1f - Smooth(3.5f, 7f, d));
                            }
                            break;
                        }
                    case PadKind.Crater:
                        {
                            const float craterRadius = 5.2f;
                            const float craterDepth = 2.8f;
                            float bowl = craterDepth * Smooth(0f, craterRadius, d);
                            float rimT = (d - craterRadius) / (craterRadius * 0.2f);
                            float rim = 0.8f * Mathf.Exp(-rimT * rimT);
                            float target = plan.padH + bowl + rim;
                            res = Mathf.Lerp(orig, target, 1f - Smooth(craterRadius * 1.15f, craterRadius * 1.7f, d));
                            break;
                        }
                }

                heights[i] = res;
            }
        }

        private static void FlattenPads(float[] heights, List<PadPlan> plans)
        {
            for (int p = 0; p < plans.Count; p++)
            {
                for (int k = 0; k <= plans[p].widthSeg; k++)
                {
                    int idx = plans[p].startIdx + k;
                    if (idx >= 0 && idx < heights.Length) heights[idx] = plans[p].padH;
                }
            }
        }

        // ------------------------------------------------------------------
        // Visuales
        // ------------------------------------------------------------------

        private void EnsureComponents()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (edgeCollider == null) edgeCollider = GetComponent<EdgeCollider2D>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        }

        private void UpdateLineRenderer()
        {
            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.loop = false;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.numCapVertices = 2;
            lineRenderer.positionCount = terrainPoints3D.Length;
            lineRenderer.SetPositions(terrainPoints3D);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.sortingOrder = sortingOrder;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            ApplyStyle();
        }

        private void UpdateCollider()
        {
            if (edgeCollider != null && terrainPoints2D != null)
            {
                edgeCollider.SetPoints(new List<Vector2>(terrainPoints2D));
            }
        }

        private void ApplyStyle()
        {
            lineRenderer.sharedMaterial = ResolveMaterial();
            ApplyColor(lineRenderer, terrainColor);
        }

        private void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = Color.white;
            lr.endColor = Color.white;

            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);
        }

        private Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            if (runtimeMaterial != null) return runtimeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("VectorTerrain: no se encontró ningún shader Unlit. " +
                               "Asigna un material en el campo 'Line Material'.", this);
                return null;
            }

            runtimeMaterial = new Material(shader) { name = "VectorTerrain (runtime)" };
            return runtimeMaterial;
        }

        /// <summary>Reconstruye estilo, estratos, fondo y plataformas sin regenerar el relieve.</summary>
        private void RebuildVisuals()
        {
            EnsureComponents();
            ClearVisuals();
            UpdateLineRenderer();
            CreateStrata();
            CreateBackgroundRidges();
            CreatePadVisuals();
        }

        private void ClearVisuals()
        {
            blinkingBeacons.Clear();
            fxObjects.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);

                // La bandera plantada se conserva aunque se reconstruyan los visuales
                // (por ejemplo al tocar valores en el Inspector con el juego en marcha).
                if (child.name == FlagName) continue;

                if (child.name.StartsWith(FxPrefix) || child.name.StartsWith("PadVisual_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private LineRenderer CreateLine(string childName, Vector3[] points, Color color, float lineW, int order, bool loop = false)
        {
            var go = new GameObject(FxPrefix + childName);
            go.transform.SetParent(transform, false);
            fxObjects.Add(go);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.loop = loop;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.startWidth = lineW;
            lr.endWidth = lineW;
            lr.sortingOrder = order;
            lr.sharedMaterial = ResolveMaterial();
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);

            ApplyColor(lr, color);
            return lr;
        }

        /// <summary>Contornos paralelos bajo la superficie que se van apagando (efecto de estratos).</summary>
        private void CreateStrata()
        {
            for (int k = 1; k <= strataLines; k++)
            {
                var pts = new Vector3[terrainPoints3D.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i] = terrainPoints3D[i] + new Vector3(0f, -k * strataSpacing, 0f);
                }

                float fade = 1f - k / (float)(strataLines + 1);
                Color c = Color.Lerp(Color.black, terrainColor, 0.15f + 0.45f * fade);
                CreateLine("Strata_" + k, pts, c, lineWidth * 0.55f, sortingOrder - 1);
            }
        }

        /// <summary>
        /// Cordilleras lejanas dibujadas con eliminación de líneas ocultas: solo se ve el tramo
        /// que queda por encima del terreno y de las capas más cercanas.
        /// </summary>
        private void CreateBackgroundRidges()
        {
            if (backgroundLayers <= 0) return;

            int n = Mathf.Max(60, segments);
            float spanX = width * 1.5f;
            float step = spanX / n;
            float startX = -spanX * 0.5f;
            float range = maxHeight - minHeight;

            // ys[0] = capa más lejana ... ys[last] = más cercana.
            float[][] ys = new float[backgroundLayers][];
            for (int L = 0; L < backgroundLayers; L++)
            {
                float t = backgroundLayers == 1 ? 0f : L / (float)(backgroundLayers - 1);
                float baseY = Mathf.Lerp(minHeight, maxHeight, Mathf.Lerp(0.72f, 0.45f, t));
                float amp = range * Mathf.Lerp(0.6f, 0.42f, t);
                float freq = mountainFrequency * Mathf.Lerp(0.7f, 1.15f, t);

                ys[L] = new float[n + 1];
                for (int j = 0; j <= n; j++)
                {
                    float x = startX + j * step;
                    float r = RidgeFbm(x, freq, 4, 0.5f, 100f * (L + 1));
                    ys[L][j] = baseY + amp * (r - 0.35f);
                }
            }

            for (int L = 0; L < backgroundLayers; L++)
            {
                float t = backgroundLayers == 1 ? 0f : L / (float)(backgroundLayers - 1);
                Color color = Color.Lerp(Color.black, terrainColor, Mathf.Lerp(0.16f, 0.32f, t));
                float lw = lineWidth * 0.7f;
                int order = sortingOrder - 10 + L;

                var run = new List<Vector3>();
                int runIndex = 0;

                for (int j = 0; j <= n; j++)
                {
                    float x = startX + j * step;
                    float occluder = SampleHeight(Mathf.Clamp(x, -width * 0.5f, width * 0.5f));
                    if (x < -width * 0.5f || x > width * 0.5f) occluder = float.MinValue;
                    for (int M = L + 1; M < backgroundLayers; M++)
                    {
                        occluder = Mathf.Max(occluder, ys[M][j]);
                    }

                    bool visible = ys[L][j] > occluder + 0.15f;
                    if (visible)
                    {
                        run.Add(new Vector3(x, ys[L][j], 0f));
                    }

                    if ((!visible || j == n) && run.Count > 0)
                    {
                        if (run.Count >= 2)
                        {
                            CreateLine($"Ridge_{L}_{runIndex++}", run.ToArray(), color, lw, order);
                        }
                        run.Clear();
                    }
                }
            }
        }

        private void CreatePadVisuals()
        {
            for (int i = 0; i < landingPads.Count; i++)
            {
                LandingPad pad = landingPads[i];
                Color padColor = PadColor(pad.multiplier);
                int order = sortingOrder + 1;

                // Línea gruesa de la plataforma.
                CreateLine($"Pad_{i}_{pad.multiplier}x",
                    new[] { (Vector3)pad.startPoint, (Vector3)pad.endPoint },
                    padColor, lineWidth * padLineWidthMultiplier, order);

                // Balizas en los extremos; parpadean en las plataformas difíciles.
                bool blink = pad.multiplier >= 4;
                CreateBeacon($"Beacon_{i}_L", pad.startPoint, padColor, blink);
                CreateBeacon($"Beacon_{i}_R", pad.endPoint, padColor, blink);
            }
        }

        private Color PadColor(int multiplier)
        {
            if (multiplier >= 5) return colorPad5x;
            if (multiplier == 4) return colorPad4x;
            if (multiplier == 3) return colorPad3x;
            return colorPad2x;
        }

        private void CreateBeacon(string childName, Vector2 basePoint, Color color, bool blink)
        {
            const float h = 0.5f;
            const float w = 0.1f;
            var pts = new[]
            {
                new Vector3(basePoint.x, basePoint.y, 0f),
                new Vector3(basePoint.x, basePoint.y + h, 0f),
                new Vector3(basePoint.x + w, basePoint.y + h + 0.1f, 0f),
                new Vector3(basePoint.x, basePoint.y + h + 0.2f, 0f),
                new Vector3(basePoint.x - w, basePoint.y + h + 0.1f, 0f),
                new Vector3(basePoint.x, basePoint.y + h, 0f),
            };

            LineRenderer lr = CreateLine(childName, pts, color, lineWidth * 0.7f, sortingOrder + 1);
            if (blink) blinkingBeacons.Add(lr);
        }
    }
}