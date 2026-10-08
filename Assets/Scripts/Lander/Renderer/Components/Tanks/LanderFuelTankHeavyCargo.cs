using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Depósitos industriales para Heavy Cargo:
    /// Dos bombonas principales amplias y diáfanas en los flancos laterales,
    /// con llenado horizontal escalonado limpio sin tuberías cruzadas.
    /// </summary>
    public sealed class LanderFuelTankHeavyCargo : LanderFuelTankRendererBase
    {
        private const float TankTopY = 0.36f;
        private const float TankBottomY = -0.04f;
        private const float TankXMin = 0.28f;
        private const float TankXMax = 0.46f;
        private const float Inset = 0.015f;
        private const float RowStep = 0.028f;

        private sealed class IndustrialTank
        {
            public LineRenderer Outline;
            public LineRenderer Fill;
            public Vector3[] OutlinePoints;
            public float XMin;
            public float XMax;
            public bool HasFill;
        }

        private readonly List<IndustrialTank> tanks = new List<IndustrialTank>();
        private readonly List<Vector3> fillPoints = new List<Vector3>();

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            // Depósito flanco derecho
            AddTank(TankXMin, TankXMax);
            // Depósito flanco izquierdo
            AddTank(-TankXMax, -TankXMin);

            RedrawTanks();
        }

        private void ClearTanks()
        {
            for (int i = 0; i < tanks.Count; i++)
            {
                if (tanks[i].Outline != null) Destroy(tanks[i].Outline.gameObject);
                if (tanks[i].Fill != null) Destroy(tanks[i].Fill.gameObject);
            }
            tanks.Clear();
        }

        private void AddTank(float xMin, float xMax)
        {
            var outline = new[] {
                new Vector3(xMin, TankBottomY, 0f),
                new Vector3(xMax, TankBottomY, 0f),
                new Vector3(xMax, TankTopY, 0f),
                new Vector3(xMin, TankTopY, 0f)
            };

            var tank = new IndustrialTank
            {
                XMin = xMin,
                XMax = xMax,
                OutlinePoints = outline,
                Outline = mainRenderer.CreateChildLine("HeavyTank_Out_" + tanks.Count),
                Fill = mainRenderer.CreateChildLine("HeavyTank_Fill_" + tanks.Count)
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(tank.Outline, outline, true, mainRenderer.LineColor, detailWidth);
            mainRenderer.ConfigureLine(tank.Fill, new[] { Vector3.zero, Vector3.zero }, false, mainRenderer.LineColor, detailWidth * 0.8f);

            tanks.Add(tank);
        }

        public override void RedrawTanks()
        {
            if (tanks.Count == 0 || mainRenderer == null) return;

            Color color = fuelGradient.Evaluate(fuelLevel);

            for (int t = 0; t < tanks.Count; t++)
            {
                IndustrialTank tank = tanks[t];
                mainRenderer.ApplyColor(tank.Outline, color);
                mainRenderer.ApplyColor(tank.Fill, color);

                BuildFillPoints(tank);
                tank.HasFill = fillPoints.Count > 0;
                tank.Fill.positionCount = fillPoints.Count;
                for (int i = 0; i < fillPoints.Count; i++) tank.Fill.SetPosition(i, fillPoints[i]);
            }
            ApplyTankVisibility();
        }

        private void BuildFillPoints(IndustrialTank tank)
        {
            fillPoints.Clear();
            if (fuelLevel <= 0.005f) return;

            float xL = tank.XMin + Inset;
            float xR = tank.XMax - Inset;
            float bottom = TankBottomY + Inset;
            float totalHeight = (TankTopY - TankBottomY) - (2f * Inset);
            float currentHeight = fuelLevel * totalHeight;
            float surfaceY = bottom + currentHeight;

            int row = 0;
            for (float y = bottom; y < surfaceY; y += RowStep, row++)
            {
                AddRow(row, y, xL, xR);
            }

            // Fila en el menisco superior
            if (row > 0)
            {
                AddRow(row, surfaceY, xL, xR);
            }
        }

        private void AddRow(int rowIndex, float y, float xL, float xR)
        {
            if ((rowIndex & 1) == 0)
            {
                fillPoints.Add(new Vector3(xL, y, 0f));
                fillPoints.Add(new Vector3(xR, y, 0f));
            }
            else
            {
                fillPoints.Add(new Vector3(xR, y, 0f));
                fillPoints.Add(new Vector3(xL, y, 0f));
            }
        }

        protected override void ApplyTankVisibility()
        {
            for (int t = 0; t < tanks.Count; t++)
            {
                if (tanks[t].Outline != null) tanks[t].Outline.enabled = !isHidden;
                if (tanks[t].Fill != null) tanks[t].Fill.enabled = !isHidden && fillVisible && tanks[t].HasFill;
            }
        }

        public override void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed)
        {
            if (mainRenderer == null) return;
            Color tankColor = fuelGradient.Evaluate(fuelLevel);
            float width = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;

            for (int t = 0; t < tanks.Count; t++)
            {
                mainRenderer.SpawnStrokeFragments(tanks[t].OutlinePoints, true, width, tankColor, center, baseVelocity, fragmentSpeed, 1f);
            }
        }
    }
}