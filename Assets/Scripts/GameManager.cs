using System.Collections;
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
        [SerializeField] private VectorHUD hud;
        [SerializeField] private VectorTerrain terrain;

        [Header("Configuración de Puntuación")]
        [Tooltip("Puntos base por un aterrizaje exitoso.")]
        [SerializeField, Min(10)] private int baseLandingPoints = 50;

        [Header("Ciclo de Juego")]
        [Tooltip("Punto de reaparición (Spawn) de la nave al iniciar o reiniciar.")]
        [SerializeField] private Vector2 spawnPosition = new Vector2(0f, 3.5f);

        [Tooltip("Tiempo de espera en segundos tras aterrizar o chocar antes de reiniciar el intento.")]
        [SerializeField, Min(0.5f)] private float resetDelay = 3.0f;

        private Coroutine resetCoroutine;

        private void Awake()
        {
            // Búsqueda de referencias por si no se asignaron en el Inspector
            if (lander == null) lander = FindObjectOfType<LanderController>();
            if (hud == null) hud = FindObjectOfType<VectorHUD>();
            if (terrain == null) terrain = FindObjectOfType<VectorTerrain>();
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

            if (terrain != null)
            {
                terrain.GenerateTerrain();
            }

            if (hud != null)
            {
                hud.ResetTimer();
            }

            if (lander != null)
            {
                lander.ResetLander(spawnPosition);
            }
        }

        private void HandleLanded(LandingResult result)
        {
            // 1. Obtener multiplicador de la plataforma donde tocó pie la nave
            int multiplier = GetLandingPadMultiplier(lander.transform.position);

            // 2. Calcular puntos (Puntos base x Multiplicador)
            int earnedPoints = baseLandingPoints * multiplier;

            // 3. Sumar la puntuación al HUD
            if (hud != null)
            {
                hud.AddScore(earnedPoints);
            }

            // 4. Programar el reinicio tras la espera
            ScheduleReset();
        }

        private void HandleCrashed(LandingResult result)
        {
            // En caso de colisión se espera un tiempo y se reintenta el nivel
            ScheduleReset();
        }

        private int GetLandingPadMultiplier(Vector2 landerPosition)
        {
            if (terrain == null) return 1;

            // Tolerancia de 0.5 unidades a cada lado para cubrir el ancho de las patas del lander
            float tolerance = 0.5f;

            foreach (var pad in terrain.LandingPads)
            {
                float minX = Mathf.Min(pad.startPoint.x, pad.endPoint.x) - tolerance;
                float maxX = Mathf.Max(pad.startPoint.x, pad.endPoint.x) + tolerance;

                if (landerPosition.x >= minX && landerPosition.x <= maxX)
                {
                    return pad.multiplier;
                }
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

            resetCoroutine = null;
        }
    }
}