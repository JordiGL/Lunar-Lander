using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    public sealed class AlienTerrain : VectorTerrain
    {
        [Header("Geometría Alienígena")]
        [SerializeField, Min(200)] private int segments = 520;
        [SerializeField, Min(100f)] private float width = 200f;
        [SerializeField] private float minHeight = -8f;
        [SerializeField] private float maxHeight = 18f;

        [Header("Ondas y Púas (Spikes)")]
        [Tooltip("Frecuencia de las ondas alienígenas base.")]
        [SerializeField, Min(0.1f)] private float waveFrequency = 0.5f;
        [Tooltip("Cantidad de púas cristalinas que sobresalen.")]
        [SerializeField, Range(0f, 1f)] private float spikeIntensity = 0.6f;
        [Tooltip("Ruido errático del terreno.")]
        [SerializeField, Range(0f, 2f)] private float alienNoise = 1.2f;

        [Header("Detalles del mapa")]
        [SerializeField, Range(10f, 60f)] private float edgeFadeMargin = 30f;
        [SerializeField] private int randomSeed = 0;

        [Header("Plataformas")]
        [SerializeField, Range(2, 50)] private int padCount = 6;
        [SerializeField, Min(2.2f)] private float minPadWidth = 2.5f;

        private System.Random rng;
        private float seedOffset;

        [ContextMenu("Regenerar Alien Terrain")]
        public override void GenerateTerrain()
        {
            rng = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            seedOffset = (float)rng.NextDouble() * 1000f;
            minGeneratedHeight = minHeight;

            int pointCount = segments + 1;
            float stepX = width / segments;
            float startX = -width * 0.5f;

            landingPads.Clear();

            // 1. Ruido Base (Ondas y Spikes)
            float[] heights = BuildAlienHeights(pointCount, stepX, startX);

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

        private float[] BuildAlienHeights(int pointCount, float stepX, float startX)
        {
            float[] v = new float[pointCount];
            float lo = float.MaxValue, hi = float.MinValue;

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;

                // Interferencia de dos ondas sinusoidales para dar aspecto orgánico/alien
                float wave1 = Mathf.Sin((x + seedOffset) * waveFrequency);
                float wave2 = Mathf.Cos((x + seedOffset) * waveFrequency * 1.618f); // Golden ratio
                float baseWaves = wave1 * wave2;

                // Spikes (Púas cristalinas) hechas elevando el seno a una potencia par muy alta
                float spikePhase = Mathf.Sin((x + seedOffset * 2f) * waveFrequency * 3.5f);
                float spikes = Mathf.Pow(spikePhase, 12f) * spikeIntensity * 5f;

                // Ruido caótico
                float noise = (Mathf.PerlinNoise(x * 0.5f, seedOffset) - 0.5f) * alienNoise;

                v[i] = baseWaves + spikes + noise;
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
                    heights[i] = Mathf.Lerp(heights[i], minHeight + 1f, t * t * (3f - 2f * t));
                }
            }
        }

        // Estructuras y Lógica de Pads
        private struct PadPlan { public PadKind kind; public int multiplier; public float widthUnits; public float heightFrac; public int startIdx; public int widthSeg; public float padH; public int wallSide; }

        private List<PadPlan> PlanPads(int pointCount, float stepX)
        {
            var plans = new List<PadPlan>();
            for (int i = 0; i < padCount; i++)
            {
                PadKind kind = (i % 3 == 0) ? PadKind.Peak : PadKind.Plain;
                int mult = kind == PadKind.Peak ? 5 : 2;
                if (i == 1) mult = 3; // Asegurar algo de variedad

                float widthUnits = minPadWidth * (mult == 5 ? 0.7f : 1.3f); // Muy estrechas las de alien

                plans.Add(new PadPlan
                {
                    kind = kind,
                    multiplier = mult,
                    widthUnits = widthUnits,
                    heightFrac = kind == PadKind.Peak ? 0.9f : 0.3f, // Muy altas o bajas
                    widthSeg = Mathf.Max(2, Mathf.CeilToInt(widthUnits / stepX)),
                    wallSide = 0
                });
            }

            int margin = Mathf.CeilToInt(edgeFadeMargin / stepX) + 5;
            float zoneWidth = (pointCount - 2 * margin) / (float)plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                float centerIdx = margin + zoneWidth * (i + 0.5f + (Rand01() - 0.5f) * 0.4f);
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
                float slopeNoise = (Mathf.PerlinNoise(x * 3f, seedOffset) - 0.5f) * 0.5f; // Más caótico

                if (plan.kind == PadKind.Peak)
                    heights[i] = Mathf.Max(orig, plan.padH - d * 2.5f + slopeNoise); // Agujas afiladas soportando el pad
                else
                    heights[i] = Mathf.Lerp(orig, plan.padH + d * 0.2f + slopeNoise, 1f - Smooth(4f, 10f, d));
            }
        }

        private static void FlattenPads(float[] heights, List<PadPlan> plans) { foreach (var p in plans) for (int k = 0; k <= p.widthSeg; k++) heights[p.startIdx + k] = p.padH; }
    }
}