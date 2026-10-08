using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public abstract class LanderFuelTankRendererBase : MonoBehaviour
    {
        [Header("Configuración Base Fuel")]
        [SerializeField] protected bool showFuelTanks = true;
        [SerializeField] protected Gradient fuelGradient = CreateDefaultFuelGradient();
        [SerializeField, Range(0f, 1f)] protected float lowFuelThreshold = 0.25f;
        [SerializeField, Min(0f)] protected float lowFuelBlinkRate = 3f;

        protected VectorLanderRendererBase mainRenderer;
        protected LanderController lander;
        protected float fuelLevel = 1f;
        protected bool fillVisible = true;
        protected bool isHidden;

        public float FuelLevel => fuelLevel;
        public Gradient FuelGradient => fuelGradient;

        protected static Gradient CreateDefaultFuelGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.15f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.10f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.92f, 0.20f), 0.5f),
                    new GradientColorKey(new Color(0.30f, 1f, 0.40f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        public virtual void Initialize(VectorLanderRendererBase renderer, LanderController controller)
        {
            mainRenderer = renderer;
            lander = controller;
            BuildTanks();
        }

        protected virtual void Update() => UpdateFuelWarningBlink();

        public virtual void SetFuelLevel(float normalized)
        {
            fuelLevel = Mathf.Clamp01(normalized);
            RedrawTanks();
        }

        public virtual void SetHidden(bool hidden)
        {
            isHidden = hidden;
            ApplyTankVisibility();
        }

        public virtual void ResetState()
        {
            isHidden = false;
            fillVisible = true;
            if (lander != null) fuelLevel = lander.FuelNormalized;
            RedrawTanks();
        }

        protected virtual void UpdateFuelWarningBlink()
        {
            if (isHidden) return;
            bool warning = fuelLevel > 0f && fuelLevel <= lowFuelThreshold && lowFuelBlinkRate > 0f;
            bool visible = !warning || ((int)(Time.time * lowFuelBlinkRate * 2f) & 1) == 0;
            if (visible == fillVisible) return;
            fillVisible = visible;
            ApplyTankVisibility();
        }

        // Métodos abstractos que define cada nave según su forma
        public abstract void BuildTanks();
        public abstract void RedrawTanks();
        protected abstract void ApplyTankVisibility();
        public abstract void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed);
    }
}