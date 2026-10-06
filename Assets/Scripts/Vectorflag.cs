using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Bandera vectorial que se planta y se iza sobre una plataforma tras un aterrizaje correcto.
    /// La crea y la gestiona VectorTerrain (no hace falta añadirla a mano en la escena).
    ///
    /// Secuencia:
    ///   1. El mástil crece desde el suelo hasta su altura (30 % del tiempo total).
    ///   2. La tela sube por el mástil (70 % restante) con un temblor mayor mientras sube.
    ///   3. Queda izada: varilla superior recta (como la bandera lunar del Apolo) y borde inferior
    ///      con un leve ondeo.
    ///
    /// Dibujada con tres LineRenderers hijos: mástil (con remate en rombo), contorno de la tela
    /// y una franja central.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VectorFlag : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const int ClothSegments = 8;
        private const float PoleGrowFraction = 0.3f;
        private const float FinialSize = 0.12f;

        [Header("Ondeo de la tela")]
        [Tooltip("Activa o desactiva el ondeo. Desactivado, la tela queda rígida (también al subir).")]
        [SerializeField] private bool waveEnabled = true;

        [Tooltip("Amplitud del ondeo del borde inferior (0 = tela rígida).")]
        [SerializeField, Min(0f)] private float waveAmplitude = 0.05f;

        [SerializeField, Min(0f)] private float waveSpeed = 3f;

        [Tooltip("Número de ondulaciones a lo largo de la tela.")]
        [SerializeField, Min(0f)] private float waveFrequency = 4f;

        private LineRenderer poleLine;
        private LineRenderer clothLine;
        private LineRenderer stripeLine;
        private MaterialPropertyBlock propertyBlock;

        private float poleHeight;
        private float flagWidth;
        private float flagHeight;
        private float direction;
        private float raiseDuration;
        private float elapsed;
        private bool initialized;

        /// <summary>true cuando la tela ya ha llegado arriba del todo.</summary>
        public bool IsRaised { get; private set; }

        /// <summary>Activa o desactiva el ondeo en cualquier momento.</summary>
        public bool WaveEnabled
        {
            get => waveEnabled;
            set => waveEnabled = value;
        }

        /// <summary>
        /// Configura y arranca la animación. La posición del GameObject es la base del mástil.
        /// </summary>
        /// <param name="direction">+1 la tela se extiende a la derecha, -1 a la izquierda.</param>
        /// <param name="waves">false = la tela no ondea.</param>
        public void Init(Material material, Color color, float lineWidth, int sortingOrder,
                         float poleHeight, float flagWidth, float flagHeight, int direction, float raiseDuration,
                         bool waves = true)
        {
            waveEnabled = waves;
            this.poleHeight = Mathf.Max(0.3f, poleHeight);
            this.flagWidth = Mathf.Max(0.1f, flagWidth);
            this.flagHeight = Mathf.Clamp(flagHeight, 0.1f, this.poleHeight * 0.8f);
            this.direction = direction >= 0 ? 1f : -1f;
            this.raiseDuration = Mathf.Max(0.2f, raiseDuration);

            propertyBlock = new MaterialPropertyBlock();

            poleLine = CreateLine("Pole", material, color, lineWidth, sortingOrder, false);
            clothLine = CreateLine("Cloth", material, color, lineWidth * 0.8f, sortingOrder, true);
            stripeLine = CreateLine("Stripe", material, color, lineWidth * 0.6f, sortingOrder, false);

            clothLine.positionCount = (ClothSegments + 1) * 2;
            stripeLine.positionCount = ClothSegments + 1;

            elapsed = 0f;
            IsRaised = false;
            initialized = true;

            Draw();
        }

        private void Update()
        {
            if (!initialized) return;

            // Tiempo sin escalar: la bandera sigue izándose aunque el juego se pause al aterrizar.
            elapsed += Time.unscaledDeltaTime;
            Draw();
        }

        private LineRenderer CreateLine(string childName, Material material, Color color, float width,
                                        int sortingOrder, bool loop)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);

            LineRenderer lr = child.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ;
            lr.textureMode = LineTextureMode.Stretch;
            lr.loop = loop;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sortingOrder = sortingOrder;
            lr.sharedMaterial = material;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            lr.SetPropertyBlock(propertyBlock);

            return lr;
        }

        private void Draw()
        {
            float total = raiseDuration;

            // Fase 1: el mástil crece (ease-out).
            float growT = Mathf.Clamp01(elapsed / (total * PoleGrowFraction));
            float grow = 1f - (1f - growT) * (1f - growT);

            // Fase 2: la tela sube (smoothstep).
            float hoistT = Mathf.Clamp01((elapsed - total * PoleGrowFraction) / (total * (1f - PoleGrowFraction)));
            float hoist = hoistT * hoistT * (3f - 2f * hoistT);

            IsRaised = hoistT >= 1f;

            DrawPole(grow);
            DrawCloth(hoist, growT >= 1f);
        }

        private void DrawPole(float grow)
        {
            float h = poleHeight * grow;

            if (grow < 1f)
            {
                poleLine.positionCount = 2;
                poleLine.SetPosition(0, Vector3.zero);
                poleLine.SetPosition(1, new Vector3(0f, h, 0f));
                return;
            }

            // Mástil completo con remate en rombo.
            poleLine.positionCount = 6;
            poleLine.SetPosition(0, Vector3.zero);
            poleLine.SetPosition(1, new Vector3(0f, h, 0f));
            poleLine.SetPosition(2, new Vector3(FinialSize * 0.5f, h + FinialSize * 0.5f, 0f));
            poleLine.SetPosition(3, new Vector3(0f, h + FinialSize, 0f));
            poleLine.SetPosition(4, new Vector3(-FinialSize * 0.5f, h + FinialSize * 0.5f, 0f));
            poleLine.SetPosition(5, new Vector3(0f, h, 0f));
        }

        private void DrawCloth(float hoist, bool poleReady)
        {
            // Hasta que el mástil no está completo, la tela no se ve.
            clothLine.enabled = poleReady;
            stripeLine.enabled = poleReady;
            if (!poleReady) return;

            // La tela sube desde la base del mástil hasta justo debajo del remate.
            float topAtBottom = flagHeight + 0.1f;
            float topAtTop = poleHeight - 0.08f;
            float yTop = Mathf.Lerp(topAtBottom, topAtTop, hoist);
            float yBottom = yTop - flagHeight;

            // Mientras sube, el aire la agita más.
            float amplitude = waveEnabled ? waveAmplitude * Mathf.Lerp(2.5f, 1f, hoist) : 0f;
            float phase = elapsed * waveSpeed;

            // Contorno: varilla superior recta (índices 0..N) y borde inferior ondeante (N..0).
            for (int i = 0; i <= ClothSegments; i++)
            {
                float u = i / (float)ClothSegments;
                float x = direction * u * flagWidth;
                float ripple = Mathf.Sin(phase - u * waveFrequency) * amplitude * u;

                clothLine.SetPosition(i, new Vector3(x, yTop, 0f));
                clothLine.SetPosition((ClothSegments + 1) * 2 - 1 - i, new Vector3(x, yBottom + ripple, 0f));

                // Franja central: sigue la mitad del ondeo.
                stripeLine.SetPosition(i, new Vector3(x, (yTop + yBottom) * 0.5f + ripple * 0.5f, 0f));
            }
        }
    }
}