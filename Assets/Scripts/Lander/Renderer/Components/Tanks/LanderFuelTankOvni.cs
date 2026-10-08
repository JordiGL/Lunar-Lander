using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Núcleo de energía: una hilera horizontal de celdas en el anillo del disco.
    /// El combustible se consume desde los extremos hacia el centro, como un
    /// reactor que se va apagando hasta quedar solo la celda central.
    /// </summary>
    public sealed class LanderFuelTankOvni : LanderFuelTankRendererBase
    {
        private const float HalfWidth = 0.48f;
        private const float FrameYMin = 0.115f;
        private const float FrameYMax = 0.225f;
        private const float CellYMin = 0.135f;
        private const float CellYMax = 0.205f;
        private const float CellInset = 0.02f;

        [SerializeField, Min(1)] private int cellsPerSide = 6;

        private LineRenderer frame;
        private Vector3[] framePoints;
        private readonly List<LineRenderer> cells = new List<LineRenderer>();
        private readonly List<int> cellRanks = new List<int>(); // 0 = celda central
        private bool[] cellLit = new bool[0];

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            framePoints = new[] {
                new Vector3(-HalfWidth, FrameYMin, 0f), new Vector3(HalfWidth, FrameYMin, 0f),
                new Vector3( HalfWidth, FrameYMax, 0f), new Vector3(-HalfWidth, FrameYMax, 0f),
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            frame = mainRenderer.CreateChildLine("OvniCore_Frame");
            mainRenderer.ConfigureLine(frame, framePoints, true, mainRenderer.LineColor, detailWidth);

            int total = cellsPerSide * 2 + 1;
            float inner = 2f * (HalfWidth - CellInset);
            float step = inner / total;
            float cellWidth = step * 0.6f;

            for (int i = 0; i < total; i++)
            {
                float x = -inner * 0.5f + step * (i + 0.5f);
                var cell = mainRenderer.CreateChildLine("OvniCore_Cell_" + i);
                mainRenderer.ConfigureLine(cell,
                    new[] { new Vector3(x, CellYMin, 0f), new Vector3(x, CellYMax, 0f) },
                    false, mainRenderer.LineColor, cellWidth);
                cells.Add(cell);
                cellRanks.Add(Mathf.Abs(i - cellsPerSide));
            }

            cellLit = new bool[total];
            RedrawTanks();
        }

        private void ClearTanks()
        {
            if (frame != null) Destroy(frame.gameObject);
            frame = null;
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] != null) Destroy(cells[i].gameObject);
            cells.Clear();
            cellRanks.Clear();
        }

        public override void RedrawTanks()
        {
            if (frame == null || mainRenderer == null) return;
            Color color = fuelGradient.Evaluate(fuelLevel);
            mainRenderer.ApplyColor(frame, color);

            if (cellLit.Length != cells.Count) cellLit = new bool[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                cellLit[i] = fuelLevel * (cellsPerSide + 1) > cellRanks[i] + 0.0001f;
                mainRenderer.ApplyColor(cells[i], color);
            }
            ApplyTankVisibility();
        }

        protected override void ApplyTankVisibility()
        {
            if (frame != null) frame.enabled = !isHidden;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == null) continue;
                bool lit = i < cellLit.Length && cellLit[i];
                cells[i].enabled = !isHidden && fillVisible && lit;
            }
        }

        public override void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed)
        {
            if (mainRenderer == null || framePoints == null) return;
            Color tankColor = fuelGradient.Evaluate(fuelLevel);
            float width = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.SpawnStrokeFragments(framePoints, true, width, tankColor,
                                              center, baseVelocity, fragmentSpeed, 1f);
        }
    }
}