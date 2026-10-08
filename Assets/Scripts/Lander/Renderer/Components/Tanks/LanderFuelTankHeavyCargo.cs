using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Depósitos industriales para Heavy Cargo:
    /// Cuatro bombonas en paralelo (dos a cada costado) con llenado sincronizado
    /// y tuberías de distribución directas a la base del motor.
    /// </summary>
    public sealed class LanderFuelTankHeavyCargo : LanderFuelTankRendererBase
    {
        private const float TankTopY = 0.32f;
        private const float TankBottomY = -0.04f;
        private const float TankWidth = 0.08f;
        private const float RowStep = 0.03f;
        private const float InnerTankX = 0.26f;
        private const float OuterTankX = 0.38f;

        private sealed class IndustrialTank
        {
            public LineRenderer Outline;
            public LineRenderer Fill;
            public LineRenderer Manifold;
            public Vector3[] OutlinePoints;
            public Vector3[] ManifoldPoints;
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

            // Dos bombonas en el flanco derecho
            AddTank(InnerTankX, InnerTankX + TankWidth);
            AddTank(OuterTankX, OuterTankX + TankWidth);

            // Dos bombonas en el flanco izquierdo
            AddTank(-OuterTankX - TankWidth, -OuterTankX);
            AddTank(-InnerTankX - TankWidth, -InnerTankX);

            RedrawTanks();
        }

        private void ClearTanks()
        {
            for (int i = 0; i < tanks.Count; i++)
            {
                if (tanks[i].Outline != null) Destroy(tanks[i].Outline.gameObject);
                if (tanks[i].Fill != null) Destroy(tanks[i].Fill.gameObject);
                if (tanks[i].Manifold != null) Destroy(tanks[i].Manifold.gameObject);
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

            float midX = (xMin + xMax) * 0.5f;
            var manifold = new[] {
                new Vector3(midX, TankBottomY, 0f),
                new Vector3(midX, -0.18f, 0f),
                new Vector3(Mathf.Sign(midX) * 0.20f, -0.18f, 0f)
            };

            var tank = new IndustrialTank
            {
                XMin = xMin,
                XMax = xMax,
                OutlinePoints = outline,
                ManifoldPoints = manifold,
                Outline = mainRenderer.CreateChildLine("HeavyTank_Out_" + tanks.Count),
                Fill = mainRenderer.CreateChildLine("HeavyTank_Fill_" + tanks.Count),
                Manifold = mainRenderer.CreateChildLine("HeavyTank_Pipe_" + tanks.Count)
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(tank.Outline, outline, true, mainRenderer.LineColor, detailWidth);
            mainRenderer.ConfigureLine(tank.Fill, new[] { Vector3.zero, Vector3.zero }, false, mainRenderer.LineColor, detailWidth * 0.8f);
            mainRenderer.ConfigureLine(tank.Manifold, manifold, false, mainRenderer.LineColor, detailWidth * 0.7f);

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
                mainRenderer.ApplyColor(tank.Manifold, color);

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
            if (fuelLevel <= 0.001f) return;

            float inset = 0.012f;
            float xL = tank.XMin + inset;
            float xR = tank.XMax - inset;
            float bottom = TankBottomY + inset;
            float height = (TankTopY - TankBottomY) - 2f * inset;
            float currentHeight = fuelLevel * height;
            int rows = Mathf.FloorToInt(currentHeight / RowStep);

            int r = 0;
            for (; r <= rows; r++)
            {
                float y = bottom + r * RowStep;
                AddRow(r, y, xL, xR);
            }

            if (currentHeight - rows * RowStep > 0.004f)
            {
                AddRow(r, bottom + currentHeight, xL, xR);
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
                if (tanks[t].Manifold != null) tanks[t].Manifold.enabled = !isHidden;
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
                mainRenderer.SpawnStrokeFragments(tanks[t].ManifoldPoints, false, width * 0.7f, tankColor, center, baseVelocity, fragmentSpeed, 1f);
            }
        }
    }
}