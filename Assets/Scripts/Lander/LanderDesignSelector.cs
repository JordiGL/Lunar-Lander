using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderDesignSelector : MonoBehaviour
    {
        [Tooltip("Arrastra aquí el GameObject de tu nave (el que tiene el VectorLanderRenderer).")]
        [SerializeField] private VectorLanderRenderer targetRenderer;

        [Header("Controles")]
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        public LanderDesign Current
        {
            get
            {
                if (targetRenderer != null) return targetRenderer.Design;
                return VectorLanderRenderer.LoadSavedDesign(LanderDesign.Classic);
            }
        }

        private void Start()
        {
            if (targetRenderer == null) targetRenderer = FindObjectOfType<VectorLanderRenderer>();

            // Forzar el diseño guardado al iniciar
            if (targetRenderer != null)
            {
                targetRenderer.SetDesign(VectorLanderRenderer.LoadSavedDesign(LanderDesign.Classic), false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                Toggle();
            }
        }

        public void SelectClassic() => Select(LanderDesign.Classic);
        public void SelectModern() => Select(LanderDesign.Modern);
        public void SelectFuture() => Select(LanderDesign.Future);

        public void SelectIndex(int index) => Select((LanderDesign)Mathf.Clamp(index, 0, 2));

        public void Toggle()
        {
            int nextDesign = ((int)Current + 1) % 3;
            Select((LanderDesign)nextDesign);
        }

        public void Select(LanderDesign design)
        {
            if (targetRenderer != null)
            {
                targetRenderer.SetDesign(design, true); // Esto redibuja la nave automáticamente
            }
            else
            {
                VectorLanderRenderer.SaveDesign(design);
            }
        }
    }
}