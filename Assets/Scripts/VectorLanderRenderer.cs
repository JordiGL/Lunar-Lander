using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Dibuja la nave con estética de vectores (monitor XY estilo Atari, 1979) usando LineRenderer.
    ///
    /// - La silueta (cuerpo trapezoidal, cabina, tobera y patas) se define con un único array de
    ///   puntos 2D locales y se pinta con el LineRenderer de este mismo GameObject.
    /// - La llama del propulsor es un segundo LineRenderer (hijo "Flame") que solo se muestra
    ///   mientras el empuje principal está activo; parpadea con una longitud aleatoria.
    /// - Si hay un LanderController (en este objeto o en un padre), se suscribe a su evento
    ///   OnThrustChanged. También se puede controlar a mano con SetFlameActive(bool).
    ///
    /// Sobre el material: lo más fiable en builds es asignar en el Inspector un material con el
    /// shader "Universal Render Pipeline/Unlit". Si se deja vacío, se crea uno en tiempo de
    /// ejecución con Shader.Find (URP/Unlit y, como último recurso, Sprites/Default).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class VectorLanderRenderer : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Geometría (unidades locales; la nave mide ~1.2 de alto por ~1.7 de ancho con patas)
        // ------------------------------------------------------------------

        /// <summary>Coordenada Y de la boca de la tobera; de aquí sale la llama.</summary>
        private const float EngineBottomY = -0.38f;

        /// <summary>Semianchura de la base de la llama (algo menor que la boca de la tobera).</summary>
        private const float FlameHalfWidth = 0.12f;

        private const int FlamePointCount = 5;

        /// <summary>
        /// Silueta de la nave como UNA sola polilínea cerrada (loop = true: el último punto
        /// se une con el primero).
        ///
        /// Un LineRenderer solo dibuja un trazo continuo, así que las patas se "recorren" como
        /// un desvío desde la pared del cuerpo: se sale por el puntal, se dibuja la planta del pie
        /// y se vuelve por el tirante al mismo punto de anclaje. Los tramos repetidos se
        /// solapan exactamente y no se distinguen con líneas finas.
        ///
        /// Orden: cabina -> pared derecha + pata derecha -> base con tobera ->
        ///        pared izquierda + pata izquierda -> hombro izquierdo -> (cierra en el punto 0).
        /// </summary>
        private static readonly Vector3[] ShipOutline =
        {
            // Cabina (parte superior, trapecio estrecho)
            new Vector3(-0.20f,  0.60f, 0f), //  0 cabina, arriba-izquierda
            new Vector3( 0.20f,  0.60f, 0f), //  1 cabina, arriba-derecha
            new Vector3( 0.30f,  0.30f, 0f), //  2 cabina, base-derecha
            new Vector3( 0.45f,  0.30f, 0f), //  3 hombro derecho (el cuerpo es más ancho arriba)

            // Pared derecha del cuerpo + pata derecha (desvío)
            new Vector3( 0.39f,  0.10f, 0f), //  4 anclaje de la pata derecha
            new Vector3( 0.70f, -0.55f, 0f), //  5 pie derecho (puntal)
            new Vector3( 0.85f, -0.55f, 0f), //  6 planta derecha, extremo exterior
            new Vector3( 0.55f, -0.55f, 0f), //  7 planta derecha, extremo interior
            new Vector3( 0.39f,  0.10f, 0f), //  8 vuelta al anclaje (tirante)

            // Base del cuerpo con la tobera del motor
            new Vector3( 0.30f, -0.20f, 0f), //  9 esquina inferior derecha
            new Vector3( 0.12f, -0.20f, 0f), // 10 tobera, arranque derecho
            new Vector3( 0.18f, EngineBottomY, 0f), // 11 tobera, boca derecha
            new Vector3(-0.18f, EngineBottomY, 0f), // 12 tobera, boca izquierda
            new Vector3(-0.12f, -0.20f, 0f), // 13 tobera, arranque izquierdo
            new Vector3(-0.30f, -0.20f, 0f), // 14 esquina inferior izquierda

            // Pared izquierda del cuerpo + pata izquierda (desvío)
            new Vector3(-0.39f,  0.10f, 0f), // 15 anclaje de la pata izquierda
            new Vector3(-0.55f, -0.55f, 0f), // 16 planta izquierda, extremo interior (tirante)
            new Vector3(-0.85f, -0.55f, 0f), // 17 planta izquierda, extremo exterior
            new Vector3(-0.70f, -0.55f, 0f), // 18 pie izquierdo (puntal)
            new Vector3(-0.39f,  0.10f, 0f), // 19 vuelta al anclaje

            // Hombro izquierdo; el loop cierra hacia el punto 0
            new Vector3(-0.45f,  0.30f, 0f), // 20 hombro izquierdo
            new Vector3(-0.30f,  0.30f, 0f), // 21 cabina, base-izquierda
        };

        // Ids de propiedades para teñir el material sin instanciarlo (URP usa _BaseColor,
        // los shaders legacy como Sprites/Default usan _Color).
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        // ------------------------------------------------------------------
        // Configuración (Inspector)
        // ------------------------------------------------------------------

        [Header("Referencias")]
        [Tooltip("Opcional. Si está vacío se busca un LanderController en este objeto o en sus padres.")]
        [SerializeField] private LanderController lander;

        [Header("Aspecto de la línea")]
        [Tooltip("Grosor de la línea de la nave y de la llama.")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;

        [SerializeField] private Color lineColor = Color.white;

        [Tooltip("Material Unlit (URP/Unlit recomendado). Vacío = se crea uno en tiempo de ejecución.")]
        [SerializeField] private Material lineMaterial;

        [Tooltip("Orden de dibujado; súbelo para que la nave quede por encima del terreno.")]
        [SerializeField] private int sortingOrder = 10;

        [Tooltip("Vértices extra en las esquinas para evitar huecos entre segmentos.")]
        [SerializeField, Range(0, 8)] private int cornerVertices = 2;

        [Tooltip("Vértices extra en los extremos de la línea.")]
        [SerializeField, Range(0, 8)] private int capVertices = 2;

        [Header("Llama del propulsor")]
        [SerializeField] private Color flameColor = Color.white;
        [SerializeField, Min(0.05f)] private float flameMinLength = 0.30f;
        [SerializeField, Min(0.05f)] private float flameMaxLength = 0.60f;

        [Tooltip("Segundos entre cambios de forma de la llama (parpadeo de estilo retro).")]
        [SerializeField, Min(0.01f)] private float flameFlickerInterval = 0.05f;

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private LineRenderer lineRenderer;
        private LineRenderer flameRenderer;
        private Material activeMaterial;
        private Material runtimeMaterial; // solo si lo hemos creado nosotros (para destruirlo)
        private MaterialPropertyBlock propertyBlock;
        private Vector3[] flamePoints;
        private bool flameActive;
        private float flickerTimer;

        /// <summary>true mientras la llama se está dibujando.</summary>
        public bool IsFlameActive => flameActive;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            flamePoints = new Vector3[FlamePointCount];
            activeMaterial = ResolveMaterial();

            if (lander == null)
            {
                lander = GetComponentInParent<LanderController>();
            }

            // Silueta: el último punto se une con el primero (loop cerrado).
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor);

            BuildFlame();
        }

        private void OnEnable()
        {
            if (lander != null)
            {
                lander.OnThrustChanged += SetFlameActive;
                SetFlameActive(lander.IsThrusting);
            }
            else
            {
                SetFlameActive(false);
            }
        }

        private void OnDisable()
        {
            if (lander != null)
            {
                lander.OnThrustChanged -= SetFlameActive;
            }

            SetFlameActive(false);
        }

        private void Update()
        {
            if (!flameActive) return;

            // Cambia la forma de la llama a intervalos fijos para el típico parpadeo vectorial.
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        private void OnValidate()
        {
            flameMaxLength = Mathf.Max(flameMinLength, flameMaxLength);

            // Permite ajustar grosor/color desde el Inspector con el juego en marcha.
            if (Application.isPlaying && lineRenderer != null && flameRenderer != null)
            {
                RefreshStyle();
            }
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>
        /// Muestra u oculta la llama del propulsor. Está conectado a LanderController.OnThrustChanged,
        /// así que la llama solo aparece cuando el empuje principal está realmente activo
        /// (tecla pulsada y con combustible).
        /// </summary>
        public void SetFlameActive(bool active)
        {
            flameActive = active;

            if (flameRenderer == null) return;

            flameRenderer.enabled = active;

            if (active)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        private void BuildFlame()
        {
            var flameObject = new GameObject("Flame");
            flameObject.transform.SetParent(transform, false);

            flameRenderer = flameObject.AddComponent<LineRenderer>();

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor);

            flameRenderer.enabled = false; // oculta hasta que se active el empuje
        }

        /// <summary>Calcula los puntos de la llama (forma de gota) para la longitud dada.</summary>
        private void FillFlamePoints(float length)
        {
            float midY = EngineBottomY - length * 0.55f;
            float tipY = EngineBottomY - length;
            float sway = Random.Range(-0.04f, 0.04f); // ligera oscilación lateral de la punta

            flamePoints[0] = new Vector3(-FlameHalfWidth, EngineBottomY, 0f);
            flamePoints[1] = new Vector3(-FlameHalfWidth * 0.4f, midY, 0f);
            flamePoints[2] = new Vector3(sway, tipY, 0f);
            flamePoints[3] = new Vector3(FlameHalfWidth * 0.4f, midY, 0f);
            flamePoints[4] = new Vector3(FlameHalfWidth, EngineBottomY, 0f);
        }

        private void DrawFlame(float length)
        {
            FillFlamePoints(length);
            flameRenderer.SetPositions(flamePoints);
        }

        /// <summary>Configura un LineRenderer completo por código (se usa para nave y llama).</summary>
        private void ConfigureLine(LineRenderer lr, Vector3[] points, bool loop, Color color)
        {
            lr.useWorldSpace = false;                    // la línea se mueve y rota con la nave
            lr.alignment = LineAlignment.TransformZ;     // el plano de la línea mira a lo largo del eje Z local
            lr.textureMode = LineTextureMode.Stretch;
            lr.loop = loop;
            lr.numCornerVertices = cornerVertices;
            lr.numCapVertices = capVertices;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.sortingOrder = sortingOrder;
            lr.sharedMaterial = activeMaterial;

            // Es una línea Unlit: sin sombras ni sondas de luz/reflejo ni vectores de movimiento.
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.lightProbeUsage = LightProbeUsage.Off;
            lr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            lr.positionCount = points.Length;
            lr.SetPositions(points);

            ApplyColor(lr, color);
        }

        /// <summary>
        /// Tiñe la línea mediante un MaterialPropertyBlock (no modifica el material compartido).
        /// El color de vértice se deja en blanco para no multiplicar el tinte dos veces en
        /// shaders que lo usan (Sprites/Default); URP/Unlit ignora el color de vértice.
        /// </summary>
        private void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = Color.white;
            lr.endColor = Color.white;

            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);
        }

        private void RefreshStyle()
        {
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor);

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor);
        }

        /// <summary>Devuelve el material asignado o, si falta, crea uno Unlit básico.</summary>
        private Material ResolveMaterial()
        {
            if (lineMaterial != null) return lineMaterial;

            // Shader.Find solo es fiable en el Editor o si el shader entra en el build
            // (por eso se recomienda asignar el material en el Inspector).
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("VectorLanderRenderer: no se encontró ningún shader Unlit. " +
                               "Asigna un material en el campo 'Line Material'.", this);
                return null;
            }

            runtimeMaterial = new Material(shader) { name = "VectorLine (runtime)" };
            return runtimeMaterial;
        }
    }
}