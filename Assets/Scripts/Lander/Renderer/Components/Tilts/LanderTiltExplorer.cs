using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Torres de tres lámparas en la punta de cada panel solar, dentro de un soporte
    /// en forma de corchete. Con inclinación peligrosa, las lámparas de ese lado
    /// hacen una animación de persecución de arriba abajo.
    /// </summary>
    public sealed class LanderTiltExplorer : LanderTiltRcsRendererBase
    {
        private const int LampCount = 3;
        private const float LampX = 0.80f;
        private const float LampBaseY = 0.60f;
        private const float LampSpacing = 0.05f;
        private const float LampHalfLength = 0.012f;
        private const float LampWidth = 0.045f;

        [SerializeField, Min(0f)] private float chaseRate = 6f;

        private readonly LineRenderer[] leftLamps = new LineRenderer[LampCount];
        private readonly LineRenderer[] rightLamps = new LineRenderer[LampCount];
        private LineRenderer leftBracket, rightBracket;

        public override void BuildTiltIndicator()
        {
            DestroyChild("ExplorerTilt_LBracket");
            DestroyChild("ExplorerTilt_RBracket");
            for (int i = 0; i < LampCount; i++)
            {
                DestroyChild("ExplorerTilt_LLamp" + i);
                DestroyChild("ExplorerTilt_RLamp" + i);
            }

            if (!showTiltLight || mainRenderer == null) return;

            float bracketWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            rightBracket = CreateBracket("ExplorerTilt_RBracket", 1f, bracketWidth);
            leftBracket = CreateBracket("ExplorerTilt_LBracket", -1f, bracketWidth);

            for (int i = 0; i < LampCount; i++)
            {
                rightLamps[i] = CreateLamp("ExplorerTilt_RLamp" + i, 1f, i);
                leftLamps[i] = CreateLamp("ExplorerTilt_LLamp" + i, -1f, i);
            }
        }

        private void DestroyChild(string childName)
        {
            Transform old = transform.Find(childName);
            if (old != null) Destroy(old.gameObject);
        }

        private LineRenderer CreateBracket(string childName, float side, float width)
        {
            var line = mainRenderer.CreateChildLine(childName);
            mainRenderer.ConfigureLine(line, new[] {
                new Vector3(side * 0.76f, 0.57f, 0f),
                new Vector3(side * 0.85f, 0.57f, 0f),
                new Vector3(side * 0.85f, 0.73f, 0f),
                new Vector3(side * 0.76f, 0.73f, 0f),
            }, false, tiltDefaultColor, width);
            line.sortingOrder = mainRenderer.SortingOrder + 1;
            return line;
        }

        private LineRenderer CreateLamp(string childName, float side, int index)
        {
            float y = LampBaseY + index * LampSpacing;
            var line = mainRenderer.CreateChildLine(childName);
            mainRenderer.ConfigureLine(line, new[] {
                new Vector3(side * LampX, y - LampHalfLength, 0f),
                new Vector3(side * LampX, y + LampHalfLength, 0f),
            }, false, tiltDefaultColor, LampWidth);
            line.sortingOrder = mainRenderer.SortingOrder + 2;
            return line;
        }

        public override void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            int chaseStep = chaseRate > 0f ? (int)(Time.time * chaseRate) % LampCount : -1;
            ApplyTower(leftBracket, leftLamps, leftColor, chaseStep);
            ApplyTower(rightBracket, rightLamps, rightColor, chaseStep);
        }

        private void ApplyTower(LineRenderer bracket, LineRenderer[] lamps, Color color, int chaseStep)
        {
            bool unsafeSide = color == tiltUnsafeColor;
            if (bracket != null) mainRenderer.ApplyColor(bracket, color);

            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;
                mainRenderer.ApplyColor(lamps[i], color);
                // Persecución descendente: la lámpara 2 (arriba) se enciende primero
                bool lit = !unsafeSide || chaseStep < 0 || (LampCount - 1 - i) == chaseStep;
                lamps[i].enabled = lit && !isHidden && showTiltLight;
            }
        }

        protected override void ApplyTiltVisibility()
        {
            bool on = !isHidden && showTiltLight;
            if (leftBracket != null) leftBracket.enabled = on;
            if (rightBracket != null) rightBracket.enabled = on;
            for (int i = 0; i < LampCount; i++)
            {
                if (leftLamps[i] != null) leftLamps[i].enabled = on;
                if (rightLamps[i] != null) rightLamps[i].enabled = on;
            }
        }
    }
}