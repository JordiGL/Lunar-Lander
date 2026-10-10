using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Terreno distópico: el suelo es un vertedero de televisores apilados.
    /// - Las antenas crecen sobre los televisores como plantas (tallo + ramas) y se mecen.
    /// - Las pantallas se encienden y se apagan con el clásico efecto CRT:
    ///   línea horizontal que se colapsa a un punto brillante.
    /// </summary>
    public sealed class DystopianTVTerrain : TerrainBase
    {
        [Header("Geometría")]
        [SerializeField, Min(200)] private int segments = 520;
        [SerializeField, Min(100f)] private float width = 200f;
        [SerializeField] private float minHeight = -10f;
        [SerializeField] private float maxHeight = 14f;
        [SerializeField, Range(0.1f, 1f)] private float heightStep = 0.35f;
        [SerializeField, Min(0.005f)] private float pileFrequency = 0.03f;
        [SerializeField, Range(10f, 60f)] private float edgeFadeMargin = 30f;
        [SerializeField] private int randomSeed = 0;

        [Header("Televisores")]
        [SerializeField, Range(2, 8)] private int minTvSegments = 3;
        [SerializeField, Range(3, 14)] private int maxTvSegments = 6;
        [SerializeField, Range(0.6f, 2f)] private float tvHeightMin = 0.9f;
        [SerializeField, Range(0.8f, 2.5f)] private float tvHeightMax = 1.35f;
        [SerializeField, Range(1, 5)] private int rowsDown = 3;
        [SerializeField] private Color bodyColor = new Color(0.45f, 0.15f, 0.65f, 0.9f);
        [SerializeField] private Color deadScreenColor = new Color(0.25f, 0.2f, 0.35f, 0.7f);
        [Tooltip("Color del interior de las pantallas (visible cuando están apagadas o sin señal codificada).")]
        [SerializeField] private Color screenFillColor = new Color(0.04f, 0.03f, 0.07f, 1f);
        [SerializeField]
        private Color[] screenPalette =
        {
            new Color(0.15f, 0.95f, 1f), new Color(1f, 0.2f, 0.85f), new Color(0.3f, 1f, 0.45f),
            new Color(0.9f, 0.9f, 1f), new Color(1f, 0.25f, 0.25f)
        };

        [Header("Pantallas: parpadeo CRT")]
        [Tooltip("Televisores rotos que nunca se encienden.")]
        [SerializeField, Range(0f, 1f)] private float deadScreenChance = 0.2f;
        [Tooltip("Duración de la animación de encendido / apagado.")]
        [SerializeField, Min(0.05f)] private float blinkDuration = 0.4f;
        [SerializeField, Min(0.5f)] private float onTimeMin = 3f;
        [SerializeField, Min(0.5f)] private float onTimeMax = 12f;
        [SerializeField, Min(0.2f)] private float offTimeMin = 0.8f;
        [SerializeField, Min(0.3f)] private float offTimeMax = 3.5f;
        [Tooltip("Retraso máximo del primer encendido (para que no se enciendan todas a la vez).")]
        [SerializeField, Min(0f)] private float initialDelayMax = 5f;
        [SerializeField, Min(0.02f)] private float flickerInterval = 0.09f;
        [SerializeField, Range(0f, 1f)] private float flickerFraction = 0.08f;

        [Header("Pantallas: señal codificada (estilo Canal+)")]
        [Tooltip("Probabilidad de que un televisor (no roto) muestre la señal codificada, siempre encendida.")]
        [SerializeField, Range(0f, 1f)] private float scrambledChance = 0.3f;
        [Tooltip("Tamaño del 'píxel' del ruido.")]
        [SerializeField, Min(0.03f)] private float pixelSize = 0.07f;
        [SerializeField, Range(2, 8)] private int grayLevels = 5;
        [SerializeField, Min(0.02f)] private float scrambleInterval = 0.09f;
        [Tooltip("Velocidad de la banda brillante que recorre la pantalla de arriba abajo.")]
        [SerializeField, Min(0f)] private float bandSpeed = 0.35f;
        [SerializeField] private Color scrambledOutlineColor = new Color(0.75f, 0.75f, 0.8f);
        [Tooltip("Cada cuánto se apagan las pantallas codificadas (segundos encendidas).")]
        [SerializeField, Min(1f)] private float scrambledOnTimeMin = 8f;
        [SerializeField, Min(1f)] private float scrambledOnTimeMax = 25f;
        [Tooltip("Cuánto tiempo permanecen apagadas (segundos).")]
        [SerializeField, Min(0.5f)] private float scrambledOffTimeMin = 2f;
        [SerializeField, Min(0.5f)] private float scrambledOffTimeMax = 6f;

        [Header("Antenas-planta")]
        [SerializeField, Range(0f, 1f)] private float antennaChance = 0.7f;
        [SerializeField, Range(1, 4)] private int antennasPerTvMax = 2;
        [Tooltip("Longitud de cada varilla telescópica.")]
        [SerializeField, Min(0.1f)] private float antennaMinHeight = 0.6f;
        [SerializeField, Min(0.2f)] private float antennaMaxHeight = 1.3f;
        [Tooltip("Apertura de cada varilla respecto a la vertical (grados): forma la V clásica.")]
        [SerializeField, Range(5f, 60f)] private float openAngleMin = 15f;
        [SerializeField, Range(5f, 80f)] private float openAngleMax = 40f;
        [SerializeField, Min(0.01f)] private float tipRadius = 0.05f;
        [Tooltip("Segundos máximos hasta que una antena empieza a brotar.")]
        [SerializeField, Min(0f)] private float growDelayMax = 6f;
        [SerializeField, Min(0.2f)] private float growDuration = 2.5f;
        [SerializeField, Range(0f, 0.5f)] private float swayAmount = 0.12f;
        [SerializeField, Min(0f)] private float swaySpeed = 1.3f;
        [Tooltip("Segundos que tardan en replegarse / desplegarse las antenas vivas cuando su televisor se apaga / enciende.")]
        [SerializeField, Min(0.05f)] private float antennaRetractTime = 1f;
        [SerializeField] private Color antennaColor = new Color(0.78f, 0.84f, 0.92f);
        [SerializeField] private Color antennaTipColor = new Color(1f, 1f, 1f);

        [Header("Antenas muertas (televisor sin señal codificada)")]
        [SerializeField] private Color deadAntennaColor = new Color(0.32f, 0.30f, 0.38f);
        [SerializeField] private Color deadAntennaTipColor = new Color(0.42f, 0.38f, 0.45f);
        [Tooltip("Ángulo (grados desde la vertical) de las varillas caídas.")]
        [SerializeField, Range(30f, 120f)] private float deadAngleMin = 55f;
        [SerializeField, Range(30f, 140f)] private float deadAngleMax = 100f;
        [Tooltip("Cuánto se doblan hacia abajo las varillas (proporción de su longitud).")]
        [SerializeField, Range(0f, 1f)] private float deadDroop = 0.4f;

        [Header("Plataformas")]
        [SerializeField, Range(2, 50)] private int padCount = 5;
        [SerializeField, Min(2.2f)] private float minPadWidth = 3.2f;

        private struct Block { public int s, e; public float h; public bool pad; }
        private struct PadPlan { public int startIdx, widthSeg, multiplier; public float padH; }

        private enum ScreenState { Off, TurningOn, On, TurningOff }

        private sealed class TvScreen
        {
            public LineRenderer Line;
            public Color Color;
            public ScreenState State;
            public bool Dead;
            public bool Scrambled;
            public Scramble Scramble;
            public float Timer;
            public float Elapsed;
            public float Cx, Cy, Hw, Hh;
            public float BaseWidth;
        }

        private sealed class Scramble
        {
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Transform Transform;
            public Color[] Colors;
            public float[] RowBias;
            public int Cols, Rows;
        }

        private sealed class Plant
        {
            public Vector3 Base;
            public float Height, Phase, Delay;
            public bool Alive;
            public TvScreen Screen;   // televisor al que pertenece (solo antenas vivas)
            public float Retract = 1f; // 1 = desplegada, 0 = replegada
            public float[] Angles;
            public float[] LenScale;
            public float[] Bend;
            public LineRenderer BaseLine;
            public LineRenderer[] Rods;
        }

        private const int RodPoints = 12; // puntos de la varilla (más puntos = escalones más nítidos)
        private const int TipPoints = 9;  // bolita de la punta

        private System.Random rng;
        private float seedOffset;
        private float plantsStartTime;
        private float flickerTimer;

        private readonly List<TvScreen> screens = new List<TvScreen>();
        private readonly List<Plant> plants = new List<Plant>();
        private readonly List<Scramble> scrambles = new List<Scramble>();
        private readonly List<Vector4> fillQuads = new List<Vector4>(); // (xMin, yMin, xMax, yMax) de cada pantalla
        private float scrambleBudget;
        private int scrambleCursor;
        private readonly Vector3[] rectBuffer = new Vector3[4];
        private readonly Vector3[] rodBuffer = new Vector3[RodPoints + TipPoints];
        private readonly Vector3[] baseBuffer = new Vector3[4];

        private float Rand01() => (float)rng.NextDouble();
        private float RandRange(float a, float b) => Mathf.Lerp(a, b, Rand01());
        private static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        [ContextMenu("Regenerar TV Terrain")]
        public override void GenerateTerrain()
        {
            rng = new System.Random(randomSeed != 0 ? randomSeed : System.Environment.TickCount);
            seedOffset = (float)rng.NextDouble() * 1000f;
            minGeneratedHeight = minHeight;
            screens.Clear();
            plants.Clear();
            scrambles.Clear();
            fillQuads.Clear();
            landingPads.Clear();
            plantsStartTime = Time.time;

            int pointCount = segments + 1;
            float stepX = width / segments;
            float startX = -width * 0.5f;

            // 1. Plataformas
            List<PadPlan> plans = PlanPads(pointCount, stepX);

            // 2. Bloques (televisores) entre plataformas
            var blocks = new List<Block>();
            var heights = new float[pointCount];
            int idx = 0, planIdx = 0;
            while (idx < pointCount)
            {
                if (planIdx < plans.Count && idx == plans[planIdx].startIdx)
                {
                    PadPlan p = plans[planIdx++];
                    int end = Mathf.Min(pointCount - 1, p.startIdx + p.widthSeg);
                    blocks.Add(new Block { s = idx, e = end, h = p.padH, pad = true });
                    for (int i = idx; i <= end; i++) heights[i] = p.padH;
                    idx = end + 1;
                    continue;
                }

                int limit = planIdx < plans.Count ? plans[planIdx].startIdx - 1 : pointCount - 1;
                int len = rng.Next(minTvSegments, Mathf.Max(minTvSegments, maxTvSegments) + 1);
                int e2 = Mathf.Min(idx + len - 1, limit);
                if (e2 < idx) { idx = limit + 1; continue; }

                float h = BlockHeight(startX + (idx + e2) * 0.5f * stepX);
                blocks.Add(new Block { s = idx, e = e2, h = h, pad = false });
                for (int i = idx; i <= e2; i++) heights[i] = h;
                idx = e2 + 1;
            }

            // 3. Registrar pads
            foreach (PadPlan p in plans)
            {
                landingPads.Add(new LandingPad
                {
                    startPoint = new Vector2(startX + p.startIdx * stepX, p.padH),
                    endPoint = new Vector2(startX + (p.startIdx + p.widthSeg) * stepX, p.padH),
                    multiplier = p.multiplier,
                    kind = PadKind.Plain
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
            BuildTelevisions(blocks, startX, stepX);
            BuildScreenFill();
        }

        private float BlockHeight(float x)
        {
            float broad = Mathf.PerlinNoise((x + seedOffset) * pileFrequency, 2.3f);
            float mid = Mathf.PerlinNoise((x + seedOffset) * pileFrequency * 3.1f, 8.7f);
            float n = Mathf.Clamp01(broad * 0.75f + mid * 0.25f);
            n = Mathf.Pow(n, 1.2f);
            float h = Mathf.Lerp(minHeight + 1f, maxHeight, n);

            float fadeStart = width * 0.5f - edgeFadeMargin;
            float ax = Mathf.Abs(x);
            if (ax > fadeStart)
            {
                float t = Mathf.Clamp01((ax - fadeStart) / edgeFadeMargin);
                t = t * t * (3f - 2f * t);
                h = Mathf.Lerp(h, minHeight + 0.5f, t);
            }

            h += (Rand01() - 0.5f) * heightStep * 2f;
            return Mathf.Clamp(Mathf.Round(h / heightStep) * heightStep, minHeight, maxHeight);
        }

        private List<PadPlan> PlanPads(int pointCount, float stepX)
        {
            var plans = new List<PadPlan>();
            int margin = Mathf.CeilToInt(edgeFadeMargin / stepX) + 5;
            float zoneWidth = (pointCount - 2 * margin) / (float)padCount;
            int[] mults = { 2, 3, 4, 5, 3, 2, 4 };

            for (int i = 0; i < padCount; i++)
            {
                int mult = mults[i % mults.Length];
                float widthUnits = minPadWidth * (mult >= 5 ? 0.9f : mult == 4 ? 1.1f : 1.5f);
                int widthSeg = Mathf.Max(2, Mathf.CeilToInt(widthUnits / stepX));
                float centerIdx = margin + zoneWidth * (i + 0.5f + (Rand01() - 0.5f) * 0.4f);
                int start = Mathf.Clamp(Mathf.RoundToInt(centerIdx - widthSeg * 0.5f), margin, pointCount - 2 - margin - widthSeg);

                float frac = mult >= 5 ? RandRange(0.7f, 0.9f) : mult == 4 ? RandRange(0.5f, 0.7f) : RandRange(0.2f, 0.45f);
                float padH = Mathf.Round(Mathf.Lerp(minHeight, maxHeight, frac) / heightStep) * heightStep;
                plans.Add(new PadPlan { startIdx = start, widthSeg = widthSeg, multiplier = mult, padH = padH });
            }

            plans.Sort((a, b) => a.startIdx.CompareTo(b.startIdx));
            // Evitar solapes entre plataformas
            for (int i = 1; i < plans.Count; i++)
            {
                PadPlan prev = plans[i - 1], cur = plans[i];
                int minStart = prev.startIdx + prev.widthSeg + 4;
                if (cur.startIdx < minStart) { cur.startIdx = minStart; plans[i] = cur; }
            }
            return plans;
        }

        // ------------------------------------------------------------------
        // Construcción de televisores y antenas-planta
        // ------------------------------------------------------------------

        private void BuildTelevisions(List<Block> blocks, float startX, float stepX)
        {
            float lw = mainLineWidth * 0.55f;
            float floorY = minGeneratedHeight - 1f;

            for (int b = 0; b < blocks.Count; b++)
            {
                Block blk = blocks[b];
                float x0 = startX + blk.s * stepX;
                float x1 = startX + blk.e * stepX;
                if (x1 - x0 < 0.5f) continue;

                float top = blk.h;
                for (int r = 0; r < rowsDown; r++)
                {
                    float tvH = RandRange(tvHeightMin, tvHeightMax);
                    float bot = top - tvH;
                    if (top < floorY) break;

                    // Cuerpo del televisor
                    var body = new[]
                    {
                        new Vector3(x0, top, 0f), new Vector3(x1, top, 0f),
                        new Vector3(x1, bot, 0f), new Vector3(x0, bot, 0f),
                    };
                    CreateLine($"TVBody_{b}_{r}", body, bodyColor, lw, sortingOrder - 2, true);

                    // Pantalla (a la izquierda, con panel de botones a la derecha)
                    float inset = Mathf.Min(0.14f, (x1 - x0) * 0.1f);
                    float sx1 = Mathf.Lerp(x0, x1, 0.78f);
                    float sx0 = x0 + inset;
                    float sTop = top - inset, sBot = bot + inset;

                    float roll = Rand01();
                    bool dead = roll < deadScreenChance;
                    bool scrambled = !dead && roll < deadScreenChance + scrambledChance;
                    Color c = screenPalette[rng.Next(screenPalette.Length)];
                    var scr = new[]
                    {
                        new Vector3(sx0, sTop, 0f), new Vector3(sx1, sTop, 0f),
                        new Vector3(sx1, sBot, 0f), new Vector3(sx0, sBot, 0f),
                    };
                    float scrWidth = lw * 0.9f;
                    LineRenderer lr = CreateLine($"TVScreen_{b}_{r}", scr, scrambled ? scrambledOutlineColor : deadScreenColor, scrWidth, sortingOrder - 1, true);
                    fillQuads.Add(new Vector4(sx0, sBot, sx1, sTop));
                    var tvScreen = new TvScreen
                    {
                        Line = lr,
                        Color = scrambled ? scrambledOutlineColor : c,
                        Dead = dead,
                        Scrambled = scrambled,
                        State = scrambled ? ScreenState.On : ScreenState.Off,
                        Timer = scrambled ? RandRange(scrambledOnTimeMin * 0.3f, scrambledOnTimeMax) : RandRange(0f, initialDelayMax),
                        Cx = (sx0 + sx1) * 0.5f,
                        Cy = (sTop + sBot) * 0.5f,
                        Hw = (sx1 - sx0) * 0.5f,
                        Hh = (sTop - sBot) * 0.5f,
                        BaseWidth = scrWidth
                    };
                    screens.Add(tvScreen);
                    if (scrambled) tvScreen.Scramble = CreateScramble($"{b}_{r}", sx0, sx1, sBot, sTop);

                    // Botón / dial
                    float dx = (sx1 + x1) * 0.5f;
                    float dy = (top + bot) * 0.5f;
                    CreateLine($"TVDial_{b}_{r}", new[] { new Vector3(dx, dy + 0.06f, 0f), new Vector3(dx, dy - 0.06f, 0f) },
                               bodyColor, lw, sortingOrder - 1);

                    // Antenas-planta (solo en la fila superior y fuera de plataformas)
                    if (r == 0 && !blk.pad && Rand01() < antennaChance)
                    {
                        int count = rng.Next(1, antennasPerTvMax + 1);
                        for (int k = 0; k < count; k++)
                        {
                            float px = RandRange(x0 + 0.12f, x1 - 0.12f);
                            CreatePlant($"{b}_{k}", new Vector3(px, top, 0f), lw, scrambled, scrambled ? tvScreen : null);
                        }
                    }

                    top = bot;
                }
            }
        }

        private void CreatePlant(string id, Vector3 basePoint, float lw, bool alive, TvScreen screen)
        {
            // Viva (señal codificada): V erguida y plateada. Muerta: varillas caídas, dobladas, una rota y apagadas.
            float aMin = alive ? Mathf.Min(openAngleMin, openAngleMax) : Mathf.Min(deadAngleMin, deadAngleMax);
            float aMax = alive ? Mathf.Max(openAngleMin, openAngleMax) : Mathf.Max(deadAngleMin, deadAngleMax);
            float height = RandRange(antennaMinHeight, Mathf.Max(antennaMinHeight, antennaMaxHeight));
            int brokenRod = rng.Next(2);

            var plant = new Plant
            {
                Base = basePoint,
                Height = height,
                Alive = alive,
                Screen = screen,
                Retract = 1f,
                Phase = RandRange(0f, Mathf.PI * 2f),
                Delay = RandRange(0f, growDelayMax),
                Angles = new[] { RandRange(aMin, aMax), RandRange(aMin, aMax) },
                LenScale = new[] { 1f, 1f },
                Bend = new[] { 0f, 0f },
                Rods = new LineRenderer[2]
            };

            if (!alive)
            {
                for (int s = 0; s < 2; s++)
                {
                    plant.LenScale[s] = s == brokenRod ? RandRange(0.3f, 0.55f) : RandRange(0.8f, 1f);
                    plant.Bend[s] = height * plant.LenScale[s] * deadDroop * RandRange(0.7f, 1.2f);
                }
            }

            Color mainColor = alive ? antennaColor : deadAntennaColor;
            Color tipColor = alive ? antennaTipColor : deadAntennaTipColor;

            // Base: pequeño zócalo trapezoidal sobre el televisor
            var baseInit = new Vector3[4];
            for (int i = 0; i < baseInit.Length; i++) baseInit[i] = basePoint;
            plant.BaseLine = CreateLine($"TVAntenna_{id}_base", baseInit, mainColor, lw, sortingOrder - 1, true);
            plant.BaseLine.enabled = false;

            // Varillas telescópicas: tres tramos que se van estrechando + bolita en la punta
            for (int s = 0; s < 2; s++)
            {
                float rodLen = plant.Height * plant.LenScale[s];
                float rodFrac = rodLen / (rodLen + 2f * Mathf.PI * tipRadius);
                AnimationCurve widthCurve = BuildTelescopicCurve(rodFrac);
                Gradient colorGradient = BuildRodGradient(rodFrac, mainColor, tipColor);

                var init = new Vector3[RodPoints + TipPoints];
                for (int i = 0; i < init.Length; i++) init[i] = basePoint;
                LineRenderer rod = CreateLine($"TVAntenna_{id}_rod{s}", init, mainColor, lw, sortingOrder - 1);
                rod.widthCurve = widthCurve;
                rod.widthMultiplier = lw * 1.2f;
                rod.colorGradient = colorGradient;
                rod.enabled = false;
                plant.Rods[s] = rod;
            }

            plants.Add(plant);
        }

        private static AnimationCurve BuildTelescopicCurve(float rodFrac)
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f, 0f, 0f),
                new Keyframe(rodFrac * 0.33f, 1f, 0f, 0f),
                new Keyframe(rodFrac * 0.38f, 0.72f, 0f, 0f),
                new Keyframe(rodFrac * 0.66f, 0.72f, 0f, 0f),
                new Keyframe(rodFrac * 0.71f, 0.48f, 0f, 0f),
                new Keyframe(rodFrac, 0.48f, 0f, 0f),
                new Keyframe(rodFrac + 0.01f, 0.9f, 0f, 0f),
                new Keyframe(1f, 0.9f, 0f, 0f));
        }

        private static Gradient BuildRodGradient(float rodFrac, Color mainColor, Color tipColor)
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(mainColor, 0f),
                    new GradientColorKey(mainColor, rodFrac),
                    new GradientColorKey(tipColor, Mathf.Min(1f, rodFrac + 0.01f)),
                    new GradientColorKey(tipColor, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------
        // Animación
        // ------------------------------------------------------------------

        protected override void Update()
        {
            base.Update();
            float dt = Time.deltaTime;
            UpdateScreens(dt);
            UpdatePlants();
            UpdateStaticFlicker(dt);
            UpdateScrambles(dt);
        }

        // ------------------------------------------------------------------
        // Señal codificada estilo Canal+: píxeles grises, bandas y banda rodante
        // ------------------------------------------------------------------

        private Scramble CreateScramble(string id, float x0, float x1, float yBot, float yTop)
        {
            int cols = Mathf.Clamp(Mathf.RoundToInt((x1 - x0) / pixelSize), 2, 48);
            int rows = Mathf.Clamp(Mathf.RoundToInt((yTop - yBot) / pixelSize), 2, 28);
            float cw = (x1 - x0) / cols, ch = (yTop - yBot) / rows;
            float hw = (x1 - x0) * 0.5f, hh = (yTop - yBot) * 0.5f; // la malla se construye centrada

            var verts = new Vector3[cols * rows * 4];
            var colors = new Color[verts.Length];
            var tris = new int[cols * rows * 6];
            int v = 0, t = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float x = -hw + c * cw, y = -hh + r * ch;
                    verts[v] = new Vector3(x, y, 0f);
                    verts[v + 1] = new Vector3(x + cw, y, 0f);
                    verts[v + 2] = new Vector3(x + cw, y + ch, 0f);
                    verts[v + 3] = new Vector3(x, y + ch, 0f);
                    tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                    tris[t++] = v; tris[t++] = v + 3; tris[t++] = v + 2;
                    v += 4;
                }
            }

            var mesh = new Mesh { name = "TVScramble_" + id, vertices = verts, colors = colors, triangles = tris };
            mesh.MarkDynamic();
            mesh.RecalculateBounds();
            fillMeshes.Add(mesh); // la base los destruye al regenerar

            var go = new GameObject(FxPrefix + "Scramble_" + id);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3((x0 + x1) * 0.5f, (yBot + yTop) * 0.5f, 0.02f);
            fxObjects.Add(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResolveFillMaterial();
            mr.sortingOrder = sortingOrder - 1; // por encima de la rejilla; el z (0.02) lo deja bajo el contorno

            var sc = new Scramble
            {
                Mesh = mesh,
                Renderer = mr,
                Transform = go.transform,
                Colors = colors,
                Cols = cols,
                Rows = rows,
                RowBias = new float[rows]
            };
            for (int r = 0; r < rows; r++) sc.RowBias[r] = Rand01();
            scrambles.Add(sc);
            RefreshScramble(sc, 0f);
            return sc;
        }

        /// <summary>Un único mesh estático con el interior de todas las pantallas (color elegible).</summary>
        private void BuildScreenFill()
        {
            if (fillQuads.Count == 0) return;

            var verts = new Vector3[fillQuads.Count * 4];
            var colors = new Color[verts.Length];
            var tris = new int[fillQuads.Count * 6];
            int v = 0, t = 0;
            foreach (Vector4 q in fillQuads)
            {
                verts[v] = new Vector3(q.x, q.y, 0.05f);
                verts[v + 1] = new Vector3(q.z, q.y, 0.05f);
                verts[v + 2] = new Vector3(q.z, q.w, 0.05f);
                verts[v + 3] = new Vector3(q.x, q.w, 0.05f);
                for (int k = 0; k < 4; k++) colors[v + k] = screenFillColor;
                tris[t++] = v; tris[t++] = v + 2; tris[t++] = v + 1;
                tris[t++] = v; tris[t++] = v + 3; tris[t++] = v + 2;
                v += 4;
            }

            var mesh = new Mesh { name = "TVScreenFill", vertices = verts, colors = colors, triangles = tris };
            mesh.RecalculateBounds();
            fillMeshes.Add(mesh);

            var go = new GameObject(FxPrefix + "ScreenFill");
            go.transform.SetParent(transform, false);
            fxObjects.Add(go);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ResolveFillMaterial();
            mr.sortingOrder = sortingOrder - 1; // el z (0.05) lo deja bajo la malla codificada y el contorno
        }

        private void UpdateScrambles(float dt)
        {
            if (scrambles.Count == 0) return;

            // Reparte las actualizaciones entre frames: cada pantalla se refresca cada scrambleInterval
            scrambleBudget += scrambles.Count * dt / scrambleInterval;
            int toUpdate = Mathf.Min(scrambles.Count, Mathf.FloorToInt(scrambleBudget));
            if (toUpdate <= 0) return;
            scrambleBudget -= toUpdate;

            float band = Mathf.Repeat(Time.time * bandSpeed, 1f);
            for (int n = 0; n < toUpdate; n++)
            {
                scrambleCursor = (scrambleCursor + 1) % scrambles.Count;
                RefreshScramble(scrambles[scrambleCursor], band);
            }
        }

        private void RefreshScramble(Scramble sc, float band)
        {
            if (sc.Mesh == null || (sc.Renderer != null && !sc.Renderer.enabled)) return;
            float steps = Mathf.Max(1, grayLevels - 1);
            int v = 0;
            for (int r = 0; r < sc.Rows; r++)
            {
                // Cada fila conserva su tono la mitad de las veces: da el aspecto de líneas desplazadas
                if (Random.value < 0.5f) sc.RowBias[r] = Random.value;
                float rowBias = sc.RowBias[r];

                float rr = r / (float)Mathf.Max(1, sc.Rows - 1);
                float d = Mathf.Abs(Mathf.Repeat(rr - band + 0.5f, 1f) - 0.5f);
                float boost = d < 0.15f ? 0.25f : 0f;

                for (int c = 0; c < sc.Cols; c++)
                {
                    float g = Mathf.Clamp01(rowBias * 0.5f + Random.value * 0.5f + boost);
                    g = Mathf.Round(g * steps) / steps;
                    g = Mathf.Lerp(0.08f, 0.92f, g);
                    var col = new Color(g, g, g, 1f);
                    sc.Colors[v] = col; sc.Colors[v + 1] = col; sc.Colors[v + 2] = col; sc.Colors[v + 3] = col;
                    v += 4;
                }
            }
            sc.Mesh.colors = sc.Colors;
        }

        private void UpdatePlants()
        {
            if (plants.Count == 0) return;
            float time = Time.time;

            const float baseH = 0.07f, baseHalfW = 0.12f, baseTopHalfW = 0.07f, pivotOffset = 0.025f;

            for (int p = 0; p < plants.Count; p++)
            {
                Plant pl = plants[p];
                if (pl.BaseLine == null) continue;

                float raw = Mathf.Clamp01((time - plantsStartTime - pl.Delay) / growDuration);
                if (raw <= 0f) continue; // aún no ha brotado

                float g = 1f - (1f - raw) * (1f - raw) * (1f - raw); // ease-out

                // Las antenas vivas se repliegan cuando su televisor se apaga y se despliegan al encenderse
                bool open = pl.Screen == null || pl.Screen.State == ScreenState.On || pl.Screen.State == ScreenState.TurningOn;
                pl.Retract = Mathf.MoveTowards(pl.Retract, open ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, antennaRetractTime));
                float gr = g * Smooth01(pl.Retract);

                // Zócalo
                float h = baseH * g;
                pl.BaseLine.enabled = true;
                baseBuffer[0] = pl.Base + new Vector3(-baseHalfW * g, 0f, 0f);
                baseBuffer[1] = pl.Base + new Vector3(-baseTopHalfW * g, h, 0f);
                baseBuffer[2] = pl.Base + new Vector3(baseTopHalfW * g, h, 0f);
                baseBuffer[3] = pl.Base + new Vector3(baseHalfW * g, 0f, 0f);
                pl.BaseLine.SetPositions(baseBuffer);

                // Varillas: brotan desplegándose desde la vertical hasta su ángulo y se balancean un poco
                for (int s = 0; s < 2; s++)
                {
                    float side = s == 0 ? -1f : 1f;
                    if (gr <= 0.005f)
                    {
                        if (pl.Rods[s] != null) pl.Rods[s].enabled = false;
                        continue;
                    }
                    float sway = Mathf.Sin(time * swaySpeed + pl.Phase + s * 1.7f) * swayAmount * 25f * (pl.Alive ? 1f : 0.1f);
                    float angle = (pl.Angles[s] * gr + sway) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(side * Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                    Vector3 start = pl.Base + new Vector3(side * pivotOffset * g, h, 0f);
                    float len = pl.Height * pl.LenScale[s] * gr;
                    float bend = pl.Bend[s] * gr; // caída hacia abajo de las antenas muertas

                    for (int k = 0; k < RodPoints; k++)
                    {
                        float t = k / (float)(RodPoints - 1);
                        rodBuffer[k] = start + dir * (len * t) + Vector3.down * (bend * t * t);
                    }

                    // Bolita en la punta: círculo que arranca justo donde acaba la varilla
                    float r = tipRadius * gr * (pl.Alive ? 1f : 0.7f);
                    Vector3 end = start + dir * len + Vector3.down * bend;
                    Vector3 tipDir = (dir * len + Vector3.down * (2f * bend)).normalized;
                    Vector3 center = end + tipDir * r;
                    float phi0 = Mathf.Atan2(-tipDir.y, -tipDir.x);
                    for (int k = 0; k < TipPoints; k++)
                    {
                        float ang = phi0 + k * (2f * Mathf.PI / (TipPoints - 1));
                        rodBuffer[RodPoints + k] = center + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * r;
                    }

                    LineRenderer rod = pl.Rods[s];
                    if (rod == null) continue;
                    rod.enabled = true;
                    rod.SetPositions(rodBuffer);
                }
            }
        }

        private void UpdateScreens(float dt)
        {
            for (int i = 0; i < screens.Count; i++)
            {
                TvScreen s = screens[i];
                if (s.Line == null || s.Dead) continue;

                switch (s.State)
                {
                    case ScreenState.Off:
                        s.Timer -= dt;
                        if (s.Timer <= 0f) { s.State = ScreenState.TurningOn; s.Elapsed = 0f; }
                        break;

                    case ScreenState.TurningOn:
                        s.Elapsed += dt;
                        if (s.Elapsed >= blinkDuration)
                        {
                            s.State = ScreenState.On;
                            s.Timer = OnDuration(s);
                            ApplyScreenShape(s, 1f);
                        }
                        else ApplyScreenShape(s, s.Elapsed / blinkDuration);
                        break;

                    case ScreenState.On:
                        s.Timer -= dt;
                        if (s.Timer <= 0f) { s.State = ScreenState.TurningOff; s.Elapsed = 0f; }
                        break;

                    case ScreenState.TurningOff:
                        s.Elapsed += dt;
                        if (s.Elapsed >= blinkDuration)
                        {
                            s.State = ScreenState.Off;
                            s.Timer = OffDuration(s);
                            SetScreenOff(s);
                        }
                        else ApplyScreenShape(s, 1f - s.Elapsed / blinkDuration);
                        break;
                }
            }
        }

        /// <summary>
        /// open: 0 = punto, 0.4 = línea horizontal completa, 1 = pantalla entera.
        /// Encender recorre 0→1; apagar recorre 1→0 (colapso vertical a línea y luego a punto).
        /// </summary>
        private void ApplyScreenShape(TvScreen s, float open)
        {
            float hScale = Smooth01(open / 0.4f);
            float vScale = open < 0.4f ? 0f : Smooth01((open - 0.4f) / 0.6f);

            // La malla codificada se colapsa igual que el contorno (escala alrededor del centro)
            if (s.Scramble != null && s.Scramble.Renderer != null)
            {
                s.Scramble.Renderer.enabled = true;
                s.Scramble.Transform.localScale = new Vector3(Mathf.Max(0.001f, hScale), Mathf.Max(0.02f, vScale), 1f);
            }

            float hw = Mathf.Max(0.004f, s.Hw * hScale);
            float hh = Mathf.Max(0.004f, s.Hh * vScale);

            rectBuffer[0] = new Vector3(s.Cx - hw, s.Cy + hh, 0f);
            rectBuffer[1] = new Vector3(s.Cx + hw, s.Cy + hh, 0f);
            rectBuffer[2] = new Vector3(s.Cx + hw, s.Cy - hh, 0f);
            rectBuffer[3] = new Vector3(s.Cx - hw, s.Cy - hh, 0f);
            s.Line.SetPositions(rectBuffer);

            // Más blanco y más grueso cuanto más colapsado (brillo del haz de electrones)
            float collapse = 1f - vScale;
            Color c = Color.Lerp(s.Color, Color.white, Mathf.Clamp01(collapse * 0.95f));
            c.a = 1f;
            s.Line.startColor = c;
            s.Line.endColor = c;
            float w = s.BaseWidth * (1f + 1.6f * collapse);
            s.Line.startWidth = w;
            s.Line.endWidth = w;
        }

        private float OnDuration(TvScreen s)
        {
            return s.Scrambled
                ? RandRange(scrambledOnTimeMin, Mathf.Max(scrambledOnTimeMin, scrambledOnTimeMax))
                : RandRange(onTimeMin, Mathf.Max(onTimeMin, onTimeMax));
        }

        private float OffDuration(TvScreen s)
        {
            return s.Scrambled
                ? RandRange(scrambledOffTimeMin, Mathf.Max(scrambledOffTimeMin, scrambledOffTimeMax))
                : RandRange(offTimeMin, Mathf.Max(offTimeMin, offTimeMax));
        }

        private void SetScreenOff(TvScreen s)
        {
            if (s.Scramble != null && s.Scramble.Renderer != null) s.Scramble.Renderer.enabled = false;
            rectBuffer[0] = new Vector3(s.Cx - s.Hw, s.Cy + s.Hh, 0f);
            rectBuffer[1] = new Vector3(s.Cx + s.Hw, s.Cy + s.Hh, 0f);
            rectBuffer[2] = new Vector3(s.Cx + s.Hw, s.Cy - s.Hh, 0f);
            rectBuffer[3] = new Vector3(s.Cx - s.Hw, s.Cy - s.Hh, 0f);
            s.Line.SetPositions(rectBuffer);
            s.Line.startColor = deadScreenColor;
            s.Line.endColor = deadScreenColor;
            s.Line.startWidth = s.BaseWidth;
            s.Line.endWidth = s.BaseWidth;
        }

        private void UpdateStaticFlicker(float dt)
        {
            if (screens.Count == 0) return;

            flickerTimer -= dt;
            if (flickerTimer > 0f) return;
            flickerTimer = flickerInterval;

            int count = Mathf.Max(1, Mathf.RoundToInt(screens.Count * flickerFraction));
            for (int i = 0; i < count; i++)
            {
                TvScreen s = screens[Random.Range(0, screens.Count)];
                if (s.Line == null || s.Dead || s.Scrambled || s.State != ScreenState.On) continue;

                float roll = Random.value;
                Color c;
                if (roll < 0.4f) { float g = Random.Range(0.4f, 1f); c = new Color(g, g, g, 0.95f); } // estática
                else c = s.Color;
                s.Line.startColor = c;
                s.Line.endColor = c;
            }
        }
    }
}