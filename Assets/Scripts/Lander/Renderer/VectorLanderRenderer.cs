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

        // Depósitos de combustible
        private const float TankMinY = -0.14f;
        private const float TankMaxY = 0.06f;
        private const float TankInset = 0.03f;
        private const float TankRowStep = 0.035f;

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

        private readonly struct Stroke
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

            // Propulsores RCS AMPLIADOS (El contorno se queda normal, el relleno será la luz)
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
                {
                    debrisPhysics = new PhysicsMaterial2D("VectorDebris") { friction = 0.6f, bounciness = 0.35f };
                }
                return debrisPhysics;
            }
        }

        private static Gradient CreateDefaultFuelGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.15f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.10f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.92f, 0.20f), 0.5f),
                    new GradientColorKey(new Color(0.30f, 1f, 0.40f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return gradient;
        }

        private sealed class FuelTank
        {
            public LineRenderer Outline;
            public LineRenderer Fill;
            public Vector3[] OutlinePoints;
            public float XMin;
            public float XMax;
            public bool HasFill;
        }

        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Referencias")]
        [SerializeField] private LanderController lander;

        [Header("Aspecto de la línea")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;
        [SerializeField, Range(0.2f, 1f)] private float detailWidthScale = 0.6f;
        [SerializeField] private Color lineColor = Color.white;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField, Range(0, 8)] private int cornerVertices = 2;
        [SerializeField, Range(0, 8)] private int capVertices = 2;

        [Header("Luces Laterales (Tilt RCS)")]
        [Tooltip("Rellena los propulsores RCS para usarlos como indicadores luminosos de alineación.")]
        [SerializeField] private bool showTiltLight = true;
        [SerializeField] private Color tiltDefaultColor = Color.white;
        [SerializeField] private Color tiltSafeColor = new Color(0.30f, 1f, 0.40f);
        [SerializeField] private Color tiltUnsafeColor = new Color(1f, 0.20f, 0.20f);

        [Header("Depósitos de combustible")]
        [SerializeField] private bool showFuelTanks = true;
        [SerializeField] private bool dualTanks = true;
        [SerializeField] private Gradient fuelGradient = CreateDefaultFuelGradient();
        [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.25f;
        [SerializeField, Min(0f)] private float lowFuelBlinkRate = 3f;

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

        private LineRenderer leftTiltFillRenderer;
        private LineRenderer rightTiltFillRenderer;

        private readonly List<LineRenderer> detailRenderers = new List<LineRenderer>();
        private readonly List<Stroke> detailStrokes = new List<Stroke>();
        private readonly List<int> detailSides = new List<int>();
        private readonly List<bool> detailIsLeg = new List<bool>();
        private readonly List<GameObject> spawnedDebris = new List<GameObject>();

        private int rightTiltIndex = -1;
        private int leftTiltIndex = -1;

        private readonly List<FuelTank> tanks = new List<FuelTank>();
        private readonly List<Vector3> fillPoints = new List<Vector3>();
        private float fuelLevel = 1f;
        private bool fillVisible = true;
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

        public bool IsFlameActive => flameActive;
        public float FuelLevel => fuelLevel;

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

            if (!string.IsNullOrEmpty(debrisLayerName))
                debrisLayer = LayerMask.NameToLayer(debrisLayerName);

            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            BuildDetails();
            BuildTanks();
            BuildTiltIndicator();
            BuildFlame();
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
            if (lander != null) SetFuelLevel(lander.FuelNormalized);
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
            UpdateFuelWarningBlink();
            UpdateTiltIndicator();

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
        // API pública
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

        public void SetFuelLevel(float normalized)
        {
            fuelLevel = Mathf.Clamp01(normalized);
            RedrawTanks();
        }

        // ------------------------------------------------------------------
        // Luces Laterales (Relleno sólido de propulsores RCS)
        // ------------------------------------------------------------------

        private void BuildTiltIndicator()
        {
            Transform oldL = transform.Find("LeftTiltFill");
            if (oldL != null) Destroy(oldL.gameObject);

            Transform oldR = transform.Find("RightTiltFill");
            if (oldR != null) Destroy(oldR.gameObject);

            if (!showTiltLight) return;

            leftTiltFillRenderer = CreateChildLine("LeftTiltFill");
            rightTiltFillRenderer = CreateChildLine("RightTiltFill");

            // Centro del cuadrado RCS (actualizado para coincidir con la caja más grande)
            float yCenter = 0.42f;
            float xCenter = 0.40f;

            // La caja mide ahora 0.12 de lado. Usamos un grosor de relleno de 0.09.
            float fillSize = 0.09f;

            Vector3[] rightFillPoints = new Vector3[] {
                new Vector3(xCenter - fillSize * 0.5f, yCenter, 0f),
                new Vector3(xCenter + fillSize * 0.5f, yCenter, 0f)
            };

            Vector3[] leftFillPoints = new Vector3[] {
                new Vector3(-xCenter + fillSize * 0.5f, yCenter, 0f),
                new Vector3(-xCenter - fillSize * 0.5f, yCenter, 0f)
            };

            ConfigureLine(rightTiltFillRenderer, rightFillPoints, false, tiltDefaultColor, fillSize);
            rightTiltFillRenderer.sortingOrder = sortingOrder + 1; // Un poco por encima para asegurar visibilidad

            ConfigureLine(leftTiltFillRenderer, leftFillPoints, false, tiltDefaultColor, fillSize);
            leftTiltFillRenderer.sortingOrder = sortingOrder + 1;

            rightTiltIndex = -1;
            leftTiltIndex = -1;

            // Buscamos los cuadrados RCS ya dibujados (con Y superior actualizado a 0.48f)
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

        private void GetTiltColors(out Color leftColor, out Color rightColor)
        {
            // Estado por defecto: Blanco brillante
            leftColor = tiltDefaultColor;
            rightColor = tiltDefaultColor;

            if (lander == null) return;

            float deltaAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
            float absAngle = Mathf.Abs(deltaAngle);

            bool badTilt = absAngle > lander.MaxLandingAngle;
            bool goodSpeed = Mathf.Abs(lander.Velocity.x) <= lander.MaxLandingHorizontalSpeed &&
                             Mathf.Abs(lander.Velocity.y) <= lander.MaxLandingVerticalSpeed;

            if (badTilt)
            {
                // Si deltaAngle es positivo (gira a la izquierda), el lado izquierdo baja. Peligro en la izquierda.
                if (deltaAngle > 0f)
                {
                    leftColor = tiltUnsafeColor;
                }
                else
                {
                    rightColor = tiltUnsafeColor;
                }
            }
            else if (goodSpeed)
            {
                // Buen ángulo y buena velocidad = ATERRIZAJE SEGURO
                leftColor = tiltSafeColor;
                rightColor = tiltSafeColor;
            }
        }

        private void UpdateTiltIndicator()
        {
            if (!showTiltLight || shipHidden) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            if (leftTiltFillRenderer != null) ApplyColor(leftTiltFillRenderer, leftColor);
            if (rightTiltFillRenderer != null) ApplyColor(rightTiltFillRenderer, rightColor);
        }

        // ------------------------------------------------------------------
        // Depósitos de combustible
        // ------------------------------------------------------------------

        private void HandleFuelChanged(float fuel, float normalized)
        {
            SetFuelLevel(normalized);
        }

        private void BuildTanks()
        {
            tanks.Clear();
            if (!showFuelTanks) return;

            if (dualTanks)
            {
                AddTank(0.20f, 0.36f);
                AddTank(-0.36f, -0.20f);
            }
            else
            {
                AddTank(-0.10f, 0.10f);
            }
            RedrawTanks();
        }

        private void AddTank(float xMin, float xMax)
        {
            var points = new[]
            {
                new Vector3(xMin, TankMinY, 0f),
                new Vector3(xMax, TankMinY, 0f),
                new Vector3(xMax, TankMaxY, 0f),
                new Vector3(xMin, TankMaxY, 0f),
            };

            var tank = new FuelTank
            {
                XMin = xMin,
                XMax = xMax,
                OutlinePoints = points,
                Outline = CreateChildLine("FuelTank_" + tanks.Count),
                Fill = CreateChildLine("FuelFill_" + tanks.Count),
            };

            float detailWidth = lineWidth * detailWidthScale;
            ConfigureLine(tank.Outline, points, true, lineColor, detailWidth);
            ConfigureLine(tank.Fill, new[] { Vector3.zero, Vector3.zero }, false, lineColor, detailWidth * 0.8f);

            tanks.Add(tank);
        }

        private void RedrawTanks()
        {
            if (tanks.Count == 0) return;

            Color color = fuelGradient.Evaluate(fuelLevel);

            for (int t = 0; t < tanks.Count; t++)
            {
                FuelTank tank = tanks[t];
                ApplyColor(tank.Outline, color);
                ApplyColor(tank.Fill, color);

                BuildFillPoints(tank);

                tank.HasFill = fillPoints.Count > 0;
                tank.Fill.positionCount = fillPoints.Count;
                for (int i = 0; i < fillPoints.Count; i++)
                {
                    tank.Fill.SetPosition(i, fillPoints[i]);
                }
            }
            ApplyTankVisibility();
        }

        private void BuildFillPoints(FuelTank tank)
        {
            fillPoints.Clear();
            if (fuelLevel <= 0.0001f) return;

            float xL = tank.XMin + TankInset;
            float xR = tank.XMax - TankInset;
            float bottom = TankMinY + TankInset;
            float height = (TankMaxY - TankMinY) - 2f * TankInset;
            float levelHeight = fuelLevel * height;

            int fullRows = Mathf.FloorToInt(levelHeight / TankRowStep);
            int row = 0;

            for (; row <= fullRows; row++)
            {
                AddFillRow(row, bottom + row * TankRowStep, xL, xR);
            }

            if (levelHeight - fullRows * TankRowStep > 0.005f)
            {
                AddFillRow(row, bottom + levelHeight, xL, xR);
            }
        }

        private void AddFillRow(int rowIndex, float y, float xL, float xR)
        {
            if ((rowIndex & 1) == 0)
            {
                fillPoints.Add(new Vector3(xL, y, 0f));
                fillPoints.Add(new Vector3(xR, y, 0f));
            }
            else
            {
                fillPoints.Add(new Vector3(xR, y, 0f));
                fillPoints.Add(new Vector3(xL, y, 0f));
            }
        }

        private void UpdateFuelWarningBlink()
        {
            if (tanks.Count == 0 || shipHidden) return;

            bool warning = fuelLevel > 0f && fuelLevel <= lowFuelThreshold && lowFuelBlinkRate > 0f;
            bool visible = !warning || ((int)(Time.time * lowFuelBlinkRate * 2f) & 1) == 0;

            if (visible == fillVisible) return;

            fillVisible = visible;
            ApplyTankVisibility();
        }

        private void ApplyTankVisibility()
        {
            for (int t = 0; t < tanks.Count; t++)
            {
                tanks[t].Outline.enabled = !shipHidden;
                tanks[t].Fill.enabled = !shipHidden && fillVisible && tanks[t].HasFill;
            }
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
            fillVisible = true;

            lineRenderer.enabled = true;
            for (int i = 0; i < detailRenderers.Count; i++)
            {
                detailRenderers[i].enabled = true;
            }

            if (leftTiltFillRenderer != null) leftTiltFillRenderer.enabled = showTiltLight;
            if (rightTiltFillRenderer != null) rightTiltFillRenderer.enabled = showTiltLight;

            if (lander != null) fuelLevel = lander.FuelNormalized;
            RedrawTanks();
            UpdateTiltIndicator();

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

            if (leftTiltFillRenderer != null) leftTiltFillRenderer.enabled = false;
            if (rightTiltFillRenderer != null) rightTiltFillRenderer.enabled = false;
            ApplyTankVisibility();

            SpawnStrokeFragments(ShipOutline, true, lineWidth, lineColor, center, baseVelocity, fragmentSpeed, 1f);

            GetTiltColors(out Color leftColor, out Color rightColor);

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                Color strokeColor = lineColor;
                if (i == rightTiltIndex) strokeColor = rightColor;
                else if (i == leftTiltIndex) strokeColor = leftColor;

                SpawnStrokeFragments(detailStrokes[i].Points, detailStrokes[i].Loop,
                                     lineWidth * detailWidthScale, strokeColor, center, baseVelocity, fragmentSpeed, 1f);
            }

            Color tankColor = fuelGradient.Evaluate(fuelLevel);
            for (int t = 0; t < tanks.Count; t++)
            {
                SpawnStrokeFragments(tanks[t].OutlinePoints, true, lineWidth * detailWidthScale, tankColor,
                                     center, baseVelocity, fragmentSpeed, 1f);
            }

            SpawnSparks(explosionSparkCount, center, fragmentSpeed * 1.4f, baseVelocity);
        }

        private void CacheLanderColliders()
        {
            landerColliders = lander != null
                ? lander.GetComponentsInChildren<Collider2D>()
                : new Collider2D[0];
        }

        private void SpawnStrokeFragments(Vector3[] points, bool loop, float width, Color color, Vector2 center,
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

        private LineRenderer CreateChildLine(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<LineRenderer>();
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

        private void ConfigureLine(LineRenderer lr, Vector3[] points, bool loop, Color color, float width)
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

        private void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = Color.white;
            lr.endColor = Color.white;

            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);
        }

        private void RefreshStyle()
        {
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                ConfigureLine(detailRenderers[i], detailStrokes[i].Points, detailStrokes[i].Loop,
                              lineColor, lineWidth * detailWidthScale);
            }

            float detailWidth = lineWidth * detailWidthScale;
            for (int t = 0; t < tanks.Count; t++)
            {
                ConfigureLine(tanks[t].Outline, tanks[t].OutlinePoints, true, lineColor, detailWidth);
                ConfigureLine(tanks[t].Fill, new[] { Vector3.zero, Vector3.zero }, false, lineColor, detailWidth * 0.8f);
            }

            RedrawTanks();

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameColor, lineWidth * detailWidthScale);

            BuildTiltIndicator();
            UpdateTiltIndicator();
        }

        private Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Shader.Find("Sprites/Default");
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