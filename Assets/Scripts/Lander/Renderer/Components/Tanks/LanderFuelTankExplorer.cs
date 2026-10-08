using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Dos esferas de combustible con líquido que sube por cuerdas horizontales
    /// recortadas al círculo, más tuberías de alimentación hacia el motor.
    /// </summary>
    public sealed class LanderFuelTankExplorer : LanderFuelTankRendererBase
    {
        private const float TankCenterX = 0.28f;
        private const float TankCenterY = 0.05f;
        private const float TankRadius = 0.11f;
        private const float WallInset = 0.02f;
        private const float RowStep = 0.03f;
        private const int OutlineSegments = 20;

        private sealed class FuelTank
        {
            public LineRenderer Outline;
            public LineRenderer Pipe;
            public LineRenderer Fill;
            public Vector3[] OutlinePoints;
            public Vector3[] PipePoints;
            public float CenterX;
            public bool HasFill;
        }

        private readonly List<FuelTank> tanks = new List<FuelTank>();
        private readonly List<Vector3> fillPoints = new List<Vector3>();

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            AddTank(TankCenterX);
            AddTank(-TankCenterX);
            RedrawTanks();
        }

        private void ClearTanks()
        {
            for (int i = 0; i < tanks.Count; i++)
            {
                if (tanks[i].Outline != null) Destroy(tanks[i].Outline.gameObject);
                if (tanks[i].Pipe != null) Destroy(tanks[i].Pipe.gameObject);
                if (tanks[i].Fill != null) Destroy(tanks[i].Fill.gameObject);
            }
            tanks.Clear();
        }

        private void AddTank(float cx)
        {
            var circle = new Vector3[OutlineSegments];
            for (int i = 0; i < OutlineSegments; i++)
            {
                float a = i * Mathf.PI * 2f / OutlineSegments;
                circle[i] = new Vector3(cx + Mathf.Cos(a) * TankRadius, TankCenterY + Mathf.Sin(a) * TankRadius, 0f);
            }

            float side = Mathf.Sign(cx);
            var pipe = new[] {
                new Vector3(cx, TankCenterY - TankRadius, 0f),
                new Vector3(cx, -0.175f, 0f),
                new Vector3(side * 0.02f, -0.175f, 0f),
            };

            var tank = new FuelTank
            {
                CenterX = cx,
                OutlinePoints = circle,
                PipePoints = pipe,
                Outline = mainRenderer.CreateChildLine("ExplorerTank_" + tanks.Count),
                Pipe = mainRenderer.CreateChildLine("ExplorerPipe_" + tanks.Count),
                Fill = mainRenderer.CreateChildLine("ExplorerFill_" + tanks.Count),
            };

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(tank.Outline, circle, true, mainRenderer.LineColor, detailWidth);
            mainRenderer.ConfigureLine(tank.Pipe, pipe, false, mainRenderer.LineColor, detailWidth * 0.8f);
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
                mainRenderer.ApplyColor(tank.Pipe, color);
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

            float r = TankRadius - WallInset;
            float bottom = TankCenterY - r;
            float levelHeight = fuelLevel * (2f * r);
            float firstY = bottom + 0.012f;
            int row = 0;

            for (float y = firstY; y <= bottom + levelHeight; y += RowStep, row++)
                AddFillRow(row, y, tank.CenterX, r);

            // Fila final justo en la superficie del líquido
            float surface = bottom + levelHeight;
            float lastRowY = firstY + (row - 1) * RowStep;
            if (row == 0 || surface - lastRowY > 0.005f) AddFillRow(row, Mathf.Max(surface, firstY), tank.CenterX, r);
        }

        private void AddFillRow(int rowIndex, float y, float cx, float r)
        {
            float dy = y - TankCenterY;
            float half = Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            float xL = cx - half, xR = cx + half;
            if ((rowIndex & 1) == 0) { fillPoints.Add(new Vector3(xL, y, 0f)); fillPoints.Add(new Vector3(xR, y, 0f)); }
            else { fillPoints.Add(new Vector3(xR, y, 0f)); fillPoints.Add(new Vector3(xL, y, 0f)); }
        }

        protected override void ApplyTankVisibility()
        {
            for (int t = 0; t < tanks.Count; t++)
            {
                if (tanks[t].Outline != null) tanks[t].Outline.enabled = !isHidden;
                if (tanks[t].Pipe != null) tanks[t].Pipe.enabled = !isHidden;
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
                mainRenderer.SpawnStrokeFragments(tanks[t].PipePoints, false, width * 0.8f, tankColor,
                                                   center, baseVelocity, fragmentSpeed, 1f);
            }
        }
    }
}