using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderRendererModern : VectorLanderRendererBase
    {
        public override string DesignName => "Modern";

        protected override LanderGeometry GetLanderGeometry() => ModernGeometry;

        private static readonly LanderGeometry ModernGeometry = new LanderGeometry(
            new[] {
                new Vector3(-0.14f,  0.70f, 0f), new Vector3( 0.14f,  0.70f, 0f),
                new Vector3( 0.30f,  0.54f, 0f), new Vector3( 0.34f,  0.34f, 0f),
                new Vector3( 0.50f,  0.26f, 0f), new Vector3( 0.50f, -0.06f, 0f),
                new Vector3( 0.38f, -0.22f, 0f), new Vector3( 0.16f, -0.22f, 0f),
                new Vector3( 0.20f, EngineBottomY, 0f), new Vector3(-0.20f, EngineBottomY, 0f),
                new Vector3(-0.16f, -0.22f, 0f), new Vector3(-0.38f, -0.22f, 0f),
                new Vector3(-0.50f, -0.06f, 0f), new Vector3(-0.50f,  0.26f, 0f),
                new Vector3(-0.34f,  0.34f, 0f), new Vector3(-0.30f,  0.54f, 0f),
            },
            new[] {
                new Stroke(true, new Vector3(-0.10f, 0.58f, 0f), new Vector3(0.10f, 0.58f, 0f), new Vector3(0.15f, 0.48f, 0f), new Vector3(0.08f, 0.38f, 0f), new Vector3(-0.08f, 0.38f, 0f), new Vector3(-0.15f, 0.48f, 0f)),
                new Stroke(false, new Vector3(0f, 0.70f, 0f), new Vector3(0f, 0.58f, 0f)),
                new Stroke(true, new Vector3(-0.10f, 0.64f, 0f), new Vector3(0.10f, 0.64f, 0f), new Vector3(0.25f, 0.50f, 0f), new Vector3(0.29f, 0.31f, 0f), new Vector3(0.45f, 0.23f, 0f), new Vector3(0.45f, -0.05f, 0f), new Vector3(0.36f, -0.18f, 0f), new Vector3(0.12f, -0.18f, 0f), new Vector3(-0.12f, -0.18f, 0f), new Vector3(-0.36f, -0.18f, 0f), new Vector3(-0.45f, -0.05f, 0f), new Vector3(-0.45f, 0.23f, 0f), new Vector3(-0.29f, 0.31f, 0f), new Vector3(-0.25f, 0.50f, 0f)),
                new Stroke(false, new Vector3(-0.14f, 0.30f, 0f), new Vector3(0f, 0.22f, 0f), new Vector3(0.14f, 0.30f, 0f)),
                new Stroke(false, new Vector3(-0.14f, 0.22f, 0f), new Vector3(0f, 0.14f, 0f), new Vector3(0.14f, 0.22f, 0f)),
                new Stroke(true, new Vector3(-0.10f, 0f, 0f), new Vector3(-0.05f, 0.085f, 0f), new Vector3(0.05f, 0.085f, 0f), new Vector3(0.10f, 0f, 0f), new Vector3(0.05f, -0.085f, 0f), new Vector3(-0.05f, -0.085f, 0f)),
                new Stroke(false, new Vector3(-0.05f, 0f, 0f), new Vector3(0.05f, 0f, 0f)),
                new Stroke(false, new Vector3(0f, 0.14f, 0f), new Vector3(0f, 0.085f, 0f)),
                new Stroke(false, new Vector3(0f, -0.085f, 0f), new Vector3(0f, -0.20f, 0f)),
                new Stroke(false, new Vector3(-0.155f, -0.29f, 0f), new Vector3(0.155f, -0.29f, 0f)),
                new Stroke(false, new Vector3(-0.17f, -0.335f, 0f), new Vector3(0.17f, -0.335f, 0f)),
                new Stroke(false, new Vector3(0.10f, 0.70f, 0f), new Vector3(0.16f, 0.92f, 0f)),
                new Stroke(true, new Vector3(0.16f, 0.92f, 0f), new Vector3(0.19f, 0.96f, 0f), new Vector3(0.16f, 1.00f, 0f), new Vector3(0.13f, 0.96f, 0f)),
            },
            new[] { RoleHull, RoleHull, RoleLight, RoleAccent, RoleAccent, RoleAccent, RoleLight, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent },
            new[] {
                new Stroke(false, new Vector3(0.50f, 0.18f, 0f), new Vector3(0.70f, -0.14f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.50f, -0.06f, 0f), new Vector3(0.66f, -0.26f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.68f, -0.56f, 0f), new Vector3(1.02f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.60f, 0.02f, 0f), new Vector3(0.58f, -0.16f, 0f)),
                new Stroke(false, new Vector3(0.72f, -0.56f, 0f), new Vector3(0.78f, -0.50f, 0f), new Vector3(0.92f, -0.50f, 0f), new Vector3(0.98f, -0.56f, 0f)),
                new Stroke(true, new Vector3(0.34f, 0.48f, 0f), new Vector3(0.46f, 0.48f, 0f), new Vector3(0.46f, 0.36f, 0f), new Vector3(0.34f, 0.36f, 0f)),
                new Stroke(false, new Vector3(0.14f, 0.70f, 0f), new Vector3(0.10f, 0.58f, 0f)),
                new Stroke(false, new Vector3(0.30f, 0.22f, 0f), new Vector3(0.42f, 0.22f, 0f)),
                new Stroke(false, new Vector3(0.30f, 0.17f, 0f), new Vector3(0.42f, 0.17f, 0f)),
                new Stroke(false, new Vector3(0.30f, 0.12f, 0f), new Vector3(0.42f, 0.12f, 0f)),
                new Stroke(false, new Vector3(0.07f, -0.20f, 0f), new Vector3(0.05f, -0.38f, 0f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleHull, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent },
            new Vector3(0.50f, 0.18f, 0f),
            0.48f, 0.11f, 0.10f);
    }
}