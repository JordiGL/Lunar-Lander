using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class LanderCameraController : MonoBehaviour
    {
        [Header("Objetivo")]
        [Tooltip("Referencia al módulo lunar.")]
        [SerializeField] private LanderController target;

        [Header("Zoom")]
        [Tooltip("Tamaño ortográfico en la vista lejana (espacio).")]
        [SerializeField, Min(10f)] private float maxZoomSize = 22f;

        [Tooltip("Tamaño ortográfico en aproximación de aterrizaje.")]
        [SerializeField, Min(2f)] private float minZoomSize = 6.5f;

        [Tooltip("Altitud sobre el suelo a partir de la cual se alcanza el zoom máximo (vista lejana).")]
        [SerializeField, Min(5f)] private float zoomOutAltitude = 16f;

        [Tooltip("Altitud bajo la cual se alcanza el zoom mínimo (máximo detalle).")]
        [SerializeField, Min(0.5f)] private float zoomInAltitude = 3.5f;

        [Tooltip("Curva de respuesta del zoom: 0 = cerca del suelo, 1 = lejos.")]
        [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Suavizado al acercarse (zoom in).")]
        [SerializeField, Min(0.01f)] private float zoomInSmoothTime = 0.5f;

        [Tooltip("Suavizado al alejarse (más lento para evitar tirones).")]
        [SerializeField, Min(0.01f)] private float zoomOutSmoothTime = 1.2f;

        [Tooltip("Zoom extra (en unidades ortográficas) cuando la nave va a máxima velocidad.")]
        [SerializeField, Min(0f)] private float speedZoomBonus = 2.5f;

        [Tooltip("Velocidad a la que se aplica el bonus completo de zoom.")]
        [SerializeField, Min(1f)] private float speedForFullBonus = 14f;

        [Header("Detección de suelo")]
        [Tooltip("Distancia máxima de los rayos hacia abajo.")]
        [SerializeField, Min(10f)] private float groundRayDistance = 80f;

        [Tooltip("Apertura lateral del abanico de rayos (0 = todos verticales).")]
        [SerializeField, Range(0f, 1f)] private float fanSpread = 0.6f;

        [Tooltip("Velocidad con la que la altitud suavizada baja (acercándose al suelo).")]
        [SerializeField, Min(0.1f)] private float altitudeDescendRate = 12f;

        [Tooltip("Velocidad con la que la altitud suavizada sube (alejándose del suelo).")]
        [SerializeField, Min(0.1f)] private float altitudeAscendRate = 4f;

        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Seguimiento de posición")]
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.18f;
        [SerializeField] private float verticalOffset = 1.2f;

        [Tooltip("Cuánto se adelanta la cámara en la dirección de movimiento (segundos de velocidad).")]
        [SerializeField, Min(0f)] private float lookAheadTime = 0.35f;

        [Tooltip("Desplazamiento máximo del adelanto, en unidades.")]
        [SerializeField, Min(0f)] private Vector2 maxLookAhead = new Vector2(4f, 2.5f);

        [Tooltip("Suavizado del vector de adelanto (evita bandazos al cambiar de empuje).")]
        [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.5f;

        [Header("Límites del mundo (opcional)")]
        [SerializeField] private bool clampToWorld = false;
        [SerializeField] private float worldMinX = -90f;
        [SerializeField] private float worldMaxX = 90f;

        [Header("Sacudida")]
        [Tooltip("Sacudida de cámara al estrellarse.")]
        [SerializeField] private bool shakeOnCrash = true;
        [SerializeField, Min(0f)] private float crashShakeMagnitude = 0.6f;
        [SerializeField, Min(0.05f)] private float crashShakeDuration = 0.6f;

        // Direcciones del abanico (precalculadas, sin asignaciones por frame).
        private const int RayCount = 5;
        private readonly Vector2[] fanDirections = new Vector2[RayCount];
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private ContactFilter2D groundFilter;

        private Camera cam;
        private Vector3 followVelocity;
        private float zoomVelocity;
        private float smoothedAltitude;

        private Vector3 lastTargetPos;
        private Vector2 targetVelocity;
        private Vector2 lookAhead;
        private Vector2 lookAheadVelocity;

        private float shakeTimeLeft;
        private float shakeStrength;

        private bool subscribed;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;

            if (target == null) target = FindFirstObjectByType<LanderController>();

            BuildFanDirections();
            RebuildFilter();

            smoothedAltitude = zoomOutAltitude;
            cam.orthographicSize = maxZoomSize;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void OnValidate()
        {
            maxZoomSize = Mathf.Max(maxZoomSize, minZoomSize + 0.5f);
            zoomOutAltitude = Mathf.Max(zoomOutAltitude, zoomInAltitude + 0.5f);
            worldMaxX = Mathf.Max(worldMaxX, worldMinX + 1f);

            BuildFanDirections();
            RebuildFilter();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                target = FindFirstObjectByType<LanderController>();
                if (target == null) return;
                Subscribe();
                SnapToTarget();
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateTargetVelocity(dt);
            UpdateZoom(dt);
            UpdatePosition(dt);
        }

        // ------------------------------------------------------------------
        // Zoom
        // ------------------------------------------------------------------

        private void UpdateZoom(float dt)
        {
            float detected = SampleFanAltitude();

            float rate = detected < smoothedAltitude ? altitudeDescendRate : altitudeAscendRate;
            smoothedAltitude = Mathf.MoveTowards(smoothedAltitude, detected, rate * dt);

            float t = Mathf.InverseLerp(zoomInAltitude, zoomOutAltitude, smoothedAltitude);
            t = Mathf.Clamp01(zoomCurve.Evaluate(t));

            float targetSize = Mathf.Lerp(minZoomSize, maxZoomSize, t);

            // Al ir rápido se abre un poco la vista para anticipar el terreno.
            float speedT = Mathf.Clamp01(targetVelocity.magnitude / speedForFullBonus);
            targetSize = Mathf.Min(maxZoomSize, targetSize + speedZoomBonus * speedT);

            float smoothTime = targetSize > cam.orthographicSize ? zoomOutSmoothTime : zoomInSmoothTime;
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref zoomVelocity, smoothTime, Mathf.Infinity, dt);
        }

        private float SampleFanAltitude()
        {
            Vector2 origin = target.transform.position;
            float best = float.MaxValue;

            for (int d = 0; d < RayCount; d++)
            {
                int count = Physics2D.Raycast(origin, fanDirections[d], groundFilter, groundHits, groundRayDistance);
                for (int i = 0; i < count; i++)
                {
                    Collider2D col = groundHits[i].collider;
                    if (col == null || col.transform.IsChildOf(target.transform)) continue;

                    // Altitud vertical respecto al punto de impacto del rayo.
                    float vertical = origin.y - groundHits[i].point.y;
                    if (vertical < 0f) vertical = 0f;
                    if (vertical < best) best = vertical;
                }
            }

            // Sin suelo detectado: se considera "lejos" (vista panorámica).
            return best < float.MaxValue ? best : zoomOutAltitude * 2f;
        }

        // ------------------------------------------------------------------
        // Posición
        // ------------------------------------------------------------------

        private void UpdateTargetVelocity(float dt)
        {
            Vector3 pos = target.transform.position;
            Vector2 instant = (pos - lastTargetPos) / dt;
            lastTargetPos = pos;

            // Filtro ligero para evitar picos por teletransportes o colisiones.
            instant = Vector2.ClampMagnitude(instant, 60f);
            targetVelocity = Vector2.Lerp(targetVelocity, instant, 1f - Mathf.Exp(-12f * dt));
        }

        private void UpdatePosition(float dt)
        {
            Vector2 desiredAhead = new Vector2(
                Mathf.Clamp(targetVelocity.x * lookAheadTime, -maxLookAhead.x, maxLookAhead.x),
                Mathf.Clamp(targetVelocity.y * lookAheadTime, -maxLookAhead.y, maxLookAhead.y));

            lookAhead = Vector2.SmoothDamp(lookAhead, desiredAhead, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, dt);

            Vector3 targetPos = target.transform.position;
            targetPos.x += lookAhead.x;
            targetPos.y += verticalOffset + lookAhead.y;
            targetPos.z = transform.position.z;

            if (clampToWorld) targetPos.x = ClampXToWorld(targetPos.x);

            Vector3 pos = Vector3.SmoothDamp(transform.position, targetPos, ref followVelocity, followSmoothTime, Mathf.Infinity, dt);

            if (clampToWorld) pos.x = ClampXToWorld(pos.x);

            pos += (Vector3)ComputeShakeOffset(dt);
            transform.position = pos;
        }

        private float ClampXToWorld(float x)
        {
            // La mitad del ancho visible depende del zoom actual.
            float halfWidth = cam.orthographicSize * cam.aspect;
            float min = worldMinX + halfWidth;
            float max = worldMaxX - halfWidth;
            if (min > max) return (worldMinX + worldMaxX) * 0.5f;
            return Mathf.Clamp(x, min, max);
        }

        private Vector2 ComputeShakeOffset(float dt)
        {
            if (shakeTimeLeft <= 0f) return Vector2.zero;

            shakeTimeLeft -= dt;
            float falloff = Mathf.Clamp01(shakeTimeLeft / crashShakeDuration);
            return Random.insideUnitCircle * (shakeStrength * falloff * falloff);
        }

        // ------------------------------------------------------------------
        // Eventos del módulo
        // ------------------------------------------------------------------

        private void Subscribe()
        {
            if (subscribed) return;
            if (target == null) target = FindFirstObjectByType<LanderController>();
            if (target == null) return;

            target.OnLanded += HandleLanded;
            target.OnReset += HandleReset;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || target == null) return;
            target.OnLanded -= HandleLanded;
            target.OnReset -= HandleReset;
            subscribed = false;
        }

        private void HandleLanded(LandingResult result)
        {
            if (!shakeOnCrash || result.Success) return;
            shakeStrength = crashShakeMagnitude;
            shakeTimeLeft = crashShakeDuration;
        }

        private void HandleReset()
        {
            shakeTimeLeft = 0f;
            SnapToTarget();
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        /// <summary>Coloca la cámara de inmediato sobre el objetivo (inicio o reinicio).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            followVelocity = Vector3.zero;
            zoomVelocity = 0f;
            lookAhead = Vector2.zero;
            lookAheadVelocity = Vector2.zero;
            targetVelocity = Vector2.zero;
            lastTargetPos = target.transform.position;

            smoothedAltitude = zoomOutAltitude;

            Vector3 p = target.transform.position;
            p.y += verticalOffset;
            p.z = transform.position.z;
            if (clampToWorld && cam != null) p.x = ClampXToWorld(p.x);
            transform.position = p;
        }

        private void BuildFanDirections()
        {
            // Rayos: abajo, dos diagonales suaves y dos diagonales más abiertas.
            float s = fanSpread;
            fanDirections[0] = Vector2.down;
            fanDirections[1] = new Vector2(-0.35f * s / 0.6f, -1f).normalized;
            fanDirections[2] = new Vector2(0.35f * s / 0.6f, -1f).normalized;
            fanDirections[3] = new Vector2(-0.9f * s / 0.6f, -1f).normalized;
            fanDirections[4] = new Vector2(0.9f * s / 0.6f, -1f).normalized;
        }

        private void RebuildFilter()
        {
            groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = groundMask
            };
        }
    }
}