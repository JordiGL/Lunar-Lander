using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// HUD vectorial definitivo: Dibuja la interfaz mediante trazos de LineRenderer (VectorFont).
    /// Incluye padding clásico de ceros y asistencia visual de aterrizaje seguro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VectorHUD : MonoBehaviour
    {
        private enum LabelAlign { Left, Center, Right }

        // Maquetación
        private const int PanelChars = 14;      // Ancho interior del panel (caracteres)
        private const float AlertScale = 1.4f;  // Tamaño relativo del aviso
        private const float BannerScale = 2f;   // Tamaño relativo del título de mensaje final

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Referencias")]
        [Tooltip("Vacío = se busca un LanderController en la escena.")]
        [SerializeField] private LanderController lander;

        [Tooltip("Vacía = Camera.main. El HUD se cuelga de esta cámara.")]
        [SerializeField] private Camera mainCamera;

        [Header("Paleta")]
        [Tooltip("Color principal: valores, aguja y medidores.")]
        [SerializeField] private Color primaryColor = new Color(0.40f, 1f, 0.55f);

        [Tooltip("Intensidad del color atenuado (etiquetas) respecto al principal.")]
        [SerializeField, Range(0.2f, 0.9f)] private float dimLevel = 0.5f;

        [SerializeField] private Color warningColor = new Color(1f, 0.32f, 0.22f);

        [Header("Brillo (glow)")]
        [Tooltip("Dibuja un halo tenue y más ancho detrás de cada trazo.")]
        [SerializeField] private bool glow = true;

        [Tooltip("Anchura del halo respecto al trazo.")]
        [SerializeField, Range(1.5f, 8f)] private float glowWidthMultiplier = 3.5f;

        [Tooltip("Intensidad del halo (transparencia/brillo).")]
        [SerializeField, Range(0.05f, 0.6f)] private float glowIntensity = 0.2f;

        [Header("Estilo Retro (Analógico)")]
        [Tooltip("Añade una ligera inestabilidad a la posición del HUD.")]
        [SerializeField, Range(0f, 0.005f)] private float analogJitter = 0.001f;

        [Tooltip("Simula el parpadeo de intensidad de un monitor CRT.")]
        [SerializeField, Range(0f, 0.1f)] private float intensityFlicker = 0.03f;

        [Header("Material y orden")]
        [Tooltip("Vacío = Sprites/Default (admite transparencia).")]
        [SerializeField] private Material lineMaterial;

        [SerializeField] private int sortingOrder = 20;

        [Header("Tamaño (relativo a la pantalla)")]
        [SerializeField, Range(0.01f, 0.1f)] private float charHeightFraction = 0.03f;
        [SerializeField, Range(0.02f, 0.3f)] private float lineWidthRatio = 0.07f;
        [SerializeField, Range(0f, 1f)] private float charSpacingRatio = 0.32f;
        [SerializeField, Range(0f, 0.2f)] private float marginFraction = 0.035f;
        [SerializeField, Min(0.5f)] private float hudDistance = 5f;

        [Header("Medidores")]
        [SerializeField, Range(10, 40)] private int fuelSegments = 24;
        [SerializeField, Range(0f, 1f)] private float lowFuelFraction = 0.2f;
        [SerializeField, Range(20f, 90f)] private float tiltDisplayRange = 45f;

        [Header("Avisos y mensajes")]
        [SerializeField, Range(1, 10)] private int altitudeUpdateInterval = 2;
        [SerializeField, Min(0.05f)] private float blinkInterval = 0.3f;
        [SerializeField, Min(0f)] private float alertAltitude = 4f;
        [SerializeField, Min(1f)] private float revealCharsPerSecond = 40f;

        [Header("Valores mostrados")]
        [SerializeField, Min(0.01f)] private float altitudeDisplayScale = 10f;
        [SerializeField, Min(0.01f)] private float speedDisplayScale = 10f;
        [SerializeField, Min(0f)] private float footOffset = 0.55f;

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private Transform hudRoot;
        private Material activeMaterial;
        private Material runtimeMaterial;
        private bool glowTransparent;
        private MaterialPropertyBlock propertyBlock;
        private Color dimColor;
        private Color faintColor;
        private float currentLineWidth;

        private GlyphSet hudGlyphs;
        private GlyphSet alertGlyphs;
        private GlyphSet bannerGlyphs;

        private readonly List<HudLabel> labels = new List<HudLabel>();
        private readonly List<VectorStroke> staticStrokes = new List<VectorStroke>();

        // Textos
        private HudLabel scoreValue;
        private HudLabel timeValue;
        private HudLabel fuelValue;
        private HudLabel altitudeValue;
        private HudLabel hSpeedValue;
        private HudLabel vSpeedValue;
        private HudLabel alertLabel;
        private HudLabel bannerTitle;
        private HudLabel bannerDetail;

        // Marcos
        private VectorStroke leftFrame;
        private VectorStroke rightFrame;
        private VectorStroke bannerFrame;

        // Medidor de combustible
        private VectorStroke fuelFrame;
        private VectorStroke[] fuelCells;
        private int lowFuelCells;
        private int shownLitCells = -1;

        // Medidor de inclinación
        private VectorStroke tiltFrame;
        private VectorStroke tiltCenter;
        private VectorStroke tiltSafe;
        private VectorStroke tiltNeedle;
        private float tiltCenterX;
        private float tiltSpan;
        private float tiltYBottom;
        private float tiltYTop;
        private float needleNorm;
        private float shownNeedleX;
        private bool tiltDirty = true;

        // Buffers reutilizables
        private readonly Vector3[] rectPoints = new Vector3[4];
        private readonly Vector3[] framePoints = new Vector3[6];
        private readonly Vector3[] cellPoints = new Vector3[2];
        private readonly Vector3[] safePoints = new Vector3[6];
        private readonly Vector3[] needlePoints = new Vector3[2];

        // Raycast de terreno
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        private ContactFilter2D groundFilter;

        // Juego
        private int score;
        private float flightTime;
        private LanderState lastState = LanderState.Flying;
        private bool blinkOn = true;
        private float blinkTimer;

        // Mensaje final
        private bool bannerActive;
        private string bannerTitleFull;
        private string bannerDetailFull;
        private float bannerReveal;
        private int shownTitleChars;
        private int shownDetailChars;

        // Caché de strings para optimización
        private static readonly string[] CacheD4 = new string[10000];
        private static readonly string[] CacheD3 = new string[1000];
        private static readonly string[] CacheD2 = new string[60];
        private static bool stringsCached = false;

        // Layout
        private float lastHalfHeight;
        private float lastHalfWidth;
        private Vector3 baseHudRootPos;

        // Caché de dibujo
        private int frameCounter;
        private float cachedAltitude;
        private int shownScore = int.MinValue;
        private int shownTime = int.MinValue;
        private int shownFuel = int.MinValue;
        private int shownAltitude = int.MinValue;
        private int shownHSpeed = int.MinValue;
        private int shownVSpeed = int.MinValue;

        public int Score => score;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            InitializeStringCache();

            if (mainCamera == null) mainCamera = Camera.main;
            if (lander == null) lander = FindObjectOfType<LanderController>();

            if (mainCamera == null)
            {
                Debug.LogError("VectorHUD: No se encontró la cámara principal.", this);
                enabled = false;
                return;
            }

            propertyBlock = new MaterialPropertyBlock();

            groundFilter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = Physics2D.AllLayers
            };

            activeMaterial = ResolveMaterial();
            glowTransparent = activeMaterial != null && activeMaterial.renderQueue >= (int)RenderQueue.Transparent;

            dimColor = Color.Lerp(Color.black, primaryColor, dimLevel);
            faintColor = Color.Lerp(Color.black, primaryColor, dimLevel * 0.7f);

            hudGlyphs = new GlyphSet();
            alertGlyphs = new GlyphSet();
            bannerGlyphs = new GlyphSet();

            hudRoot = new GameObject("VectorHUD_Root").transform;
            hudRoot.SetParent(mainCamera.transform, false);
            baseHudRootPos = new Vector3(0f, 0f, hudDistance);
            hudRoot.localPosition = baseHudRootPos;

            BuildPanels();
            BuildTexts();
            BuildGauges();
            HideBanner();
        }

        private void OnEnable()
        {
            if (hudRoot != null) hudRoot.gameObject.SetActive(true);

            if (lander != null)
            {
                lander.OnLanded += HandleLanded;
                lander.OnCrashed += HandleCrashed;
            }
        }

        private void OnDisable()
        {
            if (lander != null)
            {
                lander.OnLanded -= HandleLanded;
                lander.OnCrashed -= HandleCrashed;
            }

            if (hudRoot != null) hudRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (lander == null || mainCamera == null || hudRoot == null) return;

            UpdateLayoutIfNeeded();
            UpdateBlink();
            UpdateFlightState();

            Vector2 velocity = lander.Velocity;

            // Optimización de Raycast: No calcular cada frame
            if (frameCounter++ % altitudeUpdateInterval == 0)
            {
                cachedAltitude = CalculateTerrainAltitude();
            }

            float angle = Mathf.DeltaAngle(0f, lander.transform.eulerAngles.z);

            UpdateReadouts(velocity, cachedAltitude);
            UpdateFuelGauge();
            UpdateTiltGauge(angle);
            UpdateAlert(velocity, cachedAltitude, angle);
            UpdateBanner();
            ApplyAnalogEffects();
        }

        private void ApplyAnalogEffects()
        {
            // Jitter: Vibración analógica aleatoria
            if (analogJitter > 0f)
            {
                Vector3 noise = Random.insideUnitSphere * analogJitter;
                noise.z = 0f;
                hudRoot.localPosition = baseHudRootPos + noise;
            }

            // Flicker: Variación de brillo sutil
            if (intensityFlicker > 0f)
            {
                float flicker = 1f + Random.Range(-intensityFlicker, intensityFlicker);
                // Aplicar el factor de parpadeo a los colores base del HUD
                // Esto es sutil pero añade mucha "vida" al efecto vectorial
                dimColor = Color.Lerp(Color.black, primaryColor, dimLevel * flicker);
                faintColor = Color.Lerp(Color.black, primaryColor, dimLevel * 0.7f * flicker);
            }
        }


        private static void InitializeStringCache()
        {
            if (stringsCached) return;
            for (int i = 0; i < 10000; i++)
            {
                CacheD4[i] = i.ToString("D4");
                if (i < 1000) CacheD3[i] = i.ToString("D3");
                if (i < 60) CacheD2[i] = i.ToString("D2");
            }
            stringsCached = true;
        }

        private string GetD4(int val) => CacheD4[Mathf.Clamp(val, 0, 9999)];
        private string GetD3(int val) => CacheD3[Mathf.Clamp(val, 0, 999)];
        private string GetD2(int val) => CacheD2[Mathf.Clamp(val, 0, 59)];


        private void OnDestroy()
        {
            if (hudRoot != null) Destroy(hudRoot.gameObject);
            if (runtimeMaterial != null) Destroy(runtimeMaterial);
        }

        // ------------------------------------------------------------------
        // API Pública
        // ------------------------------------------------------------------

        public void AddScore(int points)
        {
            score += points;
        }

        public void ResetTimer()
        {
            flightTime = 0f;
        }

        // ------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------

        private void BuildPanels()
        {
            leftFrame = NewStroke("PanelLeft", true, faintColor, 0.8f);
            rightFrame = NewStroke("PanelRight", true, faintColor, 0.8f);
            bannerFrame = NewStroke("BannerFrame", true, primaryColor, 1f);
        }

        private void BuildTexts()
        {
            NewLabel("ScoreName", hudGlyphs, LabelAlign.Left, dimColor, "SCORE");
            NewLabel("TimeName", hudGlyphs, LabelAlign.Left, dimColor, "TIME");
            NewLabel("FuelName", hudGlyphs, LabelAlign.Left, dimColor, "FUEL");
            NewLabel("AltitudeName", hudGlyphs, LabelAlign.Left, dimColor, "ALTITUDE");
            NewLabel("HSpeedName", hudGlyphs, LabelAlign.Left, dimColor, "H SPEED");
            NewLabel("VSpeedName", hudGlyphs, LabelAlign.Left, dimColor, "V SPEED");

            scoreValue = NewLabel("ScoreValue", hudGlyphs, LabelAlign.Right, primaryColor, null);
            timeValue = NewLabel("TimeValue", hudGlyphs, LabelAlign.Right, primaryColor, null);
            fuelValue = NewLabel("FuelValue", hudGlyphs, LabelAlign.Right, primaryColor, null);
            altitudeValue = NewLabel("AltitudeValue", hudGlyphs, LabelAlign.Right, primaryColor, null);
            hSpeedValue = NewLabel("HSpeedValue", hudGlyphs, LabelAlign.Right, primaryColor, null);
            vSpeedValue = NewLabel("VSpeedValue", hudGlyphs, LabelAlign.Right, primaryColor, null);

            alertLabel = NewLabel("Alert", alertGlyphs, LabelAlign.Center, warningColor, null);

            bannerTitle = NewLabel("BannerTitle", bannerGlyphs, LabelAlign.Left, primaryColor, null);
            bannerDetail = NewLabel("BannerDetail", hudGlyphs, LabelAlign.Left, primaryColor, null);
        }

        private void BuildGauges()
        {
            fuelFrame = NewStroke("FuelFrame", true, faintColor, 0.8f);

            fuelCells = new VectorStroke[fuelSegments];
            lowFuelCells = Mathf.Clamp(Mathf.CeilToInt(lowFuelFraction * fuelSegments), 0, fuelSegments);
            for (int i = 0; i < fuelCells.Length; i++)
            {
                Color cellColor = i < lowFuelCells ? warningColor : primaryColor;
                fuelCells[i] = NewStroke("FuelCell" + i, false, cellColor, 1f);
            }

            NewLabel("FuelCapLeft", hudGlyphs, LabelAlign.Left, dimColor, "E").Tag = GaugeCap.FuelLeft;
            NewLabel("FuelCapRight", hudGlyphs, LabelAlign.Right, dimColor, "F").Tag = GaugeCap.FuelRight;

            tiltFrame = NewStroke("TiltFrame", true, faintColor, 0.8f);
            tiltCenter = NewStroke("TiltCenter", false, faintColor, 0.8f);
            tiltSafe = NewStroke("TiltSafeZone", false, dimColor, 1f);
            tiltNeedle = NewStroke("TiltNeedle", false, primaryColor, 1.8f);

            NewLabel("TiltCapLeft", hudGlyphs, LabelAlign.Left, dimColor, "<").Tag = GaugeCap.TiltLeft;
            NewLabel("TiltCapRight", hudGlyphs, LabelAlign.Right, dimColor, ">").Tag = GaugeCap.TiltRight;
        }

        private HudLabel NewLabel(string objectName, GlyphSet glyphs, LabelAlign align, Color color, string text)
        {
            var label = new HudLabel(this, objectName, glyphs, align, color);
            if (!string.IsNullOrEmpty(text)) label.SetText(text);
            labels.Add(label);
            return label;
        }

        private VectorStroke NewStroke(string objectName, bool loop, Color color, float widthScale)
        {
            var stroke = new VectorStroke(this, hudRoot, objectName, loop) { WidthScale = widthScale };
            stroke.SetColor(color);
            staticStrokes.Add(stroke);
            return stroke;
        }

        // ------------------------------------------------------------------
        // Actualización
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
                    HideBanner();
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
            // Panel izquierdo (Uso de caché de strings para evitar GC)
            int scoreNow = Mathf.Clamp(score, 0, 9999);
            if (scoreNow != shownScore)
            {
                shownScore = scoreNow;
                scoreValue.SetText(GetD4(scoreNow));
            }

            int totalSeconds = Mathf.Min(Mathf.FloorToInt(flightTime), 5999);
            if (totalSeconds != shownTime)
            {
                shownTime = totalSeconds;
                int mins = totalSeconds / 60;
                int secs = totalSeconds % 60;
                // Pequeña concatenación inevitable para el tiempo, o caché de 0:00 a 99:59
                timeValue.SetText($"{mins}:{GetD2(secs)}");
            }

            int fuelNow = Mathf.Clamp(Mathf.CeilToInt(lander.CurrentFuel), 0, 9999);
            if (fuelNow != shownFuel)
            {
                shownFuel = fuelNow;
                fuelValue.SetText(GetD4(fuelNow));
            }

            bool fuelEmpty = lander.CurrentFuel <= 0f;
            bool fuelLow = lander.FuelNormalized <= lowFuelFraction;
            fuelValue.SetColor(fuelLow ? warningColor : primaryColor);
            fuelValue.SetVisible(!fuelEmpty || blinkOn);

            // Panel derecho
            int altitudeNow = Mathf.Clamp(Mathf.FloorToInt(altitude * altitudeDisplayScale), 0, 9999);
            if (altitudeNow != shownAltitude)
            {
                shownAltitude = altitudeNow;
                altitudeValue.SetText(GetD4(altitudeNow));
            }

            // Flechas y velocidades optimizadas
            int hNow = Mathf.RoundToInt(velocity.x * speedDisplayScale);
            if (hNow != shownHSpeed)
            {
                shownHSpeed = hNow;
                char arrow = hNow > 0 ? VectorFont.ArrowRight : hNow < 0 ? VectorFont.ArrowLeft : ' ';
                hSpeedValue.SetText($"{arrow} {GetD3(Mathf.Abs(hNow))}");
            }

            int vNow = Mathf.RoundToInt(velocity.y * speedDisplayScale);
            if (vNow != shownVSpeed)
            {
                shownVSpeed = vNow;
                char arrow = vNow > 0 ? VectorFont.ArrowUp : vNow < 0 ? VectorFont.ArrowDown : ' ';
                vSpeedValue.SetText($"{arrow} {GetD3(Mathf.Abs(vNow))}");
            }

            bool hUnsafe = Mathf.Abs(velocity.x) > lander.MaxLandingHorizontalSpeed;
            bool vUnsafe = -velocity.y > lander.MaxLandingVerticalSpeed;

            hSpeedValue.SetColor(hUnsafe ? warningColor : primaryColor);
            vSpeedValue.SetColor(vUnsafe ? warningColor : primaryColor);
        }


        private void UpdateFuelGauge()
        {
            bool empty = lander.CurrentFuel <= 0f;
            bool low = lander.FuelNormalized <= lowFuelFraction;

            int lit = empty ? 0 : Mathf.Clamp(Mathf.CeilToInt(lander.FuelNormalized * fuelSegments), 1, fuelSegments);
            if (lit != shownLitCells)
            {
                shownLitCells = lit;
                for (int i = 0; i < fuelCells.Length; i++)
                {
                    fuelCells[i].SetActive(i < lit);
                }
            }

            fuelFrame.SetColor(low ? warningColor : faintColor);
            fuelFrame.SetActive(!empty || blinkOn);
        }

        private void UpdateTiltGauge(float angle)
        {
            float target = Mathf.Clamp(-angle / tiltDisplayRange, -1f, 1f);
            needleNorm = Mathf.Lerp(needleNorm, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

            float x = tiltCenterX + needleNorm * tiltSpan;
            if (tiltDirty || Mathf.Abs(x - shownNeedleX) > 0.0005f)
            {
                tiltDirty = false;
                shownNeedleX = x;
                needlePoints[0] = new Vector3(x, tiltYBottom, 0f);
                needlePoints[1] = new Vector3(x, tiltYTop, 0f);
                tiltNeedle.SetPoints(needlePoints);
            }

            bool tiltUnsafe = Mathf.Abs(angle) > lander.MaxLandingAngle;
            tiltNeedle.SetColor(tiltUnsafe ? warningColor : primaryColor);
        }

        private void UpdateAlert(Vector2 velocity, float altitude, float angle)
        {
            string message = string.Empty;
            bool isSafeToLand = false;

            if (lander.State == LanderState.Flying)
            {
                bool nearGround = altitude < alertAltitude;

                if (lander.CurrentFuel <= 0f) message = "! NO FUEL !";
                else if (nearGround && -velocity.y > lander.MaxLandingVerticalSpeed) message = "! TOO FAST !";
                else if (nearGround && Mathf.Abs(angle) > lander.MaxLandingAngle) message = "! LEVEL SHIP !";
                else if (nearGround && Mathf.Abs(velocity.x) > lander.MaxLandingHorizontalSpeed) message = "! DRIFTING !";
                else if (lander.FuelNormalized <= lowFuelFraction) message = "! LOW FUEL !";
                else if (nearGround)
                {
                    // Asistencia Visual: Confirmación de parámetros perfectos
                    message = "SAFE TO LAND";
                    isSafeToLand = true;
                }
            }

            alertLabel.SetColor(isSafeToLand ? primaryColor : warningColor);
            alertLabel.SetText(message);
            // El mensaje de "SAFE" se queda fijo y confiable, los errores parpadean.
            alertLabel.SetVisible(message.Length > 0 && (isSafeToLand || blinkOn));
        }

        private float CalculateTerrainAltitude()
        {
            Vector2 origin = (Vector2)lander.transform.position + Vector2.down * footOffset;
            // Uso de RaycastNonAlloc para evitar generar basura cada frame
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


        // ------------------------------------------------------------------
        // Banner Final
        // ------------------------------------------------------------------

        private void HandleLanded(LandingResult result)
        {
            ShowBanner("CONGRATULATIONS", "THE EAGLE HAS LANDED", primaryColor);
        }

        private void HandleCrashed(LandingResult result)
        {
            ShowBanner("CRASH", CrashReason(result), warningColor);
        }

        private string CrashReason(LandingResult result)
        {
            if (result.ImpactVerticalSpeed > lander.MaxLandingVerticalSpeed) return "VERTICAL SPEED TOO HIGH";
            if (result.ImpactHorizontalSpeed > lander.MaxLandingHorizontalSpeed) return "HORIZONTAL SPEED TOO HIGH";
            if (result.ImpactAngle > lander.MaxLandingAngle) return "BAD LANDING ANGLE";
            if (!result.OnLandingPad) return "NOT A LANDING PAD";
            return "CRASH LANDING";
        }

        private void ShowBanner(string title, string detail, Color color)
        {
            if (bannerTitle == null) return;

            bannerTitleFull = title;
            bannerDetailFull = detail;
            bannerReveal = 0f;
            shownTitleChars = 0;
            shownDetailChars = 0;
            bannerActive = true;

            bannerTitle.SetColor(color);
            bannerDetail.SetColor(color);
            bannerFrame.SetColor(Color.Lerp(Color.black, color, 0.6f));

            bannerTitle.SetText(string.Empty);
            bannerDetail.SetText(string.Empty);
            bannerTitle.SetVisible(true);
            bannerDetail.SetVisible(true);
            bannerFrame.SetActive(true);

            PositionBanner();
        }

        private void HideBanner()
        {
            if (bannerTitle == null) return;

            bannerActive = false;
            bannerTitleFull = null;
            bannerDetailFull = null;

            bannerTitle.SetVisible(false);
            bannerDetail.SetVisible(false);
            bannerFrame.SetActive(false);
        }

        private void UpdateBanner()
        {
            if (!bannerActive) return;

            bannerReveal += Time.unscaledDeltaTime * revealCharsPerSecond;

            int total = bannerTitleFull.Length + bannerDetailFull.Length;
            int revealed = Mathf.Min(Mathf.FloorToInt(bannerReveal), total);
            int titleChars = Mathf.Min(revealed, bannerTitleFull.Length);
            int detailChars = Mathf.Max(0, revealed - bannerTitleFull.Length);

            if (titleChars != shownTitleChars)
            {
                shownTitleChars = titleChars;
                bannerTitle.SetText(bannerTitleFull.Substring(0, titleChars));
            }

            if (detailChars != shownDetailChars)
            {
                shownDetailChars = detailChars;
                bannerDetail.SetText(bannerDetailFull.Substring(0, detailChars));
            }
        }

        private void PositionBanner()
        {
            if (!bannerActive || bannerTitleFull == null) return;

            float ch = hudGlyphs.Height;
            float titleWidth = bannerGlyphs.TextWidth(bannerTitleFull.Length);
            float detailWidth = hudGlyphs.TextWidth(bannerDetailFull.Length);
            float titleHeight = bannerGlyphs.Height;
            float gap = ch * 0.9f;
            float pad = ch * 1.1f;

            float blockHeight = titleHeight + gap + ch;
            float detailY = -blockHeight * 0.5f;
            float titleY = detailY + ch + gap;
            float halfFrameWidth = Mathf.Max(titleWidth, detailWidth) * 0.5f + pad;

            bannerTitle.SetAnchor(new Vector2(-titleWidth * 0.5f, titleY));
            bannerDetail.SetAnchor(new Vector2(-detailWidth * 0.5f, detailY));

            SetChamferedFrame(bannerFrame, -halfFrameWidth, detailY - pad, halfFrameWidth, titleY + titleHeight + pad, pad * 0.8f, false);
        }

        // ------------------------------------------------------------------
        // Maquetación
        // ------------------------------------------------------------------

        private void UpdateLayoutIfNeeded()
        {
            float halfHeight = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : hudDistance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * mainCamera.aspect;

            if (Mathf.Approximately(halfHeight, lastHalfHeight) && Mathf.Approximately(halfWidth, lastHalfWidth))
            {
                return;
            }

            lastHalfHeight = halfHeight;
            lastHalfWidth = halfWidth;
            ApplyLayout(halfHeight, halfWidth);
        }

        private void ApplyLayout(float halfHeight, float halfWidth)
        {
            float screenHeight = halfHeight * 2f;
            float ch = screenHeight * charHeightFraction;
            float margin = screenHeight * marginFraction;
            float pad = ch * 0.75f;
            float rowPitch = ch * 1.85f;

            hudGlyphs.Configure(ch, charSpacingRatio);
            alertGlyphs.Configure(ch * AlertScale, charSpacingRatio);
            bannerGlyphs.Configure(ch * BannerScale, charSpacingRatio);

            currentLineWidth = ch * lineWidthRatio;
            foreach (HudLabel label in labels) label.SetLineWidth(currentLineWidth);
            alertLabel.SetLineWidth(currentLineWidth * 1.3f);
            bannerTitle.SetLineWidth(currentLineWidth * 1.6f);
            foreach (VectorStroke stroke in staticStrokes) stroke.SetWidth(currentLineWidth);

            foreach (HudLabel label in labels) label.Refresh();

            float innerWidth = hudGlyphs.TextWidth(PanelChars);
            float panelWidth = innerWidth + 2f * pad;
            float panelHeight = pad + ch + 3f * rowPitch + pad;
            float top = halfHeight - margin;
            float bottom = top - panelHeight;
            float chamfer = pad * 0.9f;

            float leftX0 = -halfWidth + margin;
            float leftX1 = leftX0 + panelWidth;
            float rightX1 = halfWidth - margin;
            float rightX0 = rightX1 - panelWidth;

            SetChamferedFrame(leftFrame, leftX0, bottom, leftX1, top, chamfer, false);
            SetChamferedFrame(rightFrame, rightX0, bottom, rightX1, top, chamfer, true);

            float innerLeftL = leftX0 + pad;
            float innerRightL = leftX1 - pad;
            float innerLeftR = rightX0 + pad;
            float innerRightR = rightX1 - pad;

            float row0 = top - pad - ch;
            float row1 = row0 - rowPitch;
            float row2 = row0 - 2f * rowPitch;
            float row3 = row0 - 3f * rowPitch;

            PlaceRow("ScoreName", "ScoreValue", innerLeftL, innerRightL, row0);
            PlaceRow("TimeName", "TimeValue", innerLeftL, innerRightL, row1);
            PlaceRow("FuelName", "FuelValue", innerLeftL, innerRightL, row2);
            PlaceRow("AltitudeName", "AltitudeValue", innerLeftR, innerRightR, row0);
            PlaceRow("HSpeedName", "HSpeedValue", innerLeftR, innerRightR, row1);
            PlaceRow("VSpeedName", "VSpeedValue", innerLeftR, innerRightR, row2);

            float capWidth = hudGlyphs.Advance * 1.4f;
            float barWidth = innerWidth - 2f * capWidth;
            float barHeight = ch * 0.8f;
            float barY = row3 + ch * 0.1f;
            float xPad = barHeight * 0.4f;
            float yPad = barHeight * 0.25f;

            float fuelBarX = innerLeftL + capWidth;
            PlaceCap(GaugeCap.FuelLeft, innerLeftL, row3);
            PlaceCap(GaugeCap.FuelRight, innerRightL, row3);

            fuelFrame.Root.localPosition = new Vector3(fuelBarX, barY, 0f);
            SetRect(fuelFrame, barWidth, barHeight);

            float pitch = fuelSegments > 1 ? (barWidth - 2f * xPad) / (fuelSegments - 1) : 0f;
            cellPoints[0] = new Vector3(0f, 0f, 0f);
            cellPoints[1] = new Vector3(0f, barHeight - 2f * yPad, 0f);
            for (int i = 0; i < fuelCells.Length; i++)
            {
                fuelCells[i].Root.localPosition = new Vector3(fuelBarX + xPad + i * pitch, barY + yPad, 0f);
                fuelCells[i].SetPoints(cellPoints);
            }

            shownLitCells = -1;

            float tiltBarX = innerLeftR + capWidth;
            PlaceCap(GaugeCap.TiltLeft, innerLeftR, row3);
            PlaceCap(GaugeCap.TiltRight, innerRightR, row3);

            var tiltOrigin = new Vector3(tiltBarX, barY, 0f);
            tiltFrame.Root.localPosition = tiltOrigin;
            tiltCenter.Root.localPosition = tiltOrigin;
            tiltSafe.Root.localPosition = tiltOrigin;
            tiltNeedle.Root.localPosition = tiltOrigin;

            SetRect(tiltFrame, barWidth, barHeight);

            tiltCenterX = barWidth * 0.5f;
            tiltSpan = tiltCenterX - xPad;
            tiltYBottom = yPad;
            tiltYTop = barHeight - yPad;

            needlePoints[0] = new Vector3(tiltCenterX, 0f, 0f);
            needlePoints[1] = new Vector3(tiltCenterX, barHeight, 0f);
            tiltCenter.SetPoints(needlePoints);

            float safe = Mathf.Min(lander.MaxLandingAngle / tiltDisplayRange, 1f) * tiltSpan;
            float mid = barHeight * 0.5f;
            float tick = barHeight * 0.28f;
            safePoints[0] = new Vector3(tiltCenterX - safe, mid - tick, 0f);
            safePoints[1] = new Vector3(tiltCenterX - safe, mid + tick, 0f);
            safePoints[2] = new Vector3(tiltCenterX - safe, mid, 0f);
            safePoints[3] = new Vector3(tiltCenterX + safe, mid, 0f);
            safePoints[4] = new Vector3(tiltCenterX + safe, mid + tick, 0f);
            safePoints[5] = new Vector3(tiltCenterX + safe, mid - tick, 0f);
            tiltSafe.SetPoints(safePoints);

            tiltDirty = true;

            alertLabel.SetAnchor(new Vector2(0f, top - alertGlyphs.Height));
            PositionBanner();
        }

        private void PlaceRow(string nameObject, string valueObject, float innerLeft, float innerRight, float baseline)
        {
            FindLabel(nameObject).SetAnchor(new Vector2(innerLeft, baseline));
            FindLabel(valueObject).SetAnchor(new Vector2(innerRight, baseline));
        }

        private void PlaceCap(GaugeCap cap, float x, float baseline)
        {
            foreach (HudLabel label in labels)
            {
                if (label.Tag == cap)
                {
                    label.SetAnchor(new Vector2(x, baseline));
                    return;
                }
            }
        }

        private HudLabel FindLabel(string objectName)
        {
            foreach (HudLabel label in labels)
            {
                if (label.ObjectName == objectName) return label;
            }
            return null;
        }

        private void SetRect(VectorStroke stroke, float width, float height)
        {
            rectPoints[0] = new Vector3(0f, 0f, 0f);
            rectPoints[1] = new Vector3(width, 0f, 0f);
            rectPoints[2] = new Vector3(width, height, 0f);
            rectPoints[3] = new Vector3(0f, height, 0f);
            stroke.SetPoints(rectPoints);
        }

        private void SetChamferedFrame(VectorStroke frame, float x0, float y0, float x1, float y1, float c, bool mirror)
        {
            if (!mirror)
            {
                framePoints[0] = new Vector3(x0 + c, y1, 0f);
                framePoints[1] = new Vector3(x1, y1, 0f);
                framePoints[2] = new Vector3(x1, y0 + c, 0f);
                framePoints[3] = new Vector3(x1 - c, y0, 0f);
                framePoints[4] = new Vector3(x0, y0, 0f);
                framePoints[5] = new Vector3(x0, y1 - c, 0f);
            }
            else
            {
                framePoints[0] = new Vector3(x0, y1, 0f);
                framePoints[1] = new Vector3(x1 - c, y1, 0f);
                framePoints[2] = new Vector3(x1, y1 - c, 0f);
                framePoints[3] = new Vector3(x1, y0, 0f);
                framePoints[4] = new Vector3(x0 + c, y0, 0f);
                framePoints[5] = new Vector3(x0, y0 + c, 0f);
            }

            frame.SetPoints(framePoints);
        }

        // ------------------------------------------------------------------
        // Auxiliares de Dibujo
        // ------------------------------------------------------------------

        private LineRenderer CreateLine(Transform parent, string objectName, int order, bool loop, float zOffset)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, zOffset);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.loop = loop;
            lr.positionCount = 0;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sortingOrder = order;
            lr.sharedMaterial = activeMaterial;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return lr;
        }

        private void ApplyColor(LineRenderer lr, Color color)
        {
            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);
        }

        private Color GlowColor(Color color)
        {
            if (glowTransparent)
            {
                return new Color(color.r, color.g, color.b, glowIntensity);
            }

            return new Color(color.r * glowIntensity, color.g * glowIntensity, color.b * glowIntensity, 1f);
        }

        private Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
            {
                Debug.LogError("VectorHUD: No se encontró un Shader Unlit adecuado.", this);
                return null;
            }

            runtimeMaterial = new Material(shader) { name = "VectorHUD (runtime)" };
            return runtimeMaterial;
        }

        // ------------------------------------------------------------------
        // Clases Internas
        // ------------------------------------------------------------------

        private enum GaugeCap { None, FuelLeft, FuelRight, TiltLeft, TiltRight }

        private sealed class GlyphSet
        {
            private readonly Dictionary<char, Vector3[][]> cache = new Dictionary<char, Vector3[][]>();
            private float spacingRatio;

            public float Height { get; private set; }
            public float Width => Height * VectorFont.Aspect;
            public float Spacing => Height * spacingRatio;
            public float Advance => Width + Spacing;

            public void Configure(float height, float newSpacingRatio)
            {
                if (Mathf.Approximately(height, Height) && Mathf.Approximately(newSpacingRatio, spacingRatio)) return;

                Height = height;
                spacingRatio = newSpacingRatio;
                cache.Clear();
            }

            public float TextWidth(int charCount)
            {
                return charCount <= 0 ? 0f : charCount * Advance - Spacing;
            }

            public Vector3[][] Get(char c)
            {
                c = char.ToUpperInvariant(c);
                if (cache.TryGetValue(c, out Vector3[][] scaled)) return scaled;

                Vector2[][] source = VectorFont.GetStrokes(c);
                scaled = new Vector3[source.Length][];

                for (int s = 0; s < source.Length; s++)
                {
                    Vector2[] points = source[s];
                    var strokePoints = new Vector3[points.Length];

                    for (int p = 0; p < points.Length; p++)
                    {
                        strokePoints[p] = new Vector3(points[p].x * Width, points[p].y * Height, 0f);
                    }

                    scaled[s] = strokePoints;
                }

                cache[c] = scaled;
                return scaled;
            }
        }

        private sealed class VectorStroke
        {
            private readonly VectorHUD owner;
            private readonly LineRenderer core;
            private readonly LineRenderer halo;
            private Color color;
            private bool hasColor;

            public readonly Transform Root;
            public float WidthScale = 1f;

            public VectorStroke(VectorHUD owner, Transform parent, string objectName, bool loop)
            {
                this.owner = owner;

                Root = new GameObject(objectName).transform;
                Root.SetParent(parent, false);

                core = owner.CreateLine(Root, "Core", owner.sortingOrder, loop, 0f);

                if (owner.glow)
                {
                    halo = owner.CreateLine(Root, "Glow", owner.sortingOrder - 1, loop, 0.002f);
                }
            }

            public void SetPoints(Vector3[] points)
            {
                core.positionCount = points.Length;
                core.SetPositions(points);

                if (halo != null)
                {
                    halo.positionCount = points.Length;
                    halo.SetPositions(points);
                }
            }

            public void SetWidth(float baseWidth)
            {
                float width = baseWidth * WidthScale;
                core.startWidth = width;
                core.endWidth = width;

                if (halo != null)
                {
                    float haloWidth = width * owner.glowWidthMultiplier;
                    halo.startWidth = haloWidth;
                    halo.endWidth = haloWidth;
                }
            }

            public void SetColor(Color newColor)
            {
                if (hasColor && newColor == color) return;

                hasColor = true;
                color = newColor;
                owner.ApplyColor(core, newColor);

                if (halo != null)
                {
                    owner.ApplyColor(halo, owner.GlowColor(newColor));
                }
            }

            public void SetActive(bool active)
            {
                if (Root.gameObject.activeSelf != active)
                {
                    Root.gameObject.SetActive(active);
                }
            }
        }

        private sealed class HudLabel
        {
            private readonly VectorHUD owner;
            private readonly GlyphSet glyphs;
            private readonly LabelAlign align;
            private readonly Transform root;
            private readonly List<VectorStroke> strokes = new List<VectorStroke>();

            private string text = string.Empty;
            private int usedStrokes;
            private float textWidth;
            private Vector2 anchor;
            private Color color;
            private float lineWidth;

            public readonly string ObjectName;
            public GaugeCap Tag = GaugeCap.None;

            public HudLabel(VectorHUD owner, string objectName, GlyphSet glyphs, LabelAlign align, Color color)
            {
                this.owner = owner;
                this.glyphs = glyphs;
                this.align = align;
                this.color = color;
                ObjectName = objectName;
                lineWidth = owner.currentLineWidth;

                root = new GameObject("HUD_" + objectName).transform;
                root.SetParent(owner.hudRoot, false);
            }

            public void SetText(string newText)
            {
                if (newText == text) return;

                text = newText;
                Rebuild();
            }

            public void Refresh()
            {
                Rebuild();
            }

            public void SetAnchor(Vector2 newAnchor)
            {
                anchor = newAnchor;
                Reposition();
            }

            public void SetColor(Color newColor)
            {
                if (newColor == color) return;

                color = newColor;
                for (int i = 0; i < strokes.Count; i++)
                {
                    strokes[i].SetColor(color);
                }
            }

            public void SetLineWidth(float width)
            {
                lineWidth = width;
                for (int i = 0; i < strokes.Count; i++)
                {
                    strokes[i].SetWidth(width);
                }
            }

            public void SetVisible(bool visible)
            {
                if (root.gameObject.activeSelf != visible)
                {
                    root.gameObject.SetActive(visible);
                }
            }

            private void Rebuild()
            {
                float advance = glyphs.Advance;
                int strokeIndex = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    Vector3[][] glyphStrokes = glyphs.Get(text[i]);
                    float xOffset = i * advance;

                    for (int s = 0; s < glyphStrokes.Length; s++)
                    {
                        VectorStroke stroke = GetStroke(strokeIndex++);
                        stroke.Root.localPosition = new Vector3(xOffset, 0f, 0f);
                        stroke.SetPoints(glyphStrokes[s]);
                    }
                }

                for (int k = strokeIndex; k < usedStrokes; k++)
                {
                    strokes[k].SetActive(false);
                }

                usedStrokes = strokeIndex;
                textWidth = glyphs.TextWidth(text.Length);
                Reposition();
            }

            private VectorStroke GetStroke(int index)
            {
                if (index < strokes.Count)
                {
                    VectorStroke existing = strokes[index];
                    existing.SetActive(true);
                    return existing;
                }

                var stroke = new VectorStroke(owner, root, "Stroke", false);
                stroke.SetColor(color);
                stroke.SetWidth(lineWidth);
                strokes.Add(stroke);
                return stroke;
            }

            private void Reposition()
            {
                float alignFactor = align == LabelAlign.Left ? 0f : align == LabelAlign.Center ? 0.5f : 1f;
                root.localPosition = new Vector3(anchor.x - textWidth * alignFactor, anchor.y, 0f);
            }
        }
    }
}