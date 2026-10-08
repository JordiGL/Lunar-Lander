using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    public sealed class LanderFuelTankClassic : LanderFuelTankRendererBase
    {
        private const float TankMinY = -0.14f;
        private const float TankMaxY = 0.06f;
        private const float TankInset = 0.03f;
        private const float TankRowStep = 0.035f;

        private sealed class FuelTank
        {
            public LineRenderer Outline;
            public LineRenderer Fill;
            public Vector3[] OutlinePoints;
            public float XMin;
            public float XMax;
            public bool HasFill;
        }

        [SerializeField] private bool dualTanks = true;

        private readonly List<FuelTank> tanks = new List<FuelTank>();
        private readonly List<Vector3> fillPoints = new List<Vector3>();

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            if (dualTanks) { AddTank(0.20f, 0.36f); AddTank(-0.36f, -0.20f); }
            else { AddTank(-0.10f, 0.10f); }
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
            var points = new[] {
                new Vector3(xMin, TankMinY, 0f), new Vector3(xMax, TankMinY, 0f),
                new Vector3(xMax, TankMaxY, 0f), new Vector3(xMin, TankMaxY, 0f),
            };

            var tank = new FuelTank
            {
                XMin = xMin,
                XMax = xMax,
                OutlinePoints = points,
                Outline = mainRenderer.CreateChildLine("FuelTank_" + tanks.Count),
                Fill = mainRenderer.CreateChildLine("FuelFill_" + tanks.Count),
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(tank.Outline, points, true, mainRenderer.LineColor, detailWidth);
            mainRenderer.ConfigureLine(tank.Fill, new[] { Vector3.zero, Vector3.zero }, false, mainRenderer.LineColor, detailWidth * 0.8f);

            tanks.Add(tank);
        }

        public override void RedrawTanks()
        {
            if (tanks.Count == 0 || mainRenderer == null) return;
            Color color = fuelGradient.Evaluate(fuelLevel);

            for (int t = 0; t < tanks.Count; t++)
            {
                FuelTank tank = tanks[t];
                mainRenderer.ApplyColor(tank.Outline, color);
                mainRenderer.ApplyColor(tank.Fill, color);

                BuildFillPoints(tank);
                tank.HasFill = fillPoints.Count > 0;
                tank.Fill.positionCount = fillPoints.Count;
                for (int i = 0; i < fillPoints.Count; i++) tank.Fill.SetPosition(i, fillPoints[i]);
            }
            ApplyTankVisibility();
        }

        private void BuildFillPoints(FuelTank tank)
        {
            fillPoints.Clear();
            if (fuelLevel <= 0.0001f) return;

            float xL = tank.XMin + TankInset, xR = tank.XMax - TankInset;
            float bottom = TankMinY + TankInset;
            float height = (TankMaxY - TankMinY) - 2f * TankInset;
            float levelHeight = fuelLevel * height;
            int fullRows = Mathf.FloorToInt(levelHeight / TankRowStep);
            int row = 0;

            for (; row <= fullRows; row++) AddFillRow(row, bottom + row * TankRowStep, xL, xR);
            if (levelHeight - fullRows * TankRowStep > 0.005f) AddFillRow(row, bottom + levelHeight, xL, xR);
        }

        private void AddFillRow(int rowIndex, float y, float xL, float xR)
        {
            if ((rowIndex & 1) == 0) { fillPoints.Add(new Vector3(xL, y, 0f)); fillPoints.Add(new Vector3(xR, y, 0f)); }
            else { fillPoints.Add(new Vector3(xR, y, 0f)); fillPoints.Add(new Vector3(xL, y, 0f)); }
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
                mainRenderer.SpawnStrokeFragments(tanks[t].OutlinePoints, true, width, tankColor,
                                                   center, baseVelocity, fragmentSpeed, 1f);
            }
        }
    }
}