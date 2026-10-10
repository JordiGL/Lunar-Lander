using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    public sealed class MoonTerrain : TerrainBase
    {
        [Header("Geometría Base y Ruido")]
        [SerializeField, Min(200)] private int segments = 520;
        [SerializeField, Min(100f)] private float width = 200f;
        [SerializeField] private float minHeight = -10f;
        [SerializeField] private float maxHeight = 16f;

        [Header("Fractales")]
        [SerializeField, Min(0.005f)] private float mountainFrequency = 0.018f;
        [SerializeField, Range(0f, 1f)] private float ridgedMix = 0.72f;
        [SerializeField, Range(0.8f, 2.5f)] private float peakSharpness = 1.30f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.55f;
        [SerializeField, Range(0f, 1.5f)] private float microDetail = 0.75f;
        [SerializeField, Range(2f, 12f)] private float microFrequency = 6.5f;
        [SerializeField, Range(0f, 1f)] private float fractalJaggedness = 0.65f;

        [Header("Detalles del mapa")]
        [SerializeField, Range(10f, 60f)] private float edgeFadeMargin = 30f;
        [SerializeField, Range(0, 15)] private int craterCount = 5;
        [SerializeField] private int randomSeed = 0;

        [Header("Plataformas")]
        [SerializeField, Range(2, 50)] private int padCount = 5;
        [SerializeField, Min(2.2f)] private float minPadWidth = 3.0f;

        private System.Random rng;
        private float seedOffset;

        [ContextMenu("Regenerar Moon Terrain")]
        public override void GenerateTerrain()
        {
            rng = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            seedOffset = (float)rng.NextDouble() * 1000f;
            minGeneratedHeight = minHeight;

            // 1. Incorporar el buffer exterior manteniendo la resolución/densidad de vértices
            float totalWidth = width + (outerBufferWidth * 2f);
            int totalSegments = Mathf.RoundToInt(segments * (totalWidth / width));
            int pointCount = totalSegments + 1;
            float stepX = totalWidth / totalSegments;
            float startX = -totalWidth * 0.5f;

            landingPads.Clear();

            // 2. Ruido Base continuo sobre todo el ancho total
            float[] heights = BuildBaseHeights(pointCount, stepX, startX);

            // 3. Planificar Plataformas (PadEdgeMarginSegments protege el buffer exterior y los límites)
            List<PadPlan> plans = PlanPads(pointCount, stepX);

            // 4. Añadir Cráteres (respetando Pads y márgenes)
            for (int c = 0; c < craterCount; c++)
            {
                AddDecorativeCrater(heights, plans, startX, stepX, totalWidth);
            }

            // 5. Tallar Plataformas
            for (int p = 0; p < plans.Count; p++)
            {
                ShapePad(heights, plans[p], startX, stepX);
            }

            FlattenPads(heights, plans);
            for (int i = 0; i < pointCount; i++) heights[i] = Mathf.Clamp(heights[i], minHeight, maxHeight);
            FlattenPads(heights, plans);

            // 6. Suavizar Bordes en los extremos exteriores del mapa total
            ApplyEdgeFade(heights, startX, stepX, totalWidth);

            // 7. Registrar LandingPads para el juego
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

            // 8. Generar Arrays para Líneas y Colliders
            terrainPoints3D = new Vector3[pointCount];
            terrainPoints2D = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                terrainPoints3D[i] = new Vector3(x, heights[i], 0f);
                terrainPoints2D[i] = new Vector2(x, heights[i]);
            }

            // 9. Construir gráficos vectoriales
            BuildTerrainGraphics();
        }

        private float Rand01() => (float)rng.NextDouble();
        private float RandRange(float a, float b) => Mathf.Lerp(a, b, Rand01());

        private float[] BuildBaseHeights(int pointCount, float stepX, float startX)
        {
            float[] v = new float[pointCount];
            float persistence = Mathf.Lerp(0.40f, 0.62f, roughness);
            float lo = float.MaxValue, hi = float.MinValue;

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                float broad = Mathf.PerlinNoise((x + seedOffset) * mountainFrequency * 0.5f, 3.7f);
                float ridged = RidgeFbm(x, mountainFrequency, 5, persistence, 0f);
                float macro = Mathf.Lerp(broad, ridged, ridgedMix);

                float midRoll = (Mathf.PerlinNoise((x + seedOffset * 1.3f) * mountainFrequency * 2.8f, 7.1f) - 0.5f) * 0.45f;

                float highFreq = mountainFrequency * microFrequency * 4f;
                float microNoise1 = Mathf.PerlinNoise((x + seedOffset * 2.1f) * highFreq, 14.2f);
                float microNoise2 = Mathf.PerlinNoise((x + seedOffset * 3.7f) * (highFreq * 2.3f), 29.5f);

                float jagged = Mathf.Abs(2f * microNoise1 - 1f);
                float microOffset = (Mathf.Lerp(microNoise1 - 0.5f, jagged - 0.5f, fractalJaggedness) + (microNoise2 - 0.5f) * 0.5f) * microDetail * 0.35f;

                v[i] = macro + midRoll + microOffset;
                lo = Mathf.Min(lo, v[i]);
                hi = Mathf.Max(hi, v[i]);
            }

            float range = Mathf.Max(0.0001f, hi - lo);
            for (int i = 0; i < pointCount; i++)
            {
                float norm = (v[i] - lo) / range;
                v[i] = Mathf.Lerp(minHeight, maxHeight, Mathf.Pow(norm, peakSharpness));
            }
            return v;
        }

        private float RidgeFbm(float x, float frequency, int octaves, float persistence, float offset)
        {
            float sum = 0f, amp = 1f, norm = 0f, freq = frequency;
            for (int o = 0; o < octaves; o++)
            {
                float n = Mathf.PerlinNoise((x + seedOffset) * freq + offset, 5.3f + o * 7.1f);
                float r = Mathf.Pow(1f - Mathf.Abs(2f * n - 1f), 1.25f);
                sum += r * amp;
                norm += amp;
                amp *= persistence;
                freq *= 2.05f;
            }
            return sum / norm;
        }

        private void ApplyEdgeFade(float[] heights, float startX, float stepX, float currentTotalWidth)
        {
            float halfW = currentTotalWidth * 0.5f;
            float fadeStart = halfW - edgeFadeMargin;

            for (int i = 0; i < heights.Length; i++)
            {
                float x = Mathf.Abs(startX + i * stepX);
                if (x > fadeStart)
                {
                    float smoothT = Mathf.Clamp01((x - fadeStart) / edgeFadeMargin);
                    smoothT = smoothT * smoothT * (3f - 2f * smoothT);
                    heights[i] = Mathf.Lerp(heights[i], minHeight + 0.5f, smoothT);
                }
            }
        }

        private void AddDecorativeCrater(float[] heights, List<PadPlan> plans, float startX, float stepX, float currentTotalWidth)
        {
            float radius = RandRange(2.5f, 5.5f);
            float margin = outerBufferWidth + edgeFadeMargin;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float cx = RandRange(startX + radius * 2f + margin, startX + currentTotalWidth - radius * 2f - margin);

                bool clear = true;
                for (int p = 0; p < plans.Count && clear; p++)
                {
                    float padCx = startX + (plans[p].startIdx + plans[p].widthSeg * 0.5f) * stepX;
                    if (Mathf.Abs(cx - padCx) < radius * 1.7f + plans[p].widthUnits * 0.5f + 10f) clear = false;
                }
                if (!clear) continue;

                float depth = radius * 0.28f;
                for (int i = 0; i < heights.Length; i++)
                {
                    float r = Mathf.Abs(startX + i * stepX - cx);
                    if (r > radius * 2f) continue;

                    float offset = r < radius ? -depth * (1f - (r / radius) * (r / radius)) : 0f;
                    float rim = (r - radius) / (radius * 0.22f);
                    offset += depth * 0.5f * Mathf.Exp(-rim * rim);
                    heights[i] += offset;
                }
                return;
            }
        }

        private struct PadPlan
        {
            public PadKind kind;
            public int multiplier;
            public float widthUnits;
            public float heightFrac;
            public int startIdx;
            public int widthSeg;
            public float padH;
            public int wallSide;
        }

        private void KindData(PadKind kind, out int multiplier, out float widthFactor, out float heightFrac)
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

            for (int i = plans.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = plans[i]; plans[i] = plans[j]; plans[j] = tmp;
            }

            // PadEdgeMarginSegments reserva el buffer exterior + límites del stage + margen propio
            int edgeMarginSegments = PadEdgeMarginSegments(pointCount, stepX, edgeFadeMargin);
            float zoneWidth = (pointCount - 2 * edgeMarginSegments) / (float)plans.Count;

            for (int i = 0; i < plans.Count; i++)
            {
                var plan = plans[i];
                float centerIdx = edgeMarginSegments + zoneWidth * (i + 0.3f + 0.4f * Rand01());
                plan.startIdx = Mathf.Clamp(Mathf.RoundToInt(centerIdx - plan.widthSeg * 0.5f), edgeMarginSegments, pointCount - 1 - edgeMarginSegments - plan.widthSeg);
                plan.padH = Mathf.Lerp(minHeight, maxHeight, plan.heightFrac);
                plans[i] = plan;
            }

            // Pasada para evitar solapamientos entre plataformas
            plans.Sort((a, b) => a.startIdx.CompareTo(b.startIdx));
            for (int i = 1; i < plans.Count; i++)
            {
                PadPlan prev = plans[i - 1], cur = plans[i];
                int minStart = prev.startIdx + prev.widthSeg + 4;
                if (cur.startIdx < minStart) { cur.startIdx = minStart; plans[i] = cur; }
            }

            // Pasada inversa para garantizar que no se empuje ninguna al margen derecho
            for (int i = plans.Count - 1; i >= 0; i--)
            {
                PadPlan cur = plans[i];
                int maxStart = (i == plans.Count - 1)
                    ? pointCount - 1 - edgeMarginSegments - cur.widthSeg
                    : plans[i + 1].startIdx - 4 - cur.widthSeg;
                if (cur.startIdx > maxStart) { cur.startIdx = maxStart; plans[i] = cur; }
            }

            return plans;
        }

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

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
                float slopeNoise = (Mathf.PerlinNoise(x * 1.5f, 4.2f) - 0.5f) * 0.35f;

                switch (plan.kind)
                {
                    case PadKind.Plain: res = Mathf.Lerp(orig, plan.padH + d * 0.12f + slopeNoise, 1f - Smooth(6f, 14f, d)); break;
                    case PadKind.Canyon: res = Mathf.Lerp(orig, Mathf.Max(orig, plan.padH + Mathf.Min(d * 3.2f, 10f) + slopeNoise), 1f - Smooth(7f, 14f, d)); break;
                    case PadKind.Peak: res = Mathf.Max(orig, plan.padH - d * 1.15f + slopeNoise); break;
                    case PadKind.Ledge:
                        if ((right ? 1 : -1) == plan.wallSide) res = Mathf.Lerp(orig, Mathf.Max(orig, plan.padH + Mathf.Min(d * 1.8f, 7f) + slopeNoise), 1f - Smooth(7f, 14f, d));
                        else res = Mathf.Lerp(orig, Mathf.Min(orig, plan.padH - d * 2.8f + slopeNoise), 1f - Smooth(5f, 12f, d));
                        break;
                    case PadKind.Crater:
                        float bowl = 4.2f * Smooth(0f, 7.5f, d);
                        float rim = 1.1f * Mathf.Exp(-Mathf.Pow((d - 7.5f) / (7.5f * 0.2f), 2));
                        res = Mathf.Lerp(orig, plan.padH + bowl + rim + slopeNoise, 1f - Smooth(7.5f * 1.15f, 7.5f * 1.7f, d));
                        break;
                }
                heights[i] = res;
            }
        }

        private static void FlattenPads(float[] heights, List<PadPlan> plans)
        {
            foreach (var plan in plans)
            {
                for (int k = 0; k <= plan.widthSeg; k++)
                {
                    int idx = plan.startIdx + k;
                    if (idx >= 0 && idx < heights.Length) heights[idx] = plan.padH;
                }
            }
        }
    }
}