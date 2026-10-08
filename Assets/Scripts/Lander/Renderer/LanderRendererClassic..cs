using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderRendererClassic : VectorLanderRendererBase
    {
        public override LanderDesign Design => LanderDesign.Classic;

        protected override LanderGeometry GetLanderGeometry() => ClassicGeometry;

        private static readonly LanderGeometry ClassicGeometry = new LanderGeometry(
            new[] {
                new Vector3(-0.20f,  0.66f, 0f), new Vector3( 0.20f,  0.66f, 0f),
                new Vector3( 0.34f,  0.50f, 0f), new Vector3( 0.34f,  0.30f, 0f),
                new Vector3( 0.46f,  0.30f, 0f), new Vector3( 0.46f, -0.05f, 0f),
                new Vector3( 0.36f, -0.20f, 0f), new Vector3( 0.14f, -0.20f, 0f),
                new Vector3( 0.19f, EngineBottomY, 0f), new Vector3(-0.19f, EngineBottomY, 0f),
                new Vector3(-0.14f, -0.20f, 0f), new Vector3(-0.36f, -0.20f, 0f),
                new Vector3(-0.46f, -0.05f, 0f), new Vector3(-0.46f,  0.30f, 0f),
                new Vector3(-0.34f,  0.30f, 0f), new Vector3(-0.34f,  0.50f, 0f),
            },
            new[] {
                new Stroke(true, new Vector3(-0.13f, 0.56f, 0f), new Vector3(0.13f, 0.56f, 0f), new Vector3(0.09f, 0.41f, 0f), new Vector3(-0.09f, 0.41f, 0f)),
                new Stroke(false, new Vector3(-0.34f, 0.30f, 0f), new Vector3(0.34f, 0.30f, 0f)),
                new Stroke(false, new Vector3(-0.46f, 0.10f, 0f), new Vector3(0.46f, 0.10f, 0f)),
                new Stroke(true, new Vector3(-0.12f, 0.24f, 0f), new Vector3(0.12f, 0.24f, 0f), new Vector3(0.12f, 0.14f, 0f), new Vector3(-0.12f, 0.14f, 0f)),
                new Stroke(false, new Vector3(-0.155f, -0.29f, 0f), new Vector3(0.155f, -0.29f, 0f)),
                new Stroke(false, new Vector3(0.08f, 0.66f, 0f), new Vector3(0.14f, 0.86f, 0f)),
                new Stroke(false, new Vector3(0.07f, 0.82f, 0f), new Vector3(0.14f, 0.78f, 0f), new Vector3(0.21f, 0.82f, 0f)),
                new Stroke(false, new Vector3(-0.11f, 0.485f, 0f), new Vector3(0.11f, 0.485f, 0f)),
                new Stroke(false, new Vector3(-0.165f, -0.335f, 0f), new Vector3(0.165f, -0.335f, 0f)),
                new Stroke(false, new Vector3(0f, -0.20f, 0f), new Vector3(0f, -0.29f, 0f)),
                new Stroke(false, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.14f, 0f)),
            },
            new[] { RoleHull, RoleHull, RoleHull, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleHull, RoleAccent, RoleAccent, RoleAccent },
            new[] {
                new Stroke(false, new Vector3(0.46f, 0.18f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.46f, -0.05f, 0f), new Vector3(0.68f, -0.22f, 0f), new Vector3(0.85f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.70f, -0.56f, 0f), new Vector3(1.00f, -0.56f, 0f)),
                new Stroke(true, new Vector3(0.34f, 0.48f, 0f), new Vector3(0.46f, 0.48f, 0f), new Vector3(0.46f, 0.36f, 0f), new Vector3(0.34f, 0.36f, 0f)),
                new Stroke(false, new Vector3(0.28f, 0.30f, 0f), new Vector3(0.28f, 0.10f, 0f)),
                new Stroke(false, new Vector3(0.34f, 0.02f, 0f), new Vector3(0.42f, 0.02f, 0f)),
                new Stroke(false, new Vector3(0.34f, -0.04f, 0f), new Vector3(0.42f, -0.04f, 0f)),
                new Stroke(false, new Vector3(0.36f, -0.20f, 0f), new Vector3(0.30f, 0.10f, 0f)),
                new Stroke(false, new Vector3(0.85f, -0.56f, 0f), new Vector3(0.85f, -0.66f, 0f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleHull, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent },
            new Vector3(0.46f, 0.18f, 0f),
            0.485f, 0.085f, 0.11f);
    }
}