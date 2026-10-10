using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Límite alienígena: membrana bioluminiscente de zarcillos ondulantes y esporas flotantes.
    /// Dentro de la zona genera una ANOMALÍA GRAVITATORIA: la gravedad vertical "respira"
    /// (pulsos que empujan la nave hacia arriba y hacia abajo) y un leve empuje hacia el interior.
    /// Es un efecto distinto al viento (empuje lateral), al polvo (frenado) y a la radiación (combustible):
    /// aquí lo que se pierde es el control de la altura, no el combustible.
    /// </summary>
    public sealed class AlienBoundary : StageBoundaryBase
    {
        [Header("Anomalía gravitatoria")]
        [Tooltip("Aceleración vertical máxima de los pulsos en el muro exterior (unidades/s²).")]
        [SerializeField, Min(0f)] private float warpAcceleration = 4f;
        [Tooltip("Velocidad de los pulsos (ciclos por segundo).")]
        [SerializeField, Min(0.05f)] private float warpFrequency = 0.35f;
        [Tooltip("Empuje suave hacia el interior (unidades/s²). 0 = sin empuje.")]
        [SerializeField, Min(0f)] private float pushInAcceleration = 3f;

        [Header("Aspecto")]
        [SerializeField] private Color membraneColor = new Color(0.45f, 0.10f, 0.75f);
        [SerializeField] private Color tendrilColor = new Color(0.30f, 1.00f, 0.55f);
        [SerializeField] private Color sporeColor = new Color(0.80f, 1.00f, 0.70f);
        [SerializeField, Range(0f, 1f)] private float stripPeakAlpha = 0.75f;
        [SerializeField, Range(4, 40)] private int tendrilsPerSide = 14;
        [SerializeField, Range(4, 16)] private int tendrilPoints = 9;
        [SerializeField, Min(0.01f)] private float tendrilWidth = 0.08f;
        [SerializeField, Min(0.1f)] private float tendrilWaveSpeed = 1.6f;
        [SerializeField, Min(0f)] private float tendrilWaveAmplitude = 1.4f;
        [SerializeField, Range(0, 60)] private int sporesPerSide = 24;
        [SerializeField, Min(0.01f)] private float sporeSize = 0.18f;
        [SerializeField, Min(0f)] private float sporeDriftSpeed = 0.6f;

        private sealed class Tendril
        {
            public LineRenderer line;
            public float y;        // -1..1 relativo a la cámara
            public float length;   // fracción de zoneWidth
            public float phase;
            public float freq;
        }

        private sealed class Spore
        {
            public LineRenderer line;
            public float u;        // 0 = interior, 1 = exterior
            public float y;        // -1..1 relativo a la cámara
            public float phase;
            public float freq;
        }

        private readonly Strip[] stripBySide = new Strip[2];
        private readonly List<Tendril>[] tendrils = { new List<Tendril>(), new List<Tendril>() };
        private readonly List<Spore>[] spores = { new List<Spore>(), new List<Spore>() };

        protected override void BuildVisuals()
        {
            for (int s = 0; s < 2; s++)
            {
                tendrils[s].Clear();
                spores[s].Clear();

                int side = s == 0 ? Left : Right;
                stripBySide[s] = CreateStrip("AlienStrip", side, membraneColor, stripPeakAlpha, true);

                for (int i = 0; i < tendrilsPerSide; i++)
                {
                    tendrils[s].Add(new Tendril
                    {
                        line = CreateLine("AlienTendril", tendrilPoints, tendrilWidth, sortingOrder + 1),
                        y = Mathf.Lerp(-1f, 1f, (i + Random.value * 0.8f) / tendrilsPerSide),
                        length = Random.Range(0.35f, 0.95f),
                        phase = Random.value * 6.2831f,
                        freq = Random.Range(0.7f, 1.3f)
                    });
                }

                for (int i = 0; i < sporesPerSide; i++)
                {
                    LineRenderer lr = CreateLine("AlienSpore", 2, sporeSize, sortingOrder + 2);
                    lr.numCapVertices = 6;
                    spores[s].Add(new Spore
                    {
                        line = lr,
                        u = Mathf.Pow(Random.value, 0.7f),
                        y = Random.Range(-1f, 1f),
                        phase = Random.value * 6.2831f,
                        freq = Random.Range(0.3f, 1f)
                    });
                }
            }
        }

        protected override void ApplyEffect(Rigidbody2D body, float depth01, int side, float dt)
        {
            // Gravedad "respirando": pulsos suaves arriba/abajo que crecen con la profundidad.
            if (warpAcceleration > 0f)
            {
                float pulse = Mathf.Sin(Time.time * warpFrequency * 6.2831f);
                body.AddForce(new Vector2(0f, pulse * warpAcceleration * depth01 * body.mass), ForceMode2D.Force);
            }

            if (pushInAcceleration > 0f)
                body.AddForce(new Vector2(-side * pushInAcceleration * depth01 * body.mass, 0f), ForceMode2D.Force);
        }

        protected override void UpdateVisuals(float time, float leftDepth01, float rightDepth01)
        {
            float camY = CameraY;
            float halfH = CameraHeight * 0.5f;

            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? Left : Right;
                float depth = s == 0 ? leftDepth01 : rightDepth01;
                bool visible = IsSideVisible(side);
                float intensity = Mathf.Lerp(idleIntensity, 1f, depth);

                // La membrana respira al ritmo de la anomalía.
                float breathe = 0.85f + 0.15f * Mathf.Sin(time * warpFrequency * 6.2831f);

                Strip strip = stripBySide[s];
                if (strip != null)
                {
                    strip.tr.gameObject.SetActive(visible);
                    strip.SetIntensity(Mathf.Lerp(0.6f, 1f, depth) * breathe);
                }

                float inner = ZoneInner(side);
                float outer = ZoneOuter(side);

                // Zarcillos: nacen en el muro exterior y se ondulan hacia el interior.
                List<Tendril> tl = tendrils[s];
                for (int i = 0; i < tl.Count; i++)
                {
                    Tendril t = tl[i];
                    t.line.enabled = visible;
                    if (!visible) continue;

                    float baseY = camY + t.y * halfH;
                    float len = zoneWidth * t.length * Mathf.Lerp(0.6f, 1f, depth);
                    int n = t.line.positionCount;
                    for (int p = 0; p < n; p++)
                    {
                        float f = p / (float)(n - 1);
                        float amp = tendrilWaveAmplitude * f * (0.5f + depth);
                        float wy = Mathf.Sin(time * tendrilWaveSpeed * t.freq + t.phase + f * 5f) * amp;
                        t.line.SetPosition(p, new Vector3(outer - side * len * f, baseY + wy, 0f));
                    }

                    float a = (0.5f + 0.5f * Mathf.Sin(time * t.freq + t.phase)) * intensity;
                    t.line.startColor = new Color(tendrilColor.r, tendrilColor.g, tendrilColor.b, a);
                    t.line.endColor = new Color(tendrilColor.r, tendrilColor.g, tendrilColor.b, 0f);
                }

                // Esporas: puntos que flotan y titilan dentro de la zona.
                List<Spore> sl = spores[s];
                for (int i = 0; i < sl.Count; i++)
                {
                    Spore sp = sl[i];
                    sp.line.enabled = visible;
                    if (!visible) continue;

                    float ph = time * sporeDriftSpeed * sp.freq + sp.phase;
                    float u = Mathf.Clamp01(sp.u + Mathf.Sin(ph) * 0.08f);
                    float x = Mathf.Lerp(inner, outer, u);
                    float y = camY + sp.y * halfH + Mathf.Cos(ph * 1.3f) * 1.2f;

                    sp.line.SetPosition(0, new Vector3(x, y, 0f));
                    sp.line.SetPosition(1, new Vector3(x + 0.01f, y, 0f));

                    float a = (0.4f + 0.6f * Mathf.PerlinNoise(time * 0.8f + sp.phase, sp.phase)) * intensity * Mathf.Lerp(0.3f, 1f, u);
                    Color c = new Color(sporeColor.r, sporeColor.g, sporeColor.b, a);
                    sp.line.startColor = c;
                    sp.line.endColor = c;
                }
            }
        }
    }
}