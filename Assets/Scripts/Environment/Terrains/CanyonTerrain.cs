using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    public sealed class CanyonTerrain : TerrainBase
    {
        [Header("Geometría del Cañón")]
        [SerializeField, Min(200)] private int segments = 520;
        [SerializeField, Min(100f)] private float width = 200f;
        [SerializeField] private float minHeight = -15f;
        [SerializeField] private float maxHeight = 10f;

        [Header("Generación de Mesetas y Barrancos")]
        [Tooltip("Frecuencia base de los cortes del cañón.")]
        [SerializeField, Min(0.005f)] private float canyonFrequency = 0.015f;
        [Tooltip("Agresividad de las caídas (mayor = más vertical).")]
        [SerializeField, Range(1f, 15f)] private float cliffSteepness = 8f;
        [Tooltip("Detalle de las rocas en las paredes del cañón.")]
        [SerializeField, Range(0f, 2f)] private float rockDetail = 0.8f;

        [Header("Detalles del mapa")]
        [SerializeField, Range(10f, 60f)] private float edgeFadeMargin = 30f;
        [SerializeField] private int randomSeed = 0;

        [Header("Plataformas")]
        [SerializeField, Range(2, 50)] private int padCount = 4;
        [SerializeField, Min(2.2f)] private float minPadWidth = 3.5f;

        private System.Random rng;
        private float seedOffset;

        [ContextMenu("Regenerar Canyon Terrain")]
        public override void GenerateTerrain()
        {
            rng = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            seedOffset = (float)rng.NextDouble() * 1000f;
            minGeneratedHeight = minHeight;

            int pointCount = segments + 1;
            float stepX = width / segments;
            float startX = -width * 0.5f;

            landingPads.Clear();

            // 1. Ruido Base (Mesetas y Barrancos)
            float[] heights = BuildCanyonHeights(pointCount, stepX, startX);

            // 2. Planificar Plataformas
            List<PadPlan> plans = PlanPads(pointCount, stepX);

            // 3. Tallar Plataformas
            for (int p = 0; p < plans.Count; p++) ShapePad(heights, plans[p], startX, stepX);

            FlattenPads(heights, plans);
            for (int i = 0; i < pointCount; i++) heights[i] = Mathf.Clamp(heights[i], minHeight, maxHeight);
            FlattenPads(heights, plans);

            // 4. Suavizar Bordes
            ApplyEdgeFade(heights, startX, stepX);

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

            terrainPoints3D = new Vector3[pointCount];
            terrainPoints2D = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                terrainPoints3D[i] = new Vector3(x, heights[i], 0f);
                terrainPoints2D[i] = new Vector2(x, heights[i]);
            }

            BuildTerrainGraphics();
        }

        private float[] BuildCanyonHeights(int pointCount, float stepX, float startX)
        {
            float[] v = new float[pointCount];
            float lo = float.MaxValue, hi = float.MinValue;

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;

                // Algoritmo de mesetas (Terraced Noise)
                float baseNoise = Mathf.PerlinNoise((x + seedOffset) * canyonFrequency, 10f);

                // Aplicar una curva en S muy agresiva para aplanar arriba/abajo y dejar paredes verticales
                float canyonShape = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((baseNoise - 0.5f) * cliffSteepness + 0.5f));

                // Añadir detalle rocoso a las paredes (se aplica menos en las zonas planas)
                float wallFactor = 1f - Mathf.Abs(canyonShape - 0.5f) * 2f; // 1 en la pared, 0 en el plano
                float rocks = (Mathf.PerlinNoise((x + seedOffset) * canyonFrequency * 10f, 20f) - 0.5f) * rockDetail * wallFactor;

                v[i] = canyonShape + rocks;
                lo = Mathf.Min(lo, v[i]);
                hi = Mathf.Max(hi, v[i]);
            }

            // Normalizar y escalar
            float range = Mathf.Max(0.0001f, hi - lo);
            for (int i = 0; i < pointCount; i++)
            {
                float norm = (v[i] - lo) / range;
                v[i] = Mathf.Lerp(minHeight, maxHeight, norm);
            }
            return v;
        }

        private void ApplyEdgeFade(float[] heights, float startX, float stepX)
        {
            float halfW = width * 0.5f;
            float fadeStart = halfW - edgeFadeMargin;

            for (int i = 0; i < heights.Length; i++)
            {
                float x = Mathf.Abs(startX + i * stepX);
                if (x > fadeStart)
                {
                    float t = Mathf.Clamp01((x - fadeStart) / edgeFadeMargin);
                    heights[i] = Mathf.Lerp(heights[i], maxHeight - 2f, t * t * (3f - 2f * t)); // En el cañón sube hacia el borde
                }
            }
        }

        // Estructuras y Lógica de Pads adaptada
        private struct PadPlan { public PadKind kind; public int multiplier; public float widthUnits; public float heightFrac; public int startIdx; public int widthSeg; public float padH; public int wallSide; }

        private List<PadPlan> PlanPads(int pointCount, float stepX)
        {
            var plans = new List<PadPlan>();
            for (int i = 0; i < padCount; i++)
            {
                // El cañón favorece Ledges (Repisas) y Canyons (Fondo)
                PadKind kind = (i % 2 == 0) ? PadKind.Canyon : PadKind.Ledge;
                int mult = kind == PadKind.Canyon ? 5 : 3;
                float widthUnits = minPadWidth * (mult == 5 ? 0.8f : 1.2f);

                plans.Add(new PadPlan
                {
                    kind = kind,
                    multiplier = mult,
                    widthUnits = widthUnits,
                    heightFrac = kind == PadKind.Canyon ? 0.05f : 0.6f, // Fondo vs Repisa
                    widthSeg = Mathf.Max(2, Mathf.CeilToInt(widthUnits / stepX)),
                    wallSide = Rand01() < 0.5f ? -1 : 1
                });
            }

            // Distribución equitativa
            int margin = Mathf.CeilToInt(edgeFadeMargin / stepX) + 5;
            float zoneWidth = (pointCount - 2 * margin) / (float)plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                float centerIdx = margin + zoneWidth * (i + 0.5f + (Rand01() - 0.5f) * 0.3f);
                plan.startIdx = Mathf.Clamp(Mathf.RoundToInt(centerIdx - plan.widthSeg * 0.5f), margin, pointCount - 1 - margin - plan.widthSeg);
                plan.padH = Mathf.Lerp(minHeight, maxHeight, plan.heightFrac);
                plans[i] = plan;
            }
            return plans;
        }

        private float Rand01() => (float)rng.NextDouble();
        private static float Smooth(float edge0, float edge1, float x) { float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0)); return t * t * (3f - 2f * t); }

        private void ShapePad(float[] heights, PadPlan plan, float startX, float stepX)
        {
            float padStartX = startX + plan.startIdx * stepX;
            float padEndX = startX + (plan.startIdx + plan.widthSeg) * stepX;

            for (int i = 0; i < heights.Length; i++)
            {
                float x = startX + i * stepX;
                float d = x < padStartX ? padStartX - x : (x > padEndX ? x - padEndX : 0f);
                if (d == 0f) { heights[i] = plan.padH; continue; }

                float orig = heights[i];
                float res = orig;
                float slopeNoise = (Mathf.PerlinNoise(x * 2f, 0f) - 0.5f) * 0.2f;

                if (plan.kind == PadKind.Canyon) res = Mathf.Lerp(orig, Mathf.Max(orig, plan.padH + Mathf.Min(d * 4f, 15f) + slopeNoise), 1f - Smooth(4f, 12f, d));
                else if (plan.kind == PadKind.Ledge) res = Mathf.Lerp(orig, plan.padH + slopeNoise, 1f - Smooth(2f, 8f, d)); // Talla una repisa dura

                heights[i] = res;
            }
        }

        private static void FlattenPads(float[] heights, List<PadPlan> plans) { foreach (var p in plans) for (int k = 0; k <= p.widthSeg; k++) heights[p.startIdx + k] = p.padH; }
    }
}