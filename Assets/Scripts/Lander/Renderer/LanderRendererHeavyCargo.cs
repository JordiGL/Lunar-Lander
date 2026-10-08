using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Lander "HeavyCargo": transporte minero y de carga pesada.
    /// Silueta ancha y robusta, cabina superior blindada, bodega central
    /// con cerchas estructurales en 'X', vigas maestras y patas pesadas con doble pistón.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanderRendererHeavyCargo : VectorLanderRendererBase
    {
        public override string DesignName => "Heavy Cargo";

        protected override LanderGeometry GetLanderGeometry() => HeavyCargoGeometry;

        // ---------- Helpers de construcción ----------
        private static Vector3 V(float x, float y) => new Vector3(x, y, 0f);

        private static T[] Rep<T>(T value, int count)
        {
            var arr = new T[count];
            for (int i = 0; i < count; i++) arr[i] = value;
            return arr;
        }

        private static T[] Cat<T>(params T[][] parts)
        {
            int total = 0;
            for (int i = 0; i < parts.Length; i++) total += parts[i].Length;
            var result = new T[total];
            int k = 0;
            for (int i = 0; i < parts.Length; i++)
                for (int j = 0; j < parts[i].Length; j++) result[k++] = parts[i][j];
            return result;
        }

        private static readonly LanderGeometry HeavyCargoGeometry = new LanderGeometry(
            // ---- Contorno exterior del fuselaje (Bucle cerrado) ----
            new[] {
                // Cabina superior facetada
                V(-0.24f, 0.60f), V(-0.16f, 0.68f), V( 0.16f, 0.68f), V( 0.24f, 0.60f),
                // Hombros anchos y montantes superiores de carga
                V( 0.52f, 0.46f), V( 0.56f, 0.30f),
                // Flanco exterior industrial
                V( 0.56f, -0.10f), V( 0.44f, -0.22f),
                // Vientre y falda del motor
                V( 0.22f, -0.22f), V( 0.22f, EngineBottomY), V(-0.22f, EngineBottomY),
                V(-0.22f, -0.22f), V(-0.44f, -0.22f),
                // Lado izquierdo simétrico
                V(-0.56f, -0.10f), V(-0.56f, 0.30f), V(-0.52f, 0.46f)
            },
            // ---- Trazos centrales / Estructura no espejada ----
            Cat(
                new[] {
                    // 0: Ventanal de cabina horizontal blindado
                    new Stroke(true, V(-0.18f, 0.62f), V(0.18f, 0.62f), V(0.14f, 0.52f), V(-0.14f, 0.52f)),
                    // 1: Viga divisoria cabina-bodega
                    new Stroke(false, V(-0.52f, 0.46f), V(0.52f, 0.46f)),
                    // 2: Contenedor central de mineral / carga
                    new Stroke(true, V(-0.22f, 0.40f), V(0.22f, 0.40f), V(0.22f, -0.10f), V(-0.22f, -0.10f)),
                    // 3-4: Cercha de refuerzo en 'X' en la bahía de carga
                    new Stroke(false, V(-0.22f, 0.40f), V(0.22f, -0.10f)),
                    new Stroke(false, V(0.22f, 0.40f), V(-0.22f, -0.10f)),
                    // 5: Base de la bahía central (reducida a -0.22 a 0.22 para no atravesar los depósitos)
                    new Stroke(false, V(-0.22f, -0.10f), V(0.22f, -0.10f)),
                    // 6: Tobera pesada rectangular/trapezoidal
                    new Stroke(true, V(-0.18f, EngineBottomY), V(0.18f, EngineBottomY), V(0.24f, EngineBottomY - 0.08f), V(-0.24f, EngineBottomY - 0.08f)),
                    // 7: Nervadura central de tobera
                    new Stroke(false, V(0f, -0.22f), V(0f, EngineBottomY - 0.08f)),
                    // 8-9: Grúa / Mástil de radio superior
                    new Stroke(false, V(0.12f, 0.68f), V(0.12f, 0.88f)),
                    new Stroke(false, V(0.06f, 0.82f), V(0.18f, 0.82f))
                }
            ),
            new[] {
                RoleLight, RoleHull, RoleHull, RoleAccent, RoleAccent,
                RoleHull, RoleHull, RoleAccent, RoleAccent, RoleAccent
            },
            // ---- Patas pesadas y detalles del flanco (lado derecho; el izquierdo se calcula automáticamente) ----
            new[] {
                // 0: Viga maestra exterior de la zanca principal (desde el hombro hasta el patín)
                new Stroke(false, V(0.54f, 0.05f), V(0.80f, -0.32f), V(0.90f, -0.56f)),

                // 1: Viga paralela interior (refuerzo estructural continuo, arranca en la base del casco)
                new Stroke(false, V(0.44f, -0.10f), V(0.70f, -0.36f), V(0.78f, -0.56f)),

                // 2-3: Travesaños en 'Z' / celosía soldados entre viga exterior e interior
                new Stroke(false, V(0.54f, 0.05f), V(0.57f, -0.23f)),
                new Stroke(false, V(0.80f, -0.32f), V(0.70f, -0.36f)),

                // 4: Pistón hidráulico principal (conecta la base del motor con la articulación exterior)
                new Stroke(false, V(0.32f, -0.22f), V(0.70f, -0.36f)),

                // 5: Tirante inferior de choque (conecta el vientre con el patín de suelo)
                new Stroke(false, V(0.44f, -0.22f), V(0.78f, -0.56f)),

                // 6: Base / Zapata ancha de suelo (plancha de apoyo)
                new Stroke(false, V(0.64f, -0.56f), V(1.08f, -0.56f)),

                // 7: Caja amortiguadora trapezoidal del patín
                new Stroke(true, V(0.74f, -0.56f), V(0.78f, -0.49f), V(0.94f, -0.49f), V(0.98f, -0.56f)),

                // 8: Nervio central vertical de la caja
                new Stroke(false, V(0.86f, -0.49f), V(0.86f, -0.56f)),

                // 9-10: Tacos de agarre inclinados en los extremos del patín
                new Stroke(false, V(0.64f, -0.56f), V(0.67f, -0.52f)),
                new Stroke(false, V(1.08f, -0.56f), V(1.05f, -0.52f)),

                // 11: Soporte exterior del pilar lateral (desplazado a X=0.48f para no cruzar los tanques)
                new Stroke(false, V(0.48f, 0.46f), V(0.50f, 0.32f)),

                // 12-13: Ranuras de disipación exteriores (en el borde externo del chasis, fuera del fuel)
                new Stroke(false, V(0.49f, 0.22f), V(0.55f, 0.22f)),
                new Stroke(false, V(0.49f, 0.08f), V(0.55f, 0.08f))
            },
            new[] {
                RoleLeg, RoleLeg, RoleLeg, RoleLeg,
                RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleAccent,
                RoleLeg, RoleLeg,
                RoleHull, RoleAccent, RoleAccent
            },
            // Anclaje de rotura
            V(0.54f, 0.05f),
            // Cockpit: centro Y, medio ancho, grosor
            0.57f, 0.16f, 0.07f
        );
    }
}