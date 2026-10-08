using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Depósitos apilados verticalmente (metano abajo, oxígeno líquido arriba),
    /// como los de una Starship. El combustible se muestra como celdas
    /// independientes que se apagan de arriba abajo.
    /// </summary>
    public sealed class LanderFuelTankStarship : LanderFuelTankRendererBase
    {
        private const float TankHalfWidth = 0.14f;
        private const float CellInset = 0.025f;

        private sealed class FuelTank
        {
            public LineRenderer Outline;
            public Vector3[] OutlinePoints;
            public readonly List<LineRenderer> Cells = new List<LineRenderer>();
        }

        [SerializeField, Min(1)] private int cellsPerTank = 5;

        // (yMin, yMax) de cada depósito: [0] metano (abajo), [1] LOX (arriba)
        private static readonly Vector2[] TankRanges =
        {
            new Vector2(-0.10f, 0.30f),
            new Vector2( 0.36f, 0.66f),
        };

        private readonly List<FuelTank> tanks = new List<FuelTank>();
        private bool[] cellLit = new bool[0];

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            for (int i = 0; i < TankRanges.Length; i++) AddTank(TankRanges[i].x, TankRanges[i].y);
            cellLit = new bool[tanks.Count * cellsPerTank];
            RedrawTanks();
        }

        private void ClearTanks()
        {
            for (int i = 0; i < tanks.Count; i++)
            {
                if (tanks[i].Outline != null) Destroy(tanks[i].Outline.gameObject);
                for (int c = 0; c < tanks[i].Cells.Count; c++)
                    if (tanks[i].Cells[c] != null) Destroy(tanks[i].Cells[c].gameObject);
            }
            tanks.Clear();
        }

        private void AddTank(float yMin, float yMax)
        {
            var points = new[] {
                new Vector3(-TankHalfWidth, yMin, 0f), new Vector3(TankHalfWidth, yMin, 0f),
                new Vector3( TankHalfWidth, yMax, 0f), new Vector3(-TankHalfWidth, yMax, 0f),
            };

            var tank = new FuelTank
            {
                OutlinePoints = points,
                Outline = mainRenderer.CreateChildLine("StarshipTank_" + tanks.Count),
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(tank.Outline, points, true, mainRenderer.LineColor, detailWidth);

            float innerHeight = (yMax - yMin) - 2f * CellInset;
            float cellStep = innerHeight / cellsPerTank;
            float cellWidth = cellStep * 0.7f;
            float xL = -TankHalfWidth + CellInset, xR = TankHalfWidth - CellInset;

            for (int c = 0; c < cellsPerTank; c++)
            {
                float y = yMin + CellInset + cellStep * (c + 0.5f);
                var cell = mainRenderer.CreateChildLine("StarshipCell_" + tanks.Count + "_" + c);
                mainRenderer.ConfigureLine(cell, new[] { new Vector3(xL, y, 0f), new Vector3(xR, y, 0f) },
                                           false, mainRenderer.LineColor, cellWidth);
                tank.Cells.Add(cell);
            }

            tanks.Add(tank);
        }

        public override void RedrawTanks()
        {
            if (tanks.Count == 0 || mainRenderer == null) return;
            Color color = fuelGradient.Evaluate(fuelLevel);
            int total = tanks.Count * cellsPerTank;
            if (cellLit.Length != total) cellLit = new bool[total];

            for (int t = 0; t < tanks.Count; t++)
            {
                mainRenderer.ApplyColor(tanks[t].Outline, color);
                for (int c = 0; c < tanks[t].Cells.Count; c++)
                {
                    int globalIndex = t * cellsPerTank + c; // 0 = celda más baja
                    cellLit[globalIndex] = fuelLevel * total > globalIndex + 0.0001f;
                    mainRenderer.ApplyColor(tanks[t].Cells[c], color);
                }
            }
            ApplyTankVisibility();
        }

        protected override void ApplyTankVisibility()
        {
            for (int t = 0; t < tanks.Count; t++)
            {
                if (tanks[t].Outline != null) tanks[t].Outline.enabled = !isHidden;
                for (int c = 0; c < tanks[t].Cells.Count; c++)
                {
                    int globalIndex = t * cellsPerTank + c;
                    bool lit = globalIndex < cellLit.Length && cellLit[globalIndex];
                    if (tanks[t].Cells[c] != null)
                        tanks[t].Cells[c].enabled = !isHidden && fillVisible && lit;
                }
            }
        }

        public override void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed)
        {
            if (mainRenderer == null) return;
            Color tankColor = fuelGradient.Evaluate(fuelLevel);
            float width = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;

            for (int t = 0; t < tanks.Count; t++)
            {
                mainRenderer.SpawnStrokeFragments(tanks[t].OutlinePoints, true, width, tankColor,
                                                   center, baseVelocity, fragmentSpeed, 1f);
            }
        }
    }
}