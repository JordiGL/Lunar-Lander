using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LunarLander
{
    /// <summary>
    /// Base de los límites laterales de un Stage. Define dos zonas (izquierda y derecha)
    /// justo antes del borde del mapa:
    ///
    ///   borde del mapa | inset | OUTER ... zoneWidth ... INNER | zona de juego
    ///
    /// - depth01 = 0 en el borde interior (INNER) y 1 en el exterior (OUTER).
    /// - Los primeros 'effectThreshold' de la zona solo son AVISO visual; a partir de ahí
    ///   se llama a ApplyEffect() con la profundidad ya reescalada a 0..1.
    /// - Si 'hardWall' está activo, la nave no puede pasar de OUTER (garantiza el límite).
    ///
    /// Las clases hijas deciden QUÉ se dibuja (BuildVisuals / UpdateVisuals) y
    /// QUÉ le pasa a la nave (ApplyEffect).
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class StageBoundaryBase : MonoBehaviour
    {
        public const int Left = -1;
        public const int Right = 1;

        // Ancho extra tras el muro exterior para tapar el vacío (si coverBeyond).
        protected const float CoverCapWidth = 150f;

        [Header("Referencias")]
        [Tooltip("Si se deja vacío se busca en la escena.")]
        [SerializeField] private LanderController lander;
        [Tooltip("Si se deja vacío usa la cámara principal.")]
        [SerializeField] private Camera targetCamera;

        [Header("Zona límite")]
        [Tooltip("Ancho de cada zona lateral (aviso + efecto).")]
        [SerializeField, Min(2f)] protected float zoneWidth = 14f;
        [Tooltip("Distancia entre el borde real del terreno y el muro exterior de la zona.")]
        [SerializeField, Min(0f)] protected float edgeInset = 6f;
        [Tooltip("Fracción inicial de la zona que solo avisa (sin efecto sobre la nave).")]
        [SerializeField, Range(0f, 0.9f)] protected float effectThreshold = 0.25f;
        [Tooltip("Impide físicamente que la nave pase del borde exterior de la zona.")]
        [SerializeField] protected bool hardWall = true;
        [SerializeField, Range(0f, 1f)] protected float wallBounce = 0.15f;

        [Header("Visual")]
        [Tooltip("Debe ser mayor que el de la nave (10) para que la cubra.")]
        [SerializeField] protected int sortingOrder = 20;
        [Tooltip("Intensidad visual en el borde interior de la zona (0..1).")]
        [SerializeField, Range(0f, 1f)] protected float idleIntensity = 0.2f;
        [Tooltip("Cuánto sobrepasa la altura de la cámara (evita ver huecos arriba/abajo).")]
        [SerializeField, Min(1f)] protected float verticalCoverage = 1.2f;

        [Header("Cámara")]
        [Tooltip("Margen que puede asomarse la cámara más allá del muro (ver ClampCameraX).")]
        [SerializeField, Min(0f)] protected float cameraEdgeSlack = 2f;

        // ------------------------------------------------------------------
        // Tipos internos
        // ------------------------------------------------------------------

        /// <summary>Franja con degradado de alfa (0 en el borde interior, 1 en el exterior).</summary>
        protected sealed class Strip
        {
            public Transform tr;
            public Material material;
            public int side;
            public float innerX;

            public void SetIntensity(float a)
            {
                material.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------

        private TerrainBase terrain;
        private Rigidbody2D body;
        private Camera cam;
        private GameObject root;
        private Material lineMaterial;
        private bool ready;
        private bool landerSubscribed;
        private float minX, maxX;
        private float leftDepth, rightDepth;

        private readonly List<Strip> strips = new List<Strip>();
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>Límite X jugable izquierdo (muro exterior de la zona izquierda).</summary>
        public float PlayableMinX => ZoneOuter(Left);

        /// <summary>Límite X jugable derecho (muro exterior de la zona derecha).</summary>
        public float PlayableMaxX => ZoneOuter(Right);

        /// <summary>
        /// Distancia (unidades) desde el borde del mapa hasta el borde interior de la zona (edgeInset + zoneWidth).
        /// El StageManager se la pasa al terreno antes de generarlo para que no coloque plataformas ahí dentro.
        /// </summary>
        public float ReservedEdgeMargin => edgeInset + zoneWidth;

        /// <summary>Profundidad actual de la nave en la zona (0..1), útil para un aviso en el HUD.</summary>
        public float CurrentDepth => Mathf.Max(leftDepth, rightDepth);

        /// <summary>Lado en el que está la nave: -1 izquierda, 1 derecha, 0 ninguno.</summary>
        public int CurrentSide => CurrentDepth <= 0f ? 0 : (leftDepth >= rightDepth ? Left : Right);

        /// <summary>
        /// Prepara el límite para un terreno ya generado. Lo llama el StageManager
        /// cada vez que se activa el stage (el terreno se regenera).
        /// </summary>
        public void Setup(TerrainBase newTerrain)
        {
            terrain = newTerrain;
            EnsureReferences();

            if (terrain == null || !terrain.HasBounds)
            {
                Debug.LogWarning($"{GetType().Name}: el terreno no está generado o no es válido.");
                ready = false;
                return;
            }

            minX = terrain.PlayableMinX;
            maxX = terrain.PlayableMaxX;
            leftDepth = rightDepth = 0f;

            RebuildVisuals();
            ValidatePads();
            ready = true;
        }

        /// <summary>
        /// Red de seguridad: avisa si alguna plataforma queda dentro de la zona de un límite
        /// (por ejemplo si el terreno se generó sin pasar por el StageManager).
        /// </summary>
        private void ValidatePads()
        {
            float safeMin = ZoneInner(Left);
            float safeMax = ZoneInner(Right);
            var pads = terrain.LandingPads;

            for (int i = 0; i < pads.Count; i++)
            {
                float x0 = terrain.transform.TransformPoint(pads[i].startPoint).x;
                float x1 = terrain.transform.TransformPoint(pads[i].endPoint).x;
                if (Mathf.Min(x0, x1) < safeMin || Mathf.Max(x0, x1) > safeMax)
                {
                    Debug.LogWarning($"{GetType().Name}: la plataforma {i} ({pads[i].multiplier}x) queda dentro de la zona del límite " +
                                     "y sería difícil aterrizar en ella. ¿Se asignó el límite al StageData y se generó el terreno desde el StageManager?");
                }
            }
        }

        /// <summary>
        /// Ajusta la X de la cámara para que su vista no se aleje del muro exterior.
        /// Úsalo en tu script de seguimiento de cámara: x = boundary.ClampCameraX(x, halfWidth).
        /// </summary>
        public float ClampCameraX(float x, float halfViewWidth)
        {
            if (!ready) return x;
            float lo = PlayableMinX + halfViewWidth - cameraEdgeSlack;
            float hi = PlayableMaxX - halfViewWidth + cameraEdgeSlack;
            if (lo > hi) return (PlayableMinX + PlayableMaxX) * 0.5f;
            return Mathf.Clamp(x, lo, hi);
        }

        // ------------------------------------------------------------------
        // Contrato para las clases hijas
        // ------------------------------------------------------------------

        /// <summary>Crea las franjas y efectos visuales (usar CreateStrip / CreateLine).</summary>
        protected abstract void BuildVisuals();

        /// <summary>Se llama cada frame (LateUpdate) con la profundidad visual de cada lado (0..1).</summary>
        protected abstract void UpdateVisuals(float time, float leftDepth01, float rightDepth01);

        /// <summary>
        /// Efecto sobre la nave (se llama en FixedUpdate mientras esté dentro de la zona de efecto).
        /// depth01 ya está reescalado: 0 al empezar el efecto, 1 en el muro exterior.
        /// side: -1 zona izquierda, 1 zona derecha (también es la dirección "hacia fuera").
        /// </summary>
        protected abstract void ApplyEffect(Rigidbody2D landerBody, float depth01, int side, float dt);

        /// <summary>Se llama cuando la nave reaparece.</summary>
        protected virtual void OnLanderReset() { }

        // ------------------------------------------------------------------
        // Utilidades para las hijas
        // ------------------------------------------------------------------

        protected LanderController Lander => lander;

        protected static int SideIndex(int side) => side < 0 ? 0 : 1;

        /// <summary>Borde interior de la zona (donde empieza el aviso).</summary>
        protected float ZoneInner(int side) =>
            side < 0 ? minX + edgeInset + zoneWidth : maxX - edgeInset - zoneWidth;

        /// <summary>Borde exterior de la zona (el muro).</summary>
        protected float ZoneOuter(int side) =>
            side < 0 ? minX + edgeInset : maxX - edgeInset;

        protected Camera Cam
        {
            get
            {
                if (cam == null) cam = targetCamera != null ? targetCamera : Camera.main;
                return cam;
            }
        }

        protected float CameraY => Cam != null ? Cam.transform.position.y : 0f;

        protected float CameraHeight =>
            (Cam != null && Cam.orthographic ? Cam.orthographicSize * 2f : 40f) * verticalCoverage;

        protected float CameraHalfWidth =>
            Cam != null && Cam.orthographic ? Cam.orthographicSize * Cam.aspect : 25f;

        /// <summary>True si la zona del lado indicado entra (aunque sea parcialmente) en pantalla.</summary>
        protected bool IsSideVisible(int side)
        {
            if (Cam == null) return true;
            float cx = Cam.transform.position.x;
            float half = CameraHalfWidth;
            return side < 0 ? cx - half < ZoneInner(side) : cx + half > ZoneInner(side);
        }

        protected static Shader SpriteShader()
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s;
        }

        protected Transform Root => root != null ? root.transform : null;

        /// <summary>
        /// Crea una franja con degradado: alfa 0 en el borde interior y 'peakAlpha' en el exterior.
        /// Con coverBeyond la franja continúa opaca más allá del muro para tapar el vacío.
        /// </summary>
        protected Strip CreateStrip(string name, int side, Color color, float peakAlpha, bool coverBeyond, float rampPower = 2f)
        {
            float cap = coverBeyond ? CoverCapWidth / zoneWidth : 0f;
            float[] xs = coverBeyond ? new[] { 0f, 0.5f, 1f, 1f + cap } : new[] { 0f, 0.5f, 1f };
            int cols = xs.Length;

            // Textura de degradado horizontal: alfa 0 en el borde interior, peakAlpha en el exterior.
            // La curva t^rampPower arranca con pendiente 0, así que no hay borde visible.
            const int texW = 128;
            var px = new Color32[texW];
            for (int i = 0; i < texW; i++)
            {
                float y = i / (float)(texW - 1);
                float a = Mathf.Pow(y, Mathf.Max(0.5f, rampPower)) * peakAlpha;
                px[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            var tex = new Texture2D(texW, 1, TextureFormat.RGBA32, false)
            {
                name = "Boundary_" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            ownedTextures.Add(tex);

            var verts = new Vector3[cols * 2];
            var uvs = new Vector2[cols * 2];
            for (int c = 0; c < cols; c++)
            {
                float u = Mathf.Min(xs[c], 1f);
                verts[c * 2] = new Vector3(xs[c], -0.5f, 0f);
                verts[c * 2 + 1] = new Vector3(xs[c], 0.5f, 0f);
                uvs[c * 2] = new Vector2(u, 0f);
                uvs[c * 2 + 1] = new Vector2(u, 1f);
            }

            var tris = new int[(cols - 1) * 6];
            int t = 0;
            for (int c = 0; c < cols - 1; c++)
            {
                int a = c * 2, b = a + 1, cb = (c + 1) * 2, d = cb + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = d;
                tris[t++] = a; tris[t++] = d; tris[t++] = cb;
            }

            var mesh = new Mesh { name = "Boundary_" + name, vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);

            var go = new GameObject("Boundary_" + name + (side < 0 ? "_L" : "_R"));
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mat = new Material(SpriteShader()) { name = "Boundary_" + name, mainTexture = tex };
            ownedMaterials.Add(mat);

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var strip = new Strip { tr = go.transform, material = mat, side = side, innerX = ZoneInner(side) };
            strips.Add(strip);
            return strip;
        }

        /// <summary>Crea un LineRenderer en coordenadas de mundo (se coloca desde UpdateVisuals).</summary>
        protected LineRenderer CreateLine(string name, int pointCount, float width, int order)
        {
            if (lineMaterial == null)
            {
                lineMaterial = new Material(SpriteShader()) { name = "Boundary_Line" };
                ownedMaterials.Add(lineMaterial);
            }

            var go = new GameObject("Boundary_" + name);
            go.transform.SetParent(root.transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.positionCount = pointCount;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.sortingOrder = order;
            lr.sharedMaterial = lineMaterial;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            if (root != null) root.SetActive(true);
            SubscribeLander();
        }

        protected virtual void OnDisable()
        {
            if (root != null) root.SetActive(false);
            UnsubscribeLander();
        }

        protected virtual void OnDestroy()
        {
            DestroyVisuals();
        }

        private void FixedUpdate()
        {
            if (!ready || lander == null || body == null || !body.simulated) return;

            Vector2 pos = body.position;
            float dl = DepthAt(pos.x, Left);
            float dr = DepthAt(pos.x, Right);

            if (dl > 0f) ApplyScaledEffect(dl, Left);
            if (dr > 0f) ApplyScaledEffect(dr, Right);

            if (hardWall) ClampToWall();
        }

        private void ApplyScaledEffect(float rawDepth, int side)
        {
            if (rawDepth <= effectThreshold) return;
            float d = Mathf.Clamp01((rawDepth - effectThreshold) / (1f - effectThreshold));
            ApplyEffect(body, d, side, Time.fixedDeltaTime);
        }

        private void ClampToWall()
        {
            Vector2 pos = body.position;
            Vector2 vel = body.velocity;
            bool changed = false;

            float wallL = ZoneOuter(Left);
            float wallR = ZoneOuter(Right);

            if (pos.x < wallL)
            {
                pos.x = wallL;
                if (vel.x < 0f) vel.x = -vel.x * wallBounce;
                changed = true;
            }
            else if (pos.x > wallR)
            {
                pos.x = wallR;
                if (vel.x > 0f) vel.x = -vel.x * wallBounce;
                changed = true;
            }

            if (changed)
            {
                body.position = pos;
                body.velocity = vel;
            }
        }

        private void LateUpdate()
        {
            if (!ready || root == null) return;

            float lx = lander != null ? lander.transform.position.x : (minX + maxX) * 0.5f;
            leftDepth = DepthAt(lx, Left);
            rightDepth = DepthAt(lx, Right);

            // Las franjas siguen a la cámara en vertical para cubrir siempre la pantalla.
            float camY = CameraY;
            float h = CameraHeight;
            for (int i = 0; i < strips.Count; i++)
            {
                Strip s = strips[i];
                s.tr.position = new Vector3(s.innerX, camY, 0f);
                s.tr.localScale = new Vector3(s.side * zoneWidth, h, 1f);
            }

            UpdateVisuals(Time.time, leftDepth, rightDepth);
        }

        // ------------------------------------------------------------------
        // Internos
        // ------------------------------------------------------------------

        private float DepthAt(float x, int side)
        {
            return side < 0
                ? Mathf.Clamp01((ZoneInner(Left) - x) / zoneWidth)
                : Mathf.Clamp01((x - ZoneInner(Right)) / zoneWidth);
        }

        private void EnsureReferences()
        {
            if (lander == null) lander = FindFirstObjectByType<LanderController>();
            if (lander != null && body == null) body = lander.GetComponent<Rigidbody2D>();
        }

        private void SubscribeLander()
        {
            if (landerSubscribed) return;
            EnsureReferences();
            if (lander == null) return;
            lander.OnReset += HandleLanderReset;
            landerSubscribed = true;
        }

        private void UnsubscribeLander()
        {
            if (!landerSubscribed || lander == null) return;
            lander.OnReset -= HandleLanderReset;
            landerSubscribed = false;
        }

        private void HandleLanderReset()
        {
            leftDepth = rightDepth = 0f;
            OnLanderReset();
        }

        private void RebuildVisuals()
        {
            DestroyVisuals();
            root = new GameObject($"StageBoundary_Root_{gameObject.name}");
            root.SetActive(isActiveAndEnabled);
            BuildVisuals();
        }

        private void DestroyVisuals()
        {
            strips.Clear();

            for (int i = 0; i < ownedMeshes.Count; i++) if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
            for (int i = 0; i < ownedMaterials.Count; i++) if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
            for (int i = 0; i < ownedTextures.Count; i++) if (ownedTextures[i] != null) Destroy(ownedTextures[i]);
            ownedMeshes.Clear();
            ownedMaterials.Clear();
            ownedTextures.Clear();
            lineMaterial = null;

            if (root != null)
            {
                Destroy(root);
                root = null;
            }
        }
    }
}