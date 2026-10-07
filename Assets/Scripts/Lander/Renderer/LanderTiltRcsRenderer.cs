using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderTiltRcsRenderer : MonoBehaviour
    {
        [Header("Configuración Tilt RCS")]
        [Tooltip("Rellena los propulsores RCS para usarlos como indicadores luminosos de alineación.")]
        [SerializeField] private bool showTiltLight = true;
        [SerializeField] private Color tiltDefaultColor = Color.white;
        [SerializeField] private Color tiltSafeColor = new Color(0.30f, 1f, 0.40f);
        [SerializeField] private Color tiltUnsafeColor = new Color(1f, 0.20f, 0.20f);

        private LineRenderer leftTiltFillRenderer;
        private LineRenderer rightTiltFillRenderer;

        private VectorLanderRenderer mainRenderer;
        private LanderController lander;
        private bool isHidden;

        public bool ShowTiltLight => showTiltLight;

        public void Initialize(VectorLanderRenderer renderer, LanderController controller)
        {
            mainRenderer = renderer;
            lander = controller;
            BuildTiltIndicator();
        }

        private void Update()
        {
            UpdateTiltIndicator();
        }

        public void SetHidden(bool hidden)
        {
            isHidden = hidden;
            if (leftTiltFillRenderer != null) leftTiltFillRenderer.enabled = !hidden && showTiltLight;
            if (rightTiltFillRenderer != null) rightTiltFillRenderer.enabled = !hidden && showTiltLight;
        }

        public void ResetState()
        {
            isHidden = false;
            if (leftTiltFillRenderer != null) leftTiltFillRenderer.enabled = showTiltLight;
            if (rightTiltFillRenderer != null) rightTiltFillRenderer.enabled = showTiltLight;
            UpdateTiltIndicator();
        }

        public void BuildTiltIndicator()
        {
            Transform oldL = transform.Find("LeftTiltFill");
            if (oldL != null) Destroy(oldL.gameObject);

            Transform oldR = transform.Find("RightTiltFill");
            if (oldR != null) Destroy(oldR.gameObject);

            if (!showTiltLight || mainRenderer == null) return;

            leftTiltFillRenderer = mainRenderer.CreateChildLine("LeftTiltFill");
            rightTiltFillRenderer = mainRenderer.CreateChildLine("RightTiltFill");

            float yCenter = 0.42f;
            float xCenter = 0.40f;
            float fillSize = 0.09f;

            Vector3[] rightFillPoints =
            {
                new Vector3(xCenter - fillSize * 0.5f, yCenter, 0f),
                new Vector3(xCenter + fillSize * 0.5f, yCenter, 0f)
            };

            Vector3[] leftFillPoints =
            {
                new Vector3(-xCenter + fillSize * 0.5f, yCenter, 0f),
                new Vector3(-xCenter - fillSize * 0.5f, yCenter, 0f)
            };

            mainRenderer.ConfigureLine(rightTiltFillRenderer, rightFillPoints, false, tiltDefaultColor, fillSize);
            rightTiltFillRenderer.sortingOrder = mainRenderer.SortingOrder + 1;

            mainRenderer.ConfigureLine(leftTiltFillRenderer, leftFillPoints, false, tiltDefaultColor, fillSize);
            leftTiltFillRenderer.sortingOrder = mainRenderer.SortingOrder + 1;
        }

        public void GetTiltColors(out Color leftColor, out Color rightColor)
        {
            leftColor = tiltDefaultColor;
            rightColor = tiltDefaultColor;

            if (lander == null) return;

            float deltaAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
            float absAngle = Mathf.Abs(deltaAngle);

            bool badTilt = absAngle > lander.MaxLandingAngle;
            bool goodSpeed = Mathf.Abs(lander.Velocity.x) <= lander.MaxLandingHorizontalSpeed &&
                             Mathf.Abs(lander.Velocity.y) <= lander.MaxLandingVerticalSpeed;

            if (badTilt)
            {
                if (deltaAngle > 0f)
                {
                    leftColor = tiltUnsafeColor;
                }
                else
                {
                    rightColor = tiltUnsafeColor;
                }
            }
            else if (goodSpeed)
            {
                leftColor = tiltSafeColor;
                rightColor = tiltSafeColor;
            }
        }

        public void UpdateTiltIndicator()
        {
            if (!showTiltLight || isHidden || mainRenderer == null) return;

            GetTiltColors(out Color leftColor, out Color rightColor);

            if (leftTiltFillRenderer != null) mainRenderer.ApplyColor(leftTiltFillRenderer, leftColor);
            if (rightTiltFillRenderer != null) mainRenderer.ApplyColor(rightTiltFillRenderer, rightColor);
        }
    }
}