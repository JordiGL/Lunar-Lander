using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Lander tipo platillo volante: casco lenticular ancho y plano, cúpula de cristal
    /// con nervios, anillo de portillas, antena con baliza, tobera central y tres
    /// puntales por lado terminados en platos de apoyo.
    /// Silueta opuesta a Classic (caja) y Starship (cilindro alto): aquí todo es horizontal.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanderRendererOvni : VectorLanderRendererBase
    {
        public override string DesignName => "Ovni";

        protected override LanderGeometry GetLanderGeometry() => OvniGeometry;

        private static readonly LanderGeometry OvniGeometry = new LanderGeometry(
            // ---- Casco: cúpula + disco lenticular + campana de la tobera ----
            new[] {
                // cúpula (lado derecho)
                new Vector3( 0.000f,  0.550f, 0f), new Vector3( 0.100f,  0.530f, 0f),
                new Vector3( 0.180f,  0.470f, 0f), new Vector3( 0.230f,  0.390f, 0f),
                new Vector3( 0.250f,  0.310f, 0f),
                // hombro y borde del disco
                new Vector3( 0.460f,  0.240f, 0f), new Vector3( 0.620f,  0.170f, 0f),
                new Vector3( 0.720f,  0.100f, 0f),
                // panza del disco y tobera
                new Vector3( 0.620f,  0.030f, 0f), new Vector3( 0.420f, -0.070f, 0f),
                new Vector3( 0.240f, -0.130f, 0f), new Vector3( 0.140f, -0.180f, 0f),
                new Vector3( 0.180f, EngineBottomY, 0f),
                // lado izquierdo (espejo)
                new Vector3(-0.180f, EngineBottomY, 0f), new Vector3(-0.140f, -0.180f, 0f),
                new Vector3(-0.240f, -0.130f, 0f), new Vector3(-0.420f, -0.070f, 0f),
                new Vector3(-0.620f,  0.030f, 0f), new Vector3(-0.720f,  0.100f, 0f),
                new Vector3(-0.620f,  0.170f, 0f), new Vector3(-0.460f,  0.240f, 0f),
                new Vector3(-0.250f,  0.310f, 0f), new Vector3(-0.230f,  0.390f, 0f),
                new Vector3(-0.180f,  0.470f, 0f), new Vector3(-0.100f,  0.530f, 0f),
            },
            // ---- Detalles centrados ----
            new[] {
                // 0: línea del ecuador del disco
                new Stroke(false, new Vector3(-0.72f, 0.10f, 0f), new Vector3(0.72f, 0.10f, 0f)),
                // 1: base de la cúpula
                new Stroke(false, new Vector3(-0.25f, 0.31f, 0f), new Vector3(0.25f, 0.31f, 0f)),
                // 2: anillo inferior (panza)
                new Stroke(false, new Vector3(-0.50f, 0.00f, 0f), new Vector3(-0.20f, -0.07f, 0f), new Vector3(0.20f, -0.07f, 0f), new Vector3(0.50f, 0.00f, 0f)),
                // 3: antena
                new Stroke(false, new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0.70f, 0f)),
                // 4: baliza de la antena
                new Stroke(false, new Vector3(-0.045f, 0.70f, 0f), new Vector3(0.045f, 0.70f, 0f)),
                // 5: anillo de la tobera
                new Stroke(false, new Vector3(-0.158f, -0.30f, 0f), new Vector3(0.158f, -0.30f, 0f)),
                // 6: aleta central de la tobera
                new Stroke(false, new Vector3(0f, -0.18f, 0f), new Vector3(0f, -0.30f, 0f)),
                // 7: portilla central
                new Stroke(false, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.08f, 0f)),
            },
            new[] {
                RoleHull, RoleHull, RoleHull,
                RoleAccent, RoleLight,
                RoleAccent, RoleAccent, RoleAccent,
            },
            // ---- Lado derecho (el izquierdo se espeja) ----
            new[] {
                // nervio de la cúpula
                new Stroke(false, new Vector3(0.04f, 0.54f, 0f), new Vector3(0.11f, 0.44f, 0f), new Vector3(0.13f, 0.31f, 0f)),
                // portillas del anillo
                new Stroke(false, new Vector3(0.20f, 0.04f, 0f), new Vector3(0.20f, 0.08f, 0f)),
                new Stroke(false, new Vector3(0.40f, 0.04f, 0f), new Vector3(0.40f, 0.08f, 0f)),
                new Stroke(false, new Vector3(0.60f, 0.04f, 0f), new Vector3(0.60f, 0.08f, 0f)),
                // pata: puntal principal, tirante y plato de apoyo
                new Stroke(false, new Vector3(0.38f, -0.08f, 0f), new Vector3(0.56f, -0.52f, 0f)),
                new Stroke(false, new Vector3(0.17f, -0.22f, 0f), new Vector3(0.52f, -0.42f, 0f)),
                new Stroke(true,  new Vector3(0.45f, -0.56f, 0f), new Vector3(0.50f, -0.52f, 0f), new Vector3(0.62f, -0.52f, 0f), new Vector3(0.67f, -0.56f, 0f)),
            },
            new[] {
                RoleLight, RoleAccent, RoleAccent, RoleAccent,
                RoleLeg, RoleLeg, RoleLeg,
            },
            new Vector3(0.38f, -0.08f, 0f),
            0.42f, 0.12f, 0.07f);
    }
}