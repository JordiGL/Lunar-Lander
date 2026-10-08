using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>Diseños de nave disponibles.</summary>
    public enum LanderDesign
    {
        Classic = 0,
        Modern = 1,
        Future = 2,
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed partial class VectorLanderRenderer : MonoBehaviour
    {
        public const string SelectionKey = "LunarLander.Design";

        // ------------------------------------------------------------------
        // Geometría común
        // ------------------------------------------------------------------

        private const float EngineBottomY = -0.38f;
        private const float OuterFlameHalfWidth = 0.14f;
        private const float InnerFlameHalfWidth = 0.065f;
        private const float InnerFlameLengthRatio = 0.5f;
        private const int FlamePointCount = 5;

        // Roles de color de cada trazo.
        private const int RoleHull = 0;     // lineColor
        private const int RoleAccent = 1;   // accentColor
        private const int RoleLeg = 2;      // legColor (se desprende al romperse una pata)
        private const int RoleLight = 3;    // cockpitColor

        public readonly struct Stroke
        {
            public readonly Vector3[] Points;
            public readonly bool Loop;

            public Stroke(bool loop, params Vector3[] points)
            {
                Points = points;
                Loop = loop;
            }
        }

        /// <summary>
        /// Datos de un diseño. Todos respetan la misma envolvente (casco, patas a y=-0.56,
        /// depósitos, luces RCS y pods RCS con el lazo en y=0.48) para no tocar la física
        /// ni los submódulos de depósitos / indicador de inclinación.
        /// </summary>
        private sealed class LanderGeometry
        {
            public readonly Vector3[] Outline;
            public readonly Stroke[] Center;
            public readonly int[] CenterRoles;
            public readonly Stroke[] Mirrored;
            public readonly int[] MirroredRoles;
            public readonly Vector3 LegAnchor;
            public readonly float CockpitY;
            public readonly float CockpitHalfWidth;
            public readonly float CockpitThickness;

            public LanderGeometry(Vector3[] outline, Stroke[] center, int[] centerRoles,
                                  Stroke[] mirrored, int[] mirroredRoles, Vector3 legAnchor,
                                  float cockpitY, float cockpitHalfWidth, float cockpitThickness)
            {
                Outline = outline;
                Center = center;
                CenterRoles = centerRoles;
                Mirrored = mirrored;
                MirroredRoles = mirroredRoles;
                LegAnchor = legAnchor;
                CockpitY = cockpitY;
                CockpitHalfWidth = cockpitHalfWidth;
                CockpitThickness = cockpitThickness;
            }
        }

        private static LanderGeometry GetGeometry(LanderDesign d)
        {
            switch (d)
            {
                case LanderDesign.Modern: return ModernGeometry;
                case LanderDesign.Future: return FutureGeometry;
                case LanderDesign.Classic:
                default: return ClassicGeometry;
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // Capas de resplandor: multiplicador de ancho y alfa (de fuera hacia dentro).
        private static readonly float[] GlowWidthMul = { 3.4f, 1.9f };
        private static readonly float[] GlowAlpha = { 0.10f, 0.28f };

        private static PhysicsMaterial2D debrisPhysics;
        private static PhysicsMaterial2D DebrisPhysics
        {
            get
            {
                if (debrisPhysics == null)
                    debrisPhysics = new PhysicsMaterial2D("VectorDebris") { friction = 0.6f, bounciness = 0.35f };
                return debrisPhysics;
            }
        }

        private sealed class GlowSet
        {
            public LineRenderer Source;
            public LineRenderer[] Layers;
            public float BaseWidth;
        }

        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Referencias")]
        [SerializeField] private LanderController lander;
        [SerializeField] private LanderTiltRcsRenderer tiltRenderer;
        [SerializeField] private LanderFuelTankRenderer fuelRenderer;

        [Header("Diseño de la nave")]
        [SerializeField] private LanderDesign design = LanderDesign.Classic;
        [Tooltip("Si está activo, al iniciar se usa el diseño guardado por el jugador (PlayerPrefs) en lugar del del Inspector.")]
        [SerializeField] private bool useSavedSelection = true;

        [Header("Aspecto de la línea")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.2f, 1f)] private float detailWidthScale = 0.6f;
        [SerializeField] private Color lineColor = new Color(0.25f, 0.95f, 1f);
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField, Range(0, 8)] private int cornerVertices = 2;
        [SerializeField, Range(0, 8)] private int capVertices = 2;

        [Header("Paleta neón")]
        [Tooltip("Detalles secundarios (paneles, antena, tobera).")]
        [SerializeField] private Color accentColor = new Color(1f, 0.30f, 0.85f);
        [Tooltip("Patas del módulo.")]
        [SerializeField] private Color legColor = new Color(0.80f, 0.50f, 1f);
        [Tooltip("Luz de la cabina y detalles luminosos.")]
        [SerializeField] private Color cockpitColor = new Color(0.55f, 1f, 1f);

        [Header("Resplandor (glow)")]
        [SerializeField] private bool glowEnabled = true;
        [SerializeField, Range(0f, 2f)] private float glowIntensity = 1f;
        [SerializeField, Range(0.3f, 2f)] private float glowSpread = 1f;
        [SerializeField, Range(0f, 0.4f)] private float glowPulseAmount = 0.12f;
        [SerializeField, Min(0f)] private float glowPulseSpeed = 2.2f;
        [Tooltip("Material con transparencia para el resplandor. Si está vacío se usa Sprites/Default.")]
        [SerializeField] private Material glowMaterial;

        [Header("Llama del propulsor")]
        [SerializeField] private Color flameColor = new Color(1f, 0.50f, 0.18f);
        [SerializeField] private Color flameCoreColor = new Color(1f, 0.95f, 0.65f);
        [SerializeField, Min(0.05f)] private float flameMinLength = 0.30f;
        [SerializeField, Min(0.05f)] private float flameMaxLength = 0.60f;
        [SerializeField, Min(0.01f)] private float flameFlickerInterval = 0.05f;

        [Header("Daños (explosión / pata rota)")]
        [SerializeField, Min(0.05f)] private float fragmentMaxLength = 0.35f;
        [SerializeField, Min(0.5f)] private float debrisLifetime = 4f;
        [SerializeField, Min(0.1f)] private float debrisFadeTime = 1f;
        [SerializeField, Range(0, 40)] private int explosionSparkCount = 16;
        [SerializeField] private string debrisLayerName = "";

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private LineRenderer lineRenderer;
        private LineRenderer flameRenderer;
        private LineRenderer flameCoreRenderer;
        private LineRenderer cockpitRenderer;

        private LanderGeometry geometry;
        private LanderDesign builtDesign;

        private readonly List<LineRenderer> detailRenderers = new List<LineRenderer>();
        private readonly List<Stroke> detailStrokes = new List<Stroke>();
        private readonly List<int> detailSides = new List<int>();
        private readonly List<bool> detailIsLeg = new List<bool>();
        private readonly List<int> detailRoles = new List<int>();
        private readonly List<GameObject> spawnedDebris = new List<GameObject>();

        private readonly Dictionary<LineRenderer, GlowSet> glowMap = new Dictionary<LineRenderer, GlowSet>();
        private readonly List<GlowSet> glowList = new List<GlowSet>();
        private Vector3[] posBuffer = new Vector3[64];

        private int rightTiltIndex = -1;
        private int leftTiltIndex = -1;
        private bool shipHidden;

        private Rigidbody2D landerBody;
        private Collider2D[] landerColliders;
        private int debrisLayer = -1;

        private Material activeMaterial;
        private Material activeGlowMaterial;
        private Material runtimeMaterial;
        private Material runtimeGlowMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Vector3[] flamePoints;
        private Vector3[] flameCorePoints;
        private bool flameActive;
        private float flickerTimer;

        // Propiedades públicas para submódulos
        public float LineWidth => lineWidth;
        public float DetailWidthScale => detailWidthScale;
        public Color LineColor => lineColor;
        public int SortingOrder => sortingOrder;
        public bool IsFlameActive => flameActive;
        public LanderDesign Design => design;

        // ------------------------------------------------------------------
        // Selección de diseño
        // ------------------------------------------------------------------

        /// <summary>Devuelve el diseño guardado por el jugador, o el valor por defecto.</summary>
        public static LanderDesign LoadSavedDesign(LanderDesign fallback)
        {
            return (LanderDesign)PlayerPrefs.GetInt(SelectionKey, (int)fallback);
        }

        public static void SaveDesign(LanderDesign value)
        {
            PlayerPrefs.SetInt(SelectionKey, (int)value);
            PlayerPrefs.Save();
        }

        /// <summary>Cambia el diseño de la nave en caliente y, opcionalmente, lo guarda.</summary>
        public void SetDesign(LanderDesign value, bool save = true)
        {
            design = value;
            if (save) SaveDesign(value);

            if (lineRenderer != null && flameRenderer != null && builtDesign != design)
                RebuildDesign();
        }

        private void RebuildDesign()
        {
            geometry = GetGeometry(design);
            builtDesign = design;

            ClearShipDetails();
            ConfigureLine(lineRenderer, geometry.Outline, true, lineColor, lineWidth);
            BuildDetails();
            BuildCockpit();

            if (shipHidden)
            {
                lineRenderer.enabled = false;
                for (int i = 0; i < detailRenderers.Count; i++) detailRenderers[i].enabled = false;
                if (cockpitRenderer != null) cockpitRenderer.enabled = false;
            }
        }

        private void ClearShipDetails()
        {
            for (int i = 0; i < detailRenderers.Count; i++)
            {
                LineRenderer lr = detailRenderers[i];
                if (lr == null) continue;
                RemoveGlow(lr);
                Destroy(lr.gameObject);
            }
            detailRenderers.Clear();

            if (cockpitRenderer != null)
            {
                RemoveGlow(cockpitRenderer);
                Destroy(cockpitRenderer.gameObject);
                cockpitRenderer = null;
            }
        }

        private void RemoveGlow(LineRenderer lr)
        {
            glowMap.Remove(lr);
            glowList.RemoveAll(s => s.Source == lr);
        }

        private Color RoleColor(int role)
        {
            switch (role)
            {
                case RoleAccent: return accentColor;
                case RoleLeg: return legColor;
                case RoleLight: return cockpitColor;
                default: return lineColor;
            }
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            flamePoints = new Vector3[FlamePointCount];
            flameCorePoints = new Vector3[FlamePointCount];
            activeMaterial = ResolveMaterial();
            activeGlowMaterial = ResolveGlowMaterial();

            if (useSavedSelection) design = LoadSavedDesign(design);
            geometry = GetGeometry(design);
            builtDesign = design;

            if (lander == null) lander = GetComponentInParent<LanderController>();
            if (lander != null) landerBody = lander.GetComponent<Rigidbody2D>();

            // Auto-asociar submódulos si están en el mismo GameObject
            if (tiltRenderer == null) tiltRenderer = GetComponent<LanderTiltRcsRenderer>() ?? gameObject.AddComponent<LanderTiltRcsRenderer>();
            if (fuelRenderer == null) fuelRenderer = GetComponent<LanderFuelTankRenderer>() ?? gameObject.AddComponent<LanderFuelTankRenderer>();

            if (!string.IsNullOrEmpty(debrisLayerName))
                debrisLayer = LayerMask.NameToLayer(debrisLayerName);

            ConfigureLine(lineRenderer, geometry.Outline, true, lineColor, lineWidth);

            BuildDetails();
            BuildCockpit();
            BuildFlame();

            tiltRenderer.Initialize(this, lander);
            fuelRenderer.Initialize(this, lander);
        }

        private void OnEnable()
        {
            if (lander != null)
            {
                lander.OnThrustChanged += SetFlameActive;
                lander.OnFuelChanged += HandleFuelChanged;
                lander.OnCrashed += HandleCrashed;
                lander.OnReset += HandleReset;
                SetFlameActive(lander.IsThrusting);
            }
            else
            {
                SetFlameActive(false);
            }
        }

        private void Start()
        {
            if (lander != null && fuelRenderer != null)
                fuelRenderer.SetFuelLevel(lander.FuelNormalized);
        }

        private void OnDisable()
        {
            if (lander != null)
            {
                lander.OnThrustChanged -= SetFlameActive;
                lander.OnFuelChanged -= HandleFuelChanged;
                lander.OnCrashed -= HandleCrashed;
                lander.OnReset -= HandleReset;
            }
            SetFlameActive(false);
        }

        private void Update()
        {
            UpdateCockpitPulse();

            if (!flameActive) return;

            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        private void LateUpdate()
        {
            SyncGlow();
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (runtimeGlowMaterial != null) Destroy(runtimeGlowMaterial);
        }

        private void OnValidate()
        {
            flameMaxLength = Mathf.Max(flameMinLength, flameMaxLength);
            if (Application.isPlaying && lineRenderer != null && flameRenderer != null)
            {
                if (design != builtDesign) RebuildDesign();
                else RefreshStyle();
            }
        }

        // ------------------------------------------------------------------
        // API pública y utilidades
        // ------------------------------------------------------------------

        public void SetFlameActive(bool active)
        {
            flameActive = active;
            if (flameRenderer == null) return;
            flameRenderer.enabled = active;
            flameCoreRenderer.enabled = active;

            if (active)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        private void HandleFuelChanged(float fuel, float normalized)
        {
            if (fuelRenderer != null) fuelRenderer.SetFuelLevel(normalized);
        }

        public LineRenderer CreateChildLine(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<LineRenderer>();
        }

        public void ConfigureLine(LineRenderer lr, Vector3[] points, bool loop, Color color, float width)
        {
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.textureMode = LineTextureMode.Stretch;
            lr.loop = loop;
            lr.numCornerVertices = cornerVertices;
            lr.numCapVertices = capVertices;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sortingOrder = sortingOrder;
            lr.sharedMaterial = activeMaterial;

            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            lr.positionCount = points.Length;
            lr.SetPositions(points);

            ConfigureGlow(lr, points, loop, width);
            ApplyColor(lr, color);
        }

        public void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = Color.white;
            lr.endColor = Color.white;

            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);

            if (glowMap.TryGetValue(lr, out GlowSet set))
            {
                for (int k = 0; k < set.Layers.Length; k++)
                {
                    Color g = color;
                    g.a = Mathf.Clamp01(color.a * GlowAlpha[k] * glowIntensity);

                    LineRenderer layer = set.Layers[k];
                    layer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor(BaseColorId, g);
                    propertyBlock.SetColor(ColorId, g);
                    layer.SetPropertyBlock(propertyBlock);
                }
            }
        }

        // ------------------------------------------------------------------
        // Resplandor
        // ------------------------------------------------------------------

        private void ConfigureGlow(LineRenderer lr, Vector3[] points, bool loop, float width)
        {
            // Solo las líneas de la nave (los fragmentos sueltos no llevan glow).
            if (!glowEnabled || (lr.transform.parent != transform && lr != lineRenderer)) return;

            if (!glowMap.TryGetValue(lr, out GlowSet set))
            {
                set = new GlowSet { Source = lr, Layers = new LineRenderer[GlowWidthMul.Length] };
                for (int k = 0; k < set.Layers.Length; k++)
                {
                    var go = new GameObject("Glow_" + k);
                    go.transform.SetParent(lr.transform, false);
                    set.Layers[k] = go.AddComponent<LineRenderer>();
                }
                glowMap[lr] = set;
                glowList.Add(set);
            }

            set.BaseWidth = width;

            for (int k = 0; k < set.Layers.Length; k++)
            {
                LineRenderer g = set.Layers[k];
                g.useWorldSpace = false;
                g.alignment = LineAlignment.TransformZ;
                g.textureMode = LineTextureMode.Stretch;
                g.loop = loop;
                g.numCornerVertices = cornerVertices + 2;
                g.numCapVertices = capVertices + 2;
                float w = width * GlowWidthMul[k] * glowSpread;
                g.startWidth = w;
                g.endWidth = w;
                g.sortingOrder = sortingOrder - 1 - k;
                g.sharedMaterial = activeGlowMaterial;

                g.shadowCastingMode = ShadowCastingMode.Off;
                g.receiveShadows = false;
                g.lightProbeUsage = LightProbeUsage.Off;
                g.reflectionProbeUsage = ReflectionProbeUsage.Off;
                g.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                g.positionCount = points.Length;
                g.SetPositions(points);
            }
        }

        // Mantiene el glow sincronizado con la línea original (posiciones, visibilidad, pulso).
        private void SyncGlow()
        {
            float pulse = 1f + glowPulseAmount * Mathf.Sin(Time.time * glowPulseSpeed);

            for (int i = 0; i < glowList.Count; i++)
            {
                GlowSet set = glowList[i];
                LineRenderer src = set.Source;
                if (src == null) continue;

                bool visible = glowEnabled && src.enabled && src.positionCount > 0;
                int count = src.positionCount;

                if (visible)
                {
                    if (posBuffer.Length < count) posBuffer = new Vector3[count * 2];
                    src.GetPositions(posBuffer);
                }

                for (int k = 0; k < set.Layers.Length; k++)
                {
                    LineRenderer g = set.Layers[k];
                    if (g == null) continue;

                    g.enabled = visible;
                    if (!visible) continue;

                    if (g.positionCount != count) g.positionCount = count;
                    g.SetPositions(posBuffer);
                    g.loop = src.loop;

                    float w = set.BaseWidth * GlowWidthMul[k] * glowSpread * pulse;
                    g.startWidth = w;
                    g.endWidth = w;
                }
            }
        }

        // ------------------------------------------------------------------
        // Cabina
        // ------------------------------------------------------------------

        private Vector3[] CockpitPoints()
        {
            return new[]
            {
                new Vector3(-geometry.CockpitHalfWidth, geometry.CockpitY, 0f),
                new Vector3( geometry.CockpitHalfWidth, geometry.CockpitY, 0f),
            };
        }

        private void BuildCockpit()
        {
            cockpitRenderer = CreateChildLine("CockpitLight");
            ConfigureLine(cockpitRenderer, CockpitPoints(), false, cockpitColor, geometry.CockpitThickness);
            cockpitRenderer.sortingOrder = sortingOrder - 1;
            cockpitRenderer.enabled = !shipHidden;
        }

        private void UpdateCockpitPulse()
        {
            if (cockpitRenderer == null || shipHidden) return;

            float t = 0.65f + 0.35f * Mathf.Sin(Time.time * 3.1f);
            Color c = cockpitColor;
            c.a = Mathf.Lerp(0.25f, 0.85f, t);
            ApplyColor(cockpitRenderer, c);
        }

        // ------------------------------------------------------------------
        // Daños
        // ------------------------------------------------------------------

        private void HandleCrashed(LandingResult result)
        {
            SetFlameActive(false);
            CacheLanderColliders();

            switch (result.Crash)
            {
                case CrashType.LegBroken:
                    BreakLeg(result);
                    break;
                case CrashType.Explosion:
                    Explode(result);
                    break;
            }
        }

        private void HandleReset()
        {
            for (int i = 0; i < spawnedDebris.Count; i++)
            {
                if (spawnedDebris[i] != null) Destroy(spawnedDebris[i]);
            }
            spawnedDebris.Clear();

            shipHidden = false;
            lineRenderer.enabled = true;
            for (int i = 0; i < detailRenderers.Count; i++)
            {
                detailRenderers[i].enabled = true;
            }
            if (cockpitRenderer != null) cockpitRenderer.enabled = true;

            if (tiltRenderer != null) tiltRenderer.ResetState();
            if (fuelRenderer != null) fuelRenderer.ResetState();

            SetFlameActive(lander != null && lander.IsThrusting);
        }

        private void BreakLeg(LandingResult result)
        {
            int side = result.BrokenLegSide >= 0 ? 1 : -1;
            Vector2 baseVelocity = result.LanderVelocity;
            float impactSpeed = new Vector2(result.ImpactHorizontalSpeed, result.ImpactVerticalSpeed).magnitude;
            float speed = Mathf.Clamp(impactSpeed, 1f, 6f) * 0.5f;

            Vector3 legAnchor = geometry.LegAnchor;
            Vector2 anchor = transform.TransformPoint(new Vector3(legAnchor.x * side, legAnchor.y, 0f));

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                if (!detailIsLeg[i] || detailSides[i] != side) continue;

                detailRenderers[i].enabled = false;
                SpawnStrokeFragments(detailStrokes[i].Points, detailStrokes[i].Loop,
                                     lineWidth * detailWidthScale, RoleColor(detailRoles[i]),
                                     anchor, baseVelocity, speed, 1f);
            }
            SpawnSparks(8, anchor, speed + 1.5f, baseVelocity);
        }

        private void Explode(LandingResult result)
        {
            Vector2 baseVelocity = result.LanderVelocity;
            float impactSpeed = new Vector2(result.ImpactHorizontalSpeed, result.ImpactVerticalSpeed).magnitude;
            float fragmentSpeed = Mathf.Lerp(2.5f, 6f, Mathf.InverseLerp(2f, 12f, impactSpeed));

            Vector2 center = Vector2.Lerp(transform.position, result.ImpactPoint, 0.5f);

            shipHidden = true;
            lineRenderer.enabled = false;
            for (int i = 0; i < detailRenderers.Count; i++)
            {
                detailRenderers[i].enabled = false;
            }
            if (cockpitRenderer != null) cockpitRenderer.enabled = false;

            if (tiltRenderer != null) tiltRenderer.SetHidden(true);
            if (fuelRenderer != null)
            {
                fuelRenderer.SetHidden(true);
                fuelRenderer.SpawnDebris(center, baseVelocity, fragmentSpeed);
            }

            SpawnStrokeFragments(geometry.Outline, true, lineWidth, lineColor, center, baseVelocity, fragmentSpeed, 1f);

            Color leftColor = lineColor;
            Color rightColor = lineColor;
            if (tiltRenderer != null) tiltRenderer.GetTiltColors(out leftColor, out rightColor);

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                Color strokeColor = RoleColor(detailRoles[i]);
                if (i == rightTiltIndex) strokeColor = rightColor;
                else if (i == leftTiltIndex) strokeColor = leftColor;

                SpawnStrokeFragments(detailStrokes[i].Points, detailStrokes[i].Loop,
                                     lineWidth * detailWidthScale, strokeColor, center, baseVelocity, fragmentSpeed, 1f);
            }

            SpawnSparks(explosionSparkCount, center, fragmentSpeed * 1.4f, baseVelocity);
        }

        private void CacheLanderColliders()
        {
            landerColliders = lander != null
                ? lander.GetComponentsInChildren<Collider2D>()
                : new Collider2D[0];
        }

        public void SpawnStrokeFragments(Vector3[] points, bool loop, float width, Color color, Vector2 center,
                                         Vector2 baseVelocity, float speed, float lifetimeScale)
        {
            int n = points.Length;
            int segmentCount = loop ? n : n - 1;
            float gravity = landerBody != null ? landerBody.gravityScale : 0.3f;

            for (int s = 0; s < segmentCount; s++)
            {
                Vector3 a = transform.TransformPoint(points[s]);
                Vector3 b = transform.TransformPoint(points[(s + 1) % n]);
                float length = Vector3.Distance(a, b);
                if (length < 0.01f) continue;

                int parts = Mathf.Max(1, Mathf.CeilToInt(length / fragmentMaxLength));
                for (int p = 0; p < parts; p++)
                {
                    Vector3 pa = Vector3.Lerp(a, b, p / (float)parts);
                    Vector3 pb = Vector3.Lerp(a, b, (p + 1) / (float)parts);
                    Vector2 mid = (pa + pb) * 0.5f;

                    Vector2 dir = mid - center;
                    if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle;
                    dir.Normalize();
                    dir = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * (Vector3)dir;

                    Vector2 velocity = baseVelocity * 0.5f + dir * Random.Range(speed * 0.5f, speed);
                    float life = Random.Range(debrisLifetime * 0.7f, debrisLifetime) * lifetimeScale;

                    SpawnFragment(pa, pb, color, width, velocity, Random.Range(-540f, 540f),
                                  true, gravity, 0.1f, life);
                }
            }
        }

        private void SpawnSparks(int count, Vector2 center, float speed, Vector2 baseVelocity)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = Random.Range(0.1f, 0.25f);

                Vector3 a = center + dir * Random.Range(0f, 0.15f);
                Vector3 b = a + (Vector3)(dir * length);

                Vector2 velocity = baseVelocity * 0.3f + dir * Random.Range(speed * 0.6f, speed);
                Color spark = Random.value < 0.5f ? flameColor : flameCoreColor;
                SpawnFragment(a, b, spark, lineWidth * 0.7f, velocity, Random.Range(-360f, 360f),
                              false, 0f, 1.5f, Random.Range(0.4f, 0.9f));
            }
        }

        private void SpawnFragment(Vector3 a, Vector3 b, Color color, float width, Vector2 velocity, float spin,
                                   bool collide, float gravity, float drag, float life)
        {
            float length = Vector3.Distance(a, b);

            var go = new GameObject("VectorDebris");
            go.transform.position = (a + b) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            if (debrisLayer >= 0) go.layer = debrisLayer;
            spawnedDebris.Add(go);

            var line = go.AddComponent<LineRenderer>();
            ConfigureLine(line,
                          new[] { new Vector3(-length * 0.5f, 0f, 0f), new Vector3(length * 0.5f, 0f, 0f) },
                          false, color, width);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = gravity;
            body.drag = drag;
            body.angularDrag = 0.05f;
            body.mass = 0.1f;
            body.velocity = velocity;
            body.angularVelocity = spin;

            if (collide)
            {
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(length, 0.04f);
                box.sharedMaterial = DebrisPhysics;

                if (landerColliders != null)
                {
                    for (int i = 0; i < landerColliders.Length; i++)
                    {
                        if (landerColliders[i] != null) Physics2D.IgnoreCollision(box, landerColliders[i]);
                    }
                }
            }

            go.AddComponent<VectorDebris>().Init(line, color, life, debrisFadeTime);
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        private void BuildDetails()
        {
            detailStrokes.Clear();
            detailSides.Clear();
            detailIsLeg.Clear();
            detailRoles.Clear();

            for (int c = 0; c < geometry.Center.Length; c++)
            {
                detailStrokes.Add(geometry.Center[c]);
                detailSides.Add(0);
                detailIsLeg.Add(false);
                detailRoles.Add(geometry.CenterRoles[c]);
            }

            for (int j = 0; j < geometry.Mirrored.Length; j++)
            {
                int role = geometry.MirroredRoles[j];
                bool isLeg = role == RoleLeg;

                detailStrokes.Add(geometry.Mirrored[j]);
                detailSides.Add(1);
                detailIsLeg.Add(isLeg);
                detailRoles.Add(role);

                detailStrokes.Add(Mirror(geometry.Mirrored[j]));
                detailSides.Add(-1);
                detailIsLeg.Add(isLeg);
                detailRoles.Add(role);
            }

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                LineRenderer lr = CreateChildLine("Detail_" + i);
                ConfigureLine(lr, detailStrokes[i].Points, detailStrokes[i].Loop,
                              RoleColor(detailRoles[i]), lineWidth * detailWidthScale);
                detailRenderers.Add(lr);
            }

            rightTiltIndex = -1;
            leftTiltIndex = -1;
            for (int i = 0; i < detailStrokes.Count; i++)
            {
                if (detailStrokes[i].Loop && detailStrokes[i].Points.Length == 4 &&
                    Mathf.Approximately(detailStrokes[i].Points[0].y, 0.48f))
                {
                    if (detailSides[i] > 0) rightTiltIndex = i;
                    else if (detailSides[i] < 0) leftTiltIndex = i;
                }
            }
        }

        private static Stroke Mirror(Stroke source)
        {
            var points = new Vector3[source.Points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = source.Points[i];
                points[i] = new Vector3(-p.x, p.y, p.z);
            }
            return new Stroke(source.Loop, points);
        }

        private void BuildFlame()
        {
            flameRenderer = CreateChildLine("Flame");
            flameCoreRenderer = CreateChildLine("FlameCore");

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameCoreColor, lineWidth * detailWidthScale);

            // La llama brilla por encima del casco.
            flameRenderer.sortingOrder = sortingOrder + 1;
            flameCoreRenderer.sortingOrder = sortingOrder + 2;

            flameRenderer.enabled = false;
            flameCoreRenderer.enabled = false;
        }

        private void FillFlamePoints(float length)
        {
            float sway = Random.Range(-0.04f, 0.04f);
            FillTeardrop(flamePoints, length, OuterFlameHalfWidth, sway);
            FillTeardrop(flameCorePoints, length * InnerFlameLengthRatio, InnerFlameHalfWidth, sway * 0.5f);
        }

        private static void FillTeardrop(Vector3[] points, float length, float halfWidth, float sway)
        {
            float midY = EngineBottomY - length * 0.55f;
            float tipY = EngineBottomY - length;

            points[0] = new Vector3(-halfWidth, EngineBottomY, 0f);
            points[1] = new Vector3(-halfWidth * 0.4f, midY, 0f);
            points[2] = new Vector3(sway, tipY, 0f);
            points[3] = new Vector3(halfWidth * 0.4f, midY, 0f);
            points[4] = new Vector3(halfWidth, EngineBottomY, 0f);
        }

        private void DrawFlame(float length)
        {
            FillFlamePoints(length);
            flameRenderer.SetPositions(flamePoints);
            flameCoreRenderer.SetPositions(flameCorePoints);
        }

        private void RefreshStyle()
        {
            activeGlowMaterial = ResolveGlowMaterial();

            ConfigureLine(lineRenderer, geometry.Outline, true, lineColor, lineWidth);

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                ConfigureLine(detailRenderers[i], detailStrokes[i].Points, detailStrokes[i].Loop,
                              RoleColor(detailRoles[i]), lineWidth * detailWidthScale);
            }

            if (cockpitRenderer != null)
            {
                ConfigureLine(cockpitRenderer, CockpitPoints(), false, cockpitColor, geometry.CockpitThickness);
                cockpitRenderer.sortingOrder = sortingOrder - 1;
            }

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameCoreColor, lineWidth * detailWidthScale);
            flameRenderer.sortingOrder = sortingOrder + 1;
            flameCoreRenderer.sortingOrder = sortingOrder + 2;

            if (fuelRenderer != null) fuelRenderer.BuildTanks();
            if (tiltRenderer != null)
            {
                tiltRenderer.BuildTiltIndicator();
                tiltRenderer.UpdateTiltIndicator();
            }
        }

        private Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("VectorLanderRenderer: no se encontró ningún shader Unlit. " +
                               "Asigna un material en el campo 'Line Material'.", this);
                return null;
            }

            runtimeMaterial = new Material(shader) { name = "VectorLine (runtime)" };
            return runtimeMaterial;
        }

        private Material ResolveGlowMaterial()
        {
            if (glowMaterial != null) return glowMaterial;
            if (runtimeGlowMaterial != null) return runtimeGlowMaterial;

            // Sprites/Default admite transparencia, necesaria para el halo.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return activeMaterial;
            }

            runtimeGlowMaterial = new Material(shader) { name = "VectorLineGlow (runtime)" };
            return runtimeGlowMaterial;
        }
    }
}
