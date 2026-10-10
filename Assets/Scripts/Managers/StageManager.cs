using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Configuración de una pantalla/nivel: empareja un terreno vectorial,
    /// su fondo espacial temático y la posición inicial de la nave.
    /// </summary>
    [System.Serializable]
    public sealed class StageData
    {
        public string stageName = "Nivel";

        [Tooltip("El terreno vectorial de este nivel (MoonTerrain, DystopianTVTerrain, etc.)")]
        public VectorTerrain terrain;

        [Tooltip("El fondo espacial configurado para esta pantalla")]
        public SpaceBackgroundBase background;

        [Tooltip("Música de este nivel. Solo suena la del nivel activo; las demás se paran.")]
        public AudioSource music;

        [Tooltip("Punto de reaparición (Spawn) de la nave en este nivel")]
        public Vector2 spawnPosition = new Vector2(0f, 3.5f);
    }

    /// <summary>
    /// Administrador de pantallas y niveles.
    /// Activa la pantalla correspondiente (Terreno + SpaceBackground),
    /// desactiva las restantes y genera el relieve activo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageManager : MonoBehaviour
    {
        [Header("Configuración de Pantallas")]
        [Tooltip("Lista de niveles disponibles con su Terreno, Fondo y Spawn.")]
        [SerializeField] private List<StageData> stages = new List<StageData>();

        [Header("Selección Inicial")]
        [Tooltip("Índice de la pantalla que se jugará de inicio (0 = primera).")]
        [SerializeField] private int currentStageIndex = 0;

        public int CurrentStageIndex => currentStageIndex;
        public int StageCount => stages != null ? stages.Count : 0;
        public StageData CurrentStage => (stages != null && stages.Count > 0 && currentStageIndex >= 0 && currentStageIndex < stages.Count)
            ? stages[currentStageIndex]
            : null;

        private void OnValidate()
        {
            if (stages != null && stages.Count > 0)
            {
                currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stages.Count - 1);
            }
            else
            {
                currentStageIndex = 0;
            }
        }

        /// <summary>
        /// Activa la pantalla actual, desactiva las otras y genera el terreno activo.
        /// </summary>
        public StageData SetupCurrentStage()
        {
            if (stages == null || stages.Count == 0)
            {
                Debug.LogWarning("StageManager: No hay pantallas configuradas en la lista 'stages'.");
                return null;
            }

            currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stages.Count - 1);
            StageData activeStage = stages[currentStageIndex];

            for (int i = 0; i < stages.Count; i++)
            {
                bool isActive = (i == currentStageIndex);
                StageData entry = stages[i];

                if (entry.terrain != null)
                {
                    entry.terrain.gameObject.SetActive(isActive);
                    if (isActive)
                    {
                        entry.terrain.GenerateTerrain();
                    }
                }

                if (entry.background != null)
                {
                    entry.background.gameObject.SetActive(isActive);
                }

                if (entry.music != null)
                {
                    if (isActive)
                    {
                        // No reinicia la canción si ya está sonando (p. ej. al reaparecer tras un choque).
                        if (!entry.music.isPlaying) entry.music.Play();
                    }
                    else
                    {
                        entry.music.Stop();
                    }
                }
            }

            return activeStage;
        }

        /// <summary>
        /// Avanza a la siguiente pantalla en ciclo.
        /// </summary>
        public void NextStage()
        {
            if (stages == null || stages.Count == 0) return;
            currentStageIndex = (currentStageIndex + 1) % stages.Count;
            SetupCurrentStage();
        }

        /// <summary>
        /// Cambia manualmente a una pantalla específica por su índice.
        /// </summary>
        public void SetStage(int index)
        {
            if (stages == null || stages.Count == 0) return;
            currentStageIndex = Mathf.Clamp(index, 0, stages.Count - 1);
            SetupCurrentStage();
        }
    }
}