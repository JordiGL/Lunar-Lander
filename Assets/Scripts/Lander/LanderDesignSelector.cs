using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderDesignSelector : MonoBehaviour
    {
        [Header("Naves (GameObjects)")]
        [SerializeField] private GameObject classicLander;
        [SerializeField] private GameObject modernLander;
        [SerializeField] private GameObject futureLander;

        [Header("Controles")]
        [SerializeField] private KeyCode toggleKey = KeyCode.T;

        private LanderDesign currentDesign;
        public LanderDesign Current => currentDesign;

        private void Start()
        {
            currentDesign = VectorLanderRendererBase.LoadSavedDesign(LanderDesign.Classic);
            ApplySelection(currentDesign);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) Toggle();
        }

        public void SelectClassic() => Select(LanderDesign.Classic);
        public void SelectModern() => Select(LanderDesign.Modern);
        public void SelectFuture() => Select(LanderDesign.Future);

        public void SelectIndex(int index) => Select((LanderDesign)Mathf.Clamp(index, 0, 2));

        public void Toggle()
        {
            int nextDesign = ((int)currentDesign + 1) % 3;
            Select((LanderDesign)nextDesign);
        }

        public void Select(LanderDesign design)
        {
            currentDesign = design;
            VectorLanderRendererBase.SaveDesign(design);
            ApplySelection(design);
        }

        private void ApplySelection(LanderDesign design)
        {
            if (classicLander != null) classicLander.SetActive(design == LanderDesign.Classic);
            if (modernLander != null) modernLander.SetActive(design == LanderDesign.Modern);
            if (futureLander != null) futureLander.SetActive(design == LanderDesign.Future);
        }
    }
}