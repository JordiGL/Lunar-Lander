using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Límite de polvo impenetrable: muro de nubes casi opacas que frena a la nave
    /// (drag creciente) y la empuja de vuelta hacia el interior. El polvo cubre la nave
    /// y todo lo que hay más allá del borde, así que no se ve el vacío.
    /// </summary>
    public sealed class DustWallBoundary : StageBoundaryBase
    {
        [Header("Polvo")]
        [Tooltip("Frenado exponencial de la velocidad en el muro exterior (1/s).")]
        [SerializeField, Min(0f)] private float dragStrength = 3.5f;
        [Tooltip("Aceleración hacia el interior en el muro exterior (unidades/s²).")]
        [SerializeField, Min(0f)] private float pushBackAcceleration = 10f;

        [Header("Aspecto")]
        [SerializeField] private Color dustColor = new Color(0.50f, 0.40f, 0.30f);
        [SerializeField] private Color dustHighlight = new Color(0.72f, 0.60f, 0.45f);
        [SerializeField, Range(0f, 1f)] private float stripPeakAlpha = 0.97f;
        [SerializeField, Range(6, 80)] private int blobsPerSide = 32;
        [SerializeField, Min(0.5f)] private float blobMinWidth = 2.5f;
        [SerializeField, Min(0.5f)] private float blobMaxWidth = 6f;
        [SerializeField, Range(0f, 1f)] private float blobAlpha = 0.20f;
        [SerializeField, Min(0f)] private float driftSpeed = 0.5f;

        private sealed class Blob
        {
            public LineRenderer line;
            public float u;       // posición base en la zona (0 = interior, 1 = exterior)
            public float y;       // -1..1 relativo a la cámara
            public float length;
            public float phase;
            public float freq;
            public bool bright;
        }

        private readonly Strip[] stripBySide = new Strip[2];
        private readonly List<Blob>[] blobs = { new List<Blob>(), new List<Blob>() };

        protected override void BuildVisuals()
        {
            blobs[0].Clear();
            blobs[1].Clear();

            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? Left : Right;
                stripBySide[s] = CreateStrip("DustStrip", side, dustColor, stripPeakAlpha, true);

                for (int i = 0; i < blobsPerSide; i++)
                {
                    float w = Random.Range(blobMinWidth, Mathf.Max(blobMinWidth, blobMaxWidth));
                    LineRenderer lr = CreateLine("DustBlob", 2, w, sortingOrder + 1);
                    lr.numCapVertices = 8;

                    blobs[s].Add(new Blob
                    {
                        line = lr,
                        u = Mathf.Pow(Random.value, 0.6f),   // más densos hacia el muro exterior
                        y = Random.Range(-1f, 1f),
                        length = Random.Range(2f, 7f),
                        phase = Random.value * 6.2831f,
                        freq = Random.Range(0.15f, 0.45f),
                        bright = Random.value < 0.3f
                    });
                }
            }
        }

        protected override void ApplyEffect(Rigidbody2D body, float depth01, int side, float dt)
        {
            Vector2 v = body.velocity;
            v.x *= Mathf.Exp(-dragStrength * depth01 * dt);
            v.y *= Mathf.Exp(-dragStrength * 0.35f * depth01 * dt);
            body.velocity = v;

            if (pushBackAcceleration > 0f)
                body.AddForce(new Vector2(-side * pushBackAcceleration * depth01 * body.mass, 0f), ForceMode2D.Force);
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

                Strip strip = stripBySide[s];
                if (strip != null)
                {
                    strip.tr.gameObject.SetActive(visible);
                    // El muro de polvo siempre se ve; al entrar en la zona se espesa del todo.
                    strip.SetIntensity(Mathf.Lerp(0.55f, 1f, depth));
                }

                float inner = ZoneInner(side);
                float outer = ZoneOuter(side);

                List<Blob> list = blobs[s];
                for (int i = 0; i < list.Count; i++)
                {
                    Blob b = list[i];
                    b.line.enabled = visible;
                    if (!visible) continue;

                    float sway = Mathf.Sin(time * b.freq * driftSpeed * 6.2831f + b.phase);
                    float u = Mathf.Clamp01(b.u + sway * 0.08f);
                    float x = Mathf.Lerp(inner, outer, u);
                    float y = camY + b.y * halfH + Mathf.Cos(time * b.freq * driftSpeed * 4f + b.phase) * 0.8f;

                    b.line.SetPosition(0, new Vector3(x - b.length * 0.5f, y, 0f));
                    b.line.SetPosition(1, new Vector3(x + b.length * 0.5f, y, 0f));

                    Color c = b.bright ? dustHighlight : dustColor;
                    float a = blobAlpha * intensity * Mathf.Lerp(0.4f, 1f, u);
                    c.a = a;
                    b.line.startColor = c;
                    b.line.endColor = c;
                }
            }
        }
    }
}