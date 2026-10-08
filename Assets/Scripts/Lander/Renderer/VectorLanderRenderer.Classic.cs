using UnityEngine;

namespace LunarLander
{
    public sealed partial class VectorLanderRenderer
    {
        // ------------------------------------------------------------------
        // Diseño CLASSIC
        // ------------------------------------------------------------------

        private static readonly LanderGeometry ClassicGeometry = new LanderGeometry(
            new[]
            {
                new Vector3(-0.20f,  0.66f, 0f),
                new Vector3( 0.20f,  0.66f, 0f),
                new Vector3( 0.34f,  0.50f, 0f),
                new Vector3( 0.34f,  0.30f, 0f),
                new Vector3( 0.46f,  0.30f, 0f),
                new Vector3( 0.46f, -0.05f, 0f),
                new Vector3( 0.36f, -0.20f, 0f),
                new Vector3( 0.14f, -0.20f, 0f),
                new Vector3( 0.19f, EngineBottomY, 0f),
                new Vector3(-0.19f, EngineBottomY, 0f),
                new Vector3(-0.14f, -0.20f, 0f),
                new Vector3(-0.36f, -0.20f, 0f),
                new Vector3(-0.46f, -0.05f, 0f),
                new Vector3(-0.46f,  0.30f, 0f),
                new Vector3(-0.34f,  0.30f, 0f),
                new Vector3(-0.34f,  0.50f, 0f),
            },
            new[]
            {
                // 0: marco de la cabina
                new Stroke(true,
                    new Vector3(-0.13f, 0.56f, 0f), new Vector3(0.13f, 0.56f, 0f),
                    new Vector3(0.09f, 0.41f, 0f), new Vector3(-0.09f, 0.41f, 0f)),
                // 1: banda superior
                new Stroke(false, new Vector3(-0.34f, 0.30f, 0f), new Vector3(0.34f, 0.30f, 0f)),
                // 2: banda central
                new Stroke(false, new Vector3(-0.46f, 0.10f, 0f), new Vector3(0.46f, 0.10f, 0f)),
                // 3: panel de control
                new Stroke(true,
                    new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.12f, 0.24f, 0f),
                    new Vector3(0.12f, 0.14f, 0f), new Vector3(-0.12f, 0.14f, 0f)),
                // 4: borde de la tobera
                new Stroke(false, new Vector3(-0.155f, -0.29f, 0f), new Vector3(0.155f, -0.29f, 0f)),
                // 5-6: antena
                new Stroke(false, new Vector3(0.08f, 0.66f, 0f), new Vector3(0.14f, 0.86f, 0f)),
                new Stroke(false,
                    new Vector3(0.07f, 0.82f, 0f), new Vector3(0.14f, 0.78f, 0f), new Vector3(0.21f, 0.82f, 0f)),
                // 7: travesaño de la cabina
                new Stroke(false, new Vector3(-0.11f, 0.485f, 0f), new Vector3(0.11f, 0.485f, 0f)),
                // 8: nervio de la tobera
                new Stroke(false, new Vector3(-0.165f, -0.335f, 0f), new Vector3(0.165f, -0.335f, 0f)),
                // 9: pilón central del motor
                new Stroke(false, new Vector3(0f, -0.20f, 0f), new Vector3(0f, -0.29f, 0f)),
                // 10: línea de panel
                new Stroke(false, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.14f, 0f)),
            },
            new[]
            {
                RoleHull, RoleHull, RoleHull, RoleAccent, RoleAccent, RoleAccent,
                RoleAccent, RoleHull, RoleAccent, RoleAccent, RoleAccent,
            },
            new[]
            {
                // 0-2: patas
                new Stroke(false, new Vector3(0.46f, 0.18f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false,
                    new Vector3(0.46f, -0.05f, 0f), new Vector3(0.68f, -0.22f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.70f, -0.56f, 0f), new Vector3(1.00f, -0.56f, 0f)),
                // 3: Propulsores RCS (el indicador de inclinación depende de este trazo: lazo con y=0.48)
                new Stroke(true,
                    new Vector3(0.34f, 0.48f, 0f), new Vector3(0.46f, 0.48f, 0f),
                    new Vector3(0.46f, 0.36f, 0f), new Vector3(0.34f, 0.36f, 0f)),
                // 4: puntal interior
                new Stroke(false, new Vector3(0.28f, 0.30f, 0f), new Vector3(0.28f, 0.10f, 0f)),
                // 5-6: ranuras de ventilación
                new Stroke(false, new Vector3(0.34f, 0.02f, 0f), new Vector3(0.42f, 0.02f, 0f)),
                new Stroke(false, new Vector3(0.34f, -0.04f, 0f), new Vector3(0.42f, -0.04f, 0f)),
                // 7: refuerzo del casco inferior
                new Stroke(false, new Vector3(0.36f, -0.20f, 0f), new Vector3(0.30f, 0.10f, 0f)),
                // 8: tapón de la pata
                new Stroke(false, new Vector3(0.85f, -0.56f, 0f), new Vector3(0.85f, -0.66f, 0f)),
            },
            new[]
            {
                RoleLeg, RoleLeg, RoleLeg, RoleHull, RoleAccent, RoleAccent,
                RoleAccent, RoleAccent, RoleAccent,
            },
            new Vector3(0.46f, 0.18f, 0f),
            0.485f, 0.085f, 0.11f);
    }
}
