using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Dibuja la nave con estética de vectores (monitor XY estilo Atari, 1979) usando LineRenderer.
    ///
    /// Diseño inspirado en el módulo lunar Apollo:
    /// - Casco: UNA polilínea cerrada (etapa de ascenso facetada + etapa de descenso + tobera),
    ///   dibujada con el LineRenderer de este GameObject.
    /// - Detalles: trazos independientes (hijos "Detail_N") más finos: ventana, líneas de panel,
    ///   antena con plato, propulsores RCS, y patas con puntal, tirante y plato de apoyo.
    ///   Las patas se definen solo en el lado derecho y se espejan automáticamente.
    /// - Llama: dos LineRenderers (exterior e interior/núcleo) que parpadean con longitud aleatoria
    ///   solo mientras el empuje principal está activo.
    /// - Si hay un LanderController (en este objeto o en un padre), se suscribe a OnThrustChanged.
    ///   También se puede controlar a mano con SetFlameActive(bool).
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
        // Geometría (unidades locales; ~1.25 de alto por ~1.8 de ancho con patas)
        // ------------------------------------------------------------------

        /// <summary>Coordenada Y de la boca de la tobera; de aquí sale la llama.</summary>
        private const float EngineBottomY = -0.38f;

        private const float OuterFlameHalfWidth = 0.14f;
        private const float InnerFlameHalfWidth = 0.065f;
        private const float InnerFlameLengthRatio = 0.5f;
        private const int FlamePointCount = 5;

        /// <summary>
        /// Silueta principal (loop cerrado): etapa de ascenso octogonal arriba, etapa de descenso
        /// más ancha con esquinas achaflanadas abajo, y la campana del motor.
        /// Orden: cabina arriba-izq -> sentido horario -> cierra en el punto 0.
        /// </summary>
        private static readonly Vector3[] ShipOutline =
        {
            new Vector3(-0.20f,  0.66f, 0f), //  0 techo cabina, izquierda
            new Vector3( 0.20f,  0.66f, 0f), //  1 techo cabina, derecha
            new Vector3( 0.34f,  0.50f, 0f), //  2 chaflán cabina
            new Vector3( 0.34f,  0.30f, 0f), //  3 base cabina
            new Vector3( 0.46f,  0.30f, 0f), //  4 hombro etapa de descenso
            new Vector3( 0.46f, -0.05f, 0f), //  5 pared derecha
            new Vector3( 0.36f, -0.20f, 0f), //  6 chaflán inferior derecho
            new Vector3( 0.14f, -0.20f, 0f), //  7 arranque de la tobera
            new Vector3( 0.19f, EngineBottomY, 0f), //  8 boca derecha de la tobera
            new Vector3(-0.19f, EngineBottomY, 0f), //  9 boca izquierda de la tobera
            new Vector3(-0.14f, -0.20f, 0f), // 10 arranque izquierdo
            new Vector3(-0.36f, -0.20f, 0f), // 11 chaflán inferior izquierdo
            new Vector3(-0.46f, -0.05f, 0f), // 12 pared izquierda
            new Vector3(-0.46f,  0.30f, 0f), // 13 hombro izquierdo
            new Vector3(-0.34f,  0.30f, 0f), // 14 base cabina izquierda
            new Vector3(-0.34f,  0.50f, 0f), // 15 chaflán cabina izquierdo
        };

        /// <summary>Trazo de detalle: una polilínea (abierta o cerrada).</summary>
        private readonly struct Stroke
        {
            public readonly Vector3[] Points;
            public readonly bool Loop;

            public Stroke(bool loop, params Vector3[] points)
            {
                Points = points;
                Loop = loop;
            }
        }

        /// <summary>Trazos centrados o simétricos por sí mismos (se dibujan tal cual).</summary>
        private static readonly Stroke[] CenterStrokes =
        {
            // Ventana trapezoidal de la cabina
            new Stroke(true,
                new Vector3(-0.13f, 0.56f, 0f),
                new Vector3( 0.13f, 0.56f, 0f),
                new Vector3( 0.09f, 0.41f, 0f),
                new Vector3(-0.09f, 0.41f, 0f)),

            // Junta entre etapa de ascenso y descenso
            new Stroke(false,
                new Vector3(-0.34f, 0.30f, 0f),
                new Vector3( 0.34f, 0.30f, 0f)),

            // Línea de panel de la etapa de descenso
            new Stroke(false,
                new Vector3(-0.46f, 0.10f, 0f),
                new Vector3( 0.46f, 0.10f, 0f)),

            // Escotilla / panel central
            new Stroke(true,
                new Vector3(-0.12f, 0.24f, 0f),
                new Vector3( 0.12f, 0.24f, 0f),
                new Vector3( 0.12f, 0.14f, 0f),
                new Vector3(-0.12f, 0.14f, 0f)),

            // Aro de la tobera (refuerzo de la campana)
            new Stroke(false,
                new Vector3(-0.155f, -0.29f, 0f),
                new Vector3( 0.155f, -0.29f, 0f)),

            // Antena con plato
            new Stroke(false,
                new Vector3(0.08f, 0.66f, 0f),
                new Vector3(0.14f, 0.86f, 0f)),
            new Stroke(false,
                new Vector3(0.07f, 0.82f, 0f),
                new Vector3(0.14f, 0.78f, 0f),
                new Vector3(0.21f, 0.82f, 0f)),
        };

        /// <summary>Trazos definidos solo para el lado DERECHO; se espejan al izquierdo.</summary>
        private static readonly Stroke[] MirroredStrokes =
        {
            // Puntal principal de la pata
            new Stroke(false,
                new Vector3(0.46f,  0.18f, 0f),
                new Vector3(0.76f, -0.56f, 0f)),

            // Tirante inferior (del cuerpo al puntal)
            new Stroke(false,
                new Vector3(0.38f, -0.19f, 0f),
                new Vector3(0.65f, -0.30f, 0f)),

            // Plato de apoyo
            new Stroke(false,
                new Vector3(0.62f, -0.56f, 0f),
                new Vector3(0.92f, -0.56f, 0f)),

            // Propulsor RCS (cajita junto a la cabina)
            new Stroke(true,
                new Vector3(0.34f, 0.46f, 0f),
                new Vector3(0.42f, 0.46f, 0f),
                new Vector3(0.42f, 0.38f, 0f),
                new Vector3(0.34f, 0.38f, 0f)),

            // Remache / panel lateral de la etapa de descenso
            new Stroke(false,
                new Vector3(0.28f, 0.30f, 0f),
                new Vector3(0.28f, 0.10f, 0f)),
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
        [Tooltip("Grosor del contorno principal de la nave.")]
        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;

        [Tooltip("Grosor de los detalles (ventana, patas, paneles) relativo al contorno.")]
        [SerializeField, Range(0.2f, 1f)] private float detailWidthScale = 0.6f;

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
        private LineRenderer flameCoreRenderer;
        private readonly List<LineRenderer> detailRenderers = new List<LineRenderer>();
        private readonly List<Stroke> detailStrokes = new List<Stroke>();

        private Material activeMaterial;
        private Material runtimeMaterial; // solo si lo hemos creado nosotros (para destruirlo)
        private MaterialPropertyBlock propertyBlock;
        private Vector3[] flamePoints;
        private Vector3[] flameCorePoints;
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
            flameCorePoints = new Vector3[FlamePointCount];
            activeMaterial = ResolveMaterial();

            if (lander == null)
            {
                lander = GetComponentInParent<LanderController>();
            }

            // Casco: el último punto se une con el primero (loop cerrado).
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            BuildDetails();
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
            flameCoreRenderer.enabled = active;

            if (active)
            {
                flickerTimer = flameFlickerInterval;
                DrawFlame(Random.Range(flameMinLength, flameMaxLength));
            }
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        /// <summary>Crea un hijo con LineRenderer por cada trazo de detalle (con espejado).</summary>
        private void BuildDetails()
        {
            detailStrokes.Clear();
            detailStrokes.AddRange(CenterStrokes);

            foreach (Stroke stroke in MirroredStrokes)
            {
                detailStrokes.Add(stroke);
                detailStrokes.Add(Mirror(stroke));
            }

            for (int i = 0; i < detailStrokes.Count; i++)
            {
                LineRenderer lr = CreateChildLine("Detail_" + i);
                ConfigureLine(lr, detailStrokes[i].Points, detailStrokes[i].Loop,
                              lineColor, lineWidth * detailWidthScale);
                detailRenderers.Add(lr);
            }
        }

        private static Stroke Mirror(Stroke source)
        {
            var points = new Vector3[source.Points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 p = source.Points[i];
                points[i] = new Vector3(-p.x, p.y, p.z);
            }

            return new Stroke(source.Loop, points);
        }

        private LineRenderer CreateChildLine(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child.AddComponent<LineRenderer>();
        }

        private void BuildFlame()
        {
            flameRenderer = CreateChildLine("Flame");
            flameCoreRenderer = CreateChildLine("FlameCore");

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameColor, lineWidth * detailWidthScale);

            flameRenderer.enabled = false; // ocultas hasta que se active el empuje
            flameCoreRenderer.enabled = false;
        }

        /// <summary>Calcula los puntos de la llama exterior y del núcleo para la longitud dada.</summary>
        private void FillFlamePoints(float length)
        {
            float sway = Random.Range(-0.04f, 0.04f); // ligera oscilación lateral de la punta

            FillTeardrop(flamePoints, length, OuterFlameHalfWidth, sway);
            FillTeardrop(flameCorePoints, length * InnerFlameLengthRatio, InnerFlameHalfWidth, sway * 0.5f);
        }

        private static void FillTeardrop(Vector3[] points, float length, float halfWidth, float sway)
        {
            float midY = EngineBottomY - length * 0.55f;
            float tipY = EngineBottomY - length;

            points[0] = new Vector3(-halfWidth, EngineBottomY, 0f);
            points[1] = new Vector3(-halfWidth * 0.4f, midY, 0f);
            points[2] = new Vector3(sway, tipY, 0f);
            points[3] = new Vector3(halfWidth * 0.4f, midY, 0f);
            points[4] = new Vector3(halfWidth, EngineBottomY, 0f);
        }

        private void DrawFlame(float length)
        {
            FillFlamePoints(length);
            flameRenderer.SetPositions(flamePoints);
            flameCoreRenderer.SetPositions(flameCorePoints);
        }

        /// <summary>Configura un LineRenderer completo por código (casco, detalles y llamas).</summary>
        private void ConfigureLine(LineRenderer lr, Vector3[] points, bool loop, Color color, float width)
        {
            lr.useWorldSpace = false;                    // la línea se mueve y rota con la nave
            lr.alignment = LineAlignment.TransformZ;     // el plano de la línea mira a lo largo del eje Z local
            lr.textureMode = LineTextureMode.Stretch;
            lr.loop = loop;
            lr.numCornerVertices = cornerVertices;
            lr.numCapVertices = capVertices;
            lr.startWidth = width;
            lr.endWidth = width;
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
            ConfigureLine(lineRenderer, ShipOutline, true, lineColor, lineWidth);

            for (int i = 0; i < detailRenderers.Count; i++)
            {
                ConfigureLine(detailRenderers[i], detailStrokes[i].Points, detailStrokes[i].Loop,
                              lineColor, lineWidth * detailWidthScale);
            }

            FillFlamePoints(flameMaxLength);
            ConfigureLine(flameRenderer, flamePoints, false, flameColor, lineWidth);
            ConfigureLine(flameCoreRenderer, flameCorePoints, false, flameColor, lineWidth * detailWidthScale);
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