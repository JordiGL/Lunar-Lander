using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Luces de posición integradas en el borde del disco: una barra que se funde con
    /// la línea del ecuador a cada lado, enmarcada por un tabique que la separa de las
    /// portillas. No sobresale del casco, así que la silueta de la nave no cambia.
    /// El lado inseguro parpadea en rojo.
    /// </summary>
    public sealed class LanderTiltOvni : LanderTiltRcsRendererBase
    {
        // La barra recorre el ecuador (y = 0.10) entre estas dos x.
        private const float RimY = 0.10f;
        private const float LampInnerX = 0.55f;
        private const float LampOuterX = 0.66f;
        private const float LampWidth = 0.045f;

        // Tabique vertical que cierra la barra por el lado del centro.
        private const float TickX = 0.51f;
        private const float TickHalfHeight = 0.03f;

        [SerializeField, Min(0f)] private float unsafeBlinkRate = 4f;

        private LineRenderer leftLamp, rightLamp, leftTick, rightTick;

        private static readonly string[] ChildNames =
            { "OvniTilt_LLamp", "OvniTilt_RLamp", "OvniTilt_LTick", "OvniTilt_RTick" };

        public override void BuildTiltIndicator()
        {
            for (int i = 0; i < ChildNames.Length; i++)
            {
                Transform old = transform.Find(ChildNames[i]);
                if (old != null) Destroy(old.gameObject);
            }

            if (!showTiltLight || mainRenderer == null) return;

            float tickWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;

            rightLamp = CreateLamp(ChildNames[1], 1f);
            leftLamp = CreateLamp(ChildNames[0], -1f);
            rightTick = CreateTick(ChildNames[3], 1f, tickWidth);
            leftTick = CreateTick(ChildNames[2], -1f, tickWidth);
        }

        private LineRenderer CreateLamp(string name, float side)
        {
            var line = mainRenderer.CreateChildLine(name);
            mainRenderer.ConfigureLine(line,
                new[] { new Vector3(side * LampInnerX, RimY, 0f), new Vector3(side * LampOuterX, RimY, 0f) },
                false, tiltDefaultColor, LampWidth);
            line.sortingOrder = mainRenderer.SortingOrder + 1;
            return line;
        }

        private LineRenderer CreateTick(string name, float side, float width)
        {
            var line = mainRenderer.CreateChildLine(name);
            mainRenderer.ConfigureLine(line,
                new[] { new Vector3(side * TickX, RimY - TickHalfHeight, 0f), new Vector3(side * TickX, RimY + TickHalfHeight, 0f) },
                false, tiltDefaultColor, width);
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

            Apply(leftLamp, leftTick, leftColor, !leftUnsafe || blinkOn);
            Apply(rightLamp, rightTick, rightColor, !rightUnsafe || blinkOn);
        }

        private void Apply(LineRenderer lamp, LineRenderer tick, Color color, bool visible)
        {
            bool on = visible && !isHidden && showTiltLight;
            if (lamp != null) { mainRenderer.ApplyColor(lamp, color); lamp.enabled = on; }
            if (tick != null) { mainRenderer.ApplyColor(tick, color); tick.enabled = on; }
        }

        protected override void ApplyTiltVisibility()
        {
            bool on = !isHidden && showTiltLight;
            if (leftLamp != null) leftLamp.enabled = on;
            if (rightLamp != null) rightLamp.enabled = on;
            if (leftTick != null) leftTick.enabled = on;
            if (rightTick != null) rightTick.enabled = on;
        }
    }
}