using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Sincroniza el PolygonCollider2D con el dibujo de VectorLanderRenderer.
    ///
    /// El colisionador se compone de varios paths (sin solaparse):
    ///   0 - Casco: la misma silueta que dibuja el renderer (cabina facetada, etapa de
    ///       descenso con chaflanes y tobera).
    ///   1 - Pata derecha: puntal + plato de apoyo, con el grosor real de las líneas.
    ///   2 - Pata izquierda: espejo de la anterior.
    ///
    /// La antena y los propulsores RCS se dejan fuera a propósito: son detalles visuales
    /// y no deben engancharse con el terreno.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class VectorLanderCollider : MonoBehaviour
    {
        /// <summary>Silueta del casco; debe coincidir con ShipOutline del renderer.</summary>
        private static readonly Vector2[] HullPoints =
        {
            new Vector2(-0.20f,  0.66f), // techo cabina, izquierda
            new Vector2( 0.20f,  0.66f), // techo cabina, derecha
            new Vector2( 0.34f,  0.50f), // chaflán cabina
            new Vector2( 0.34f,  0.30f), // base cabina
            new Vector2( 0.46f,  0.30f), // hombro etapa de descenso
            new Vector2( 0.46f, -0.05f), // pared derecha
            new Vector2( 0.36f, -0.20f), // chaflán inferior derecho
            new Vector2( 0.14f, -0.20f), // arranque de la tobera
            new Vector2( 0.19f, -0.38f), // boca derecha de la tobera
            new Vector2(-0.19f, -0.38f), // boca izquierda de la tobera
            new Vector2(-0.14f, -0.20f),
            new Vector2(-0.36f, -0.20f),
            new Vector2(-0.46f, -0.05f),
            new Vector2(-0.46f,  0.30f),
            new Vector2(-0.34f,  0.30f),
            new Vector2(-0.34f,  0.50f),
        };

        /// <summary>
        /// Pata derecha como polígono simple (sentido horario): arranca en la pared del cuerpo,
        /// baja por el puntal y recorre el plato de apoyo. Comparte arista con el casco
        /// (x = 0.46) sin solaparse. La izquierda se obtiene espejando X.
        /// </summary>
        private static readonly Vector2[] RightLegPoints =
        {
            new Vector2(0.46f,  0.20f), // anclaje superior (pared del cuerpo)
            new Vector2(0.50f,  0.18f), // borde exterior del puntal
            new Vector2(0.82f, -0.54f), // exterior del puntal al llegar al plato
            new Vector2(0.93f, -0.54f), // plato: arriba, extremo exterior
            new Vector2(0.93f, -0.59f), // plato: abajo, extremo exterior
            new Vector2(0.61f, -0.59f), // plato: abajo, extremo interior
            new Vector2(0.61f, -0.54f), // plato: arriba, extremo interior
            new Vector2(0.71f, -0.54f), // interior del puntal al llegar al plato
            new Vector2(0.46f,  0.10f), // anclaje inferior (pared del cuerpo)
        };

        private void Reset() => ApplyColliderShape();
        private void Awake() => ApplyColliderShape();

        [ContextMenu("Actualizar Colisionador")]
        public void ApplyColliderShape()
        {
            var polyCollider = GetComponent<PolygonCollider2D>();
            if (polyCollider == null) return;

            polyCollider.pathCount = 3;
            polyCollider.SetPath(0, HullPoints);
            polyCollider.SetPath(1, RightLegPoints);
            polyCollider.SetPath(2, MirrorX(RightLegPoints));
        }

        /// <summary>Espeja los puntos en X e invierte el orden para mantener el sentido del polígono.</summary>
        private static Vector2[] MirrorX(Vector2[] source)
        {
            int n = source.Length;
            var result = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 p = source[n - 1 - i];
                result[i] = new Vector2(-p.x, p.y);
            }

            return result;
        }
    }
}