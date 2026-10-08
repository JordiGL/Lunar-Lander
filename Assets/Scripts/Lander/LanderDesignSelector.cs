using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderDesignSelector : MonoBehaviour
    {
        [Header("Lista de Renderers de Landers")]
        [Tooltip("Añade aquí los Renderers de cada variante (Classic, Modern, Future, etc.).")]
        [SerializeField] private List<VectorLanderRendererBase> landerRenderers = new List<VectorLanderRendererBase>();

        [Header("Controles")]
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        private int currentIndex;
        public int CurrentIndex => currentIndex;
        public VectorLanderRendererBase CurrentRenderer => (currentIndex >= 0 && currentIndex < landerRenderers.Count) ? landerRenderers[currentIndex] : null;

        private void Start()
        {
            // Cargamos la nave guardada (por índice)
            int savedIndex = PlayerPrefs.GetInt(VectorLanderRendererBase.SelectionKey, 0);
            SelectIndex(savedIndex);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void SelectIndex(int index)
        {
            if (landerRenderers == null || landerRenderers.Count == 0) return;

            currentIndex = Mathf.Clamp(index, 0, landerRenderers.Count - 1);

            // Guardamos la selección
            PlayerPrefs.SetInt(VectorLanderRendererBase.SelectionKey, currentIndex);
            PlayerPrefs.Save();

            ApplySelection();
        }

        public void Toggle()
        {
            if (landerRenderers == null || landerRenderers.Count == 0) return;

            int next = (currentIndex + 1) % landerRenderers.Count;
            SelectIndex(next);
        }

        private void ApplySelection()
        {
            for (int i = 0; i < landerRenderers.Count; i++)
            {
                if (landerRenderers[i] != null)
                {
                    // Enciende únicamente el GameObject de la nave seleccionada
                    landerRenderers[i].gameObject.SetActive(i == currentIndex);
                }
            }
        }
    }
}