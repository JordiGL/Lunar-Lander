using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Lander "Explorer": módulo de exploración muy detallado.
    /// Cápsula con ventanas y escotilla, base octogonal con panel de instrumentos,
    /// remaches, paneles solares con celdas, antena parabólica con alimentador,
    /// antena látigo, cuadrantes RCS, tobera de descenso y patas con escalerilla.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanderRendererExplorer : VectorLanderRendererBase
    {
        public override string DesignName => "Explorer";

        protected override LanderGeometry GetLanderGeometry() => ExplorerGeometry;

        // ---------- Helpers de construcción ----------
        private static Vector3 V(float x, float y) => new Vector3(x, y, 0f);

        private static Vector3[] Ellipse(float cx, float cy, float rx, float ry, int n)
        {
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                pts[i] = V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return pts;
        }

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

        /// <summary>Panel solar con marco, tres divisiones verticales y una horizontal. 5 trazos.</summary>
        private static Stroke[] SolarPanel(float s)
        {
            const float x0 = 0.34f, x1 = 0.74f, y0 = 0.56f, y1 = 0.74f;
            return new[] {
                new Stroke(true, V(s * x0, y0), V(s * x1, y0), V(s * x1, y1), V(s * x0, y1)),
                new Stroke(false, V(s * 0.44f, y0), V(s * 0.44f, y1)),
                new Stroke(false, V(s * 0.54f, y0), V(s * 0.54f, y1)),
                new Stroke(false, V(s * 0.64f, y0), V(s * 0.64f, y1)),
                new Stroke(false, V(s * x0, 0.65f), V(s * x1, 0.65f)),
            };
        }

        /// <summary>Hilera de remaches: n trazos diminutos.</summary>
        private static Stroke[] Rivets(float y, float x0, float x1, int n)
        {
            var arr = new Stroke[n];
            float step = (x1 - x0) / (n - 1);
            for (int i = 0; i < n; i++)
            {
                float x = x0 + step * i;
                arr[i] = new Stroke(false, V(x - 0.006f, y), V(x + 0.006f, y));
            }
            return arr;
        }

        private static readonly LanderGeometry ExplorerGeometry = new LanderGeometry(
            // ---- Casco: cúpula + cápsula + base octogonal + falda de motor ----
            new[] {
                V(-0.16f, 0.62f), V(-0.08f, 0.70f), V( 0.08f, 0.70f), V( 0.16f, 0.62f),
                V( 0.26f, 0.50f), V( 0.28f, 0.34f), V( 0.46f, 0.30f), V( 0.50f, 0.14f),
                V( 0.50f, -0.04f), V( 0.40f, -0.20f), V( 0.17f, -0.20f),
                V( 0.21f, EngineBottomY), V(-0.21f, EngineBottomY),
                V(-0.17f, -0.20f), V(-0.40f, -0.20f), V(-0.50f, -0.04f),
                V(-0.50f, 0.14f), V(-0.46f, 0.30f), V(-0.28f, 0.34f), V(-0.26f, 0.50f),
            },
            // ---- Detalles ----
            Cat(
                new[] {
                    // 0-1 ventanas de la cápsula
                    new Stroke(true, V( 0.03f, 0.60f), V( 0.13f, 0.58f), V( 0.11f, 0.48f), V( 0.03f, 0.50f)),
                    new Stroke(true, V(-0.03f, 0.60f), V(-0.13f, 0.58f), V(-0.11f, 0.48f), V(-0.03f, 0.50f)),
                    // 2 escotilla superior
                    new Stroke(true, Ellipse(0f, 0.655f, 0.045f, 0.018f, 10)),
                    // 3-10 juntas y paneles del casco
                    new Stroke(false, V(-0.27f, 0.44f), V(0.27f, 0.44f)),
                    new Stroke(false, V( 0.20f, 0.52f), V( 0.22f, 0.34f)),
                    new Stroke(false, V(-0.20f, 0.52f), V(-0.22f, 0.34f)),
                    new Stroke(false, V(-0.46f, 0.30f), V(0.46f, 0.30f)),
                    new Stroke(false, V(-0.48f, 0.24f), V(0.48f, 0.24f)),
                    new Stroke(false, V( 0.41f, 0.10f), V( 0.50f, 0.10f)),
                    new Stroke(false, V(-0.41f, 0.10f), V(-0.50f, 0.10f)),
                    new Stroke(true, V(-0.14f, 0.20f), V(0.14f, 0.20f), V(0.14f, -0.12f), V(-0.14f, -0.12f)),
                    // 11-20 panel de instrumentos, motor y antena parabólica
                    new Stroke(true, Ellipse(0f, 0.08f, 0.065f, 0.065f, 12)),
                    new Stroke(false, V(-0.065f, 0.08f), V(0.065f, 0.08f)),
                    new Stroke(false, V(0f, 0.145f), V(0f, 0.015f)),
                    new Stroke(false, V(-0.10f, -0.06f), V(0.10f, -0.06f)),
                    new Stroke(false, V(-0.10f, -0.09f), V(0.10f, -0.09f)),
                    new Stroke(true, V(-0.12f, EngineBottomY), V(0.12f, EngineBottomY), V(0.17f, EngineBottomY - 0.09f), V(-0.17f, EngineBottomY - 0.09f)),
                    new Stroke(false, V(-0.145f, EngineBottomY - 0.045f), V(0.145f, EngineBottomY - 0.045f)),
                    new Stroke(false, V(0f, -0.12f), V(0f, EngineBottomY)),
                    new Stroke(false, V(0.10f, 0.70f), V(0.14f, 0.85f)),
                    new Stroke(false, V(0.03f, 0.93f), V(0.07f, 0.87f), V(0.14f, 0.85f), V(0.21f, 0.87f), V(0.25f, 0.93f)),
                    // 21-23 alimentador, antena látigo y su baliza
                    new Stroke(false, V(0.14f, 0.855f), V(0.14f, 0.96f)),
                    new Stroke(false, V(-0.10f, 0.70f), V(-0.20f, 0.98f)),
                    new Stroke(true, Ellipse(-0.205f, 0.995f, 0.015f, 0.015f, 6)),
                    // 24-29 cuadrantes RCS
                    new Stroke(true, V( 0.27f, 0.48f), V( 0.33f, 0.48f), V( 0.33f, 0.42f), V( 0.27f, 0.42f)),
                    new Stroke(true, V(-0.27f, 0.48f), V(-0.33f, 0.48f), V(-0.33f, 0.42f), V(-0.27f, 0.42f)),
                    new Stroke(false, V( 0.30f, 0.48f), V( 0.30f, 0.53f)),
                    new Stroke(false, V( 0.30f, 0.42f), V( 0.30f, 0.37f)),
                    new Stroke(false, V(-0.30f, 0.48f), V(-0.30f, 0.53f)),
                    new Stroke(false, V(-0.30f, 0.42f), V(-0.30f, 0.37f)),
                    // 30-31 brazos de los paneles solares
                    new Stroke(false, V( 0.23f, 0.545f), V( 0.34f, 0.65f)),
                    new Stroke(false, V(-0.23f, 0.545f), V(-0.34f, 0.65f)),
                },
                SolarPanel(1f), SolarPanel(-1f),
                Rivets(0.27f, -0.44f, 0.44f, 13)),
            Cat(
                new[] { RoleLight, RoleLight, RoleAccent },
                Rep(RoleHull, 8),
                Rep(RoleAccent, 10),
                new[] { RoleLight, RoleAccent, RoleLight },
                Rep(RoleAccent, 8),
                new[] { RoleHull }, Rep(RoleAccent, 4),
                new[] { RoleHull }, Rep(RoleAccent, 4),
                Rep(RoleAccent, 13)),
            // ---- Patas (lado derecho; el izquierdo se espeja) ----
            new[] {
                new Stroke(false, V(0.48f, 0.12f), V(0.88f, -0.56f)),
                new Stroke(false, V(0.50f, -0.04f), V(0.70f, -0.24f), V(0.88f, -0.56f)),
                new Stroke(false, V(0.70f, -0.56f), V(1.04f, -0.56f)),
                new Stroke(false, V(0.74f, -0.56f), V(0.79f, -0.50f), V(0.97f, -0.50f), V(1.02f, -0.56f)),
                new Stroke(false, V(0.88f, -0.56f), V(0.88f, -0.68f)),
                // escalerilla
                new Stroke(false, V(0.574f, -0.10f), V(0.644f, -0.10f)),
                new Stroke(false, V(0.669f, -0.26f), V(0.739f, -0.26f)),
                new Stroke(false, V(0.763f, -0.42f), V(0.833f, -0.42f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleAccent, RoleAccent, RoleAccent, RoleAccent },
            V(0.48f, 0.12f),
            0.485f, 0.085f, 0.11f);
    }
}