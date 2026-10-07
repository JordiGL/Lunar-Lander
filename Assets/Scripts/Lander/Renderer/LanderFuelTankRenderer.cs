using System.Collections.Generic;
using UnityEngine;

namespace LunarLander
{
    [DisallowMultipleComponent]
    public sealed class LanderFuelTankRenderer : MonoBehaviour
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

        [Header("Configuración")]
        [SerializeField] private bool showFuelTanks = true;
        [SerializeField] private bool dualTanks = true;
        [SerializeField] private Gradient fuelGradient = CreateDefaultFuelGradient();
        [SerializeField, Range(0f, 1f)] private float lowFuelThreshold = 0.25f;
        [SerializeField, Min(0f)] private float lowFuelBlinkRate = 3f;

        private readonly List<FuelTank> tanks = new List<FuelTank>();
        private readonly List<Vector3> fillPoints = new List<Vector3>();

        private VectorLanderRenderer mainRenderer;
        private LanderController lander;
        private float fuelLevel = 1f;
        private bool fillVisible = true;
        private bool isHidden;

        public float FuelLevel => fuelLevel;
        public Gradient FuelGradient => fuelGradient;

        private static Gradient CreateDefaultFuelGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.15f, 0.15f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.10f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.92f, 0.20f), 0.5f),
                    new GradientColorKey(new Color(0.30f, 1f, 0.40f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f),
                });
            return gradient;
        }

        public void Initialize(VectorLanderRenderer renderer, LanderController controller)
        {
            mainRenderer = renderer;
            lander = controller;
            BuildTanks();
        }

        private void Update()
        {
            UpdateFuelWarningBlink();
        }

        public void SetFuelLevel(float normalized)
        {
            fuelLevel = Mathf.Clamp01(normalized);
            RedrawTanks();
        }

        public void SetHidden(bool hidden)
        {
            isHidden = hidden;
            ApplyTankVisibility();
        }

        public void ResetState()
        {
            isHidden = false;
            fillVisible = true;
            if (lander != null) fuelLevel = lander.FuelNormalized;
            RedrawTanks();
        }

        public void BuildTanks()
        {
            ClearTanks();
            if (!showFuelTanks || mainRenderer == null) return;

            if (dualTanks)
            {
                AddTank(0.20f, 0.36f);
                AddTank(-0.36f, -0.20f);
            }
            else
            {
                AddTank(-0.10f, 0.10f);
            }
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
            var points = new[]
            {
                new Vector3(xMin, TankMinY, 0f),
                new Vector3(xMax, TankMinY, 0f),
                new Vector3(xMax, TankMaxY, 0f),
                new Vector3(xMin, TankMaxY, 0f),
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

        public void RedrawTanks()
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
                for (int i = 0; i < fillPoints.Count; i++)
                {
                    tank.Fill.SetPosition(i, fillPoints[i]);
                }
            }
            ApplyTankVisibility();
        }

        private void BuildFillPoints(FuelTank tank)
        {
            fillPoints.Clear();
            if (fuelLevel <= 0.0001f) return;

            float xL = tank.XMin + TankInset;
            float xR = tank.XMax - TankInset;
            float bottom = TankMinY + TankInset;
            float height = (TankMaxY - TankMinY) - 2f * TankInset;
            float levelHeight = fuelLevel * height;

            int fullRows = Mathf.FloorToInt(levelHeight / TankRowStep);
            int row = 0;

            for (; row <= fullRows; row++)
            {
                AddFillRow(row, bottom + row * TankRowStep, xL, xR);
            }

            if (levelHeight - fullRows * TankRowStep > 0.005f)
            {
                AddFillRow(row, bottom + levelHeight, xL, xR);
            }
        }

        private void AddFillRow(int rowIndex, float y, float xL, float xR)
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

        private void UpdateFuelWarningBlink()
        {
            if (tanks.Count == 0 || isHidden) return;

            bool warning = fuelLevel > 0f && fuelLevel <= lowFuelThreshold && lowFuelBlinkRate > 0f;
            bool visible = !warning || ((int)(Time.time * lowFuelBlinkRate * 2f) & 1) == 0;

            if (visible == fillVisible) return;

            fillVisible = visible;
            ApplyTankVisibility();
        }

        private void ApplyTankVisibility()
        {
            for (int t = 0; t < tanks.Count; t++)
            {
                if (tanks[t].Outline != null) tanks[t].Outline.enabled = !isHidden;
                if (tanks[t].Fill != null) tanks[t].Fill.enabled = !isHidden && fillVisible && tanks[t].HasFill;
            }
        }

        public void SpawnDebris(Vector2 center, Vector2 baseVelocity, float fragmentSpeed)
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