using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    /// <summary>
    /// Depósito de plasma/combustible central para el diseño Saucer:
    /// Núcleo toroidal elíptico en el cuerpo central con llenado horizontal escalonado.
    /// </summary>
    public sealed class LanderFuelTankSaucer : LanderFuelTankRendererBase
    {
        private const float CenterY = -0.02f;
        private const float RadiusX = 0.22f;
        private const float RadiusY = 0.11f;
        private const float WallInset = 0.018f;
        private const float RowStep = 0.022f;
        private const int Segments = 24;

        private LineRenderer outline;
        private LineRenderer fill;
        private Vector3[] outlinePoints;
        private readonly List<Vector3> fillPoints = new List<Vector3>();

        public override void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            outlinePoints = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                outlinePoints[i] = new Vector3(Mathf.Cos(a) * RadiusX, CenterY + Mathf.Sin(a) * RadiusY, 0f);
            }

            outline = mainRenderer.CreateChildLine("SaucerPlasmaCore_Outline");
            fill = mainRenderer.CreateChildLine("SaucerPlasmaCore_Fill");

            float detailWidth = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.ConfigureLine(outline, outlinePoints, true, mainRenderer.LineColor, detailWidth);
            mainRenderer.ConfigureLine(fill, new[] { Vector3.zero, Vector3.zero }, false, mainRenderer.LineColor, detailWidth * 0.75f);

            RedrawTanks();
        }

        private void ClearTanks()
        {
            if (outline != null) Destroy(outline.gameObject);
            if (fill != null) Destroy(fill.gameObject);
        }

        public override void RedrawTanks()
        {
            if (outline == null || mainRenderer == null) return;

            Color color = fuelGradient.Evaluate(fuelLevel);
            mainRenderer.ApplyColor(outline, color);
            mainRenderer.ApplyColor(fill, color);

            BuildFillPoints();
            fill.positionCount = fillPoints.Count;
            for (int i = 0; i < fillPoints.Count; i++) fill.SetPosition(i, fillPoints[i]);

            ApplyTankVisibility();
        }

        private void BuildFillPoints()
        {
            fillPoints.Clear();
            if (fuelLevel <= 0.001f) return;

            float rx = RadiusX - WallInset;
            float ry = RadiusY - WallInset * 0.7f;
            float bottom = CenterY - ry;
            float height = fuelLevel * (2f * ry);
            float firstY = bottom + 0.008f;
            int row = 0;

            for (float y = firstY; y <= bottom + height; y += RowStep, row++)
            {
                AddFillRow(row, y, rx, ry);
            }

            float surface = bottom + height;
            if (row == 0 || surface - (firstY + (row - 1) * RowStep) > 0.004f)
            {
                AddFillRow(row, Mathf.Max(surface, firstY), rx, ry);
            }
        }

        private void AddFillRow(int rowIndex, float y, float rx, float ry)
        {
            float dy = (y - CenterY) / ry;
            float factor = Mathf.Sqrt(Mathf.Max(0f, 1f - dy * dy));
            float halfX = rx * factor;

            if ((rowIndex & 1) == 0)
            {
                fillPoints.Add(new Vector3(-halfX, y, 0f));
                fillPoints.Add(new Vector3(halfX, y, 0f));
            }
            else
            {
                fillPoints.Add(new Vector3(halfX, y, 0f));
                fillPoints.Add(new Vector3(-halfX, y, 0f));
            }
        }

        protected override void ApplyTankVisibility()
        {
            if (outline != null) outline.enabled = !isHidden;
            if (fill != null) fill.enabled = !isHidden && fillVisible && fillPoints.Count > 0;
        }

        public override void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed)
        {
            if (mainRenderer == null || outlinePoints == null) return;
            Color tankColor = fuelGradient.Evaluate(fuelLevel);
            float width = mainRenderer.LineWidth * mainRenderer.DetailWidthScale;
            mainRenderer.SpawnStrokeFragments(outlinePoints, true, width, tankColor, center, baseVelocity, fragmentSpeed, 1f);
        }
    }
}