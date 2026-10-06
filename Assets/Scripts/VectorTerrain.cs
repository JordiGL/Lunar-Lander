using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Representa una plataforma de aterrizaje plana dentro del terreno lunar.
    /// </summary>
    [System.Serializable]
    public struct LandingPad
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
        public int multiplier; // Multiplicador de puntos (ej: 2x, 3x, 5x)

        public Vector2 Center => (startPoint + endPoint) * 0.5f;
        public float Width => Mathf.Abs(endPoint.x - startPoint.x);
    }

    /// <summary>
    /// Genera un terreno lunar vectorial procedural utilizando LineRenderer y EdgeCollider2D.
    /// Crea picos, valles y zonas llanas destinadas al aterrizaje.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
    public sealed class VectorTerrain : MonoBehaviour
    {
        [Header("Configuración del Terreno")]
        [SerializeField, Min(10)] private int segments = 40;
        [SerializeField] private float width = 50f;
        [SerializeField] private float minHeight = -5f;
        [SerializeField] private float maxHeight = 2f;
        [SerializeField, Range(0f, 1f)] private float roughness = 0.5f;
        [SerializeField] private int randomSeed = 0;

        [Header("Plataformas de Aterrizaje")]
        [SerializeField, Range(1, 5)] private int minPads = 2;
        [SerializeField] private float minPadWidth = 2.5f;

        [Header("Aspecto Visual")]
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color terrainColor = Color.green;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private int sortingOrder = 5;

        [Header("Plataformas de Aterrizaje (Colores por Dificultad)")]
        [SerializeField] private Color colorPad2x = Color.white;
        [SerializeField] private Color colorPad3x = new Color(1f, 0.92f, 0.2f); // Amarillo
        [SerializeField] private Color colorPad5x = new Color(1f, 0.28f, 0.28f); // Rojo
        [SerializeField] private float padLineWidthMultiplier = 1.6f;

        // Propiedades e identificadores
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private LineRenderer lineRenderer;
        private EdgeCollider2D edgeCollider;
        private MaterialPropertyBlock propertyBlock;

        private Vector3[] terrainPoints3D;
        private Vector2[] terrainPoints2D;
        private readonly List<LandingPad> landingPads = new List<LandingPad>();
        private readonly List<GameObject> padVisualObjects = new List<GameObject>();

        public IReadOnlyList<LandingPad> LandingPads => landingPads;


        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            edgeCollider = GetComponent<EdgeCollider2D>();
            propertyBlock = new MaterialPropertyBlock();

            GenerateTerrain();
        }

        private void OnValidate()
        {
            if (Application.isPlaying && lineRenderer != null)
            {
                ApplyStyle();
                CreateLandingPadVisuals();
            }
        }

        /// <summary>
        /// Genera los vértices del terreno, posiciona plataformas llanas y actualiza los componentes.
        /// </summary>
        [ContextMenu("Regenerar Terreno")]
        public void GenerateTerrain()
        {
            if (randomSeed != 0) Random.InitState(randomSeed);

            int pointCount = segments + 1;
            terrainPoints3D = new Vector3[pointCount];
            terrainPoints2D = new Vector2[pointCount];
            landingPads.Clear();

            // Limpieza exhaustiva de cualquier objeto visual antiguo de plataformas
            ClearLandingPadVisuals();

            float stepX = width / segments;
            float startX = -width * 0.5f;

            // 1. Generación de alturas base mediante ruido Perlin
            float[] heights = new float[pointCount];
            float seedOffset = Random.Range(0f, 1000f);

            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                float sampleX = (x + seedOffset) * roughness * 0.1f;
                heights[i] = Mathf.Lerp(minHeight, maxHeight, Mathf.PerlinNoise(sampleX, 0f));
            }

            // 2. Insertar plataformas de aterrizaje estratégicas con dificultad y altura relacionada.
            List<int> zones = new List<int> { 0, 1, 2 }; // 0: Izquierda, 1: Centro, 2: Derecha
            for (int i = 0; i < zones.Count; i++)
            {
                int r = Random.Range(i, zones.Count);
                int temp = zones[i];
                zones[i] = zones[r];
                zones[r] = temp;
            }

            for (int z = 0; z < zones.Count; z++)
            {
                int zoneIndex = zones[z];
                int minIdx = Mathf.RoundToInt(pointCount * (0.05f + zoneIndex * 0.3f));
                int maxIdx = Mathf.RoundToInt(pointCount * (0.28f + zoneIndex * 0.3f));

                int mult = 2;
                float currentPadWidth = minPadWidth;

                if (z == 0) // Configuración Fácil (2x)
                {
                    mult = 2;
                    currentPadWidth = minPadWidth * 1.5f;
                }
                else if (z == 1) // Configuración Media (3x)
                {
                    mult = 3;
                    currentPadWidth = minPadWidth * 1.0f;
                }
                else // Configuración Difícil (5x)
                {
                    mult = 5;
                    currentPadWidth = minPadWidth * 0.7f;
                }

                int padSegmentWidth = Mathf.Max(1, Mathf.CeilToInt(currentPadWidth / stepX));
                int startIndex = -1;

                if (z == 0) // Fácil (2x)
                {
                    float minDiff = float.MaxValue;
                    for (int i = minIdx; i <= maxIdx - padSegmentWidth; i++)
                    {
                        float diff = 0f;
                        for (int j = 0; j < padSegmentWidth; j++)
                        {
                            diff += Mathf.Abs(heights[i + j + 1] - heights[i + j]);
                        }
                        if (diff < minDiff)
                        {
                            minDiff = diff;
                            startIndex = i;
                        }
                    }
                }
                else if (z == 1) // Media (3x) - Valle
                {
                    float lowestHeight = float.MaxValue;
                    for (int i = minIdx; i <= maxIdx - padSegmentWidth; i++)
                    {
                        if (heights[i] < lowestHeight)
                        {
                            lowestHeight = heights[i];
                            startIndex = i;
                        }
                    }
                }
                else // Difícil (5x) - Pico
                {
                    float highestHeight = float.MinValue;
                    for (int i = minIdx; i <= maxIdx - padSegmentWidth; i++)
                    {
                        if (heights[i] > highestHeight)
                        {
                            highestHeight = heights[i];
                            startIndex = i;
                        }
                    }
                }

                if (startIndex != -1)
                {
                    float padHeight = heights[startIndex];

                    if (z == 1)
                    {
                        padHeight = Mathf.Min(padHeight, Mathf.Lerp(minHeight, maxHeight, 0.2f));
                    }
                    else if (z == 2)
                    {
                        padHeight = Mathf.Max(padHeight, Mathf.Lerp(minHeight, maxHeight, 0.8f));
                    }

                    for (int k = 0; k <= padSegmentWidth; k++)
                    {
                        SetHeightSafe(heights, startIndex + k, padHeight);
                    }

                    if (z == 0)
                    {
                        SetHeightSafe(heights, startIndex - 1, padHeight + 0.1f);
                        SetHeightSafe(heights, startIndex - 2, padHeight - 0.1f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 1, padHeight - 0.1f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 2, padHeight + 0.1f);
                    }
                    else if (z == 1)
                    {
                        SetHeightSafe(heights, startIndex - 1, padHeight + 1.5f);
                        SetHeightSafe(heights, startIndex - 2, padHeight + 3.0f);
                        SetHeightSafe(heights, startIndex - 3, padHeight + 4.5f);

                        SetHeightSafe(heights, startIndex + padSegmentWidth + 1, padHeight + 1.5f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 2, padHeight + 3.0f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 3, padHeight + 4.5f);
                    }
                    else
                    {
                        SetHeightSafe(heights, startIndex - 1, padHeight - 2.0f);
                        SetHeightSafe(heights, startIndex - 2, padHeight - 4.0f);
                        SetHeightSafe(heights, startIndex - 3, padHeight - 5.5f);

                        SetHeightSafe(heights, startIndex + padSegmentWidth + 1, padHeight - 2.0f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 2, padHeight - 4.0f);
                        SetHeightSafe(heights, startIndex + padSegmentWidth + 3, padHeight - 5.5f);
                    }

                    Vector2 padStart = new Vector2(startX + startIndex * stepX, padHeight);
                    Vector2 padEnd = new Vector2(startX + (startIndex + padSegmentWidth) * stepX, padHeight);

                    landingPads.Add(new LandingPad
                    {
                        startPoint = padStart,
                        endPoint = padEnd,
                        multiplier = mult
                    });
                }
            }

            // 3. Asignar puntos 2D y 3D
            for (int i = 0; i < pointCount; i++)
            {
                float x = startX + i * stepX;
                float y = heights[i];

                terrainPoints3D[i] = new Vector3(x, y, 0f);
                terrainPoints2D[i] = new Vector2(x, y);
            }

            // 4. Actualizar LineRenderer y EdgeCollider2D
            UpdateLineRenderer();
            UpdateCollider();

            // 5. Generar los realces visuales por color de plataforma
            CreateLandingPadVisuals();
        }

        private void SetHeightSafe(float[] heights, int index, float value)
        {
            if (index >= 0 && index < heights.Length)
            {
                heights[index] = Mathf.Clamp(value, minHeight, maxHeight);
            }
        }

        private void ClearLandingPadVisuals()
        {
            foreach (var go in padVisualObjects)
            {
                if (go != null)
                {
                    if (Application.isPlaying) Destroy(go);
                    else DestroyImmediate(go);
                }
            }
            padVisualObjects.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.name.StartsWith("PadVisual_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void CreateLandingPadVisuals()
        {
            ClearLandingPadVisuals();

            for (int i = 0; i < landingPads.Count; i++)
            {
                var pad = landingPads[i];

                Color padColor = pad.multiplier switch
                {
                    5 => colorPad5x, // Rojo (Difícil / Pico)
                    3 => colorPad3x, // Amarillo (Medio / Valle)
                    _ => colorPad2x  // Blanco (Fácil / Llano)
                };

                GameObject padObj = new GameObject($"PadVisual_{i}_{pad.multiplier}x");
                padObj.transform.SetParent(transform, false);
                padVisualObjects.Add(padObj);

                LineRenderer lr = padObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = false;
                lr.positionCount = 2;
                lr.SetPositions(new Vector3[] { pad.startPoint, pad.endPoint });

                lr.startWidth = lineWidth * padLineWidthMultiplier;
                lr.endWidth = lineWidth * padLineWidthMultiplier;
                lr.numCapVertices = 2;
                lr.sharedMaterial = lineMaterial;
                lr.sortingOrder = sortingOrder + 1; // Encima del terreno

                lr.startColor = padColor;
                lr.endColor = padColor;

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                lr.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, padColor);
                mpb.SetColor(ColorId, padColor);
                lr.SetPropertyBlock(mpb);
            }
        }

        private void UpdateLineRenderer()
        {
            if (lineRenderer == null) return;

            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.positionCount = terrainPoints3D.Length;
            lineRenderer.SetPositions(terrainPoints3D);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.sortingOrder = sortingOrder;

            ApplyStyle();
        }

        private void UpdateCollider()
        {
            if (edgeCollider != null && terrainPoints2D != null)
            {
                edgeCollider.SetPoints(new List<Vector2>(terrainPoints2D));
            }
        }

        private void ApplyStyle()
        {
            if (lineMaterial != null)
            {
                lineRenderer.sharedMaterial = lineMaterial;
            }

            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;

            lineRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, terrainColor);
            propertyBlock.SetColor(ColorId, terrainColor);
            lineRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}