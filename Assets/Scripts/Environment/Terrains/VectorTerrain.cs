using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    public enum PadKind
    {
        Plain, Crater, Ledge, Peak, Canyon
    }

    [System.Serializable]
    public struct LandingPad
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
        public int multiplier;
        public PadKind kind;

        public Vector2 Center => (startPoint + endPoint) * 0.5f;
        public float Width => Mathf.Abs(endPoint.x - startPoint.x);
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public abstract class VectorTerrain : MonoBehaviour
    {
        protected const string FxPrefix = "TerrainFX_";
        protected const string FlagName = FxPrefix + "Flag";
        protected const float FillZ = 0.5f;
        protected const float FillEdgeExtension = 200f;

        [Header("Estilo Retro (Línea Vectorial)")]
        [SerializeField] protected Color vectorRidgeColor = new Color(0.15f, 0.95f, 1.0f);
        [SerializeField] protected float mainLineWidth = 0.08f;
        [SerializeField] protected Material lineMaterial;
        [SerializeField] protected int sortingOrder = 5;

        [Header("Efectos Adicionales")]
        [SerializeField] protected bool enableWireframeGrid = true;
        [SerializeField] protected Color gridLineColor = new Color(0.65f, 0.15f, 0.85f, 0.45f);
        [SerializeField, Range(6, 24)] protected int gridColumns = 16;
        [SerializeField, Range(3, 10)] protected int gridHorizontalRungs = 6;
        [SerializeField] protected float gridLineWidth = 0.04f;

        [Header("Relleno de Fondo")]
        [SerializeField] protected bool fillTerrain = true;
        [SerializeField] protected Color synthwaveFillTop = new Color(0.12f, 0.04f, 0.22f);
        [SerializeField] protected Color synthwaveFillDeep = new Color(0.015f, 0.005f, 0.035f);
        [SerializeField, Min(0.5f)] protected float fillFadeDepth = 10f;
        [SerializeField, Min(5f)] protected float fillDepth = 50f;

        [Header("Resplandor de Neón")]
        [SerializeField] protected bool neonGlow = true;
        [SerializeField, Range(0f, 1f)] protected float glowIntensity = 0.5f;

        [Header("Plataformas (Colores Retro)")]
        [SerializeField] protected Color colorPad2x = new Color(1f, 1f, 1f);
        [SerializeField] protected Color colorPad3x = new Color(1f, 0.92f, 0.2f);
        [SerializeField] protected Color colorPad4x = new Color(1f, 0.55f, 0.1f);
        [SerializeField] protected Color colorPad5x = new Color(1f, 0.15f, 0.45f);
        [SerializeField] protected float padLineWidthMultiplier = 1.8f;
        [SerializeField, Min(0f)] protected float beaconBlinkRate = 2.5f;

        [Header("Bandera")]
        [SerializeField] protected LanderController lander;
        [SerializeField] protected bool plantFlagOnLanding = true;
        [SerializeField] protected bool flagWaves = true;
        [SerializeField, Min(0.3f)] protected float flagPoleHeight = 1.6f;
        [SerializeField, Min(0.2f)] protected float flagWidth = 0.9f;
        [SerializeField, Min(0.2f)] protected float flagHeight = 0.55f;
        [SerializeField, Min(0.2f)] protected float flagRaiseDuration = 1.8f;
        [SerializeField, Min(0.5f)] protected float flagOffsetFromLander = 1.2f;

        // Variables Internas Base
        protected LineRenderer lineRenderer;
        protected EdgeCollider2D edgeCollider;
        protected MaterialPropertyBlock propertyBlock;
        protected Material runtimeMaterial;
        protected Material runtimeFillMaterial;

        protected Vector3[] terrainPoints3D;
        protected Vector2[] terrainPoints2D;
        protected readonly List<LandingPad> landingPads = new List<LandingPad>();

        protected readonly List<Mesh> fillMeshes = new List<Mesh>();
        protected readonly List<GameObject> fxObjects = new List<GameObject>();
        protected readonly List<LineRenderer> blinkingBeacons = new List<LineRenderer>();
        protected readonly List<VectorFlag> flags = new List<VectorFlag>();
        protected readonly HashSet<int> flaggedPads = new HashSet<int>();

        protected float blinkTimer;
        protected bool landerSubscribed;
        protected float minGeneratedHeight = 0f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public IReadOnlyList<LandingPad> LandingPads => landingPads;

        protected virtual void Awake()
        {
            EnsureComponents();
        }

        protected virtual void OnEnable() => SubscribeLander();
        protected virtual void OnDisable() => UnsubscribeLander();

        protected virtual void Update()
        {
            if (blinkingBeacons.Count == 0 || beaconBlinkRate <= 0f) return;

            blinkTimer += Time.deltaTime;
            bool on = ((int)(blinkTimer * beaconBlinkRate * 2f) & 1) == 0;
            for (int i = 0; i < blinkingBeacons.Count; i++)
            {
                if (blinkingBeacons[i] != null) blinkingBeacons[i].enabled = on;
            }
        }

        protected virtual void OnDestroy()
        {
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
            if (runtimeFillMaterial != null) Destroy(runtimeFillMaterial);
            DestroyFillMeshes();
        }

        public abstract void GenerateTerrain();

        public float SampleHeight(float x)
        {
            if (terrainPoints2D == null || terrainPoints2D.Length < 2) return 0f;
            float step = (terrainPoints2D[terrainPoints2D.Length - 1].x - terrainPoints2D[0].x) / (terrainPoints2D.Length - 1);
            float f = (x - terrainPoints2D[0].x) / step;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, terrainPoints2D.Length - 2);
            float t = Mathf.Clamp01(f - i);
            return Mathf.Lerp(terrainPoints2D[i].y, terrainPoints2D[i + 1].y, t);
        }

        protected void EnsureComponents()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (edgeCollider == null) edgeCollider = GetComponent<EdgeCollider2D>();
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        }

        protected void BuildTerrainGraphics()
        {
            EnsureComponents();
            ClearVisuals();

            if (lineRenderer != null && terrainPoints3D != null)
            {
                lineRenderer.useWorldSpace = false;
                lineRenderer.alignment = LineAlignment.TransformZ;
                lineRenderer.loop = false;
                lineRenderer.numCornerVertices = 2;
                lineRenderer.numCapVertices = 2;
                lineRenderer.positionCount = terrainPoints3D.Length;
                lineRenderer.SetPositions(terrainPoints3D);
                lineRenderer.startWidth = mainLineWidth;
                lineRenderer.endWidth = mainLineWidth;
                lineRenderer.sortingOrder = sortingOrder;
                lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;

                ApplyStyle();
            }

            if (edgeCollider != null && terrainPoints2D != null)
            {
                edgeCollider.SetPoints(new List<Vector2>(terrainPoints2D));
            }

            CreateSynthwaveFill();
            if (enableWireframeGrid) CreateWireframeGrid();
            CreateNeonGlow();
            CreatePadVisuals();
        }

        protected void ClearVisuals()
        {
            blinkingBeacons.Clear();
            fxObjects.Clear();
            DestroyFillMeshes();
            ClearFlags();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == FlagName) continue;

                if (child.name.StartsWith(FxPrefix) || child.name.StartsWith("PadVisual_") || child.name.StartsWith("Beacon_") || child.name.StartsWith("NeonGlow_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        public void ClearFlags()
        {
            for (int i = 0; i < flags.Count; i++)
            {
                if (flags[i] != null)
                {
                    if (Application.isPlaying) Destroy(flags[i].gameObject);
                    else DestroyImmediate(flags[i].gameObject);
                }
            }
            flags.Clear();
            flaggedPads.Clear();
        }

        protected void ApplyStyle()
        {
            lineRenderer.sharedMaterial = ResolveMaterial();
            ApplyColor(lineRenderer, vectorRidgeColor);
        }

        protected void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);
        }

        protected Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;
            if (runtimeMaterial != null) return runtimeMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            runtimeMaterial = new Material(shader) { name = "VectorTerrain (runtime)" };
            return runtimeMaterial;
        }

        protected Material ResolveFillMaterial()
        {
            if (runtimeFillMaterial != null) return runtimeFillMaterial;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            runtimeFillMaterial = new Material(shader) { name = "VectorTerrain Fill" };
            return runtimeFillMaterial;
        }

        protected LineRenderer CreateLine(string childName, Vector3[] points, Color color, float lineW, int order, bool loop = false)
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
            lr.sharedMaterial = ResolveFillMaterial();
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = points.Length;
            lr.SetPositions(points);

            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        protected void CreateNeonGlow()
        {
            if (!neonGlow || terrainPoints3D == null) return;

            float[] glowWidths = { mainLineWidth * 2.5f, mainLineWidth * 6f, mainLineWidth * 12f };
            float[] alphas = { 0.35f, 0.15f, 0.05f };

            for (int k = 0; k < glowWidths.Length; k++)
            {
                Color c = vectorRidgeColor;
                c.a = alphas[k] * glowIntensity;
                CreateLine($"NeonGlow_{k}", terrainPoints3D, c, glowWidths[k], sortingOrder - 1);
            }
        }

        protected void CreateWireframeGrid()
        {
            if (terrainPoints2D == null || terrainPoints2D.Length < 2) return;

            float width = terrainPoints2D[terrainPoints2D.Length - 1].x - terrainPoints2D[0].x;
            float stepX = width / (terrainPoints2D.Length - 1);
            float startX = terrainPoints2D[0].x;
            float bottomY = minGeneratedHeight - 2f;

            float maxHeight = minGeneratedHeight;
            for (int i = 0; i < terrainPoints2D.Length; i++)
            {
                if (terrainPoints2D[i].y > maxHeight) maxHeight = terrainPoints2D[i].y;
            }

            float rungStep = (maxHeight - minGeneratedHeight) / (gridHorizontalRungs + 1);
            for (int r = 1; r <= gridHorizontalRungs; r++)
            {
                float targetH = minGeneratedHeight + r * rungStep;
                var rungPts = new List<Vector3>();

                for (int i = 0; i < terrainPoints2D.Length; i++)
                {
                    if (terrainPoints2D[i].y >= targetH)
                    {
                        rungPts.Add(new Vector3(terrainPoints2D[i].x, targetH, 0f));
                    }
                    else if (rungPts.Count > 1)
                    {
                        CreateLine($"WireRung_{r}_{i}", rungPts.ToArray(), gridLineColor, gridLineWidth, sortingOrder - 2);
                        rungPts.Clear();
                    }
                    else
                    {
                        rungPts.Clear();
                    }
                }
                if (rungPts.Count > 1) CreateLine($"WireRung_{r}_end", rungPts.ToArray(), gridLineColor, gridLineWidth, sortingOrder - 2);
            }

            int segments = terrainPoints2D.Length - 1;
            int colStep = Mathf.Max(2, segments / gridColumns);
            for (int i = colStep; i < segments; i += colStep)
            {
                float x = startX + i * stepX;
                float peakY = SampleHeight(x);

                if (peakY > bottomY + 1f)
                {
                    Vector3[] vertLine = new Vector3[] { new Vector3(x, peakY, 0f), new Vector3(x, bottomY, 0f) };
                    CreateLine($"WireCol_{i}", vertLine, gridLineColor, gridLineWidth, sortingOrder - 3);
                }
            }
        }

        protected void CreateSynthwaveFill()
        {
            if (!fillTerrain || terrainPoints2D == null || terrainPoints2D.Length < 2) return;

            int n = terrainPoints2D.Length;
            int cols = n + 2;
            const int rows = 3;
            float bottomY = minGeneratedHeight - fillDepth;

            var verts = new Vector3[cols * rows];
            var colors = new Color[cols * rows];

            for (int c = 0; c < cols; c++)
            {
                float x, y;
                if (c == 0) { x = terrainPoints2D[0].x - FillEdgeExtension; y = minGeneratedHeight; }
                else if (c == cols - 1) { x = terrainPoints2D[n - 1].x + FillEdgeExtension; y = minGeneratedHeight; }
                else { x = terrainPoints2D[c - 1].x; y = terrainPoints2D[c - 1].y; }

                float midY = Mathf.Max(y - fillFadeDepth, bottomY + 1f);
                int b = c * rows;

                verts[b + 0] = new Vector3(x, y, FillZ);
                verts[b + 1] = new Vector3(x, midY, FillZ);
                verts[b + 2] = new Vector3(x, bottomY, FillZ);

                colors[b + 0] = synthwaveFillTop;
                colors[b + 1] = synthwaveFillDeep;
                colors[b + 2] = synthwaveFillDeep;
            }

            var tris = new int[(cols - 1) * (rows - 1) * 6];
            int t = 0;
            for (int c = 0; c < cols - 1; c++)
            {
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = c * rows + r;
                    int b = a + 1;
                    int cc = (c + 1) * rows + r;
                    int d = cc + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = d;
                    tris[t++] = a; tris[t++] = d; tris[t++] = cc;
                }
            }

            var mesh = new Mesh { name = "VectorTerrain_SynthFill", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            fillMeshes.Add(mesh);

            var go = new GameObject(FxPrefix + "SynthFill");
            go.transform.SetParent(transform, false);
            fxObjects.Add(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResolveFillMaterial();
            mr.sortingOrder = sortingOrder - 10;
        }

        protected void CreatePadVisuals()
        {
            for (int i = 0; i < landingPads.Count; i++)
            {
                LandingPad pad = landingPads[i];
                Color padColor = PadColor(pad.multiplier);
                CreateLine($"Pad_{i}_{pad.multiplier}x", new[] { (Vector3)pad.startPoint, (Vector3)pad.endPoint }, padColor, mainLineWidth * padLineWidthMultiplier, sortingOrder + 1);

                bool blink = pad.multiplier >= 4;
                CreateBeacon($"Beacon_{i}_L", pad.startPoint, padColor, blink);
                CreateBeacon($"Beacon_{i}_R", pad.endPoint, padColor, blink);
            }
        }

        protected Color PadColor(int multiplier)
        {
            if (multiplier >= 5) return colorPad5x;
            if (multiplier == 4) return colorPad4x;
            if (multiplier == 3) return colorPad3x;
            return colorPad2x;
        }

        protected void CreateBeacon(string childName, Vector2 basePoint, Color color, bool blink)
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

            LineRenderer lr = CreateLine(childName, pts, color, mainLineWidth * 0.7f, sortingOrder + 1);
            if (blink) blinkingBeacons.Add(lr);
        }

        protected void DestroyFillMeshes()
        {
            for (int i = 0; i < fillMeshes.Count; i++)
            {
                if (fillMeshes[i] != null)
                {
                    if (Application.isPlaying) Destroy(fillMeshes[i]);
                    else DestroyImmediate(fillMeshes[i]);
                }
            }
            fillMeshes.Clear();
        }

        // ------------------------------------------------------------------
        // Sistema de Banderas
        // ------------------------------------------------------------------

        protected void SubscribeLander()
        {
            if (landerSubscribed) return;
            if (lander == null) lander = FindFirstObjectByType<LanderController>();
            if (lander == null) return;

            lander.OnLanded += HandleLanded;
            landerSubscribed = true;
        }

        protected void UnsubscribeLander()
        {
            if (!landerSubscribed || lander == null) return;
            lander.OnLanded -= HandleLanded;
            landerSubscribed = false;
        }

        private void HandleLanded(LandingResult result)
        {
            if (!plantFlagOnLanding || !result.Success || lander == null) return;

            Vector2 landerLocal = transform.InverseTransformPoint(lander.transform.position);
            int padIndex = FindPadIndex(landerLocal.x);
            if (padIndex < 0) return;

            // Si la base ya tiene bandera, no hacemos nada
            if (flaggedPads.Contains(padIndex)) return;
            flaggedPads.Add(padIndex);

            PlantFlag(landingPads[padIndex], landerLocal.x);
        }

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

        private void PlantFlag(LandingPad pad, float landerX)
        {
            //ClearFlags();

            const float margin = 0.15f;
            float minX = pad.startPoint.x + margin;
            float maxX = pad.endPoint.x - margin;

            int direction = (maxX - landerX) >= (landerX - minX) ? 1 : -1;
            float x = Mathf.Clamp(landerX + direction * flagOffsetFromLander, minX, maxX);

            var go = new GameObject(FlagName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, pad.startPoint.y, 0f);

            VectorFlag flag = go.AddComponent<VectorFlag>();
            flag.Init(ResolveMaterial(), PadColor(pad.multiplier), mainLineWidth * 0.8f, sortingOrder + 2,
                      flagPoleHeight, flagWidth, flagHeight, direction, flagRaiseDuration, flagWaves);

            flags.Add(flag);
        }
    }
}