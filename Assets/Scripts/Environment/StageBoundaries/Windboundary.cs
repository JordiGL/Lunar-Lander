using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Límite de viento: rachas horizontales que empujan a la nave hacia dentro (por defecto)
    /// o hacia fuera, con fuerza creciente según la profundidad y algo de turbulencia vertical.
    ///
    /// Visual: solo "hilos" de viento finos, curvados y con los extremos difuminados, en capas
    /// (lejanos lentos y tenues, cercanos más rápidos). Nacen muy por detrás del muro exterior
    /// (upstreamExtent), lo atraviesan y se desvanecen al acercarse a la zona de juego, así que
    /// no hay ningún borde visible y el viento se ve venir desde lejos.
    /// </summary>
    public sealed class WindBoundary : StageBoundaryBase
    {
        public enum WindMode { PushInward, PushOutward }

        private const int WispPoints = 10;
        private const int MaxWispsPerSide = 150;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Viento")]
        [SerializeField] private WindMode mode = WindMode.PushInward;
        [Tooltip("Aceleración máxima del viento (unidades/s²), independiente de la masa.")]
        [SerializeField, Min(0f)] private float windAcceleration = 16f;
        [Tooltip("Fuerza de las rachas verticales aleatorias (0 = viento limpio).")]
        [SerializeField, Range(0f, 1f)] private float turbulence = 0.35f;
        [SerializeField, Min(0.1f)] private float gustSpeed = 1.4f;

        [Header("Hilos de viento")]
        [SerializeField] private Color windColor = new Color(0.70f, 0.92f, 1.00f);
        [Tooltip("Distancia (unidades) por detrás del muro exterior desde la que nacen los hilos. " +
                 "Mayor = el viento ocupa más espacio y se ve venir desde más lejos.")]
        [SerializeField, Min(0f)] private float upstreamExtent = 60f;
        [Tooltip("Hilos por cada 100 unidades de recorrido y por lado. El número total se ajusta solo al tamaño de la zona.")]
        [SerializeField, Range(5f, 120f)] private float wispDensity = 45f;
        [Tooltip("Opacidad máxima de un hilo.")]
        [SerializeField, Range(0f, 1f)] private float wispMaxAlpha = 0.30f;
        [SerializeField, Min(1f)] private float wispSpeed = 20f;
        [SerializeField, Min(0.5f)] private float wispMinLength = 4f;
        [SerializeField, Min(0.5f)] private float wispMaxLength = 11f;
        [SerializeField, Min(0.005f)] private float wispWidth = 0.05f;
        [Tooltip("Ondulación vertical del hilo (unidades).")]
        [SerializeField, Range(0f, 1f)] private float waveAmplitude = 0.25f;
        [Tooltip("Fracción de la zona, desde su borde interior hacia fuera, en la que los hilos se desvanecen. " +
                 "Menor = los hilos llegan más cerca de la zona de juego.")]
        [SerializeField, Range(0.02f, 0.8f)] private float entryFade = 0.15f;

        private sealed class Wisp
        {
            public LineRenderer line;
            public float u;          // progreso a lo largo del recorrido (0..1)
            public float y;          // posición vertical relativa a la cámara (-1..1)
            public float length;
            public float speed;
            public float layer;      // 0 = lejano, 1 = cercano
            public float waveFreq;
            public float wavePhase;
        }

        private readonly List<Wisp>[] wisps = { new List<Wisp>(), new List<Wisp>() };
        private readonly Vector3[] pointBuffer = new Vector3[WispPoints];
        private MaterialPropertyBlock block;

        // Dirección del viento en X: hacia el centro del mapa o hacia fuera.
        private int FlowDir(int side) => mode == WindMode.PushInward ? -side : side;

        // Recorrido de los hilos: desde 'upstreamExtent' por detrás del muro exterior hasta el borde interior de la zona.
        private float PathLength => zoneWidth + upstreamExtent;

        protected override void BuildVisuals()
        {
            if (block == null) block = new MaterialPropertyBlock();
            wisps[0].Clear();
            wisps[1].Clear();

            // Forma común: transparente en la cola, opaco hacia la cabeza, y se apaga en la punta.
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(windColor, 0f), new GradientColorKey(windColor, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });

            var widthCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.7f, 1f),
                new Keyframe(1f, 0.2f));

            int count = Mathf.Clamp(Mathf.RoundToInt(PathLength / 100f * wispDensity), 1, MaxWispsPerSide);

            for (int s = 0; s < 2; s++)
            {
                for (int i = 0; i < count; i++)
                {
                    LineRenderer lr = CreateLine("WindWisp", WispPoints, 1f, sortingOrder + 1);
                    lr.colorGradient = gradient;
                    lr.widthCurve = widthCurve;
                    lr.numCapVertices = 4;

                    var w = new Wisp
                    {
                        line = lr,
                        layer = Random.value,
                        waveFreq = Random.Range(0.8f, 1.8f),
                        wavePhase = Random.value * 6.2831f
                    };
                    Respawn(w, true);
                    wisps[s].Add(w);
                    SetTint(lr, 0f);
                }
            }
        }

        private void Respawn(Wisp w, bool randomizeProgress)
        {
            w.u = randomizeProgress ? Random.value : Mathf.Max(0f, w.u - 1f);
            w.y = Random.Range(-1f, 1f);
            w.length = Mathf.Lerp(wispMinLength, Mathf.Max(wispMinLength, wispMaxLength), w.layer) * Random.Range(0.8f, 1.2f);
            w.speed = wispSpeed * Mathf.Lerp(0.55f, 1.25f, w.layer) * Random.Range(0.85f, 1.15f);
            w.wavePhase = Random.value * 6.2831f;
        }

        private void SetTint(LineRenderer lr, float alpha)
        {
            lr.GetPropertyBlock(block);
            Color c = new Color(1f, 1f, 1f, alpha);
            block.SetColor(ColorId, c);
            block.SetColor(BaseColorId, c);
            lr.SetPropertyBlock(block);
        }

        protected override void ApplyEffect(Rigidbody2D body, float depth01, int side, float dt)
        {
            float flow = FlowDir(side);
            float gust = (Mathf.PerlinNoise(Time.time * gustSpeed, side * 17.3f) - 0.5f) * 2f;

            Vector2 accel = new Vector2(
                flow * windAcceleration * depth01,
                gust * turbulence * windAcceleration * 0.6f * depth01);

            body.AddForce(accel * body.mass, ForceMode2D.Force);
        }

        protected override void UpdateVisuals(float time, float leftDepth01, float rightDepth01)
        {
            float dt = Time.deltaTime;
            float camY = CameraY;
            float halfH = CameraHeight * 0.5f;

            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? Left : Right;
                float depth = s == 0 ? leftDepth01 : rightDepth01;
                bool visible = IsSideVisible(side);
                float intensity = Mathf.Lerp(idleIntensity, 1f, depth);

                int flow = FlowDir(side);
                bool inward = flow == -side;
                float inner = ZoneInner(side);
                // Punto más alejado del recorrido: muy por detrás del muro exterior.
                float upstream = ZoneOuter(side) + side * upstreamExtent;
                // El viento "nace" en el lado desde el que sopla.
                float from = inward ? upstream : inner;
                float to = inward ? inner : upstream;
                float pathLength = Mathf.Max(0.01f, Mathf.Abs(to - from));
                float endFade = Mathf.Clamp(8f / pathLength, 0.02f, 0.3f);
                float fadeDist = Mathf.Max(0.01f, entryFade * zoneWidth);

                float speedMul = Mathf.Lerp(0.7f, 1.5f, depth);
                // Las rachas inclinan un poco los hilos.
                float gustTilt = (Mathf.PerlinNoise(time * gustSpeed * 0.5f, side * 3.1f) - 0.5f) * 2f * depth * 1.2f;

                List<Wisp> list = wisps[s];
                for (int i = 0; i < list.Count; i++)
                {
                    Wisp w = list[i];
                    if (!visible) { w.line.enabled = false; continue; }

                    // La velocidad está en unidades/s, independientemente de lo largo que sea el recorrido.
                    w.u += dt * w.speed * speedMul / pathLength;
                    if (w.u >= 1f) Respawn(w, false);

                    float headX = Mathf.Lerp(from, to, w.u);

                    // Distancia hacia fuera desde el borde interior (0 dentro de la zona de juego):
                    // los hilos solo aparecen a partir de ahí y se desvanecen suavemente al acercarse.
                    float outwardDist = Mathf.Max(0f, (headX - inner) * side);
                    float edgeFade = Mathf.SmoothStep(0f, 1f, outwardDist / fadeDist);
                    float pathFade = Mathf.SmoothStep(0f, 1f, w.u / endFade) * Mathf.SmoothStep(0f, 1f, (1f - w.u) / endFade);

                    float layerAlpha = Mathf.Lerp(0.55f, 1f, w.layer);
                    float alpha = wispMaxAlpha * layerAlpha * edgeFade * pathFade * intensity;

                    if (alpha < 0.004f) { w.line.enabled = false; continue; }
                    w.line.enabled = true;

                    float width = wispWidth * Mathf.Lerp(0.6f, 1.6f, w.layer);
                    w.line.startWidth = width;
                    w.line.endWidth = width;

                    float baseY = camY + w.y * halfH + gustTilt * (0.5f + w.layer);
                    for (int k = 0; k < WispPoints; k++)
                    {
                        float p = k / (float)(WispPoints - 1);   // 0 = cola, 1 = cabeza
                        float x = headX - flow * w.length * (1f - p);
                        float wave = Mathf.Sin(p * 6.2831f * w.waveFreq + w.wavePhase + time * 0.8f) * waveAmplitude * Mathf.Lerp(0.6f, 1.2f, w.layer);
                        pointBuffer[k] = new Vector3(x, baseY + wave, 0f);
                    }
                    w.line.SetPositions(pointBuffer);
                    SetTint(w.line, alpha);
                }
            }
        }
    }
}