using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class StandardHUD : MonoBehaviour
    {
        [Header("Referencias de Nave y Entorno")]
        [Tooltip("Vacío = se busca un LanderController en la escena.")]
        [SerializeField] private LanderController lander;

        [Header("Textos del Panel Izquierdo (TMP)")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI fuelText;

        [Header("Textos del Panel Derecho (TMP)")]
        [SerializeField] private TextMeshProUGUI altitudeText;
        [SerializeField] private TextMeshProUGUI hSpeedText;
        [SerializeField] private TextMeshProUGUI vSpeedText;

        [Header("Medidor de Combustible e Inclinación (Opcional)")]
        [Tooltip("Barra de UI de tipo Image (Fill) para ver el combustible")]
        [SerializeField] private Image fuelBarFill;
        [Tooltip("Aguja o indicador de inclinación (RectTransform que rota o se desplaza)")]
        [SerializeField] private RectTransform tiltIndicator;
        [Tooltip("Línea verde que delimita la zona de aterrizaje seguro")]
        [SerializeField] private RectTransform tiltSafeZoneLine;
        [SerializeField, Range(20f, 90f)] private float tiltDisplayRange = 45f;
        [SerializeField, Min(10f)] private float tiltMaxOffset = 100f;

        [Header("Alertas Centrales")]
        [SerializeField] private TextMeshProUGUI alertText;

        [Header("Paleta de Colores")]
        [SerializeField] private Color normalColor = new Color(0.40f, 1f, 0.55f);
        [SerializeField] private Color warningColor = new Color(1f, 0.32f, 0.22f);

        [Header("Configuración de Telemetría")]
        [SerializeField, Range(1, 10)] private int altitudeUpdateInterval = 2;
        [SerializeField, Min(0.05f)] private float blinkInterval = 0.3f;
        [SerializeField, Min(0f)] private float alertAltitude = 4f;
        [SerializeField, Min(0.5f)] private float minAlertDuration = 1.2f;
        [SerializeField, Range(0f, 1f)] private float lowFuelFraction = 0.2f;

        [Header("Escalas y Compensaciones")]
        [SerializeField, Min(0.01f)] private float altitudeDisplayScale = 10f;
        [SerializeField, Min(0.01f)] private float speedDisplayScale = 10f;
        [SerializeField, Min(0f)] private float footOffset = 0.55f;

        // Estado del juego
        private int score;
        private float flightTime;
        private LanderState lastState = LanderState.Flying;
        private bool blinkOn = true;
        private float blinkTimer;

        // Raycast optimizado sin asignaciones de memoria
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private ContactFilter2D groundFilter;
        private int frameCounter;
        private float cachedAltitude;

        // Variables internas añadidas para estabilización
        private string currentAlertMessage = string.Empty;
        private float alertHoldTimer;
        private bool currentAlertIsSafe;

        // ------------------------------------------------------------------
        // API Pública (Requerida por GameManager)
        // ------------------------------------------------------------------

        public int Score => score;

        public void AddScore(int points)
        {
            score += points;
        }

        public void ResetTimer()
        {
            flightTime = 0f;
        }

        // ------------------------------------------------------------------
        // Ciclo de Vida
        // ------------------------------------------------------------------

        private void Start()
        {
            if (tiltSafeZoneLine != null && lander != null)
            {
                float safeRatio = Mathf.Clamp01(lander.MaxLandingAngle / tiltDisplayRange);
                float safeWidth = safeRatio * tiltMaxOffset * 2f;
                tiltSafeZoneLine.sizeDelta = new Vector2(safeWidth, tiltSafeZoneLine.sizeDelta.y);
            }
        }

        private void Awake()
        {
            if (lander == null) lander = FindObjectOfType<LanderController>();

            groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = Physics2D.AllLayers
            };
        }

        private void Update()
        {
            if (lander == null) return;

            UpdateBlink();
            UpdateFlightState();

            if (frameCounter++ % altitudeUpdateInterval == 0)
            {
                cachedAltitude = CalculateTerrainAltitude();
            }

            Vector2 velocity = lander.Velocity;
            float angle = Mathf.DeltaAngle(0f, lander.transform.eulerAngles.z);

            UpdateReadouts(velocity, cachedAltitude);
            UpdateTiltGauge(angle);
            UpdateAlert(velocity, cachedAltitude, angle);
        }

        // ------------------------------------------------------------------
        // Actualizaciones de UI
        // ------------------------------------------------------------------

        private void UpdateBlink()
        {
            blinkTimer += Time.unscaledDeltaTime;
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer -= blinkInterval;
                blinkOn = !blinkOn;
            }
        }

        private void UpdateFlightState()
        {
            LanderState state = lander.State;
            if (state != lastState)
            {
                if (state == LanderState.Flying)
                {
                    flightTime = 0f;
                }
                lastState = state;
            }

            if (state == LanderState.Flying)
            {
                flightTime += Time.deltaTime;
            }
        }

        private void UpdateReadouts(Vector2 velocity, float altitude)
        {
            // 1. Puntuación
            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {Mathf.Clamp(score, 0, 9999):D4}";
            }

            // 2. Tiempo de vuelo
            if (timeText != null)
            {
                int totalSecs = Mathf.Min(Mathf.FloorToInt(flightTime), 5999);
                int mins = totalSecs / 60;
                int secs = totalSecs % 60;
                timeText.text = $"TIME:  {mins:D2}:{secs:D2}";
            }

            // 3. Combustible
            float currentFuel = lander.CurrentFuel;
            bool fuelEmpty = currentFuel <= 0f;
            bool fuelLow = lander.FuelNormalized <= lowFuelFraction;

            if (fuelText != null)
            {
                fuelText.text = $"FUEL:  {Mathf.CeilToInt(currentFuel):D4}";
                fuelText.color = fuelLow ? warningColor : normalColor;
                fuelText.enabled = !fuelEmpty || blinkOn;
            }

            if (fuelBarFill != null)
            {
                fuelBarFill.fillAmount = lander.FuelNormalized;
                fuelBarFill.color = fuelLow ? warningColor : normalColor;
            }

            // 4. Altitud sobre el terreno
            if (altitudeText != null)
            {
                int altInt = Mathf.Clamp(Mathf.FloorToInt(altitude * altitudeDisplayScale), 0, 9999);
                altitudeText.text = $"ALTITUDE: {altInt:D4}";
            }

            // 5. Velocidad Horizontal
            int hNow = Mathf.RoundToInt(velocity.x * speedDisplayScale);
            bool hUnsafe = Mathf.Abs(velocity.x) > lander.MaxLandingHorizontalSpeed;
            if (hSpeedText != null)
            {
                string arrow = hNow > 0 ? "→" : hNow < 0 ? "←" : " ";
                hSpeedText.text = $"H SPEED: {arrow}{Mathf.Abs(hNow):D3}";
                hSpeedText.color = hUnsafe ? warningColor : normalColor;
            }

            // 6. Velocidad Vertical
            int vNow = Mathf.RoundToInt(velocity.y * speedDisplayScale);
            bool vUnsafe = -velocity.y > lander.MaxLandingVerticalSpeed;
            if (vSpeedText != null)
            {
                string arrow = vNow > 0 ? "↑" : vNow < 0 ? "↓" : " ";
                vSpeedText.text = $"V SPEED: {arrow}{Mathf.Abs(vNow):D3}";
                vSpeedText.color = vUnsafe ? warningColor : normalColor;
            }
        }

        private void UpdateTiltGauge(float angle)
        {
            if (tiltIndicator == null) return;

            float norm = Mathf.Clamp(angle / tiltDisplayRange, -1f, 1f);
            tiltIndicator.anchoredPosition = new Vector2(-norm * tiltMaxOffset, tiltIndicator.anchoredPosition.y);
            tiltIndicator.localEulerAngles = Vector3.zero;

            bool tiltUnsafe = Mathf.Abs(angle) > lander.MaxLandingAngle;
            var img = tiltIndicator.GetComponent<Image>();
            if (img != null)
            {
                img.color = tiltUnsafe ? warningColor : normalColor;
            }
        }

        private void UpdateAlert(Vector2 velocity, float altitude, float angle)
        {
            if (alertText == null) return;

            if (lander.State != LanderState.Flying)
            {
                alertText.enabled = false;
                currentAlertMessage = string.Empty;
                alertHoldTimer = 0f;
                return;
            }

            string detectedMessage = string.Empty;
            bool isSafeToLand = false;
            bool nearGround = altitude < alertAltitude;

            if (lander.CurrentFuel <= 0f) detectedMessage = "! NO FUEL !";
            else if (nearGround && -velocity.y > lander.MaxLandingVerticalSpeed) detectedMessage = "! TOO FAST !";
            else if (nearGround && Mathf.Abs(angle) > lander.MaxLandingAngle) detectedMessage = "! LEVEL SHIP !";
            else if (nearGround && Mathf.Abs(velocity.x) > lander.MaxLandingHorizontalSpeed) detectedMessage = "! DRIFTING !";
            else if (lander.FuelNormalized <= lowFuelFraction) detectedMessage = "! LOW FUEL !";
            else if (nearGround)
            {
                detectedMessage = "SAFE TO LAND";
                isSafeToLand = true;
            }

            if (!string.IsNullOrEmpty(detectedMessage))
            {
                currentAlertMessage = detectedMessage;
                currentAlertIsSafe = isSafeToLand;
                alertHoldTimer = minAlertDuration;
            }
            else if (alertHoldTimer > 0f)
            {
                alertHoldTimer -= Time.deltaTime;
                if (alertHoldTimer <= 0f)
                {
                    currentAlertMessage = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(currentAlertMessage))
            {
                alertText.enabled = false;
            }
            else
            {
                alertText.text = currentAlertMessage;
                alertText.color = currentAlertIsSafe ? normalColor : warningColor;
                alertText.enabled = currentAlertIsSafe || blinkOn;
            }
        }

        private float CalculateTerrainAltitude()
        {
            Vector2 origin = (Vector2)lander.transform.position + Vector2.down * footOffset;
            int count = Physics2D.RaycastNonAlloc(origin, Vector2.down, groundHits, 100f, groundFilter.layerMask);

            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D hitCollider = groundHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(lander.transform)) continue;

                if (groundHits[i].distance < best) best = groundHits[i].distance;
            }

            if (best < float.MaxValue) return best;
            return Mathf.Max(0f, lander.transform.position.y - footOffset);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (scoreText != null) scoreText.text = "SCORE: 0000";
                if (timeText != null) timeText.text = "TIME:  00:00";
                if (fuelText != null) fuelText.text = "FUEL:  1000";
                if (altitudeText != null) altitudeText.text = "ALTITUDE: 0500";
                if (hSpeedText != null) hSpeedText.text = "H SPEED:   000";
                if (vSpeedText != null) vSpeedText.text = "V SPEED:  ↓ 010";
                if (alertText != null) alertText.text = "! LOW FUEL !";
            }
        }
    }
}