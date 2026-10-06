using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Trozo de nave (segmento de línea) lanzado por una explosión o una pata rota.
    /// Vive unos segundos, parpadea al final como un fósforo vectorial y se destruye.
    /// Lo crea y configura VectorLanderRenderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VectorDebris : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private LineRenderer line;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;
        private float lifetime;
        private float fadeTime;
        private float age;

        public void Init(LineRenderer lineRenderer, Color color, float life, float fade)
        {
            line = lineRenderer;
            baseColor = color;
            lifetime = Mathf.Max(0.05f, life);
            fadeTime = Mathf.Clamp(fade, 0.01f, lifetime);
            propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (line == null)
            {
                Destroy(gameObject);
                return;
            }

            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float fadeStart = lifetime - fadeTime;
            if (age < fadeStart) return;

            // Se apaga hacia negro y parpadea cada vez más.
            float t = (age - fadeStart) / fadeTime;
            Color c = Color.Lerp(baseColor, Color.black, t);

            line.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            line.SetPropertyBlock(propertyBlock);

            line.enabled = Random.value > t * 0.6f;
        }
    }
}