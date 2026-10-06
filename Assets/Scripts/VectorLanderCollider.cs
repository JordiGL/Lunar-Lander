using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Sincroniza los puntos del PolygonCollider2D con el dibujo de la nave.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class VectorLanderCollider : MonoBehaviour
    {
        private static readonly Vector2[] ShipColliderPoints =
        {
            new Vector2(-0.20f,  0.60f), // Cabina arriba-izq
            new Vector2( 0.20f,  0.60f), // Cabina arriba-der
            new Vector2( 0.30f,  0.30f),
            new Vector2( 0.45f,  0.30f),
            new Vector2( 0.39f,  0.10f),
            new Vector2( 0.85f, -0.55f), // Pie derecho (extremo exterior)
            new Vector2( 0.55f, -0.55f), // Pie derecho (extremo interior)
            new Vector2( 0.30f, -0.20f),
            new Vector2( 0.18f, -0.38f), // Tobera
            new Vector2(-0.18f, -0.38f), // Tobera
            new Vector2(-0.30f, -0.20f),
            new Vector2(-0.55f, -0.55f), // Pie izquierdo (extremo interior)
            new Vector2(-0.85f, -0.55f), // Pie izquierdo (extremo exterior)
            new Vector2(-0.39f,  0.10f),
            new Vector2(-0.45f,  0.30f),
            new Vector2(-0.30f,  0.30f)
        };

        private void Reset() => ApplyColliderShape();
        private void Awake() => ApplyColliderShape();

        [ContextMenu("Actualizar Colisionador")]
        public void ApplyColliderShape()
        {
            var polyCollider = GetComponent<PolygonCollider2D>();
            if (polyCollider != null)
            {
                polyCollider.pathCount = 1;
                polyCollider.SetPath(0, ShipColliderPoints);
            }
        }
    }
}