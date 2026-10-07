using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class VectorLanderRenderer : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Geometría
        // ------------------------------------------------------------------

        private const float EngineBottomY = -0.38f;
        private const float OuterFlameHalfWidth = 0.14f;
        private const float InnerFlameHalfWidth = 0.065f;
        private const float InnerFlameLengthRatio = 0.5f;
        private const int FlamePointCount = 5;

        private const int LegStrokeCount = 3;
        private static readonly Vector3 LegAnchor = new Vector3(0.46f, 0.18f, 0f);

        private static readonly Vector3[] ShipOutline =
        {
            new Vector3(-0.20f,  0.66f, 0f),
            new Vector3( 0.20f,  0.66f, 0f),
            new Vector3( 0.34f,  0.50f, 0f),
            new Vector3( 0.34f,  0.30f, 0f),
            new Vector3( 0.46f,  0.30f, 0f),
            new Vector3( 0.46f, -0.05f, 0f),
            new Vector3( 0.36f, -0.20f, 0f),
            new Vector3( 0.14f, -0.20f, 0f),
            new Vector3( 0.19f, EngineBottomY, 0f),
            new Vector3(-0.19f, EngineBottomY, 0f),
            new Vector3(-0.14f, -0.20f, 0f),
            new Vector3(-0.36f, -0.20f, 0f),
            new Vector3(-0.46f, -0.05f, 0f),
            new Vector3(-0.46f,  0.30f, 0f),
            new Vector3(-0.34f,  0.30f, 0f),
            new Vector3(-0.34f,  0.50f, 0f),
        };

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

        private static readonly Stroke[] CenterStrokes =
        {
            new Stroke(true,
                new Vector3(-0.13f, 0.56f, 0f),
                new Vector3( 0.13f, 0.56f, 0f),
                new Vector3( 0.09f, 0.41f, 0f),
                new Vector3(-0.09f, 0.41f, 0f)),

            new Stroke(false,
                new Vector3(-0.34f, 0.30f, 0f),
                new Vector3( 0.34f, 0.30f, 0f)),

            new Stroke(false,
                new Vector3(-0.46f, 0.10f, 0f),
                new Vector3( 0.46f, 0.10f, 0f)),

            new Stroke(true,
                new Vector3(-0.12f, 0.24f, 0f),
                new Vector3( 0.12f, 0.24f, 0f),
                new Vector3( 0.12f, 0.14f, 0f),
                new Vector3(-0.12f, 0.14f, 0f)),

            new Stroke(false,
                new Vector3(-0.155f, -0.29f, 0f),
                new Vector3( 0.155f, -0.29f, 0f)),

            new Stroke(false,
                new Vector3(0.08f, 0.66f, 0f),
                new Vector3(0.14f, 0.86f, 0f)),
            new Stroke(false,
                new Vector3(0.07f, 0.82f, 0f),
                new Vector3(0.14f, 0.78f, 0f),
                new Vector3(0.21f, 0.82f, 0f)),
        };

        private static readonly Stroke[] MirroredStrokes =
        {
            new Stroke(false,
                new Vector3(0.46f,  0.18f, 0f),
                new Vector3(0.85f, -0.56f, 0f)),

            new Stroke(false,
                new Vector3(0.46f, -0.05f, 0f),
                new Vector3(0.68f, -0.22f, 0f),
                new Vector3(0.85f, -0.56f, 0f)),

            new Stroke(false,
                new Vector3(0.70f, -0.56f, 0f),
                new Vector3(1.00f, -0.56f, 0f)),

            // Propulsores RCS ampliados
            new Stroke(true,
                new Vector3(0.34f, 0.48f, 0f),
                new Vector3(0.46f, 0.48f, 0f),
                new Vector3(0.46f, 0.36f, 0f),
                new Vector3(0.34f, 0.36f, 0f)),

            new Stroke(false,
                new Vector3(0.28f, 0.30f, 0f),
                new Vector3(0.28f, 0.10f, 0f)),
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Referencias")]
        [SerializeField] private LanderController lander;
        [SerializeField] private LanderTiltRcsRenderer tiltRenderer;
        [SerializeField] private LanderFuelTankRenderer fuelRenderer;

        [Header("Aspecto de la línea")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.2f, 1f)] private float detailWidthScale = 0.6f;
        [SerializeField] private Color lineColor = Color.white;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField, Range(0, 8)] private int cornerVertices = 2;
        [SerializeField, Range(0, 8)] private int capVertices = 2;

        [Header("Llama del propulsor")]
        [SerializeField] private Color flameColor = Color.white;
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

        private readonly List<LineRenderer> detailRenderers = new List<LineRenderer>();
        private readonly List<Stroke> detailStrokes = new List<Stroke>();
        private readonly List<int> detailSides = new List<int>();
        private readonly List<bool> detailIsLeg = new List<bool>();
        private readonly List<GameObject> spawnedDebris = new List<GameObject>();

        private int rightTiltIndex = -1;
        private int leftTiltIndex = -1;
        private bool shipHidden;

        private Rigidbody2D landerBody;
        private Collider2D[] landerColliders;
        private int debrisLayer = -1;

        private Material activeMaterial;
        private Material runtimeMaterial;
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

            if (lander == null) lander = GetComponentInParent<LanderController>();
            if (lander != null) landerBody = lander.GetComponent<Rigidbody2D>();

            // Auto-asociar submódulos si están en el mismo GameObject
            if (tiltRenderer == null) tiltRenderer = GetComponent<LanderTiltRcsRenderer>() ?? gameObject.AddComponent<LanderTiltRcsRenderer>();
            if (fuelRenderer == null) fuelRenderer = GetComponent<LanderFuelTankRenderer>() ?? gameObject.AddComponent<LanderFuelTankRenderer>();

            if (!string.IsNullOrEmpty(debrisLayerName))
                debrisLayer = LayerMask.NameToLayer(debrisLayerName);

            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            BuildDetails();
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
            if (!flameActive) return;

            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        private void OnValidate()
        {
            flameMaxLength = Mathf.Max(flameMinLength, flameMaxLength);
            if (Application.isPlaying && lineRenderer != null && flameRenderer != null) RefreshStyle();
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

            Vector2 anchor = transform.TransformPoint(new Vector3(LegAnchor.x * side, LegAnchor.y, 0f));

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                if (!detailIsLeg[i] || detailSides[i] != side) continue;

                detailRenderers[i].enabled = false;
                SpawnStrokeFragments(detailStrokes[i].Points, detailStrokes[i].Loop,
                                     lineWidth * detailWidthScale, lineColor, anchor, baseVelocity, speed, 1f);
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

            if (tiltRenderer != null) tiltRenderer.SetHidden(true);
            if (fuelRenderer != null)
            {
                fuelRenderer.SetHidden(true);
                fuelRenderer.SpawnDebris(center, baseVelocity, fragmentSpeed);
            }

            SpawnStrokeFragments(ShipOutline, true, lineWidth, lineColor, center, baseVelocity, fragmentSpeed, 1f);

            Color leftColor = lineColor;
            Color rightColor = lineColor;
            if (tiltRenderer != null) tiltRenderer.GetTiltColors(out leftColor, out rightColor);

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                Color strokeColor = lineColor;
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
                SpawnFragment(a, b, flameColor, lineWidth * 0.7f, velocity, Random.Range(-360f, 360f),
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

            foreach (Stroke stroke in CenterStrokes)
            {
                detailStrokes.Add(stroke);
                detailSides.Add(0);
                detailIsLeg.Add(false);
            }

            for (int j = 0; j < MirroredStrokes.Length; j++)
            {
                bool isLeg = j < LegStrokeCount;

                detailStrokes.Add(MirroredStrokes[j]);
                detailSides.Add(1);
                detailIsLeg.Add(isLeg);

                detailStrokes.Add(Mirror(MirroredStrokes[j]));
                detailSides.Add(-1);
                detailIsLeg.Add(isLeg);
            }

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                LineRenderer lr = CreateChildLine("Detail_" + i);
                ConfigureLine(lr, detailStrokes[i].Points, detailStrokes[i].Loop,
                              lineColor, lineWidth * detailWidthScale);
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
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameColor, lineWidth * detailWidthScale);

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
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                ConfigureLine(detailRenderers[i], detailStrokes[i].Points, detailStrokes[i].Loop,
                              lineColor, lineWidth * detailWidthScale);
            }

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameColor, lineWidth * detailWidthScale);

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
    }
}