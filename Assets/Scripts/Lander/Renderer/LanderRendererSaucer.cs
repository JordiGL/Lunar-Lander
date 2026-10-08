using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Lander "Saucer": platillo volante retro-futurista de estilo vectorial.
    /// Cúpula superior redondeada con cabina, disco ancho central con anillos y luces,
    /// propulsor de retrocohete inferior y zancas de aterrizaje con platos amortiguadores.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanderRendererSaucer : VectorLanderRendererBase
    {
        public override string DesignName => "Saucer";

        protected override LanderGeometry GetLanderGeometry() => SaucerGeometry;

        // ---------- Helpers de construcción ----------
        private static Vector3 V(float x, float y) => new Vector3(x, y, 0f);

        private static Vector3[] EllipseArc(float cx, float cy, float rx, float ry, float startAngleRad, float endAngleRad, int segments)
        {
            var pts = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(startAngleRad, endAngleRad, (float)i / segments);
                pts[i] = V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return pts;
        }

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

        private static readonly LanderGeometry SaucerGeometry = new LanderGeometry(
            // ---- Casco principal exterior (Outline en bucle cerrado) ----
            // Silueta de cúpula -> ala disco ancha -> borde angular -> base de motor
            new[] {
                // Domo superior
                V(-0.20f, 0.44f), V(-0.14f, 0.58f), V( 0.00f, 0.64f), V( 0.14f, 0.58f), V( 0.20f, 0.44f),
                // Parte superior del disco
                V( 0.42f, 0.32f), V( 0.72f, 0.20f), V( 0.78f, 0.14f),
                // Canto del platillo
                V( 0.75f, 0.06f), V( 0.56f, -0.06f),
                // Vientre inferior hacia la tobera
                V( 0.30f, -0.18f), V( 0.17f, -0.22f),
                V( 0.19f, EngineBottomY), V(-0.19f, EngineBottomY),
                V(-0.17f, -0.22f), V(-0.30f, -0.18f),
                // Lado izquierdo simétrico
                V(-0.56f, -0.06f), V(-0.75f, 0.06f),
                V(-0.78f, 0.14f), V(-0.72f, 0.20f), V(-0.42f, 0.32f)
            },
            // ---- Detalles centrales no espejados ----
            Cat(
                new[] {
                    // 0. Base interior del domo transparente
                    new Stroke(false, EllipseArc(0f, 0.44f, 0.22f, 0.06f, Mathf.PI, 0f, 10)),
                    // 1. Ojo de buey / Consola central del platillo
                    new Stroke(true, Ellipse(0f, 0.36f, 0.06f, 0.04f, 10)),
                    // 2. Línea de ecuador del disco (reborde metálico)
                    new Stroke(false, V(-0.76f, 0.10f), V(-0.40f, 0.11f), V(0f, 0.12f), V(0.40f, 0.11f), V(0.76f, 0.10f)),
                    // 3. Moldura inferior del platillo
                    new Stroke(false, V(-0.52f, -0.02f), V(0f, 0.02f), V(0.52f, -0.02f)),
                    // 4. Panel reactor central
                    new Stroke(true, V(-0.15f, -0.06f), V(0.15f, -0.06f), V(0.12f, -0.18f), V(-0.12f, -0.18f)),
                    // 5. Tobera cónica de sustentación
                    new Stroke(true, V(-0.12f, EngineBottomY), V(0.12f, EngineBottomY), V(0.16f, EngineBottomY - 0.08f), V(-0.16f, EngineBottomY - 0.08f)),
                    new Stroke(false, V(-0.14f, EngineBottomY - 0.04f), V(0.14f, EngineBottomY - 0.04f)),
                    // 6. Antena sensora superior del domo
                    new Stroke(false, V(0f, 0.64f), V(0f, 0.76f)),
                    new Stroke(true, Ellipse(0f, 0.775f, 0.02f, 0.02f, 8))
                }
            ),
            new[] {
                RoleHull, RoleLight, RoleAccent, RoleAccent, RoleAccent,
                RoleHull, RoleAccent, RoleAccent, RoleLight
            },
            // ---- Patas y detalles espejados (lado derecho; el izquierdo se calcula automáticamente) ----
            new[] {
                // Pata principal: zanca que desciende desde el bajo disco
                new Stroke(false, V(0.48f, -0.02f), V(0.66f, -0.42f), V(0.72f, -0.56f)),
                // Tirante tensor hidráulico
                new Stroke(false, V(0.25f, -0.18f), V(0.66f, -0.42f)),
                // Plato ancho de aterrizaje en la base
                new Stroke(false, V(0.58f, -0.56f), V(0.86f, -0.56f)),
                new Stroke(false, V(0.62f, -0.56f), V(0.66f, -0.52f), V(0.78f, -0.52f), V(0.82f, -0.56f)),
                // Escotilla/rejilla lateral de ventilación
                new Stroke(false, V(0.32f, 0.22f), V(0.46f, 0.18f)),
                new Stroke(false, V(0.34f, 0.17f), V(0.48f, 0.13f))
            },
            new[] {
                RoleLeg, RoleLeg, RoleLeg, RoleAccent,
                RoleAccent, RoleAccent
            },
            // Anclaje de la pata para fragmentación de impactos
            V(0.48f, -0.02f),
            // Cockpit: centro Y, medio ancho, grosor
            0.50f, 0.12f, 0.06f
        );
    }
}