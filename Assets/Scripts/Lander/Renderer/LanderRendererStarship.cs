using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Lander estilo Starship: cilindro alto y esbelto de acero, morro ojival,
    /// flaps delanteros y traseros, tres motores Raptor y patas de aterrizaje.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanderRendererStarship : VectorLanderRendererBase
    {
        public override string DesignName => "Starship";

        protected override LanderGeometry GetLanderGeometry() => StarshipGeometry;

        private static readonly LanderGeometry StarshipGeometry = new LanderGeometry(
            // ---- Casco (cilindro + morro ojival + falda de motores) ----
            new[] {
                new Vector3( 0.000f,  1.000f, 0f), new Vector3( 0.060f,  0.950f, 0f),
                new Vector3( 0.110f,  0.860f, 0f), new Vector3( 0.150f,  0.750f, 0f),
                new Vector3( 0.175f,  0.620f, 0f), new Vector3( 0.180f,  0.550f, 0f),
                new Vector3( 0.180f, -0.140f, 0f), new Vector3( 0.200f, -0.220f, 0f),
                new Vector3( 0.220f, EngineBottomY, 0f), new Vector3(-0.220f, EngineBottomY, 0f),
                new Vector3(-0.200f, -0.220f, 0f), new Vector3(-0.180f, -0.140f, 0f),
                new Vector3(-0.180f,  0.550f, 0f), new Vector3(-0.175f,  0.620f, 0f),
                new Vector3(-0.150f,  0.750f, 0f), new Vector3(-0.110f,  0.860f, 0f),
                new Vector3(-0.060f,  0.950f, 0f),
            },
            // ---- Detalles ----
            new[] {
                // 0-2: soldaduras entre secciones
                new Stroke(false, new Vector3(-0.18f, 0.33f, 0f), new Vector3(0.18f, 0.33f, 0f)),
                new Stroke(false, new Vector3(-0.16f, 0.69f, 0f), new Vector3(0.16f, 0.69f, 0f)),
                new Stroke(false, new Vector3(-0.18f, -0.12f, 0f), new Vector3(0.18f, -0.12f, 0f)),
                // 3-4: flaps delanteros
                new Stroke(true, new Vector3( 0.18f, 0.56f, 0f), new Vector3( 0.34f, 0.50f, 0f), new Vector3( 0.36f, 0.40f, 0f), new Vector3( 0.18f, 0.44f, 0f)),
                new Stroke(true, new Vector3(-0.18f, 0.56f, 0f), new Vector3(-0.34f, 0.50f, 0f), new Vector3(-0.36f, 0.40f, 0f), new Vector3(-0.18f, 0.44f, 0f)),
                // 5-6: flaps traseros (más grandes)
                new Stroke(true, new Vector3( 0.18f, 0.00f, 0f), new Vector3( 0.40f, -0.04f, 0f), new Vector3( 0.42f, -0.16f, 0f), new Vector3( 0.18f, -0.12f, 0f)),
                new Stroke(true, new Vector3(-0.18f, 0.00f, 0f), new Vector3(-0.40f, -0.04f, 0f), new Vector3(-0.42f, -0.16f, 0f), new Vector3(-0.18f, -0.12f, 0f)),
                // 7-9: tres motores Raptor
                new Stroke(true, new Vector3(-0.150f, EngineBottomY, 0f), new Vector3(-0.090f, EngineBottomY, 0f), new Vector3(-0.075f, EngineBottomY - 0.06f, 0f), new Vector3(-0.165f, EngineBottomY - 0.06f, 0f)),
                new Stroke(true, new Vector3(-0.030f, EngineBottomY, 0f), new Vector3( 0.030f, EngineBottomY, 0f), new Vector3( 0.045f, EngineBottomY - 0.06f, 0f), new Vector3(-0.045f, EngineBottomY - 0.06f, 0f)),
                new Stroke(true, new Vector3( 0.090f, EngineBottomY, 0f), new Vector3( 0.150f, EngineBottomY, 0f), new Vector3( 0.165f, EngineBottomY - 0.06f, 0f), new Vector3( 0.075f, EngineBottomY - 0.06f, 0f)),
                // 10: unión del morro
                new Stroke(false, new Vector3(-0.11f, 0.86f, 0f), new Vector3(0.11f, 0.86f, 0f)),
                // 11: baliza del morro
                new Stroke(false, new Vector3(0f, 1.00f, 0f), new Vector3(0f, 1.07f, 0f)),
                // 12: borde de la falda de motores
                new Stroke(false, new Vector3(-0.20f, -0.22f, 0f), new Vector3(0.20f, -0.22f, 0f)),
            },
            new[] {
                RoleHull, RoleHull, RoleHull,
                RoleHull, RoleHull, RoleHull, RoleHull,
                RoleAccent, RoleAccent, RoleAccent,
                RoleAccent, RoleLight, RoleAccent,
            },
            // ---- Patas (lado derecho; el otro se espeja) ----
            new[] {
                new Stroke(false, new Vector3(0.20f, -0.18f, 0f), new Vector3(0.60f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.22f, EngineBottomY, 0f), new Vector3(0.60f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.46f, -0.56f, 0f), new Vector3(0.74f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.50f, -0.56f, 0f), new Vector3(0.54f, -0.51f, 0f), new Vector3(0.66f, -0.51f, 0f), new Vector3(0.70f, -0.56f, 0f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleLeg },
            new Vector3(0.20f, -0.18f, 0f),
            0.36f, 0.07f, 0.08f);
    }
}