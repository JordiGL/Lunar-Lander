using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderRendererHex : VectorLanderRendererBase
    {
        public override LanderDesign Design => LanderDesign.Hex;

        protected override LanderGeometry GetLanderGeometry() => HexGeometry;

        private static readonly LanderGeometry HexGeometry = new LanderGeometry(
            // Contorno del casco: facetado, angular
            new[] {
                new Vector3(-0.12f,  0.68f, 0f), new Vector3( 0.12f,  0.68f, 0f),
                new Vector3( 0.32f,  0.52f, 0f), new Vector3( 0.48f,  0.36f, 0f),
                new Vector3( 0.48f,  0.00f, 0f), new Vector3( 0.40f, -0.16f, 0f),
                new Vector3( 0.16f, -0.22f, 0f), new Vector3( 0.20f, EngineBottomY, 0f),
                new Vector3(-0.20f, EngineBottomY, 0f), new Vector3(-0.16f, -0.22f, 0f),
                new Vector3(-0.40f, -0.16f, 0f), new Vector3(-0.48f,  0.00f, 0f),
                new Vector3(-0.48f,  0.36f, 0f), new Vector3(-0.32f,  0.52f, 0f),
            },
            // Detalles del cuerpo
            new[] {
                // Ventana hexagonal
                new Stroke(true, new Vector3(-0.09f, 0.54f, 0f), new Vector3(0.09f, 0.54f, 0f), new Vector3(0.16f, 0.44f, 0f), new Vector3(0.09f, 0.34f, 0f), new Vector3(-0.09f, 0.34f, 0f), new Vector3(-0.16f, 0.44f, 0f)),
                // Aristas diagonales hacia la ventana
                new Stroke(false, new Vector3(-0.32f, 0.52f, 0f), new Vector3(-0.16f, 0.44f, 0f)),
                new Stroke(false, new Vector3( 0.32f, 0.52f, 0f), new Vector3( 0.16f, 0.44f, 0f)),
                // Bandas horizontales
                new Stroke(false, new Vector3(-0.48f, 0.20f, 0f), new Vector3(0.48f, 0.20f, 0f)),
                new Stroke(false, new Vector3(-0.48f, 0.08f, 0f), new Vector3(0.48f, 0.08f, 0f)),
                // Depósito central
                new Stroke(true, new Vector3(-0.14f, 0.02f, 0f), new Vector3(0.14f, 0.02f, 0f), new Vector3(0.14f, -0.10f, 0f), new Vector3(-0.14f, -0.10f, 0f)),
                // Conectores verticales
                new Stroke(false, new Vector3(0f, 0.34f, 0f), new Vector3(0f, 0.20f, 0f)),
                new Stroke(false, new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.02f, 0f)),
                new Stroke(false, new Vector3(0f, -0.10f, 0f), new Vector3(0f, -0.20f, 0f)),
                // Tobera
                new Stroke(false, new Vector3(-0.155f, -0.29f, 0f), new Vector3(0.155f, -0.29f, 0f)),
                new Stroke(false, new Vector3(-0.17f, -0.335f, 0f), new Vector3(0.17f, -0.335f, 0f)),
                // Antena con baliza en rombo
                new Stroke(false, new Vector3(0f, 0.68f, 0f), new Vector3(0f, 0.84f, 0f)),
                new Stroke(true, new Vector3(0f, 0.84f, 0f), new Vector3(0.05f, 0.89f, 0f), new Vector3(0f, 0.94f, 0f), new Vector3(-0.05f, 0.89f, 0f)),
            },
            new[] { RoleLight, RoleAccent, RoleAccent, RoleHull, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleAccent, RoleHull, RoleLight },
            // Patas (lado derecho)
            new[] {
                new Stroke(false, new Vector3(0.48f, 0.12f, 0f), new Vector3(0.84f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.48f, -0.02f, 0f), new Vector3(0.66f, -0.24f, 0f), new Vector3(0.84f, -0.56f, 0f)),
                new Stroke(false, new Vector3(0.68f, -0.56f, 0f), new Vector3(1.00f, -0.56f, 0f)),
                // Propulsor RCS
                new Stroke(true, new Vector3(0.48f, 0.34f, 0f), new Vector3(0.58f, 0.34f, 0f), new Vector3(0.58f, 0.24f, 0f), new Vector3(0.48f, 0.24f, 0f)),
                // Detalle del pie
                new Stroke(false, new Vector3(0.73f, -0.56f, 0f), new Vector3(0.77f, -0.50f, 0f), new Vector3(0.91f, -0.50f, 0f), new Vector3(0.95f, -0.56f, 0f)),
            },
            new[] { RoleLeg, RoleLeg, RoleLeg, RoleHull, RoleAccent },
            new Vector3(0.48f, 0.12f, 0f),
            0.45f, 0.09f, 0.08f);
    }
}