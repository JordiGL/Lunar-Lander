using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderRendererFuture : VectorLanderRendererBase
    {
        public override LanderDesign Design => LanderDesign.Future;

        protected override LanderGeometry GetLanderGeometry() => FutureGeometry;

        private static readonly LanderGeometry FutureGeometry = new LanderGeometry(
            new[] {
                new Vector3( 0.000f,  0.700f, 0f), new Vector3( 0.198f,  0.669f, 0f),
                new Vector3( 0.373f,  0.578f, 0f), new Vector3( 0.502f,  0.440f, 0f),
                new Vector3( 0.571f,  0.270f, 0f), new Vector3( 0.571f,  0.090f, 0f),
                new Vector3( 0.502f, -0.080f, 0f), new Vector3( 0.373f, -0.218f, 0f),
                new Vector3( 0.180f, -0.280f, 0f), new Vector3( 0.210f, EngineBottomY, 0f),
                new Vector3(-0.210f, EngineBottomY, 0f), new Vector3(-0.180f, -0.280f, 0f),
                new Vector3(-0.373f, -0.218f, 0f), new Vector3(-0.502f, -0.080f, 0f),
                new Vector3(-0.571f,  0.090f, 0f), new Vector3(-0.571f,  0.270f, 0f),
                new Vector3(-0.502f,  0.440f, 0f), new Vector3(-0.373f,  0.578f, 0f),
                new Vector3(-0.198f,  0.669f, 0f),
            },
            new[] {
                new Stroke(true, new Vector3( 0.000f, 0.530f, 0f), new Vector3( 0.092f, 0.492f, 0f), new Vector3( 0.130f, 0.400f, 0f), new Vector3( 0.092f, 0.308f, 0f), new Vector3( 0.000f, 0.270f, 0f), new Vector3(-0.092f, 0.308f, 0f), new Vector3(-0.130f, 0.400f, 0f), new Vector3(-0.092f, 0.492f, 0f)),
                new Stroke(true, new Vector3( 0.000f, 0.470f, 0f), new Vector3( 0.049f, 0.449f, 0f), new Vector3( 0.070f, 0.400f, 0f), new Vector3( 0.049f, 0.351f, 0f), new Vector3( 0.000f, 0.330f, 0f), new Vector3(-0.049f, 0.351f, 0f), new Vector3(-0.070f, 0.400f, 0f), new Vector3(-0.049f, 0.449f, 0f)),
                new Stroke(false, new Vector3(-0.276f, 0.526f, 0f), new Vector3(-0.147f, 0.596f, 0f), new Vector3( 0.000f, 0.620f, 0f), new Vector3( 0.147f, 0.596f, 0f), new Vector3( 0.276f, 0.526f, 0f)),
                new Stroke(true, new Vector3( 0.000f,  0.070f, 0f), new Vector3( 0.064f,  0.044f, 0f), new Vector3( 0.090f, -0.020f, 0f), new Vector3( 0.064f, -0.084f, 0f), new Vector3( 0.000f, -0.110f, 0f), new Vector3(-0.064f, -0.084f, 0f), new Vector3(-0.090f, -0.020f, 0f), new Vector3(-0.064f,  0.044f, 0f)),
                new Stroke(false, new Vector3(0f, 0.070f, 0f), new Vector3(0f, -0.110f, 0f)),
                new Stroke(false, new Vector3(-0.090f, -0.020f, 0f), new Vector3(0.090f, -0.020f, 0f)),
                new Stroke(false, new Vector3(0f, -0.110f, 0f), new Vector3(0f, -0.300f, 0f)),
                new Stroke(false, new Vector3(-0.260f, -0.215f, 0f), new Vector3(-0.140f, -0.245f, 0f), new Vector3( 0.000f, -0.255f, 0f), new Vector3( 0.140f, -0.245f, 0f), new Vector3( 0.260f, -0.215f, 0f)),
                new Stroke(false, new Vector3(-0.175f, -0.300f, 0f), new Vector3(0.175f, -0.300f, 0f)),
                new Stroke(false, new Vector3(-0.190f, -0.345f, 0f), new Vector3(0.190f, -0.345f, 0f)),
                new Stroke(false, new Vector3(-0.140f, 0.840f, 0f), new Vector3(-0.080f, 0.770f, 0f), new Vector3( 0.000f, 0.745f, 0f), new Vector3( 0.080f, 0.770f, 0f), new Vector3( 0.140f, 0.840f, 0f)),
                new Stroke(false, new Vector3(0f, 0.700f, 0f), new Vector3(0f, 0.745f, 0f)),
                new Stroke(false, new Vector3(0f, 0.760f, 0f), new Vector3(0f, 0.860f, 0f)),
                new Stroke(false, new Vector3(0f, 0.620f, 0f), new Vector3(0f, 0.530f, 0f)),
                new Stroke(false, new Vector3(0f, 0.270f, 0f), new Vector3(0f, 0.070f, 0f)),
            },
            new[] { RoleHull, RoleAccent, RoleLight, RoleAccent, RoleLight, RoleLight, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleLight, RoleHull, RoleAccent },
            new[] {
                new Stroke(false, new Vector3(0.526f, -0.020f, 0f), new Vector3(0.80f, 0.02f, 0f), new Vector3(0.92f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.450f, -0.140f, 0f), new Vector3(0.78f, -0.30f, 0f), new Vector3(0.92f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.78f, -0.56f, 0f), new Vector3(1.06f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.82f, -0.56f, 0f), new Vector3(0.86f, -0.50f, 0f), new Vector3(0.98f, -0.50f, 0f), new Vector3(1.02f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.80f, 0.02f, 0f), new Vector3(0.78f, -0.30f, 0f)),
                new Stroke(true, new Vector3(0.34f, 0.48f, 0f), new Vector3(0.46f, 0.48f, 0f), new Vector3(0.46f, 0.36f, 0f), new Vector3(0.34f, 0.36f, 0f)),
                new Stroke(false, new Vector3(0.57f, 0.267f, 0f), new Vector3(0.66f, 0.258f, 0f), new Vector3(0.76f, 0.242f, 0f), new Vector3(0.86f, 0.200f, 0f), new Vector3(0.76f, 0.158f, 0f), new Vector3(0.66f, 0.142f, 0f), new Vector3(0.57f, 0.133f, 0f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleLeg, RoleHull, RoleAccent },
            new Vector3(0.526f, -0.02f, 0f),
            0.40f, 0.065f, 0.06f);
    }
}