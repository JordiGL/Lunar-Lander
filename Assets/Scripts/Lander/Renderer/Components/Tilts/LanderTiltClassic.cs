using UnityEngine;

namespace LunarLander
{
    public sealed class LanderTiltClassic : LanderTiltRcsRendererBase
    {
        private LineRenderer leftTiltFillRenderer;
        private LineRenderer rightTiltFillRenderer;

        public override void BuildTiltIndicator()
        {
            Transform oldL = transform.Find("LeftTiltFill");
            if (oldL != null) Destroy(oldL.gameObject);

            Transform oldR = transform.Find("RightTiltFill");
            if (oldR != null) Destroy(oldR.gameObject);

            if (!showTiltLight || mainRenderer == null) return;

            leftTiltFillRenderer = mainRenderer.CreateChildLine("LeftTiltFill");
            rightTiltFillRenderer = mainRenderer.CreateChildLine("RightTiltFill");

            float yCenter = 0.42f, xCenter = 0.40f, fillSize = 0.09f;

            Vector3[] rightFillPoints = { new Vector3(xCenter - fillSize * 0.5f, yCenter, 0f), new Vector3(xCenter + fillSize * 0.5f, yCenter, 0f) };
            Vector3[] leftFillPoints = { new Vector3(-xCenter + fillSize * 0.5f, yCenter, 0f), new Vector3(-xCenter - fillSize * 0.5f, yCenter, 0f) };

            mainRenderer.ConfigureLine(rightTiltFillRenderer, rightFillPoints, false, tiltDefaultColor, fillSize);
            rightTiltFillRenderer.sortingOrder = mainRenderer.SortingOrder + 1;

            mainRenderer.ConfigureLine(leftTiltFillRenderer, leftFillPoints, false, tiltDefaultColor, fillSize);
            leftTiltFillRenderer.sortingOrder = mainRenderer.SortingOrder + 1;
        }

        public override void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);
            if (leftTiltFillRenderer != null) mainRenderer.ApplyColor(leftTiltFillRenderer, leftColor);
            if (rightTiltFillRenderer != null) mainRenderer.ApplyColor(rightTiltFillRenderer, rightColor);
        }

        protected override void ApplyTiltVisibility()
        {
            if (leftTiltFillRenderer != null) leftTiltFillRenderer.enabled = !isHidden && showTiltLight;
            if (rightTiltFillRenderer != null) rightTiltFillRenderer.enabled = !isHidden && showTiltLight;
        }
    }
}