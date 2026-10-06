using System;
using UnityEngine;

namespace LunarLander
{
    /// <summary>Estado general de la nave.</summary>
    public enum LanderState
    {
        /// <summary>La nave está en vuelo y responde al jugador.</summary>
        Flying,
        /// <summary>Aterrizaje correcto sobre una plataforma.</summary>
        Landed,
        /// <summary>Colisión destructiva (velocidad, ángulo o superficie no válidos).</summary>
        Crashed
    }

    /// <summary>Instantánea de telemetría de la nave (pensada para HUD).</summary>
    public readonly struct LanderStatus
    {
        public readonly float Fuel;
        public readonly float FuelNormalized;
        public readonly Vector2 Velocity;
        public readonly float HorizontalSpeed;
        public readonly float VerticalSpeed;
        public readonly float Angle;
        public readonly LanderState State;

        public LanderStatus(float fuel, float fuelNormalized, Vector2 velocity, float angle, LanderState state)
        {
            Fuel = fuel;
            FuelNormalized = fuelNormalized;
            Velocity = velocity;
            HorizontalSpeed = velocity.x;
            VerticalSpeed = velocity.y;
            Angle = angle;
            State = state;
        }
    }

    /// <summary>Datos del impacto final, entregados en OnLanded / OnCrashed.</summary>
    public readonly struct LandingResult
    {
        public readonly bool Success;
        public readonly bool OnLandingPad;
        public readonly float ImpactVerticalSpeed;
        public readonly float ImpactHorizontalSpeed;
        public readonly float ImpactAngle;
        public readonly float FuelRemaining;

        public LandingResult(bool success, bool onLandingPad, float vSpeed, float hSpeed, float angle, float fuelRemaining)
        {
            Success = success;
            OnLandingPad = onLandingPad;
            ImpactVerticalSpeed = vSpeed;
            ImpactHorizontalSpeed = hSpeed;
            ImpactAngle = angle;
            FuelRemaining = fuelRemaining;
        }
    }

    /// <summary>
    /// Controla la física 2D de la nave: rotación, empuje principal, combustible
    /// y detección de aterrizaje / colisión. Expone eventos C# para que el HUD,
    /// el renderer vectorial o el GameManager reaccionen sin acoplarse a este script.
    ///
    /// Controles (Input Manager clásico):
    ///   - Rotar: A / D o flechas izquierda / derecha.
    ///   - Empuje principal: Espacio o W.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class LanderController : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Movimiento")]
        [Tooltip("Fuerza del motor principal (se aplica con ForceMode2D.Force, depende de la masa del Rigidbody2D).")]
        [SerializeField, Min(0f)] private float thrustForce = 8f;

        [Tooltip("Velocidad de rotación en grados por segundo.")]
        [SerializeField, Min(0f)] private float rotationSpeed = 120f;

        [Tooltip("Escala de gravedad del Rigidbody2D (gravedad lunar ~ 1/6 de la terrestre).")]
        [SerializeField, Min(0f)] private float gravityScale = 0.3f;

        [Header("Combustible")]
        [SerializeField, Min(0f)] private float maxFuel = 1000f;

        [Tooltip("Unidades de combustible consumidas por segundo con el empuje activo.")]
        [SerializeField, Min(0f)] private float fuelBurnRate = 40f;

        [Header("Aterrizaje")]
        [Tooltip("Capas que cuentan como plataforma de aterrizaje. El resto del terreno siempre provoca choque.")]
        [SerializeField] private LayerMask landingPadLayers;

        [Tooltip("Velocidad vertical máxima de impacto para un aterrizaje seguro.")]
        [SerializeField, Min(0f)] private float maxLandingVerticalSpeed = 2f;

        [Tooltip("Velocidad horizontal máxima de impacto para un aterrizaje seguro.")]
        [SerializeField, Min(0f)] private float maxLandingHorizontalSpeed = 1f;

        [Tooltip("Inclinación máxima (grados respecto a la vertical) para un aterrizaje seguro.")]
        [SerializeField, Range(0f, 90f)] private float maxLandingAngle = 10f;

        // ------------------------------------------------------------------
        // Eventos
        // ------------------------------------------------------------------

        /// <summary>Telemetría completa; se emite cada FixedUpdate mientras vuela y al cambiar de estado.</summary>
        public event Action<LanderStatus> OnStatusChanged;

        /// <summary>Combustible actual y normalizado (0-1); solo cuando el valor cambia.</summary>
        public event Action<float, float> OnFuelChanged;

        /// <summary>true cuando el empuje principal se enciende, false cuando se apaga.</summary>
        public event Action<bool> OnThrustChanged;

        /// <summary>Aterrizaje correcto.</summary>
        public event Action<LandingResult> OnLanded;

        /// <summary>Colisión destructiva.</summary>
        public event Action<LandingResult> OnCrashed;

        // ------------------------------------------------------------------
        // Estado público de solo lectura
        // ------------------------------------------------------------------

        public LanderState State { get; private set; } = LanderState.Flying;
        public bool IsThrusting { get; private set; }
        public float CurrentFuel => currentFuel;
        public float MaxFuel => maxFuel;
        public float FuelNormalized => maxFuel > 0f ? currentFuel / maxFuel : 0f;
        public Vector2 Velocity => rb.velocity; // En Unity 2022.3 la propiedad es "velocity".

        // Límites de aterrizaje seguro (los usa el HUD para avisar al jugador).
        public float MaxLandingVerticalSpeed => maxLandingVerticalSpeed;
        public float MaxLandingHorizontalSpeed => maxLandingHorizontalSpeed;
        public float MaxLandingAngle => maxLandingAngle;

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private Rigidbody2D rb;
        private float currentFuel;

        // La entrada se lee en Update y se consume en FixedUpdate (física estable).
        private float rotationInput;   // +1 = antihorario (izquierda), -1 = horario (derecha)
        private bool thrustInput;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (TryGetComponent(out rb))
            {
                rb.gravityScale = gravityScale;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }
            else
            {
                Debug.LogError($"[LanderController] No se encontró el componente Rigidbody2D en {gameObject.name}.", this);
            }

            currentFuel = maxFuel;
        }

        private void Start()
        {
            // En Start para que los suscriptores (HUD, etc.) ya estén registrados desde OnEnable.
            OnFuelChanged?.Invoke(currentFuel, FuelNormalized);
            EmitStatus();
        }

        private void Update()
        {
            if (State != LanderState.Flying)
            {
                rotationInput = 0f;
                thrustInput = false;
                return;
            }

            rotationInput = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rotationInput += 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rotationInput -= 1f;

            thrustInput = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W);
        }

        private void FixedUpdate()
        {
            if (State != LanderState.Flying)
            {
                SetThrusting(false);
                return;
            }

            // Rotación estilo arcade: control directo, sin inercia angular.
            rb.angularVelocity = rotationInput * rotationSpeed;

            // Empuje principal (solo si queda combustible).
            bool thrusting = thrustInput && currentFuel > 0f;
            if (thrusting)
            {
                rb.AddForce(transform.up * thrustForce, ForceMode2D.Force);
                ConsumeFuel(fuelBurnRate * Time.fixedDeltaTime);
            }

            SetThrusting(thrusting);
            EmitStatus();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (State != LanderState.Flying) return;

            // relativeVelocity refleja la velocidad de aproximación antes de que el motor
            // de física resuelva el choque (rb.velocity ya estaría modificada).
            Vector2 impact = collision.relativeVelocity;
            float vSpeed = Mathf.Abs(impact.y);
            float hSpeed = Mathf.Abs(impact.x);
            float angle = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, 0f));

            bool onPad = (landingPadLayers.value & (1 << collision.gameObject.layer)) != 0;
            bool safe = vSpeed <= maxLandingVerticalSpeed
                        && hSpeed <= maxLandingHorizontalSpeed
                        && angle <= maxLandingAngle;

            var result = new LandingResult(onPad && safe, onPad, vSpeed, hSpeed, angle, currentFuel);
            FinishFlight(result);
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>Reinicia la nave (nueva partida o nuevo intento).</summary>
        public void ResetLander(Vector2 position, float rotationDegrees = 0f)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = position;
            rb.rotation = rotationDegrees;

            currentFuel = maxFuel;
            State = LanderState.Flying;
            rotationInput = 0f;
            thrustInput = false;

            SetThrusting(false);
            OnFuelChanged?.Invoke(currentFuel, FuelNormalized);
            EmitStatus();
        }

        /// <summary>Añade combustible (p. ej. bonus), limitado al máximo.</summary>
        public void AddFuel(float amount)
        {
            if (amount <= 0f) return;
            currentFuel = Mathf.Min(maxFuel, currentFuel + amount);
            OnFuelChanged?.Invoke(currentFuel, FuelNormalized);
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        private void ConsumeFuel(float amount)
        {
            float previous = currentFuel;
            currentFuel = Mathf.Max(0f, currentFuel - amount);

            if (!Mathf.Approximately(previous, currentFuel))
            {
                OnFuelChanged?.Invoke(currentFuel, FuelNormalized);
            }
        }

        private void SetThrusting(bool value)
        {
            if (IsThrusting == value) return;
            IsThrusting = value;
            OnThrustChanged?.Invoke(value);
        }

        private void FinishFlight(LandingResult result)
        {
            SetThrusting(false);
            rotationInput = 0f;
            thrustInput = false;

            if (result.Success)
            {
                State = LanderState.Landed;

                // Congela la nave sobre la plataforma.
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;

                EmitStatus();
                OnLanded?.Invoke(result);
            }
            else
            {
                State = LanderState.Crashed;
                EmitStatus();
                OnCrashed?.Invoke(result);
            }
        }

        private void EmitStatus()
        {
            if (OnStatusChanged == null) return;

            float angle = Mathf.DeltaAngle(0f, rb.rotation); // -180..180, 0 = vertical
            OnStatusChanged.Invoke(new LanderStatus(currentFuel, FuelNormalized, rb.velocity, angle, State));
        }
    }
}