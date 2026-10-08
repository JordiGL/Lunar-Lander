using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Indicador de inclinación sobre los flaps delanteros: una barra vertical
    /// y un chevron en cada punta. El lado inseguro parpadea en rojo.
    /// </summary>
    public sealed class LanderTiltStarship : LanderTiltRcsRendererBase
    {
        [SerializeField, Min(0f)] private float unsafeBlinkRate = 4f;

        private LineRenderer leftBar, rightBar, leftChevron, rightChevron;

        private static readonly string[] ChildNames =
            { "StarshipTilt_LBar", "StarshipTilt_RBar", "StarshipTilt_LChevron", "StarshipTilt_RChevron" };

        public override void BuildTiltIndicator()
        {
            for (int i = 0; i < ChildNames.Length; i++)
            {
                Transform old = transform.Find(ChildNames[i]);
                if (old != null) Destroy(old.gameObject);
            }

            if (!showTiltLight || mainRenderer == null) return;

            const float barWidth = 0.05f;
            float chevronWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;

            rightBar = CreateBar(ChildNames[1], 0.27f, barWidth);
            leftBar = CreateBar(ChildNames[0], -0.27f, barWidth);
            rightChevron = CreateChevron(ChildNames[3], 1f, chevronWidth);
            leftChevron = CreateChevron(ChildNames[2], -1f, chevronWidth);
        }

        private LineRenderer CreateBar(string name, float x, float width)
        {
            var line = mainRenderer.CreateChildLine(name);
            mainRenderer.ConfigureLine(line, new[] { new Vector3(x, 0.455f, 0f), new Vector3(x, 0.515f, 0f) },
                                       false, tiltDefaultColor, width);
            line.sortingOrder = mainRenderer.SortingOrder + 1;
            return line;
        }

        private LineRenderer CreateChevron(string name, float side, float width)
        {
            var line = mainRenderer.CreateChildLine(name);
            mainRenderer.ConfigureLine(line, new[] {
                new Vector3(side * 0.39f, 0.53f, 0f),
                new Vector3(side * 0.44f, 0.45f, 0f),
                new Vector3(side * 0.39f, 0.37f, 0f),
            }, false, tiltDefaultColor, width);
            line.sortingOrder = mainRenderer.SortingOrder + 1;
            return line;
        }

        public override void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            bool blinkOn = unsafeBlinkRate <= 0f || ((int)(Time.time * unsafeBlinkRate * 2f) & 1) == 0;
            bool leftUnsafe = leftColor == tiltUnsafeColor;
            bool rightUnsafe = rightColor == tiltUnsafeColor;

            Apply(leftBar, leftChevron, leftColor, !leftUnsafe || blinkOn);
            Apply(rightBar, rightChevron, rightColor, !rightUnsafe || blinkOn);
        }

        private void Apply(LineRenderer bar, LineRenderer chevron, Color color, bool visible)
        {
            if (bar != null) { mainRenderer.ApplyColor(bar, color); bar.enabled = visible && !isHidden && showTiltLight; }
            if (chevron != null) { mainRenderer.ApplyColor(chevron, color); chevron.enabled = visible && !isHidden && showTiltLight; }
        }

        protected override void ApplyTiltVisibility()
        {
            bool on = !isHidden && showTiltLight;
            if (leftBar != null) leftBar.enabled = on;
            if (rightBar != null) rightBar.enabled = on;
            if (leftChevron != null) leftChevron.enabled = on;
            if (rightChevron != null) rightChevron.enabled = on;
        }
    }
}