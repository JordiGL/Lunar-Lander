using UnityEngine;

namespace LunarLander
{
    public sealed partial class VectorLanderRenderer
    {
        // ------------------------------------------------------------------
        // Diseño MODERN: casco facetado con carcasa interior, reactor hexagonal,
        // patas articuladas con pie trapezoidal y antena con baliza.
        // ------------------------------------------------------------------

        private static readonly LanderGeometry ModernGeometry = new LanderGeometry(
            new[]
            {
                new Vector3(-0.14f,  0.70f, 0f),
                new Vector3( 0.14f,  0.70f, 0f),
                new Vector3( 0.30f,  0.54f, 0f),
                new Vector3( 0.34f,  0.34f, 0f),
                new Vector3( 0.50f,  0.26f, 0f),
                new Vector3( 0.50f, -0.06f, 0f),
                new Vector3( 0.38f, -0.22f, 0f),
                new Vector3( 0.16f, -0.22f, 0f),
                new Vector3( 0.20f, EngineBottomY, 0f),
                new Vector3(-0.20f, EngineBottomY, 0f),
                new Vector3(-0.16f, -0.22f, 0f),
                new Vector3(-0.38f, -0.22f, 0f),
                new Vector3(-0.50f, -0.06f, 0f),
                new Vector3(-0.50f,  0.26f, 0f),
                new Vector3(-0.34f,  0.34f, 0f),
                new Vector3(-0.30f,  0.54f, 0f),
            },
            new[]
            {
                // 0: visera de la cabina (hexágono)
                new Stroke(true,
                    new Vector3(-0.10f, 0.58f, 0f), new Vector3(0.10f, 0.58f, 0f),
                    new Vector3(0.15f, 0.48f, 0f), new Vector3(0.08f, 0.38f, 0f),
                    new Vector3(-0.08f, 0.38f, 0f), new Vector3(-0.15f, 0.48f, 0f)),
                // 1: cresta del morro
                new Stroke(false, new Vector3(0f, 0.70f, 0f), new Vector3(0f, 0.58f, 0f)),
                // 2: carcasa interior (doble casco)
                new Stroke(true,
                    new Vector3(-0.10f, 0.64f, 0f), new Vector3(0.10f, 0.64f, 0f),
                    new Vector3(0.25f, 0.50f, 0f), new Vector3(0.29f, 0.31f, 0f),
                    new Vector3(0.45f, 0.23f, 0f), new Vector3(0.45f, -0.05f, 0f),
                    new Vector3(0.36f, -0.18f, 0f), new Vector3(0.12f, -0.18f, 0f),
                    new Vector3(-0.12f, -0.18f, 0f), new Vector3(-0.36f, -0.18f, 0f),
                    new Vector3(-0.45f, -0.05f, 0f), new Vector3(-0.45f, 0.23f, 0f),
                    new Vector3(-0.29f, 0.31f, 0f), new Vector3(-0.25f, 0.50f, 0f)),
                // 3-4: chevrones del pecho
                new Stroke(false,
                    new Vector3(-0.14f, 0.30f, 0f), new Vector3(0f, 0.22f, 0f), new Vector3(0.14f, 0.30f, 0f)),
                new Stroke(false,
                    new Vector3(-0.14f, 0.22f, 0f), new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.22f, 0f)),
                // 5: anillo del reactor (hexágono)
                new Stroke(true,
                    new Vector3(-0.10f, 0f, 0f), new Vector3(-0.05f, 0.085f, 0f),
                    new Vector3(0.05f, 0.085f, 0f), new Vector3(0.10f, 0f, 0f),
                    new Vector3(0.05f, -0.085f, 0f), new Vector3(-0.05f, -0.085f, 0f)),
                // 6: núcleo del reactor
                new Stroke(false, new Vector3(-0.05f, 0f, 0f), new Vector3(0.05f, 0f, 0f)),
                // 7-8: columna central
                new Stroke(false, new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.085f, 0f)),
                new Stroke(false, new Vector3(0f, -0.085f, 0f), new Vector3(0f, -0.20f, 0f)),
                // 9-10: anillos de la tobera
                new Stroke(false, new Vector3(-0.155f, -0.29f, 0f), new Vector3(0.155f, -0.29f, 0f)),
                new Stroke(false, new Vector3(-0.17f, -0.335f, 0f), new Vector3(0.17f, -0.335f, 0f)),
                // 11-12: mástil de antena y baliza
                new Stroke(false, new Vector3(0.10f, 0.70f, 0f), new Vector3(0.16f, 0.92f, 0f)),
                new Stroke(true,
                    new Vector3(0.16f, 0.92f, 0f), new Vector3(0.19f, 0.96f, 0f),
                    new Vector3(0.16f, 1.00f, 0f), new Vector3(0.13f, 0.96f, 0f)),
            },
            new[]
            {
                RoleHull, RoleHull, RoleLight, RoleAccent, RoleAccent, RoleAccent, RoleLight,
                RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent,
            },
            new[]
            {
                // 0-4: patas articuladas
                new Stroke(false,
                    new Vector3(0.50f, 0.18f, 0f), new Vector3(0.70f, -0.14f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false,
                    new Vector3(0.50f, -0.06f, 0f), new Vector3(0.66f, -0.26f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.68f, -0.56f, 0f), new Vector3(1.02f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.60f, 0.02f, 0f), new Vector3(0.58f, -0.16f, 0f)),
                new Stroke(false,
                    new Vector3(0.72f, -0.56f, 0f), new Vector3(0.78f, -0.50f, 0f),
                    new Vector3(0.92f, -0.50f, 0f), new Vector3(0.98f, -0.56f, 0f)),
                // 5: Propulsores RCS (lazo con y=0.48)
                new Stroke(true,
                    new Vector3(0.34f, 0.48f, 0f), new Vector3(0.46f, 0.48f, 0f),
                    new Vector3(0.46f, 0.36f, 0f), new Vector3(0.34f, 0.36f, 0f)),
                // 6: faceta del hombro
                new Stroke(false, new Vector3(0.14f, 0.70f, 0f), new Vector3(0.10f, 0.58f, 0f)),
                // 7-9: rejillas de ventilación
                new Stroke(false, new Vector3(0.30f, 0.22f, 0f), new Vector3(0.42f, 0.22f, 0f)),
                new Stroke(false, new Vector3(0.30f, 0.17f, 0f), new Vector3(0.42f, 0.17f, 0f)),
                new Stroke(false, new Vector3(0.30f, 0.12f, 0f), new Vector3(0.42f, 0.12f, 0f)),
                // 10: cono interior de la tobera
                new Stroke(false, new Vector3(0.07f, -0.20f, 0f), new Vector3(0.05f, -0.38f, 0f)),
            },
            new[]
            {
                RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleHull,
                RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent,
            },
            new Vector3(0.50f, 0.18f, 0f),
            0.48f, 0.11f, 0.10f);
    }
}
