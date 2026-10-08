using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public abstract class LanderTiltRcsRendererBase : MonoBehaviour
    {
        [Header("Configuración Base Tilt RCS")]
        [SerializeField] protected bool showTiltLight = true;
        [SerializeField] protected Color tiltDefaultColor = Color.white;
        [SerializeField] protected Color tiltSafeColor = new Color(0.30f, 1f, 0.40f);
        [SerializeField] protected Color tiltUnsafeColor = new Color(1f, 0.20f, 0.20f);

        protected VectorLanderRendererBase mainRenderer;
        protected LanderController lander;
        protected bool isHidden;

        public bool ShowTiltLight => showTiltLight;

        public virtual void Initialize(VectorLanderRendererBase renderer, LanderController controller)
        {
            mainRenderer = renderer;
            lander = controller;
            BuildTiltIndicator();
        }

        protected virtual void Update() => UpdateTiltIndicator();

        public virtual void SetHidden(bool hidden)
        {
            isHidden = hidden;
            ApplyTiltVisibility();
        }

        public virtual void ResetState()
        {
            isHidden = false;
            ApplyTiltVisibility();
            UpdateTiltIndicator();
        }

        public virtual void GetTiltColors(out Color leftColor, out Color rightColor)
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
                if (deltaAngle > 0f) leftColor = tiltUnsafeColor;
                else rightColor = tiltUnsafeColor;
            }
            else if (goodSpeed)
            {
                leftColor = tiltSafeColor;
                rightColor = tiltSafeColor;
            }
        }

        // Métodos abstractos a implementar por cada Lander
        public abstract void BuildTiltIndicator();
        public abstract void UpdateTiltIndicator();
        protected abstract void ApplyTiltVisibility();
    }
}