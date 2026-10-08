using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Indicador Tilt para Heavy Cargo:
    /// Niveladores de carga verticales ubicados en los montantes de los flancos.
    /// Si hay sobreinclinación, la barra del lado comprometido parpadea y desciende rápidamente.
    /// </summary>
    public sealed class LanderTiltHeavyCargo : LanderTiltRcsRendererBase
    {
        private const float GaugeX = 0.52f;
        private const float GaugeBottomY = 0.05f;
        private const float GaugeTopY = 0.35f;

        private LineRenderer leftFrame, rightFrame;
        private LineRenderer leftLevel, rightLevel;

        [SerializeField, Min(1f)] private float warningFlashRate = 6f;

        public override void BuildTiltIndicator()
        {
            DestroyChild("HeavyTilt_LFrame");
            DestroyChild("HeavyTilt_RFrame");
            DestroyChild("HeavyTilt_LLevel");
            DestroyChild("HeavyTilt_RLevel");

            if (!showTiltLight || mainRenderer == null) return;

            float width = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;

            rightFrame = CreateFrame("HeavyTilt_RFrame", GaugeX, width);
            leftFrame = CreateFrame("HeavyTilt_LFrame", -GaugeX, width);

            rightLevel = CreateLevelBar("HeavyTilt_RLevel", GaugeX, width * 1.6f);
            leftLevel = CreateLevelBar("HeavyTilt_LLevel", -GaugeX, width * 1.6f);
        }

        private void DestroyChild(string childName)
        {
            Transform t = transform.Find(childName);
            if (t != null) Destroy(t.gameObject);
        }

        private LineRenderer CreateFrame(string name, float x, float width)
        {
            var lr = mainRenderer.CreateChildLine(name);
            Vector3[] pts = new[] {
                new Vector3(x - 0.025f, GaugeBottomY, 0f),
                new Vector3(x + 0.025f, GaugeBottomY, 0f),
                new Vector3(x + 0.025f, GaugeTopY, 0f),
                new Vector3(x - 0.025f, GaugeTopY, 0f)
            };
            mainRenderer.ConfigureLine(lr, pts, true, tiltDefaultColor, width * 0.7f);
            lr.sortingOrder = mainRenderer.SortingOrder + 1;
            return lr;
        }

        private LineRenderer CreateLevelBar(string name, float x, float width)
        {
            var lr = mainRenderer.CreateChildLine(name);
            Vector3[] pts = new[] {
                new Vector3(x, GaugeBottomY + 0.02f, 0f),
                new Vector3(x, GaugeTopY - 0.02f, 0f)
            };
            mainRenderer.ConfigureLine(lr, pts, false, tiltDefaultColor, width);
            lr.sortingOrder = mainRenderer.SortingOrder + 2;
            return lr;
        }

        public override void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            UpdateGauge(leftFrame, leftLevel, leftColor);
            UpdateGauge(rightFrame, rightLevel, rightColor);
        }

        private void UpdateGauge(LineRenderer frame, LineRenderer level, Color color)
        {
            if (frame != null) mainRenderer.ApplyColor(frame, color);
            if (level != null)
            {
                mainRenderer.ApplyColor(level, color);
                bool isUnsafe = color == tiltUnsafeColor;
                bool flash = !isUnsafe || ((int)(Time.time * warningFlashRate) & 1) == 0;
                level.enabled = flash && !isHidden && showTiltLight;
            }
        }

        protected override void ApplyTiltVisibility()
        {
            bool on = !isHidden && showTiltLight;
            if (leftFrame != null) leftFrame.enabled = on;
            if (rightFrame != null) rightFrame.enabled = on;
            if (leftLevel != null) leftLevel.enabled = on;
            if (rightLevel != null) rightLevel.enabled = on;
        }
    }
}