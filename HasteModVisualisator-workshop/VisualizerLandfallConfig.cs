// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2025 PoWeRofGreeD
//
// This file is part of the HasteModVisualisator plugin.
//
// HasteModVisualisator is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HasteModVisualisator is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class VisualizerLandfallConfig
{
    public static string ConfigDirectory => Application.persistentDataPath;
    public static string ConfigPath => Path.Combine(ConfigDirectory, "SkyboxVisualizer_config.json");

    [Serializable]
    public class VisualizerConfigData
    {
        // Main visualizer settings
        public bool EnableVisualizer = true;
        public float Intensity = 1f;

        // Beat detection
        public float BeatSensitivity = 1f;
        public float BeatThreshold = 1f;

        // Color settings
        public float ColorIntensity = 1.5f;
        public float ColorSaturation = 1f;
        public float BeatResponse = 1f;

        // Light settings
        public bool AffectLighting = true;
        public float LightIntensityMultiplier = 1f;
        public float LightColorMultiplier = 1f;
        public float LightColorLerpSpeed = 1f;
        public bool AffectAmbient = true;
        public float AmbientIntensityMultiplier = 1f;
        public float AmbientColorMultiplier = 1f;
        public float AmbientColorLerpSpeed = 1f;
        public string SunAnglePreset = "Disable";
        public float SunAngleLerpSpeedMultiplier = 1f;

        // Cloud settings
        public bool AffectClouds = true;
        public float JumpScaleMultiplier = 1f;
        public float CloudColorIntensity = 1f;
        public bool CrazyCloudColor = false;
        public float CrazyCloudColorIntensity = 1f;
        public float CloudColorLerpSpeed = 1f;
        public float CrazyCloudColorLerpSpeed = 1f;
        public float SkyVolumeStrength = 1f;
        public float CloudWindBeatScale = 1f;
        public float VolumeStrengthMinMultiplier = 0.8f;

        // Horizon settings
        public bool AffectHorizon = false;
        public float HorizonColorIntensity = 1f;
        public float HorizonColorDensity = 1f;
        public float HorizonColorLerpSpeed = 1f;
        public float HorizonRemapX = 0f;
        public float HorizonRemapY = 0f;
        public float HorizonRemapZ = 0f;
        public float HorizonRemapW = 0f;

        // Effects settings
        public bool AffectStars = false;
        public bool EnableScreenEffects = true;
        public float BlurIntensity = 1f;

        // Advanced settings
        public bool EnableAdvanced = false;
        public bool ShowDebug = false;
        public bool ShowCSVDebug = false;
        public int HistoryDuration = 8;
        public int GaussianKernelSize = 5;
        public float GaussianKernelIntensity = 1.5f;

        // Per-band CSV configs
        public string Band_MinThresholds = "0.16, 0.13, 0.12, 0.11, 0.10, 0.09, 0.07, 0.06, 0.055";
        public string Band_DecayRates = "0.96, 0.96, 0.96, 1.19, 1.25, 1.37, 0.91, 0.84, 0.81";
        public string Band_Sensitivities = "1.55, 1.42, 1.31, 1.1, 1.08, 1.04, 1, 1.11, 1.22";
        public string Band_ThresholdMultipliers = "0.99, 0.98, 0.97, 1.35, 1.3, 1.26, 1.15, 0.97, 0.96";
        public string Band_Cooldowns = "0.20, 0.18, 0.16, 0.16, 0.16, 0.16, 0.15, 0.15, 0.15";
        public string Band_MinExceed = "0.11, 0.09, 0.08, 0.075, 0.07, 0.06, 0.05, 0.04, 0.03";
        public string Band_BeatDecays = "0.98, 0.97, 0.96, 0.86, 0.87, 0.88, 0.89, 0.90, 0.91";
    }

    public static VisualizerConfigData CurrentConfig { get; set; } = new VisualizerConfigData();

    private static bool _isInitialized = false;
    private static bool _isLoading = false;
    private static float _lastSaveTime = 0f;
    public static void Initialize()
    {
        if (_isInitialized || _isLoading) return;
        _isLoading = true;

        try
        {
            Debug.Log($"Initializing VisualizerLandfallConfig in: {ConfigDirectory}");

            // Ensure mod directory exists
            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            // Load config synchronously
            LoadConfig();

            _isInitialized = true;
            Debug.Log("VisualizerLandfallConfig initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize VisualizerLandfallConfig: {ex}");
            CurrentConfig = new VisualizerConfigData();
            _isInitialized = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                CurrentConfig = JsonUtility.FromJson<VisualizerConfigData>(json) ?? new VisualizerConfigData();
                Debug.Log("Visualizer config loaded from: " + ConfigPath);
            }
            else
            {
                // Create default config
                SaveConfig();
                Debug.Log("Created default visualizer config file");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading visualizer config: {ex}");
            CurrentConfig = new VisualizerConfigData();
        }
    }

    public static void SaveConfig()
    {
        // Prevent rapid successive saves
        if (Time.unscaledTime - _lastSaveTime < 0.5f) return;

        _lastSaveTime = Time.unscaledTime;

        // Save config synchronously
        SaveConfigInternal();
    }
    private static void SaveConfigInternal()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentConfig, true);
            File.WriteAllText(ConfigPath, json);

            if (CurrentConfig.ShowDebug)
                Debug.Log($"Visualizer config saved to: {ConfigPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving visualizer config: {ex}");
        }
    }

    // Keep your existing ParseCsvFloats method
    public static float[] ParseCsvFloats(string csv, int expected, float[] fallback)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            float[] f = new float[expected];
            for (int i = 0; i < expected; i++) f[i] = fallback[i];
            return f;
        }

        try
        {
            var parts = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.Trim())
                           .ToArray();

            var result = new float[expected];
            for (int i = 0; i < expected; i++)
            {
                if (i < parts.Length)
                {
                    if (float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float v))
                        result[i] = v;
                    else
                        result[i] = fallback[i];
                }
                else
                {
                    result[i] = fallback[i];
                }
            }
            return result;
        }
        catch
        {
            float[] f = new float[expected];
            for (int i = 0; i < expected; i++) f[i] = fallback[i];
            return f;
        }
    }
}