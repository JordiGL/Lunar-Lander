using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public class TerrainManager : MonoBehaviour
    {
        [Header("Terrenos Disponibles")]
        [Tooltip("Arrastra aquí los GameObjects que tienen tus scripts MoonTerrain, CanyonTerrain, etc.")]
        public TerrainBase[] terrainPrefabs = new TerrainBase[0];

        [Header("Selector Principal")]
        [Tooltip("Elige el índice del terreno a jugar (0 = el primero, 1 = el segundo, etc.)")]
        [SerializeField]
        private int selectedTerrainIndex = 0;

        // Se ejecuta automáticamente en el Editor de Unity cada vez que cambias un valor en el Inspector
        private void OnValidate()
        {
            if (terrainPrefabs != null && terrainPrefabs.Length > 0)
            {
                // Limita dinámicamente el selector al tamaño real del array
                selectedTerrainIndex = Mathf.Clamp(selectedTerrainIndex, 0, terrainPrefabs.Length - 1);
            }
            else
            {
                selectedTerrainIndex = 0;
            }
        }

        /// <summary>
        /// Activa el terreno seleccionado, lo genera y desactiva el resto.
        /// </summary>
        public TerrainBase SetupTerrain()
        {
            if (terrainPrefabs == null || terrainPrefabs.Length == 0)
            {
                Debug.LogWarning("TerrainManager: No hay terrenos asignados en el array.");
                return null;
            }

            // Aseguramos que el índice es válido por si se modificó por código
            int safeIndex = Mathf.Clamp(selectedTerrainIndex, 0, terrainPrefabs.Length - 1);
            TerrainBase activeTerrain = null;

            for (int i = 0; i < terrainPrefabs.Length; i++)
            {
                if (terrainPrefabs[i] == null) continue;

                if (i == safeIndex)
                {
                    terrainPrefabs[i].gameObject.SetActive(true);
                    activeTerrain = terrainPrefabs[i];
                }
                else
                {
                    terrainPrefabs[i].gameObject.SetActive(false);
                }
            }

            if (activeTerrain != null)
            {
                activeTerrain.GenerateTerrain();
            }

            return activeTerrain;
        }

        /// <summary>
        /// Útil por si en el futuro quieres añadir botones en la UI para cambiar de nivel.
        /// </summary>
        public void SetTerrainIndex(int index)
        {
            if (terrainPrefabs != null && terrainPrefabs.Length > 0)
            {
                selectedTerrainIndex = Mathf.Clamp(index, 0, terrainPrefabs.Length - 1);
            }
        }
    }
}