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
using System.Text;
using UnityEngine;

public class DebugRenderer : MonoBehaviour
{
    // Existing fields
    private UnifiedAudioProcessor audioProcessor;
    private GUIStyle debugStyle;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private bool stylesInitialized;

    private Color[] bandColor = new Color[9]
    {
        Color.red, new Color(1f, 0.5f, 0f), Color.yellow, new Color(0.5f, 1f, 0f),
        Color.green, new Color(0f, 1f, 0.5f), new Color(0f, 1f, 1f), new Color(0f, 0.5f , 1f), Color.blue
    };

    // Cache textures
    private Texture2D boxTexture;
    private static Texture2D lineTexture;
    private static bool lineTextureInitialized;
    private static bool boxTextureInitialized;

    // Optimization fields
    private int lastFrameCount = -1;
    private float lastUpdateTime;
    private const float UPDATE_INTERVAL = 0.025f;
    private string audioDebugText = "";
    private string skyboxDebugText = "";
    private string csvDebugText = "";

    public void SetAudioProcessor(UnifiedAudioProcessor processor)
    {
        audioProcessor = processor;
    }

    void Start()
    {

    }

    private void InitializeTextures()
    {
        // Create box texture
        if (!boxTextureInitialized) 
        { 
            boxTexture = CreateTexture(2, 2, new Color(1f, 1f, 1f, 0.2f));
            boxTextureInitialized = true;
        }

        // Create line texture (shared)
        if (!lineTextureInitialized)
        {
            lineTexture = CreateTexture(1, 1, Color.white);
            lineTextureInitialized = true;
        }
    }


    void OnGUI()
    {
        InitializeTextures();
        if (!VisualizerLandfallConfig.CurrentConfig.ShowDebug || audioProcessor == null) return;

        // Initialize styles if needed
        if (!stylesInitialized)
        {
            InitializeStyles();
        }

        // Only update UI at fixed intervals
        if (Time.frameCount == lastFrameCount || Time.time - lastUpdateTime < UPDATE_INTERVAL)
        {
            DrawDebugInfo();
            return;
        }

        lastUpdateTime = Time.time;
        lastFrameCount = Time.frameCount;

        UpdateDebugText();
    }

    private void UpdateDebugText()
    {
        if (!VisualizerLandfallConfig.CurrentConfig.ShowDebug || audioProcessor == null)
        {
            audioDebugText = "";
            skyboxDebugText = "";
            csvDebugText = "";
            return;
        }

        // AUDIO DEBUG AREA - 
        var audioSB = new StringBuilder();
        audioSB.AppendLine("AUDIO PROCESSOR DEBUG");
        audioSB.AppendLine($"Source: {audioProcessor.audioSource?.name ?? "None"}");
        audioSB.AppendLine($"Bands: {audioProcessor.bands}");
        audioSB.AppendLine($"Intensity: {VisualizerLandfallConfig.CurrentConfig.Intensity:F2}");
        audioSB.AppendLine($"LowBand0: {(float.IsNaN(SkyboxVisualizer.smoothedBands[0]) ? 0 : SkyboxVisualizer.smoothedBands[0]):F2}");
        audioSB.AppendLine($"MidBand3: {(float.IsNaN(SkyboxVisualizer.smoothedBands[3]) ? 0 : SkyboxVisualizer.smoothedBands[3]):F2}");
        audioSB.AppendLine($"HighBand7: {(float.IsNaN(SkyboxVisualizer.smoothedBands[7]) ? 0 : SkyboxVisualizer.smoothedBands[7]):F2}");
        audioSB.AppendLine($"BeatPulse: {audioProcessor.beatCoordinator.beatPulse:F2}");
        audioSB.AppendLine($"Processor: {(SkyboxVisualizer.audioProcessor != null ? "READY" : "NULL")}");
        audioSB.AppendLine($"Audio Source: {audioProcessor.audioSource?.name ?? "None"}");
        audioSB.AppendLine($"Is Playing: {audioProcessor.audioSource?.isPlaying.ToString() ?? "N/A"}");

        // Band values - all bands
        for (int i = 0; i < audioProcessor.bands; i++)
        {
            audioSB.AppendLine($"Band {i}: {audioProcessor.GetBandValue(i):F4}");
        }

        // Beat detection status - all bands
        for (int i = 0; i < audioProcessor.bands; i++)
        {
            audioSB.AppendLine($"{audioProcessor.bandLabels[i]}Beat: {audioProcessor.bandDetectors[i].beatStrength:F2}");
        }

        audioSB.AppendLine($"General Beat: {audioProcessor.generalBeat.beatStrength:F2}");
        audioSB.AppendLine($"Combined Beat: {audioProcessor.beatCoordinator.combinedBeatStrength:F2}");
        audioSB.AppendLine($"Beat Pulse: {audioProcessor.beatCoordinator.beatPulse:F2}");
        audioSB.AppendLine($"DominantBand: {audioProcessor.DominantBand}");
        if (audioProcessor.DominantBand >= 0)
            audioSB.AppendLine($"DominantEnergy: {audioProcessor.GetBandValue(audioProcessor.DominantBand):F3}");

        audioSB.AppendLine($"LowEnergy: {SkyboxVisualizer.lowEnergy:F3}");
        audioSB.AppendLine($"MidEnergy: {SkyboxVisualizer.midEnergy:F3}");
        audioSB.AppendLine($"HighEnergy: {SkyboxVisualizer.highEnergy:F3}");
        audioSB.AppendLine($"TotalEnergy: {SkyboxVisualizer.totalEnergy:F3}");


        audioDebugText = audioSB.ToString();

        // SKYBOX DEBUG AREA - 
        var skyboxSB = new StringBuilder();
        skyboxSB.AppendLine("SKYBOX VISUALIZER DEBUG");

        try
        {
            skyboxSB.AppendLine($"Sky Scale: {Shader.GetGlobalFloat(SkyboxVisualizer.ShaderProperties.SkyScale):F2}");

            if (SkyboxVisualizer.sunLight != null)
            {
                skyboxSB.AppendLine($"Light Intensity: {SkyboxVisualizer.sunLight.intensity:F2}");
                skyboxSB.AppendLine($"Light Color: {SkyboxVisualizer.sunLight.color}");
                skyboxSB.AppendLine($"Sun Rotation: {SkyboxVisualizer.sunLight.transform.localEulerAngles}");
            }
            skyboxSB.AppendLine($"Ambient Light: {RenderSettings.ambientLight}");
            skyboxSB.AppendLine($"Ambient Intensity: {RenderSettings.ambientIntensity:F2}");
            skyboxSB.AppendLine($"Ambient Equator: {RenderSettings.ambientEquatorColor}");
            skyboxSB.AppendLine($"Ambient Ground: {RenderSettings.ambientGroundColor}");

            skyboxSB.AppendLine($"Cloud Color: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.CloudColor)}");
            skyboxSB.AppendLine($"Shape Remap: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.CloudShapeRemap)} {SkyboxVisualizer.initialCloudShapeRemap}");
            skyboxSB.AppendLine($"Highlight Remap: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.CloudHighlightRemap)} {SkyboxVisualizer.initialCloudHighlightRemap}");
            skyboxSB.AppendLine($"Volume Remap: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.CloudVolumeRemap)} {SkyboxVisualizer.initialCloudVolumeRemap}");
            skyboxSB.AppendLine($"Volume Strength: {Shader.GetGlobalFloat(SkyboxVisualizer.ShaderProperties.SkyVolumeStrength):F4}");
            skyboxSB.AppendLine($"Wind1: {Shader.GetGlobalFloat(SkyboxVisualizer.ShaderProperties.SkyWind1):F3}, Wind2: {Shader.GetGlobalFloat(SkyboxVisualizer.ShaderProperties.SkyWind2):F3}");
            skyboxSB.AppendLine($"Fog Density: {(RenderSettings.fog ? RenderSettings.fogDensity.ToString("F4") : "Disabled")}");
            skyboxSB.AppendLine($"Star Color: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.SkyColorStars)}");

            Vector4 horizon = Shader.GetGlobalVector(SkyboxVisualizer.ShaderProperties.HorizonRemap);
            skyboxSB.AppendLine($"Horizon: {horizon.x:F2}, {horizon.y:F2}, {horizon.z:F2}, {horizon.w:F2}");

            skyboxSB.AppendLine($"Horizon Color: {Shader.GetGlobalColor(SkyboxVisualizer.ShaderProperties.SkyColorHorizon)}");
        }
        catch
        {
            skyboxSB.AppendLine("Shader properties not available");
        }

        skyboxSB.AppendLine($"Status: {(SkyboxVisualizer.isInitialized ? "ACTIVE" : "INACTIVE")}");
        skyboxSB.AppendLine($"Processor: {(SkyboxVisualizer.audioProcessor != null ? "READY" : "NULL")}");

        skyboxDebugText = skyboxSB.ToString();

        //CSV VALUE DEBUG AREA -
        var CSVDB = new StringBuilder();

        if (VisualizerLandfallConfig.CurrentConfig.ShowCSVDebug == true)
        {
            CSVDB.AppendLine("CSV Debug");
            CSVDB.AppendLine("Band  Sens  MinThresh  MinExceed  BeatDecay  ThresholdMult");

            for (int i = 0; i < 9; i++)
            {
                var detector = audioProcessor.bandDetectors[i];
                CSVDB.AppendLine($"{i,1:F0}       " +
                                 $"{detector.adaptiveThreshold.sensitivity,6:F2}   " +
                                 $"{detector.adaptiveThreshold.minThreshold,10:F2}    " +
                                 $"{detector.MinExceed,13:F2}     " +
                                 $"{detector.beatDecay,13:F2}              " +
                                 $"{detector.beatThresholdMultiplier,4:F2}");
            }
        }
        csvDebugText = CSVDB.ToString();
    }

    private void InitializeStyles()
    {
        debugStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow },
            padding = new RectOffset(10, 10, 5, 5)
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            alignment = TextAnchor.UpperLeft
        };

        boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = boxTexture },
            alignment = TextAnchor.MiddleCenter
        };

        stylesInitialized = true;
    }

    private readonly float[] _bandVisWeight = SkyboxVisualizer.GetBandVisWeights();
    private void DrawDebugInfo()
    {
        DrawAudioDebug();
        DrawSkyboxDebug();
        DrawCSVDebug();
    }

    private void DrawAudioDebug()
    {
        if (audioProcessor == null || audioProcessor.bandDetectors == null) return;

        for (int i = 0; i < audioProcessor.bands; i++)
        {
            // Null check for bandDetectors
            if (i >= audioProcessor.bandDetectors.Length) continue;

            float w = (_bandVisWeight != null && i < _bandVisWeight.Length) ? _bandVisWeight[i] : 1f;

            float height = Mathf.Clamp(audioProcessor.GetBandValue(i) * w * 100f, 5f, 200f);
            float width = 20f;
            float spacing = 25f;
            float x = 600 + i * spacing;
            float y = Screen.height - 50;

            // Draw bar
            GUI.Box(new Rect(x, y - height, width, height), "");
            // Draw label
            GUI.Label(new Rect(x - 5, y + 10, 30, 40), $"{i}", debugStyle);


            // Draw threshold line
            float resetPx = ProcessThresholdForDisplay(
                audioProcessor.bandDetectors[i].ResetThresholdDebug * w,
                audioProcessor.bandDetectors[i].currentEnergy * w
            );

            float threshPx = ProcessThresholdForDisplay(
                audioProcessor.bandDetectors[i].EffectiveThresholdDebug * w,
                audioProcessor.bandDetectors[i].currentEnergy * w
            );

            Drawing.DrawLine(new Vector2(x, y - resetPx), new Vector2(x + width, y - resetPx), Color.cyan, 2);
            Drawing.DrawLine(new Vector2(x, y - threshPx), new Vector2(x + width, y - threshPx), Color.red, 2);
        }

        // Energy history graphs
        for (int i = 0; i < 9; i++)
        {
            DrawEnergyGraph($"{audioProcessor.bandLabels[i]} Energy", audioProcessor.bandDetectors[i], bandColor[i], 200, Screen.height - 75 * (i + 1));
        }
        DrawEnergyGraph("Total Energy", audioProcessor.generalBeat, Color.white, 200, Screen.height - 75 * 10);


        // Beat indicators

        for (int i = 0; i < 9; i++)
        {
            DrawBeatIndicator(audioProcessor.bandLabels[i], audioProcessor.bandDetectors[i], bandColor[i], 500, Screen.height - 15 - 75 * i);
        }
        DrawBeatIndicator("GENERAL", audioProcessor.generalBeat, Color.black, 500, Screen.height - 15 - 75 * 9);

        GUI.Label(new Rect(10, 10, 300, 1000), audioDebugText, debugStyle);

    }




    // Helper class for drawing lines
    public static class Drawing
    {
        public static void DrawLine(Vector2 start, Vector2 end, Color color, float width)
        {
            if (lineTexture == null) return;

            Matrix4x4 matrix = GUI.matrix;
            Color savedColor = GUI.color;

            float angle = Vector3.Angle(end - start, Vector2.right);
            if (start.y > end.y) angle = -angle;

            GUI.color = color;
            GUIUtility.ScaleAroundPivot(new Vector2((end - start).magnitude, width),
                                       new Vector2(start.x, start.y + 0.5f));
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y, 1, 1), lineTexture);

            GUI.matrix = matrix;
            GUI.color = savedColor;
        }
    }

    void OnDestroy()
    {
        // Clean up textures
        if (boxTexture != null)
        {
            Destroy(boxTexture);
            boxTexture = null;
            boxTextureInitialized = false;
        }

        if (lineTexture != null)
        {
            Destroy(lineTexture);
            lineTexture = null;
            lineTextureInitialized = false;
        }
    }





    private void DrawEnergyGraph(string title, UnifiedAudioProcessor.BeatDetector beat, Color color, float x, float y)
    {
        // Add extensive null and bounds checking
        if (beat == null) return;
        if (beat._energyHistory == null || beat._energyHistory.Length == 0) return;

        float width = 300;
        float height = 60;
        float[] history = beat._energyHistory;
        int historyLength = history.Length;

        // Find max value for scaling
        float maxValue = 0.01f;
        for (int i = 0; i < historyLength; i++)
        {
            if (history[i] > maxValue) maxValue = history[i];
        }

        // Draw background box
        GUI.Box(new Rect(x, y, width, height), title);

        // Draw graph lines with step optimization
        int step = Mathf.Max(1, historyLength / 50); // Draw fewer lines
        for (int i = step; i < historyLength; i += step)
        {
            int prevIndex = i - step;
            if (prevIndex < 0) continue;

            float x1 = x + 5 + (prevIndex) * (width - 10) / historyLength;
            float y1 = y + height - (history[prevIndex] / maxValue) * (height - 10);
            float x2 = x + 5 + i * (width - 10) / historyLength;
            float y2 = y + height - (history[i] / maxValue) * (height - 10);

            // Skip drawing if values are very close
            if (Mathf.Abs(y1 - y2) < 0.5f) continue;

            Drawing.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), color, 2);
        }

        // Draw scale indicator
        GUI.Label(new Rect(x + width - 40, y - 5, 60, 40), $"{maxValue:F2}", debugStyle);
        GUI.Label(new Rect(x, y - 10, 300, 40), $"Energy/Thresh/Avg: {beat.currentEnergy:F2} / {beat.EffectiveThresholdDebug:F2} / {beat.averageEnergy:F2}", debugStyle);
    }

    private float ProcessThresholdForDisplay(float threshold, float bandValue)
    {
        float bandPixels = Mathf.Clamp(bandValue * 100f, 5f, 200f);
        float tPixels = Mathf.Clamp(threshold * 100f, 5f, 200f);
        // Optional: align proportionally to current band height
        return Mathf.Min(tPixels, bandPixels);
    }

    private void DrawBeatIndicator(string label, UnifiedAudioProcessor.BeatDetector beat, Color baseColor, float x, float y)
    {
        Color savedColor = GUI.color;

        // Base box size and positioning
        float baseWidth = 50f;
        float baseHeight = 30f;

        // Calculate dynamic size
        float width = baseWidth;
        float height = baseHeight;

        // Position so box anchors at bottom-left corner (y is bottom edge)
        float boxY = y - height;
        float boxX = x;



        // Highlight box if beat active, blend color towards white based on strength
        if (beat.beatTimer > 0)
        {
            GUI.color = baseColor;
            width = baseWidth + (beat.beatTimer * 300f);
            height = baseHeight + (beat.beatStrength * 60f);
            boxY = y - height;
            GUI.Box(new Rect(boxX, boxY, width, height), "", boxStyle);
        }
        else
        {
            // Draw background box
            GUI.color = Color.white;
            GUI.Box(new Rect(boxX, boxY, width, height), "", boxStyle);
        }
        boxY = y - height;

        // Draw label and activation snapshot
        GUI.Label(new Rect(boxX + 5, boxY + height - 25, width - 10, 20), label, labelStyle);
        GUI.Label(new Rect(x + 20, y - 55, 100, 100), beat.beatActivationSnapshot, labelStyle);

        GUI.color = savedColor;
    }

    // Helper to create textures for GUI elements
    private Texture2D CreateTexture(int width, int height, Color color)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }



    private void DrawSkyboxDebug()
    {
        GUILayout.BeginArea(new Rect(570, 10, 1000, 1000));
        GUI.Label(new Rect(0, 0, 1000, 1000), skyboxDebugText, debugStyle);
        GUILayout.EndArea();
    }

    private void DrawCSVDebug()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 550, 10, 600, Screen.height));
        GUI.Label(new Rect(0, 0, 600, 400), csvDebugText, debugStyle);
        GUILayout.EndArea();
    }
}