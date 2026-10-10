using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Controlador central del flujo de juego.
    /// Suscribe los eventos del LanderController, gestiona el avance o reinicio de niveles/pantallas,
    /// calcula la puntuación según el multiplicador del terreno y comunica el estado al HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Referencias del Sistema")]
        [SerializeField] private LanderController lander;
        [SerializeField] private StandardHUD hud;

        [Tooltip("Gestor de pantallas/niveles (cada una con su Terreno y SpaceBackground).")]
        [SerializeField] private StageManager stageManager;

        [Tooltip("Opcional: Si aún usas el antiguo TerrainManager como alternativa.")]
        [SerializeField] private TerrainManager terrainManager;

        [Header("Configuración de Puntuación")]
        [Tooltip("Puntos base por un aterrizaje exitoso.")]
        [SerializeField, Min(10)] private int baseLandingPoints = 50;

        [Header("Ciclo de Juego")]
        [Tooltip("Punto de reaparición (Spawn) por defecto si la pantalla no define uno.")]
        [SerializeField] private Vector2 defaultSpawnPosition = new Vector2(0f, 3.5f);

        [Tooltip("Tiempo de espera en segundos tras aterrizar o chocar antes de reiniciar o avanzar.")]
        [SerializeField, Min(0.5f)] private float resetDelay = 3.0f;

        [Tooltip("Si está activo, aterrizar con éxito avanza a la siguiente pantalla en lugar de repetir la misma.")]
        [SerializeField] private bool advanceStageOnLanding = false;

        private Coroutine resetCoroutine;
        private VectorTerrain currentTerrain;
        private Vector2 currentSpawnPosition;
        private readonly HashSet<int> claimedPads = new HashSet<int>();

        private void Awake()
        {
            // Búsqueda de referencias por si no se asignaron en el Inspector
            if (lander == null) lander = FindFirstObjectByType<LanderController>();
            if (hud == null) hud = FindFirstObjectByType<StandardHUD>();
            if (stageManager == null) stageManager = FindFirstObjectByType<StageManager>();
            if (terrainManager == null && stageManager == null) terrainManager = FindFirstObjectByType<TerrainManager>();
        }

        private void OnEnable()
        {
            if (lander != null)
            {
                lander.OnLanded += HandleLanded;
                lander.OnCrashed += HandleCrashed;
            }
        }

        private void OnDisable()
        {
            if (lander != null)
            {
                lander.OnLanded -= HandleLanded;
                lander.OnCrashed -= HandleCrashed;
            }
        }

        private void Start()
        {
            StartNewGame();
        }

        /// <summary>
        /// Inicia o reinicia la partida desde el estado inicial de la pantalla.
        /// </summary>
        public void StartNewGame()
        {
            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
                resetCoroutine = null;
            }

            SetupActiveStage();

            if (hud != null) hud.ResetTimer();
            if (lander != null) lander.ResetLander(currentSpawnPosition);

            claimedPads.Clear();
        }

        /// <summary>
        /// Configura el entorno activo (StageManager o TerrainManager de respaldo).
        /// </summary>
        private void SetupActiveStage()
        {
            currentSpawnPosition = defaultSpawnPosition;

            if (stageManager != null)
            {
                StageData stage = stageManager.SetupCurrentStage();
                if (stage != null)
                {
                    currentTerrain = stage.terrain;
                    currentSpawnPosition = stage.spawnPosition;
                }
            }
            else if (terrainManager != null)
            {
                currentTerrain = terrainManager.SetupTerrain();
            }
        }

        private void HandleLanded(LandingResult result)
        {
            if (!result.Success) return;

            int padIndex = GetLandingPadIndex(lander.transform.position);

            // Recompensa si es una plataforma válida no reclamada en este intento
            if (padIndex >= 0 && !claimedPads.Contains(padIndex))
            {
                claimedPads.Add(padIndex);

                int multiplier = currentTerrain != null ? currentTerrain.LandingPads[padIndex].multiplier : 1;
                int earnedPoints = baseLandingPoints * multiplier;

                if (hud != null) hud.AddScore(earnedPoints);
                if (lander != null) lander.AddFuel(250f);
            }

            // Si se desea pasar de nivel al aterrizar
            if (advanceStageOnLanding)
            {
                ScheduleReset(nextStage: true);
            }
        }

        private void HandleCrashed(LandingResult result)
        {
            // En caso de colisión se espera y se reintenta la pantalla actual
            ScheduleReset(nextStage: false);
        }

        private int GetLandingPadIndex(Vector2 landerPosition)
        {
            if (currentTerrain == null || currentTerrain.LandingPads == null) return -1;

            const float tolerance = 0.5f;
            for (int i = 0; i < currentTerrain.LandingPads.Count; i++)
            {
                LandingPad pad = currentTerrain.LandingPads[i];
                float minX = Mathf.Min(pad.startPoint.x, pad.endPoint.x) - tolerance;
                float maxX = Mathf.Max(pad.startPoint.x, pad.endPoint.x) + tolerance;

                if (landerPosition.x >= minX && landerPosition.x <= maxX)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ScheduleReset(bool nextStage)
        {
            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
            }

            resetCoroutine = StartCoroutine(ResetRoutine(nextStage));
        }

        private IEnumerator ResetRoutine(bool nextStage)
        {
            yield return new WaitForSeconds(resetDelay);

            if (nextStage && stageManager != null)
            {
                stageManager.NextStage();
                SetupActiveStage();
            }

            if (currentTerrain != null)
            {
                currentTerrain.ClearFlags();
            }

            if (lander != null)
            {
                lander.ResetLander(currentSpawnPosition);
            }

            if (hud != null)
            {
                hud.ResetTimer();
            }

            claimedPads.Clear();
            resetCoroutine = null;
        }
    }
}