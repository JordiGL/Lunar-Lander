using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Controlador central del flujo de juego.
    /// Suscribe los eventos del LanderController, gestiona el reinicio de partida,
    /// calcula la puntuación según el multiplicador del terreno y comunica los cambios al HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        [Header("Referencias del Sistema")]
        [SerializeField] private LanderController lander;
        [SerializeField] private StandardHUD hud;
        [SerializeField] private TerrainManager terrainManager;

        [Header("Configuración de Puntuación")]
        [Tooltip("Puntos base por un aterrizaje exitoso.")]
        [SerializeField, Min(10)] private int baseLandingPoints = 50;

        [Header("Ciclo de Juego")]
        [Tooltip("Punto de reaparición (Spawn) de la nave al iniciar o reiniciar.")]
        [SerializeField] private Vector2 spawnPosition = new Vector2(0f, 3.5f);

        [Tooltip("Tiempo de espera en segundos tras aterrizar o chocar antes de reiniciar el intento.")]
        [SerializeField, Min(0.5f)] private float resetDelay = 3.0f;

        private Coroutine resetCoroutine;
        private VectorTerrain currentTerrain;
        private HashSet<int> claimedPads = new HashSet<int>();

        private void Awake()
        {
            // Búsqueda de referencias por si no se asignaron en el Inspector
            if (lander == null) lander = FindObjectOfType<LanderController>();
            if (hud == null) hud = FindObjectOfType<StandardHUD>(); // Búsqueda de StandardHUD
            if (terrainManager == null) terrainManager = FindObjectOfType<TerrainManager>();
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
        /// Inicia o reinicia la partida desde el estado inicial.
        /// </summary>
        public void StartNewGame()
        {
            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
                resetCoroutine = null;
            }

            if (terrainManager != null)
            {
                // Delega la responsabilidad de elegir y generar al Manager
                currentTerrain = terrainManager.SetupTerrain();
            }

            if (hud != null) hud.ResetTimer();
            if (lander != null) lander.ResetLander(spawnPosition);
            claimedPads.Clear();
        }

        private void HandleLanded(LandingResult result)
        {
            int padIndex = GetLandingPadIndex(lander.transform.position);

            // Solo premiamos si es una plataforma válida y no ha sido reclamada
            if (padIndex >= 0 && !claimedPads.Contains(padIndex))
            {
                claimedPads.Add(padIndex);

                int multiplier = currentTerrain.LandingPads[padIndex].multiplier;
                int earnedPoints = baseLandingPoints * multiplier;

                if (hud != null) hud.AddScore(earnedPoints);
                if (lander != null) lander.AddFuel(250f);
            }
        }

        // Sustituye el antiguo GetLandingPadMultiplier por este que devuelve el Índice:
        private int GetLandingPadIndex(Vector2 landerPosition)
        {
            if (currentTerrain == null) return -1;

            float tolerance = 0.5f;
            for (int i = 0; i < currentTerrain.LandingPads.Count; i++)
            {
                var pad = currentTerrain.LandingPads[i];
                float minX = Mathf.Min(pad.startPoint.x, pad.endPoint.x) - tolerance;
                float maxX = Mathf.Max(pad.startPoint.x, pad.endPoint.x) + tolerance;

                if (landerPosition.x >= minX && landerPosition.x <= maxX)
                    return i;
            }
            return -1;
        }

        private void HandleCrashed(LandingResult result)
        {
            // En caso de colisión se espera un tiempo y se reintenta el nivel
            ScheduleReset();
        }

        private int GetLandingPadMultiplier(Vector2 landerPosition)
        {
            if (currentTerrain == null) return 1;

            float tolerance = 0.5f;
            foreach (var pad in currentTerrain.LandingPads) // <-- Usa currentTerrain
            {
                float minX = Mathf.Min(pad.startPoint.x, pad.endPoint.x) - tolerance;
                float maxX = Mathf.Max(pad.startPoint.x, pad.endPoint.x) + tolerance;

                if (landerPosition.x >= minX && landerPosition.x <= maxX)
                    return pad.multiplier;
            }
            return 1;
        }

        private void ScheduleReset()
        {
            if (resetCoroutine != null)
            {
                StopCoroutine(resetCoroutine);
            }

            resetCoroutine = StartCoroutine(ResetRoutine());
        }

        private IEnumerator ResetRoutine()
        {
            yield return new WaitForSeconds(resetDelay);

            if (lander != null)
            {
                lander.ResetLander(spawnPosition);
            }

            if (hud != null)
            {
                hud.ResetTimer();
            }

            if (currentTerrain != null)
            {
                currentTerrain.ClearFlags();
            }

            resetCoroutine = null;
        }
    }
}