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
        Crashed,
        /// <summary>
        /// Primer contacto válido con la plataforma: la física sigue activa unos instantes
        /// para que la nave se asiente sobre las patas antes de darse por aterrizada.
        /// </summary>
        Touchdown
    }

    /// <summary>Tipo de daño de un choque.</summary>
    public enum CrashType
    {
        /// <summary>No hubo choque (aterrizaje correcto).</summary>
        None,
        /// <summary>Golpe moderado en una pata: se rompe y la nave queda dañada.</summary>
        LegBroken,
        /// <summary>Golpe en el casco o a gran velocidad: la nave explota.</summary>
        Explosion
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

        /// <summary>Tipo de daño (None si el aterrizaje fue correcto).</summary>
        public readonly CrashType Crash;

        /// <summary>Pata rota: +1 derecha, -1 izquierda, 0 si no aplica.</summary>
        public readonly int BrokenLegSide;

        /// <summary>Punto de impacto en coordenadas de mundo.</summary>
        public readonly Vector2 ImpactPoint;

        /// <summary>Velocidad de la nave en el momento del choque (para los efectos).</summary>
        public readonly Vector2 LanderVelocity;

        public LandingResult(bool success, bool onLandingPad, float vSpeed, float hSpeed, float angle, float fuelRemaining,
                             CrashType crash = CrashType.None, int brokenLegSide = 0,
                             Vector2 impactPoint = default, Vector2 landerVelocity = default)
        {
            Success = success;
            OnLandingPad = onLandingPad;
            ImpactVerticalSpeed = vSpeed;
            ImpactHorizontalSpeed = hSpeed;
            ImpactAngle = angle;
            FuelRemaining = fuelRemaining;
            Crash = crash;
            BrokenLegSide = brokenLegSide;
            ImpactPoint = impactPoint;
            LanderVelocity = landerVelocity;
        }
    }

    /// <summary>
    /// Controla la física 2D de la nave: rotación, empuje principal, combustible
    /// y detección de aterrizaje / colisión. Expone eventos C# para que el HUD,
    /// el renderer vectorial o el GameManager reaccionen sin acoplarse a este script.
    ///
    /// Aterrizaje en dos fases: al primer contacto válido con una plataforma se entra en
    /// Touchdown (sin control del jugador, física activa) y solo cuando la nave se queda
    /// quieta sobre sus patas se declara Landed.
    ///
    /// Choques: según DÓNDE se apoya la nave y a qué velocidad, se rompe una pata
    /// (CrashType.LegBroken) o explota (CrashType.Explosion). El renderer y el colisionador
    /// reaccionan a OnCrashed.
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

        [Header("Asentamiento tras el contacto")]
        [Tooltip("Segundos que la nave debe permanecer quieta para darse por aterrizada.")]
        [SerializeField, Min(0.05f)] private float settleTime = 0.5f;

        [Tooltip("Tiempo máximo de asentamiento; pasado este tiempo se evalúa el aterrizaje igualmente.")]
        [SerializeField, Min(0.1f)] private float settleTimeout = 4f;

        [Tooltip("Velocidad lineal por debajo de la cual se considera quieta (unidades/s).")]
        [SerializeField, Min(0f)] private float settleLinearSpeed = 0.08f;

        [Tooltip("Velocidad angular por debajo de la cual se considera quieta (grados/s).")]
        [SerializeField, Min(0f)] private float settleAngularSpeed = 5f;

        [Tooltip("Rozamiento lineal añadido mientras se asienta (ayuda a que no resbale ni rebote).")]
        [SerializeField, Min(0f)] private float settleLinearDrag = 1.5f;

        [Tooltip("Rozamiento angular añadido mientras se asienta (ayuda a que las patas apoyen).")]
        [SerializeField, Min(0f)] private float settleAngularDrag = 3f;

        [Tooltip("Si durante el asentamiento la nave se inclina más que esto, se considera que ha volcado.")]
        [SerializeField, Range(10f, 90f)] private float tipOverAngle = 45f;

        [Header("Daños en el choque")]
        [Tooltip("Velocidad de impacto (módulo) a partir de la cual cualquier choque destruye la nave. " +
                 "Por debajo, un golpe en una pata solo la rompe; un golpe en el casco siempre explota.")]
        [SerializeField, Min(0f)] private float explosionSpeed = 5f;

        [Tooltip("Un contacto cuenta como 'pata' si |x local| supera este valor...")]
        [SerializeField, Min(0f)] private float legZoneMinX = 0.5f;

        [Tooltip("...y su y local está por debajo de este valor (coordenadas locales de la nave).")]
        [SerializeField] private float legZoneMaxY = 0.25f;

        // ------------------------------------------------------------------
        // Eventos
        // ------------------------------------------------------------------

        /// <summary>Telemetría completa; se emite cada FixedUpdate mientras vuela/se asienta y al cambiar de estado.</summary>
        public event Action<LanderStatus> OnStatusChanged;

        /// <summary>Combustible actual y normalizado (0-1); solo cuando el valor cambia.</summary>
        public event Action<float, float> OnFuelChanged;

        /// <summary>true cuando el empuje principal se enciende, false cuando se apaga.</summary>
        public event Action<bool> OnThrustChanged;

        /// <summary>Primer contacto válido con la plataforma (empieza el asentamiento).</summary>
        public event Action OnTouchdown;

        /// <summary>Aterrizaje correcto (la nave ya está asentada).</summary>
        public event Action<LandingResult> OnLanded;

        /// <summary>Colisión destructiva o vuelco. result.Crash indica pata rota o explosión.</summary>
        public event Action<LandingResult> OnCrashed;

        /// <summary>La nave se ha reiniciado (los efectos visuales deben restaurarse).</summary>
        public event Action OnReset;

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

        // Asentamiento
        private float settleElapsed;
        private float stillTimer;
        private float touchdownVSpeed;
        private float touchdownHSpeed;
        private float originalLinearDrag;
        private float originalAngularDrag;
        private bool dragOverridden;

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
            if (State == LanderState.Touchdown)
            {
                SetThrusting(false);
                UpdateSettling();
                if (State == LanderState.Touchdown) EmitStatus();
                return;
            }

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
            if (State == LanderState.Landed || State == LanderState.Crashed) return;

            // relativeVelocity refleja la velocidad de aproximación antes de que el motor
            // de física resuelva el choque (rb.velocity ya estaría modificada).
            Vector2 impact = collision.relativeVelocity;
            float vSpeed = Mathf.Abs(impact.y);
            float hSpeed = Mathf.Abs(impact.x);
            float angle = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, 0f));

            bool onPad = (landingPadLayers.value & (1 << collision.gameObject.layer)) != 0;
            bool speedsSafe = vSpeed <= maxLandingVerticalSpeed && hSpeed <= maxLandingHorizontalSpeed;

            Vector2 contactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : rb.position;

            if (State == LanderState.Touchdown)
            {
                // Contactos posteriores mientras se asienta (la otra pata, un pequeño rebote):
                // solo es un choque si es contra otra superficie o con demasiada velocidad.
                if (!onPad || !speedsSafe)
                {
                    FinishFlight(MakeCrashResult(onPad, vSpeed, hSpeed, angle, contactPoint));
                }

                return;
            }

            // Estado Flying: primer contacto.
            if (onPad && speedsSafe && angle <= maxLandingAngle)
            {
                BeginTouchdown(vSpeed, hSpeed);
            }
            else
            {
                FinishFlight(MakeCrashResult(onPad, vSpeed, hSpeed, angle, contactPoint));
            }
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>Reinicia la nave (nueva partida o nuevo intento).</summary>
        public void ResetLander(Vector2 position, float rotationDegrees = 0f)
        {
            RestoreDrag();

            rb.simulated = true; // una explosión la desactiva
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = position;
            rb.rotation = rotationDegrees;

            currentFuel = maxFuel;
            State = LanderState.Flying;
            rotationInput = 0f;
            thrustInput = false;
            settleElapsed = 0f;
            stillTimer = 0f;

            SetThrusting(false);
            OnReset?.Invoke();
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

        /// <summary>
        /// Decide el tipo de daño según el punto de contacto y la velocidad:
        /// golpe en una pata a velocidad moderada = pata rota; cualquier otro caso = explosión.
        /// </summary>
        private void ClassifyCrash(Vector2 worldPoint, float speed, out CrashType type, out int legSide)
        {
            Vector2 local = transform.InverseTransformPoint(worldPoint);
            bool onLeg = Mathf.Abs(local.x) > legZoneMinX && local.y < legZoneMaxY;

            if (onLeg && speed < explosionSpeed)
            {
                type = CrashType.LegBroken;
                legSide = local.x > 0f ? 1 : -1;
            }
            else
            {
                type = CrashType.Explosion;
                legSide = 0;
            }
        }

        private LandingResult MakeCrashResult(bool onPad, float vSpeed, float hSpeed, float angle, Vector2 contactPoint)
        {
            float speed = new Vector2(hSpeed, vSpeed).magnitude;
            ClassifyCrash(contactPoint, speed, out CrashType type, out int side);

            return new LandingResult(false, onPad, vSpeed, hSpeed, angle, currentFuel,
                                     type, side, contactPoint, rb.velocity);
        }

        /// <summary>
        /// Primer contacto válido: se quita el control al jugador pero se deja la física activa
        /// (con algo de rozamiento extra) para que la nave termine de apoyar todas las patas.
        /// </summary>
        private void BeginTouchdown(float vSpeed, float hSpeed)
        {
            touchdownVSpeed = vSpeed;
            touchdownHSpeed = hSpeed;
            settleElapsed = 0f;
            stillTimer = 0f;

            State = LanderState.Touchdown;
            SetThrusting(false);
            rotationInput = 0f;
            thrustInput = false;

            if (!dragOverridden)
            {
                originalLinearDrag = rb.drag;
                originalAngularDrag = rb.angularDrag;
                dragOverridden = true;
            }

            rb.drag = Mathf.Max(originalLinearDrag, settleLinearDrag);
            rb.angularDrag = Mathf.Max(originalAngularDrag, settleAngularDrag);

            EmitStatus();
            OnTouchdown?.Invoke();
        }

        private void UpdateSettling()
        {
            settleElapsed += Time.fixedDeltaTime;

            float angle = Mathf.Abs(Mathf.DeltaAngle(rb.rotation, 0f));

            // Ha volcado: se rompe la pata del lado que quedó abajo.
            // (rotación positiva = antihorario = el lado izquierdo baja)
            if (angle > tipOverAngle)
            {
                int side = rb.rotation > 0f ? -1 : 1;
                FinishFlight(new LandingResult(false, true, touchdownVSpeed, touchdownHSpeed, angle, currentFuel,
                                               CrashType.LegBroken, side, rb.position, rb.velocity));
                return;
            }

            bool still = rb.velocity.magnitude <= settleLinearSpeed
                         && Mathf.Abs(rb.angularVelocity) <= settleAngularSpeed;
            stillTimer = still ? stillTimer + Time.fixedDeltaTime : 0f;

            if (stillTimer >= settleTime || settleElapsed >= settleTimeout)
            {
                bool success = angle <= maxLandingAngle;
                if (success)
                {
                    FinishFlight(new LandingResult(true, true, touchdownVSpeed, touchdownHSpeed, angle, currentFuel));
                }
                else
                {
                    // Quedó apoyada pero demasiado inclinada: se da por dañada (pata rota).
                    int side = rb.rotation > 0f ? -1 : 1;
                    FinishFlight(new LandingResult(false, true, touchdownVSpeed, touchdownHSpeed, angle, currentFuel,
                                                   CrashType.LegBroken, side, rb.position, rb.velocity));
                }
            }
        }

        private void RestoreDrag()
        {
            if (!dragOverridden) return;

            rb.drag = originalLinearDrag;
            rb.angularDrag = originalAngularDrag;
            dragOverridden = false;
        }

        private void FinishFlight(LandingResult result)
        {
            SetThrusting(false);
            rotationInput = 0f;
            thrustInput = false;
            RestoreDrag();

            if (result.Success)
            {
                State = LanderState.Landed;

                // Congela la nave sobre la plataforma (ya asentada).
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;

                EmitStatus();
                OnLanded?.Invoke(result);
            }
            else
            {
                State = LanderState.Crashed;

                if (result.Crash == CrashType.Explosion)
                {
                    // La nave desaparece: sus trozos los crea el renderer. Se apaga la simulación
                    // para que el casco invisible no estorbe a los escombros.
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.simulated = false;
                }

                // Con LegBroken la nave sigue simulándose: sin la pata, caerá/volcará sobre el casco.
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