using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// NOTA: Este componente ha sido DEPRECADO y su funcionalidad ha sido unificada
    /// directamente dentro de VectorTerrain.cs utilizando el sistema centralizado de VectorFont.cs.
    /// Esto evita la duplicidad de textos, mejora drásticamente la estética retro, y asegura
    /// que los multiplicadores coincidan exactamente con la dificultad calculada del terreno.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VectorPadText : MonoBehaviour
    {
        private void Start()
        {
            // La generación de textos ahora se gestiona de forma integrada en VectorTerrain.cs
            // para evitar duplicidad de renderizado y discrepancias visuales.
            enabled = false;
        }
    }
}