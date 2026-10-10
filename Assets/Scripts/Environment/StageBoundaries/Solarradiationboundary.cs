using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Límite de radiación solar: franja de resplandor pulsante con rayos que parpadean.
    /// Mientras la nave esté en la zona de efecto pierde combustible por segundo
    /// (y, opcionalmente, recibe un leve empujón hacia el interior).
    /// </summary>
    public sealed class SolarRadiationBoundary : StageBoundaryBase
    {
        [Header("Radiación")]
        [Tooltip("Combustible perdido por segundo en el muro exterior (escala con la profundidad).")]
        [SerializeField, Min(0f)] private float fuelDrainPerSecond = 60f;
        [Tooltip("Empuje suave hacia el interior (unidades/s²). 0 = sin empuje.")]
        [SerializeField, Min(0f)] private float pushInAcceleration = 3f;

        [Header("Aspecto")]
        [SerializeField] private Color radiationColor = new Color(1.00f, 0.50f, 0.10f);
        [SerializeField] private Color rayColor = new Color(1.00f, 0.95f, 0.60f);
        [SerializeField, Range(0f, 1f)] private float stripPeakAlpha = 0.60f;
        [SerializeField, Min(0f)] private float pulseSpeed = 3f;
        [SerializeField, Range(0f, 0.6f)] private float pulseAmount = 0.25f;
        [SerializeField, Range(4, 50)] private int raysPerSide = 18;
        [SerializeField, Min(0.01f)] private float rayWidth = 0.06f;
        [SerializeField, Min(0.1f)] private float rayFlickerSpeed = 2.5f;

        private sealed class Ray
        {
            public LineRenderer line;
            public float y;      // -1..1 relativo a la cámara
            public float phase;
        }

        private readonly Strip[] stripBySide = new Strip[2];
        private readonly List<Ray>[] rays = { new List<Ray>(), new List<Ray>() };

        protected override void BuildVisuals()
        {
            rays[0].Clear();
            rays[1].Clear();

            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? Left : Right;
                stripBySide[s] = CreateStrip("SolarStrip", side, radiationColor, stripPeakAlpha, true);

                for (int i = 0; i < raysPerSide; i++)
                {
                    rays[s].Add(new Ray
                    {
                        line = CreateLine("SolarRay", 2, rayWidth, sortingOrder + 1),
                        y = Random.Range(-1f, 1f),
                        phase = Random.value * 100f
                    });
                }
            }
        }

        protected override void ApplyEffect(Rigidbody2D body, float depth01, int side, float dt)
        {
            // NOTA: asume que LanderController.AddFuel admite valores negativos.
            if (fuelDrainPerSecond > 0f && Lander != null)
                Lander.AddFuel(-fuelDrainPerSecond * depth01 * dt);

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

                float pulse = 1f - pulseAmount + pulseAmount * (0.5f + 0.5f * Mathf.Sin(time * pulseSpeed * (1f + depth * 2f)));

                Strip strip = stripBySide[s];
                if (strip != null)
                {
                    strip.tr.gameObject.SetActive(visible);
                    strip.SetIntensity(intensity * pulse);
                }

                float outer = ZoneOuter(side);
                List<Ray> list = rays[s];
                for (int i = 0; i < list.Count; i++)
                {
                    Ray r = list[i];
                    r.line.enabled = visible;
                    if (!visible) continue;

                    float flicker = Mathf.PerlinNoise(time * rayFlickerSpeed + r.phase, r.phase * 0.37f);
                    float len = zoneWidth * Mathf.Lerp(0.25f, 1f, depth) * (0.35f + 0.65f * flicker);
                    float y = camY + r.y * halfH;

                    // Nace brillante en el muro exterior y se apaga hacia el interior.
                    r.line.SetPosition(0, new Vector3(outer, y, 0f));
                    r.line.SetPosition(1, new Vector3(outer - side * len, y, 0f));

                    float a = flicker * intensity;
                    r.line.startColor = new Color(rayColor.r, rayColor.g, rayColor.b, a);
                    r.line.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0f);
                }
            }
        }
    }
}