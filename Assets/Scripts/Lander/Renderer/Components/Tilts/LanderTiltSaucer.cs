using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Indicador Tilt para Saucer: hileras de luces estroboscópicas situadas en los
    /// alerones exteriores del disco (izquierda y derecha).
    /// </summary>
    public sealed class LanderTiltSaucer : LanderTiltRcsRendererBase
    {
        private const int LightsPerWing = 4;
        private readonly LineRenderer[] leftLights = new LineRenderer[LightsPerWing];
        private readonly LineRenderer[] rightLights = new LineRenderer[LightsPerWing];

        [SerializeField, Min(0f)] private float blinkSpeed = 7f;

        public override void BuildTiltIndicator()
        {
            for (int i = 0; i < LightsPerWing; i++)
            {
                DestroyChild("SaucerTilt_L" + i);
                DestroyChild("SaucerTilt_R" + i);
            }

            if (!showTiltLight || mainRenderer == null) return;

            // Coordenadas a lo largo del borde inclinado del platillo
            float[] xOffsets = { 0.58f, 0.64f, 0.70f, 0.76f };
            float[] yOffsets = { 0.17f, 0.15f, 0.13f, 0.10f };

            float size = 0.018f;
            for (int i = 0; i < LightsPerWing; i++)
            {
                rightLights[i] = CreatePointLight("SaucerTilt_R" + i, xOffsets[i], yOffsets[i], size);
                leftLights[i] = CreatePointLight("SaucerTilt_L" + i, -xOffsets[i], yOffsets[i], size);
            }
        }

        private void DestroyChild(string childName)
        {
            Transform t = transform.Find(childName);
            if (t != null) Destroy(t.gameObject);
        }

        private LineRenderer CreatePointLight(string name, float x, float y, float radius)
        {
            var lr = mainRenderer.CreateChildLine(name);
            Vector3[] pts = new[] {
                new Vector3(x - radius, y, 0f),
                new Vector3(x + radius, y, 0f)
            };
            mainRenderer.ConfigureLine(lr, pts, false, tiltDefaultColor, radius * 2.2f);
            lr.sortingOrder = mainRenderer.SortingOrder + 2;
            return lr;
        }

        public override void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            int pattern = (int)(Time.time * blinkSpeed) % LightsPerWing;
            UpdateSide(leftLights, leftColor, pattern);
            UpdateSide(rightLights, rightColor, pattern);
        }

        private void UpdateSide(LineRenderer[] lights, Color color, int pattern)
        {
            bool isUnsafe = color == tiltUnsafeColor;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                mainRenderer.ApplyColor(lights[i], color);
                // Si la nave está en peligro, parpadean en secuencia de advertencia
                bool active = !isUnsafe || (i == pattern);
                lights[i].enabled = active && !isHidden && showTiltLight;
            }
        }

        protected override void ApplyTiltVisibility()
        {
            bool on = !isHidden && showTiltLight;
            for (int i = 0; i < LightsPerWing; i++)
            {
                if (leftLights[i] != null) leftLights[i].enabled = on;
                if (rightLights[i] != null) rightLights[i].enabled = on;
            }
        }
    }
}