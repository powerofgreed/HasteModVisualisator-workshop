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
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[HarmonyPatch(typeof(Skybox))]
public static class SkyboxVisualizer
{
    public static UnifiedAudioProcessor audioProcessor;
    public static DebugRenderer debugRenderer;
    public static bool isInitialized;
    private static bool _scenePrimed = false;
    private static int _lastSceneIndex = -1;
    private static Skybox _lastSkybox = null;
    private static bool wasEnabled = false;

    private static Dictionary<string, SceneSettings> sceneSettingsCache = new Dictionary<string, SceneSettings>();
    private static float skyScaleBeatResponse;
    private static float cloudJumpAmount;
    private static float cloudJumpVelocity;
    private static float globalPulse;
    private static float unifiedBeatPulse;
    private static float _smoothBeatPulse;
    private static float _beatSmooth;




    // Fields to track color transition
    private static Color sunStartColor;
    private static Color sunFlashColor;
    public static float sunFlashDuration = 0f;
    private static float sunFlashTimer = 0f;

    // ========================
    // SMOOTHED BAND VALUES
    // ========================
    public static float[] smoothedBands = new float[9];
    public static float _bpmPulse;
    private static float _bpmPulsePhase;

    private class SceneSettings
    {
        public float skyScale;
        public Color cloudColor;
        public float skyVolumeStrength;
        public Color starsColor;
        public Vector4 horizonRemap;
        public Color horizonColor;
        public float fogDensity;
        public Color ambientLight;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;
        public float ambientIntensity;
        public float sunIntensity;
        public Color sunColor;
        public Vector3 sunRotation;
        public Vector2 CloudShapeRemap;
        public Vector2 CloudHighlightRemap;
        public Vector2 CloudVolumeRemap;
        public float Wind1;
        public float Wind2;

    }

    // Initial values
    private static float initialSkyScale;
    private static Color initialCloudColor;
    public static float initialSkyVolumeStrength;
    private static Color initialStarsColor;
    private static Vector4 initialHorizonRemap;
    private static Color initialHorizonColor;
    private static Color _currentHorizonColor = Color.blue;
    private static float _horizonColorHue = 0.6f;
    private static float _horizonColorSaturation = 0.7f;
    private static float _horizonColorValue = 0.7f;
    private static float _horizonColorDensity = 0.1f;
    private static Vector4 _currentHorizonRemap;
    private static Color initialSunColor;
    private static float initialSunIntensity;
    private static Vector3 initialSunRotation;
    private static Color initialAmbientLight;
    private static Color initialAmbientEquatorColor;
    private static Color initialAmbientGroundColor;
    private static float initialAmbientIntensity;
    private static float initialFogDensity;
    // Cloud shader initial states
    public static Vector4 initialCloudShapeRemap;
    public static Vector4 initialCloudHighlightRemap;
    public static Vector4 initialCloudVolumeRemap;
    private static float initialWind1;
    private static float initialWind2;
    private static float _cloudHighFreqResponse;
    private static float _cloudMorphSpeed;
    private static Vector2 _cloudTargetShape;
    private static Vector2 _cloudTargetHighlight;
    private static Vector2 _cloudTargetVolume;

    // Runtime smoothed values
    private static Color _currentCloudColor;
    public static Vector4 _currentCloudShapeRemap;
    public static Vector4 _currentCloudHighlightRemap;
    public static Vector4 _currentCloudVolumeRemap;
    private static float _currentWind1;
    private static float _currentWind2;
    private static float _currentVolumeStrength;
    private static float _targetVolumeStrength;
    private static float _cloudIntensity;
    private static float _cloudDensity;
    private static float _cloudResponseTimer;

    // Debug-exposed tweak factors
    public static float CloudColorBeatBoost = 0.4f;
    public static float CloudRemapJumpAmount = 0.02f;
    public static float CloudRemapLerpSpeed = 6f;
    public static float CloudWindBeatScale = 0.1f;


    private static readonly float[] s_processedBands = new float[9];




    // Light references
    public static Light sunLight;
    private static float sunAngleTarget = 3f;
    private static float sunAngleCurrent = 3f;
    private static bool transitioningToEdge = false;
    public static float lastTrackProgress = 0f;
    public static float lastTrackLength = 0f;
    private static Color targetAmbientColor;
    private static float targetAmbientIntensity;
    private static float ambientTransitionSpeed = 2f; // Slower transition for ambient
    static bool sunLightModed = false;
    private static bool isRestoringAmbient = false;
    private static float ambientRestoreTimer = 0f;
    private const float ambientRestoreDuration = 2f;
    // Sun flash envelope (selective bands, reusable pattern)
    public static float sunTargetT = 0f;    // capacitor target 0..1
    public static float sunCurrentT = 0f;   // smoothed follower 0..1
    private static float sunKickSmooth = 0f; // smoothed kick per-frame
    private static float _satBoostCurrent = 0f;

    private static float hudSkyScale, hudVolStrength, hudWind1, hudWind2, hudBeat, hudEnergy;
    private static Vector2 hudShape, hudHighlight, hudVolume;
    private static Color hudCloudColor;
    public static float beatPulse;
    public static float BeatPulse { get; set; }
    public static float lowEnergy;
    public static float midEnergy;
    public static float highEnergy;
    public static float totalEnergy;


    public static float ResistClampWith0(float value, float min = 0.01f, float max = 1f)
    {
        if (value <= min) return min;

        float resistancePoint = 2.5f * max;
        float range = max - min;

        if (value <= resistancePoint)
        {
            // Linear growth up to resistance point
            return min + (value / resistancePoint) * range;
        }
        else
        {
            // Exponential resistance beyond resistance point
            float overshoot = value - resistancePoint;
            float resistance = 1f - Mathf.Exp(-0.1f * overshoot);
            return Mathf.Lerp(max * 0.95f, max, resistance);
        }
    }
    public static float ResistClamp(float value, float min = 0.01f, float max = 1f)
    {
        float range = max - min;
        float curve = 1.1f / (range / 2f);
        return (((-1 / (1 + Mathf.Exp(value * curve))) + 0.5f) * range * 2) + min;
    }
    private static float ClampSunAngle(float angle)
    {
        return Mathf.Clamp(angle, 3f, 177f);
    }
    public static void NewTrackForSun(float prevTrackProgress, float prevTrackLength)
    {
        // Calculate previous position
        float prevProgress = Mathf.Clamp01(prevTrackProgress / Mathf.Max(1f, prevTrackLength));
        float prevAngle = Mathf.Lerp(3f, 177f, prevProgress);

        // Find nearest edge (sunrise or sunset)
        float distToStart = Mathf.Abs(prevAngle - 3f);
        float distToEnd = Mathf.Abs(prevAngle - 177f);
        float nearestEdge = (distToStart < distToEnd) ? 3f : 177f;

        // Start transition to the nearest edge
        sunAngleTarget = nearestEdge;
        transitioningToEdge = true;
    }


    // Cached shader properties
    public static class ShaderProperties
    {
        public static readonly int SkyScale = Shader.PropertyToID("_SkyScale");
        public static readonly int CloudColor = Shader.PropertyToID("_CloudColor");
        public static readonly int SkyVolumeStrength = Shader.PropertyToID("_SkyVolumeStrength");
        public static readonly int SkyColorStars = Shader.PropertyToID("_SkyColorStars");
        public static readonly int HorizonRemap = Shader.PropertyToID("_HorizonRemap");
        public static readonly int SkyColorHorizon = Shader.PropertyToID("_SkyColorHorizon");
        public static readonly int SkyWind1 = Shader.PropertyToID("_SkyWind1");
        public static readonly int SkyWind2 = Shader.PropertyToID("_SkyWind2");
        public static readonly int CloudShapeRemap = Shader.PropertyToID("_CloudShapeRemap");
        public static readonly int CloudHighlightRemap = Shader.PropertyToID("_CloudHighlightRemap");
        public static readonly int CloudVolumeRemap = Shader.PropertyToID("_CloudVolumeRemap");
        public static readonly int SkyTextureInfluence = Shader.PropertyToID("_SkyTextureInfluence");
    }

    private static class ColorModulator
    {
        public static Color Modulate(Color baseColor, float red, float green, float blue, float intensity, float baseColorPart = 0.5f)
        {
            // New perceptual weighting matches audio processor
            float r = (red * 1.5f) - (green * 0.25f) - (blue * 0.25f);
            float g = (green * 1.5f) - (red * 0.25f) - (blue * 0.25f);
            float b = (blue * 1.5f) - (red * 0.25f) - (green * 0.25f);

            // Apply beat boost before HSV conversion
            float beatBoost = VisualizerLandfallConfig.CurrentConfig.ColorIntensity;
            Color scaledBaseColor = new Color(baseColor.r * baseColorPart, baseColor.g * baseColorPart, baseColor.b * baseColorPart);
            Color boostedColor = scaledBaseColor + new Color(r * beatBoost, g * beatBoost, b * beatBoost, 0f);
            Color NewColor = Color.Lerp(scaledBaseColor, boostedColor, intensity);

            // Convert to HSV for saturation control
            Color.RGBToHSV(NewColor, out float h, out float s, out float v);
            s *= VisualizerLandfallConfig.CurrentConfig.ColorSaturation;
            //v = Mathf.Clamp01(v); // Prevent overexposure

            return Color.HSVToRGB(h, s, v);
        }
        public static Color ApplyDensity(Color initialColor, float densityMultiplier, float minDensity = 0f)
        {
            // Density affects alpha and color intensity
            initialColor.a = Mathf.Clamp01(initialColor.a);
            float newAlpha = Mathf.Lerp(Mathf.Clamp01(initialColor.a * densityMultiplier + minDensity), 1f, densityMultiplier);
            return new Color(initialColor.r, initialColor.g, initialColor.b, newAlpha);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    [HarmonyPriority(Priority.First)]
    static void PrefixUpdate(Skybox __instance)
    {
        // New scene or new Skybox instance
        if (!_scenePrimed || _lastSkybox != __instance || _lastSceneIndex != SceneManager.GetActiveScene().buildIndex)
        {
            PrimeFromComponent(__instance);
            _lastSkybox = __instance;
            _lastSceneIndex = SceneManager.GetActiveScene().buildIndex;
            _scenePrimed = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    static void PostfixUpdate(Skybox __instance)
    {

        // Skip if visualizer disabled
        if (!VisualizerLandfallConfig.CurrentConfig.EnableVisualizer)
        {
            if (wasEnabled)
            {
                RestoreInitialValues();
                wasEnabled = false;
            }
            return;
        }
        else if (!wasEnabled)
        {
            wasEnabled = true;
        }

        if (!_scenePrimed) return;

        // Exit if initialization failed
        if (audioProcessor == null || !isInitialized)
        {
            Debug.LogWarning("Audio processor not initialized");
            return;
        }

        if (audioProcessor.GetBandValues() == null)
        {
            Debug.LogWarning("Band values not available");
            return;
        }

        // ========================
        // GET BAND VALUES
        // ========================
        EnsureWeights();
        float[] bandValues = audioProcessor.GetBandValues();
        float intensity = VisualizerLandfallConfig.CurrentConfig.Intensity;


        float[] baseSmoothRates = { 12f, 12f, 12f, 10f, 10f, 8f, 8f, 15f, 18f };

        for (int i = 0; i < 9; i++)
        {
            if (audioProcessor != null && audioProcessor.IsSilent)
            {
                // Fast decay during silence
                smoothedBands[i] = Mathf.Lerp(smoothedBands[i], 0f, Time.deltaTime * 10f);
                if (smoothedBands[i] < 0.001f) smoothedBands[i] = 0f;
                continue;
            }

            float w = BandVisWeights[i];
            s_processedBands[i] = bandValues[i] * intensity * w;
            float rate = baseSmoothRates[i] * w;
            smoothedBands[i] = Mathf.Lerp(smoothedBands[i], s_processedBands[i], Time.deltaTime * rate);

            // Force to zero if very close
            if (smoothedBands[i] < 0.001f) smoothedBands[i] = 0f;
        }

        // ========================
        // GET BEAT STRENGTHS AND TIMERS
        // ========================
        float subBassBeatStrength = audioProcessor.bandDetectors[0].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float mainBassBeatStrength = audioProcessor.bandDetectors[1].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float upperBassBeatStrength = audioProcessor.bandDetectors[2].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float lowMidsBeatStrength = audioProcessor.bandDetectors[3].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float midBeatStrength = audioProcessor.bandDetectors[4].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float highMidBeatStrength = audioProcessor.bandDetectors[5].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float presenceLowBeatStrength = audioProcessor.bandDetectors[6].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float presenceHighBeatStrength = audioProcessor.bandDetectors[7].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float airBeatStrength = audioProcessor.bandDetectors[8].beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;
        float generalBeatStrength = audioProcessor.generalBeat.beatStrength * VisualizerLandfallConfig.CurrentConfig.BeatResponse;

        float subBassBeatTimer = audioProcessor.bandDetectors[0].beatTimer;
        float mainBassBeatTimer = audioProcessor.bandDetectors[1].beatTimer;
        float upperBassBeatTimer = audioProcessor.bandDetectors[2].beatTimer;
        float lowMidsBeatTimer = audioProcessor.bandDetectors[3].beatTimer;
        float midBeatTimer = audioProcessor.bandDetectors[4].beatTimer;
        float highMidBeatTimer = audioProcessor.bandDetectors[5].beatTimer;
        float presenceLowBeatTimer = audioProcessor.bandDetectors[6].beatTimer;
        float presenceHighBeatTimer = audioProcessor.bandDetectors[7].beatTimer;
        float airBeatTimer = audioProcessor.bandDetectors[8].beatTimer;
        float generalBeatTimer = audioProcessor.generalBeat.beatTimer;


        // Handle beat effects
        float maxBeatStrength = Mathf.Max(
            subBassBeatStrength, mainBassBeatStrength, upperBassBeatStrength, lowMidsBeatStrength, midBeatStrength, highMidBeatStrength, presenceLowBeatStrength, presenceHighBeatStrength, airBeatStrength
        );
        float maxLowBeatStrength = Mathf.Max(
            subBassBeatStrength, mainBassBeatStrength, upperBassBeatStrength
        );
        float maxMidBeatStrength = Mathf.Max(
            lowMidsBeatStrength, midBeatStrength, highMidBeatStrength, presenceLowBeatStrength
        );
        float maxHighBeatStrength = Mathf.Max(
            presenceHighBeatStrength, airBeatStrength
        );





        // Apply beat response to sky scale
        float unifiedPulse = 1f + (subBassBeatStrength * 0.6f + midBeatStrength * 0.3f + presenceHighBeatStrength * 0.1f) * 2.5f;
        BeatPulse = audioProcessor.beatCoordinator.beatPulse;
        BeatPulse = Mathf.Lerp(BeatPulse, 1 + maxBeatStrength * 2, Time.deltaTime * 10f);



        // ========================
        // CATEGORIZE ENERGIES
        // ========================
        lowEnergy = CalculateEnergy(0, 2, 0.6f, 0.9f);
        midEnergy = CalculateEnergy(3, 6, 1.2f, 0.4f);
        highEnergy = CalculateEnergy(7, 8, 1.4f, 0.3f);

        // Normalize energies
        NormalizeEnergies(ref lowEnergy, ref midEnergy, ref highEnergy);

        totalEnergy = (lowEnergy * 0.3f + midEnergy * 0.3f + highEnergy * 0.4f);

        float unifiedBeat = audioProcessor.beatCoordinator.combinedBeatStrength;
        float combinedBeat = audioProcessor.beatCoordinator.combinedBeatStrength;
        unifiedBeatPulse = Mathf.Lerp(unifiedBeatPulse, 1f + combinedBeat, Time.deltaTime * 10f);

        ////////////////////////////
        // Lighting effects START //
        if (VisualizerLandfallConfig.CurrentConfig.AffectLighting && sunLight != null)
        {
            // --- Base HDR color: floor to 50% of initial when silent ---
            Color NormalColor = ColorModulator.Modulate(initialSunColor, lowEnergy, midEnergy, highEnergy, totalEnergy);


            // --- Base intensity: also floor at 50% initial ---
            float NormalIntensity = Mathf.Lerp(initialSunIntensity * 0.5f, initialSunIntensity, Mathf.Clamp01(totalEnergy));
            NormalIntensity *= Mathf.Lerp(0.85f, 1.25f, audioProcessor.generalBeat.averageEnergy);
            NormalIntensity = ResistClamp(NormalIntensity, 0.01f, 1.6f);

            // Beat detection
            float sub = subBassBeatStrength;
            float bas = mainBassBeatStrength;
            float ubs = upperBassBeatStrength;
            float lm = lowMidsBeatStrength;
            float mid = midBeatStrength;
            float hm = highMidBeatStrength;
            float pl = presenceLowBeatStrength;
            float ph = presenceHighBeatStrength;
            float air = airBeatStrength;

            // Tint mapping: warm = bass, gold = mids, cool = highs
            Color warm = new Color(1.00f, 0.66f, 0.38f, 0f) * (sub * 0.70f + bas * 0.65f + ubs * 0.35f);
            Color gold = new Color(1.00f, 0.90f, 0.60f, 0f) * (lm * 0.35f + mid * 0.30f + hm * 0.25f);
            Color cool = new Color(0.46f, 0.75f, 1.00f, 0f) * (pl * 0.25f + ph * 0.55f + air * 0.65f);
            Color BeatTint = warm + gold + cool;

            // --- Kick envelope ---
            float kick = 0f;
            if (subBassBeatTimer > 0) kick += sub * 0.28f;
            if (mainBassBeatTimer > 0) kick += bas * 0.24f;
            if (upperBassBeatTimer > 0) kick += ubs * 0.18f;

            sunFlashTimer = Mathf.Lerp(sunFlashTimer, kick, Time.deltaTime * 10f); // faster
            float energy = Mathf.Clamp01(audioProcessor.generalBeat.averageEnergy * 0.7f + totalEnergy * 0.3f);
            float attackGain = 1.25f + 2.0f * energy;

            if (kick > 0)
                sunTargetT = Mathf.Clamp01(sunTargetT + sunFlashTimer * attackGain * Time.deltaTime);
            else
                sunTargetT = Mathf.Max(0f, sunTargetT - Mathf.Lerp(1.4f, 2.8f, totalEnergy) * Time.deltaTime);

            float followSpeed = Mathf.Lerp(8f, 28f, totalEnergy);
            sunCurrentT = Mathf.Lerp(sunCurrentT, sunTargetT, Time.deltaTime * followSpeed);

            // --- High-band driven saturation boost ---
            float highBandCombined = ph + air; // high + air
                                               // fast ramp / fast decay smoothing
            const float satRampUp = 15f;
            const float satRampDown = 12f;
            if (highBandCombined > _satBoostCurrent)
                _satBoostCurrent = Mathf.Lerp(_satBoostCurrent, highBandCombined, Time.deltaTime * satRampUp);
            else
                _satBoostCurrent = Mathf.Lerp(_satBoostCurrent, highBandCombined, Time.deltaTime * satRampDown);

            float satBoostFactor = Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(_satBoostCurrent));

            // --- Blend base & beat color in HSV ---
            Color BeatCandidate = NormalColor + BeatTint;
            Color.RGBToHSV(NormalColor, out float hA, out float sA, out float vA);
            Color.RGBToHSV(BeatCandidate, out float hB, out float sB, out float vB);
            float hFinal = Mathf.LerpAngle(hA * 360f, hB * 360f, sunCurrentT) / 360f;
            float sFinal = Mathf.Clamp01(Mathf.Lerp(sA, sB, sunCurrentT) * satBoostFactor);
            float vTarget = Mathf.Lerp(vA, vB, sunCurrentT);
            float vFinal = ResistClamp(vTarget, 0.05f, 1.8f);

            Color finalColor = Color.HSVToRGB(hFinal, sFinal, vFinal, true) * VisualizerLandfallConfig.CurrentConfig.LightColorMultiplier;

            // --- Intensity boost from beats ---
            float bassImp = Mathf.Max(sub, Mathf.Max(bas, ubs));
            float midImp = Mathf.Max(lm, Mathf.Max(mid, hm));
            float trebleImp = Mathf.Max(pl, Mathf.Max(ph, air));
            float beatIntensityBoost = bassImp * 0.75f + midImp * 0.20f + trebleImp * 0.10f;
            float boostedIntensity = NormalIntensity * (1f + beatIntensityBoost * 1.7f);

            float targetIntensity = Mathf.Lerp(NormalIntensity, boostedIntensity, sunCurrentT);
            float ceiling = initialSunIntensity * VisualizerLandfallConfig.CurrentConfig.LightIntensityMultiplier * 1.9f;
            float limitedIntensity = SoftLimit(targetIntensity * VisualizerLandfallConfig.CurrentConfig.LightIntensityMultiplier, ceiling, 0.65f);

            float outSmooth = Mathf.Lerp(1f, 25f, audioProcessor.generalBeat.averageEnergy * 1.3f);
            outSmooth *= VisualizerLandfallConfig.CurrentConfig.LightColorLerpSpeed;
            finalColor = ColorModulator.ApplyDensity(finalColor, totalEnergy);
            sunLight.color = Color.Lerp(sunLight.color, finalColor, Time.deltaTime * outSmooth);
            sunLight.intensity = Mathf.Lerp(sunLight.intensity, limitedIntensity, Time.deltaTime * outSmooth);

            // Sun angle behavior 
            string preset = VisualizerLandfallConfig.CurrentConfig.SunAnglePreset ?? "Disable";
            float trackLengthSec = 0f;
            float trackPositionSec = 0f;
            float progress = 0f;

            // Get track progress safely
            if (audioProcessor.audioSource != null && audioProcessor.audioSource.clip != null)
            {
                trackLengthSec = audioProcessor.audioSource.clip.length;
                trackPositionSec = audioProcessor.audioSource.time;
                progress = Mathf.Clamp01(trackPositionSec / Mathf.Max(1f, trackLengthSec));
            }

            // Save last track progress/length for transitions
            lastTrackProgress = trackPositionSec;
            lastTrackLength = trackLengthSec;

            if (preset == "TrackLength")
            {
                // If we're transitioning to the edge, keep moving until we reach it
                if (transitioningToEdge)
                {
                    sunAngleCurrent = Mathf.Lerp(sunAngleCurrent, sunAngleTarget, Time.deltaTime * 0.25f * VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier);
                    if (Mathf.Abs(sunAngleCurrent - sunAngleTarget) < 0.5f)
                    {
                        sunAngleCurrent = sunAngleTarget;
                        transitioningToEdge = false;
                    }
                }
                else
                {
                    // Normal day cycle, move sun with track progress
                    sunAngleTarget = Mathf.Lerp(3f, 177f, progress);
                    sunAngleCurrent = Mathf.Lerp(sunAngleCurrent, sunAngleTarget, Time.deltaTime * 0.25f * VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier);
                }
            }
            else if (preset == "TotalEnergy")
            {
                // Sun rises/falls with energy (still stay above ground)
                sunAngleTarget = Mathf.Lerp(3f, 75f, Mathf.Clamp01(totalEnergy));
                sunAngleCurrent = Mathf.Lerp(sunAngleCurrent, sunAngleTarget, Time.deltaTime * 0.25f * VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier);
            }
            else // Disable
            {
                sunAngleTarget = initialSunRotation.x;
                sunAngleCurrent = Mathf.Lerp(sunAngleCurrent, sunAngleTarget, Time.deltaTime * 0.25f * VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier);
            }

            // Clamp sun angle
            sunAngleCurrent = ClampSunAngle(sunAngleCurrent);

            // Set rotation, preserving Y/Z from initial
            Vector3 targetRotation = initialSunRotation;
            targetRotation.x = sunAngleCurrent;
            sunLight.transform.localEulerAngles = targetRotation;

            // Ambient handling remains as in your code (below), untouched
            if (VisualizerLandfallConfig.CurrentConfig.AffectAmbient)
            {
                if (isRestoringAmbient)
                {
                    // Gradually restore ambient to initial values (all three: sky, equator, ground)
                    ambientRestoreTimer += Time.deltaTime;
                    float restoreProgress = Mathf.Clamp01(ambientRestoreTimer / ambientRestoreDuration);

                    RenderSettings.ambientLight = Color.Lerp(
                        RenderSettings.ambientLight,
                        initialAmbientLight,
                        restoreProgress
                    );

                    RenderSettings.ambientEquatorColor = Color.Lerp(
                        RenderSettings.ambientEquatorColor,
                        initialAmbientEquatorColor,
                        restoreProgress
                    );

                    RenderSettings.ambientGroundColor = Color.Lerp(
                        RenderSettings.ambientGroundColor,
                        initialAmbientGroundColor,
                        restoreProgress
                    );

                    RenderSettings.ambientIntensity = Mathf.Lerp(
                        RenderSettings.ambientIntensity,
                        initialAmbientIntensity,
                        restoreProgress
                    );

                    // Check if restoration is complete
                    if (restoreProgress >= 0.99f)
                    {
                        isRestoringAmbient = false;
                        ambientRestoreTimer = 0f;
                    }
                }
                else
                {
                    // 1) Ambient (sky dome) color — blue-leaning with higher bands, slow drift
                    Color ambientBaseColor = ColorModulator.Modulate(
                        initialAmbientLight,
                        sunLight.color.r * (1f - lowEnergy),   // reduce red influence
                        sunLight.color.g * (1f - midEnergy),   // reduce green influence
                        sunLight.color.b * (1f - highEnergy),  // boost blue influence
                        1 - Mathf.Min(lowEnergy, midEnergy, highEnergy),
                        0.2f
                    );
                    float intensityReduction = 0.3f * mainBassBeatStrength; // up to -30% on strong beats
                    // Optional subtle cool bias with highs so ambience breathes toward blue in bright treble
                    Color coolBias = new Color(0.05f, 0.08f, 0.12f, 0f) * Mathf.Clamp01(highEnergy);
                    Color targetAmbientColor = ColorModulator.ApplyDensity(ambientBaseColor + coolBias, (1f - intensityReduction * 2f))
                                               * VisualizerLandfallConfig.CurrentConfig.AmbientColorMultiplier;

                    // 2) Intensity — dip on strong beats, recover between them

                    float targetAmbientIntensity = Mathf.Clamp(
                        initialAmbientIntensity * (1f - intensityReduction),
                        0.1f, 1.8f  // Clamp with a reasonable range
                    ) * VisualizerLandfallConfig.CurrentConfig.AmbientIntensityMultiplier;

                    // 3) Ground — inverse of current sun color (no upper clamp)
                    Color inverseSunColor = sunLight.color;
                    Color.RGBToHSV(inverseSunColor, out float H, out float S, out float V);
                    float negativeH = (H + 0.5f) % 1f;
                    inverseSunColor = Color.HSVToRGB(negativeH, S, V);

                    Color targetGroundColor = Color.Lerp(
                        initialAmbientGroundColor,
                        inverseSunColor,
                        Mathf.Clamp01(maxBeatStrength * 0.7f) // apply inverse more on stronger beats
                    );

                    // 4) Equator — horizon wash driven by mids, blended for cohesion, with a small beat sparkle
                    // Mid bands beat average for horizon “spark”
                    float midBandsBeat = (lowMidsBeatStrength + midBeatStrength + highMidBeatStrength) / 3f;

                    // Start with a mid-focused modulate of the initial equator color
                    Color equatorModulated = ColorModulator.Modulate(
                        initialAmbientEquatorColor,
                        lowEnergy * 0.38f,
                        midEnergy * 0.9f,   // emphasize mids
                        highEnergy * 0.75f,
                        totalEnergy
                    );

                    // Blend toward the sky ambient target for cohesion
                    Color targetEquatorColor = Color.Lerp(equatorModulated, targetAmbientColor, 0.4f)
                                               * VisualizerLandfallConfig.CurrentConfig.AmbientColorMultiplier;

                    // Small beat sparkle (keeps equator lively on snares/vocals without overpowering)
                    targetEquatorColor *= 1f + Mathf.Clamp01(midBandsBeat) * 0.15f;
                    ColorModulator.ApplyDensity(targetEquatorColor, midEnergy / 3f);

                    // 5) Apply with smooth transitions (independent speeds)
                    float lightSpeed = Mathf.Lerp(0.5f, 2.5f, highEnergy) * VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed;                            // ambient (sky) color
                    float intensitySpeed = Mathf.Lerp(1.0f, 3.0f, totalEnergy) * 2.5f * VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed;                     // intensity can respond faster
                    float equatorSpeed = Mathf.Lerp(0.75f, 2.25f, Mathf.Clamp01(midEnergy * 0.8f + midEnergy * 0.2f)) * VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed;
                    float groundSpeed = Mathf.Lerp(0.8f, 2.2f, Mathf.Clamp01(lowEnergy * 0.8f + highEnergy * 0.2f)) * VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed;

                    RenderSettings.ambientLight = Color.Lerp(
                        RenderSettings.ambientLight,
                        targetAmbientColor,
                        Time.deltaTime * lightSpeed
                    );

                    RenderSettings.ambientIntensity = Mathf.SmoothStep(
                        RenderSettings.ambientIntensity,
                        targetAmbientIntensity,
                        Time.deltaTime * intensitySpeed
                    );

                    RenderSettings.ambientGroundColor = Color.Lerp(
                        RenderSettings.ambientGroundColor,
                        targetGroundColor,
                        Time.deltaTime * groundSpeed
                    );

                    RenderSettings.ambientEquatorColor = Color.Lerp(
                        RenderSettings.ambientEquatorColor,
                        targetEquatorColor,
                        Time.deltaTime * equatorSpeed
                    );
                }
            }
            else if (!isRestoringAmbient)
            {
                // Start restoring ambient to initial values
                isRestoringAmbient = true;
                ambientRestoreTimer = 0f;
            }

            sunLightModed = true;
        }
        else if (sunLightModed)
        {
            // Reset sunlight to initial values
            if (sunLight != null)
            {
                sunLight.color = initialSunColor;
                sunLight.intensity = initialSunIntensity;
                sunLight.transform.localEulerAngles = initialSunRotation;
            }
            // Restore ambient lighting
            RenderSettings.ambientEquatorColor = initialAmbientEquatorColor;
            RenderSettings.ambientLight = initialAmbientLight;
            RenderSettings.ambientIntensity = initialAmbientIntensity;
            RenderSettings.ambientGroundColor = initialAmbientGroundColor;

            sunLightModed = false;
        }
        // Lighting effects END //
        //////////////////////////

        // Apply pulse to all effects
        float pulseFactor = 1f + (globalPulse * 0.5f);
        // Smooth beat response
        _beatSmooth = Mathf.Clamp01(Mathf.SmoothStep(_beatSmooth, maxBeatStrength, Time.deltaTime * 8f));
        float bassImpact = Mathf.Max(subBassBeatStrength, mainBassBeatStrength);
        float bassBeat = Mathf.Max(subBassBeatStrength, Mathf.Max(mainBassBeatStrength, upperBassBeatStrength));



        //Clouds effects
        if (VisualizerLandfallConfig.CurrentConfig.AffectClouds)
        {
            // --- Weighted bass focus for all cloud reactions (sub, main, upper, lowMid) = [15,70,10,5] ---
            float weightedBassBeat =
                subBassBeatStrength * 0.15f +
                mainBassBeatStrength * 0.70f +
                upperBassBeatStrength * 0.10f +
                lowMidsBeatStrength * 0.05f;

            // ==========================
            // Cloud color (juicy, fast)
            // ==========================
            if (VisualizerLandfallConfig.CurrentConfig.CrazyCloudColor)
            {
                // Calculate color components from different frequency bands
                // Red channel - responds to low-mid frequencies
                float redFill = Mathf.Clamp(smoothedBands[2] * 2f + mainBassBeatStrength * 2f, 0f, 8f);
                float redEdge = Mathf.Clamp(-(smoothedBands[1] * 1f + upperBassBeatStrength * 1f), -4f, 0f);

                // Green channel - responds to mid frequencies
                float greenFill = Mathf.Clamp(smoothedBands[4] * 3f + midBeatStrength * 2f, 0f, 8f);
                float greenEdge = Mathf.Clamp(-(smoothedBands[3] * 1.5f + highMidBeatStrength * 1f), -4f, 0f);

                // Blue channel - responds to high frequencies
                float blueFill = Mathf.Clamp(smoothedBands[7] * 4f + smoothedBands[8] * 3f +
                                            (maxHighBeatStrength) * 3f, 0f, 8f);
                float blueEdge = Mathf.Clamp(-(smoothedBands[5] * 2f + presenceLowBeatStrength * 1f), -4f, 0f);

                // Combine fill and edge components
                float finalRed = Mathf.Clamp(redFill + redEdge, -4f, 8f);
                float finalGreen = Mathf.Clamp(greenFill + greenEdge, -4f, 8f);
                float finalBlue = Mathf.Clamp(blueFill + blueEdge, -4f, 8f);

                // Apply overall intensity
                float intensityMultiplier = VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorIntensity;
                finalRed *= intensityMultiplier;
                finalGreen *= intensityMultiplier;
                finalBlue *= intensityMultiplier;

                // Compose target from initial color + boosts
                Color targetCloudColor = new Color(finalRed, 0.25f + finalGreen, 0.25f + finalBlue, 1f);

                // Fast but stable response (no long tails)
                float colorSpeed = 3f + 10f * audioProcessor.generalBeat.averageEnergy + 20f * maxHighBeatStrength;
                colorSpeed *= VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorLerpSpeed;
                _currentCloudColor = Color.Lerp(_currentCloudColor, targetCloudColor, Time.deltaTime * colorSpeed);
            }
            else
            {
                Color idleCloud = new Color(0f, 0.3f, 0.3f, 1f);
                float intensityMultiplier = VisualizerLandfallConfig.CurrentConfig.CloudColorIntensity;

                // Activity measure to blend from idle → active
                float activity = Mathf.Clamp01(totalEnergy * 1.2f + generalBeatStrength * 0.8f);

                // Build raw channel targets based on your shader response
                // Red: needs big push to be visible (noticeable ~4)
                float r = 0f;
                r += 6.0f * mainBassBeatStrength;       // main bass drives fill
                r += 1.8f * smoothedBands[1];           // reinforce from LowBass band value
                r += 1.4f * subBassBeatStrength;
                r += 0.7f * upperBassBeatStrength;
                r += 0.4f * lowMidsBeatStrength;
                r = Mathf.Clamp(r, 0f, 5.0f) * intensityMultiplier;

                // Green: shifts toward yellow; allow slight negatives for blue edges
                float g = 0.45f
                        + 0.8f * smoothedBands[4]       // Mid band content
                        + 0.6f * midBeatStrength        // mid beat push
                        - 0.25f * upperBassBeatStrength; // small edge bite from upper bass
                g = Mathf.Clamp(g, -1f, 4f) * intensityMultiplier;

                // Blue:  band 7/8 push; allow slight negatives for green edges
                float b = 0.45f
                        + 1.0f * smoothedBands[7]
                        + 1.2f * smoothedBands[8]
                        + 0.6f * (maxHighBeatStrength)
                        - 0.2f * highMidBeatStrength;
                b = Mathf.Clamp(b, -1f, 4f) * intensityMultiplier;

                // Whiteness guard: avoid both G and B going too high together (white wash)
                // Linked suppression: if G is high, cap B a bit; if B is high, cap G
                if (g > 1.1f && b > 1.1f)
                {
                    if (g > b) b = Mathf.Min(b, 0.3f);
                    else g = Mathf.Min(g, 0.3f);
                }

                // Blend: idle → active, so when music is quiet 
                Color activeTarget = new Color(r, g, b, 1f);
                Color targetCloudColor = Color.Lerp(idleCloud, activeTarget, activity);

                // Fast but stable response: highs/treble increase speed, but no jittery tails
                float colorSpeed = 3f
                                 + 8f * maxHighBeatStrength
                                 + 6f * generalBeatStrength
                                 + 3f * weightedBassBeat;
                colorSpeed *= VisualizerLandfallConfig.CurrentConfig.CloudColorLerpSpeed;

                _currentCloudColor = Color.Lerp(_currentCloudColor, targetCloudColor, Time.deltaTime * colorSpeed);
            }


            Shader.SetGlobalColor(ShaderProperties.CloudColor, _currentCloudColor);

            // ==========================================
            // SkyScale spring "pop" (mainly main bass)
            // ==========================================
            // Base musical scale
            float baseScaleModifier = 1f + _beatSmooth * weightedBassBeat;

            // Spring impulse target from weighted bass
            float desiredJump = weightedBassBeat * 0.5f * 25f;
            float stiffness = 15f;  // snappy
            float damping = 25f;   // stable

            cloudJumpVelocity += (desiredJump - cloudJumpAmount) * stiffness * Time.deltaTime;
            cloudJumpVelocity *= Mathf.Exp(-damping * Time.deltaTime);
            cloudJumpAmount += cloudJumpVelocity;

            // Compose final scale with jump
            float scaleModifier = baseScaleModifier * (1f + Mathf.Max(0f, cloudJumpAmount) * 0.1f * VisualizerLandfallConfig.CurrentConfig.JumpScaleMultiplier);
            float targetScale = initialSkyScale * scaleModifier;

            float currentScale = Shader.GetGlobalFloat(ShaderProperties.SkyScale);
            float newScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * 8f);
            Shader.SetGlobalFloat(ShaderProperties.SkyScale, newScale);

            // ==========================================
            // VolumeStrength (responsive, musical)
            // ==========================================
            // Use a combination of general energy and specific beat energy
            float skyVolumeBase = initialSkyVolumeStrength * (0.5f + totalEnergy * 0.8f);

            // Add beat response - quick attack, slow release
            float beatImpact = generalBeatStrength * 0.7f + lowEnergy * 0.3f;
            float volumeBeat = beatImpact * 0.4f;

            // Combine with smooth transition
            _targetVolumeStrength = skyVolumeBase * (1f + volumeBeat * 3.5f);

            // Different response speeds based on whether we're increasing or decreasing
            float currentToTarget = _targetVolumeStrength - _currentVolumeStrength;
            float responseSpeed = currentToTarget > 0 ? 25f : 12f; // Faster attack, slower release

            _currentVolumeStrength = Mathf.Lerp(_currentVolumeStrength, _targetVolumeStrength * VisualizerLandfallConfig.CurrentConfig.SkyVolumeStrength,
                                               Time.deltaTime * responseSpeed);

            // Apply to shader
            Shader.SetGlobalFloat(ShaderProperties.SkyVolumeStrength, _currentVolumeStrength);


            // ==========================================
            // Wind 
            // ==========================================
            float windTarget1 = initialWind1 * VisualizerLandfallConfig.CurrentConfig.CloudWindBeatScale * lowEnergy * 2f;
            float windTarget2 = initialWind2 * VisualizerLandfallConfig.CurrentConfig.CloudWindBeatScale * highEnergy * 2f;
            float windSpeed1 = windTarget1 > _currentWind1 ? 0.05f + 0.2f * totalEnergy : 0.05f + 0.1f * totalEnergy;
            float windSpeed2 = windTarget2 > _currentWind2 ? 0.05f + 0.2f * totalEnergy : 0.05f + 0.1f * totalEnergy;
            _currentWind1 = Mathf.Lerp(_currentWind1, windTarget1, Time.deltaTime * windSpeed1);
            _currentWind2 = Mathf.Lerp(_currentWind2, windTarget2, Time.deltaTime * windSpeed2);

            Shader.SetGlobalFloat(ShaderProperties.SkyWind1, _currentWind1);
            Shader.SetGlobalFloat(ShaderProperties.SkyWind2, _currentWind2);


            // =================================================
            // Cloud remap properties with better audio response
            // =================================================
            float shapeBeatInfluence = Mathf.Clamp01(midBeatStrength * 0.7f + highMidBeatStrength * 0.3f);
            float highlightBeatInfluence = Mathf.Clamp01(lowMidsBeatStrength * 0.6f + midBeatStrength * 0.4f);
            float volumeBeatInfluence = Mathf.Clamp01(generalBeatStrength * 0.8f);

            // Calculate targets with safe ranges
            Vector4 shapeBase = initialCloudShapeRemap * (0.8f + highEnergy * 0.4f);
            Vector4 shapePulse = new Vector2(
                shapeBeatInfluence * 0.06f * Mathf.Sin(Time.time * 0.5f),
                shapeBeatInfluence * 0.08f
            );
            Vector4 shapeTarget = EnsureValidRemap(shapeBase + shapePulse, initialCloudShapeRemap);

            Vector4 highlightBase = initialCloudHighlightRemap * (0.7f + lowEnergy * 0.6f);
            Vector4 highlightMod = new Vector2(
                highlightBeatInfluence * 0.06f,
                highlightBeatInfluence * 0.04f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2f))
            );
            Vector4 highlightTarget = EnsureValidRemap(highlightBase + highlightMod, initialCloudHighlightRemap);

            Vector4 volumeBase = initialCloudVolumeRemap;
            Vector4 volumeMod = new Vector2(
                generalBeatStrength / 2f,
                Mathf.Lerp(0f, 0.12f, totalEnergy) + volumeBeatInfluence * 0.08f
            );
            Vector4 volumeRemapTarget = EnsureValidRemap(volumeBase + volumeMod, initialCloudVolumeRemap);

            // Different lerp speeds for different properties
            float shapeLerpSpeed = 4f + shapeBeatInfluence * 8f;
            float highlightLerpSpeed = 3f + highlightBeatInfluence * 7f;
            float volumeLerpSpeed = 2f + volumeBeatInfluence * 6f;

            _currentCloudShapeRemap = Vector4.Lerp(_currentCloudShapeRemap, shapeTarget, Time.deltaTime * shapeLerpSpeed);
            _currentCloudHighlightRemap = Vector4.Lerp(_currentCloudHighlightRemap, highlightTarget, Time.deltaTime * highlightLerpSpeed);
            _currentCloudVolumeRemap = Vector4.Lerp(_currentCloudVolumeRemap, volumeRemapTarget, Time.deltaTime * volumeLerpSpeed);

            // Ensure current values are also valid (in case of any issues)
            _currentCloudShapeRemap = EnsureValidRemap(_currentCloudShapeRemap, initialCloudShapeRemap);
            _currentCloudHighlightRemap = EnsureValidRemap(_currentCloudHighlightRemap, initialCloudHighlightRemap);
            _currentCloudVolumeRemap = EnsureValidRemap(_currentCloudVolumeRemap, initialCloudVolumeRemap);

            // Apply to shader
            Shader.SetGlobalVector(ShaderProperties.CloudShapeRemap,
                new Vector4(_currentCloudShapeRemap.x, _currentCloudShapeRemap.y,
                            0.5f + midEnergy * 0.1f, 0.5f + highEnergy * 0.2f));

            Shader.SetGlobalVector(ShaderProperties.CloudHighlightRemap,
                new Vector4(_currentCloudHighlightRemap.x, _currentCloudHighlightRemap.y,
                            0.5f + maxHighBeatStrength * 0.3f, 0.5f));

            Shader.SetGlobalVector(ShaderProperties.CloudVolumeRemap,
                new Vector4(_currentCloudVolumeRemap.x, _currentCloudVolumeRemap.y,
                            0.5f, 0.5f + generalBeatStrength * 0.2f));
        }

        // Apply beat-driven fog effects
        //if (RenderSettings.fog)
        //{
        //    float energyFactor = (subBassBeatStrength +
        //                         presenceLowBeatStrength) / 2f;

        //    float targetDensity = initialFogDensity * (1f + energyFactor * 3f);

        //    RenderSettings.fogDensity = Mathf.Lerp(
        //        RenderSettings.fogDensity,
        //        targetDensity,
        //        Time.deltaTime * 10f
        //    );
        //}



        ///////////
        //Horizon//
        if (VisualizerLandfallConfig.CurrentConfig.AffectHorizon)
        {
            // Adjust lerp speed based on frequency bands
            float baseLerpSpeed = 4f;

            // Ramp up lerp speed if high frequencies are strong but other bands are low
            if (highEnergy > 0.6f && (lowEnergy < 0.2f && midEnergy < 0.2f))
            {
                baseLerpSpeed = 16f * maxHighBeatStrength;
            }

            float horizonColorLerpSpeed = baseLerpSpeed * VisualizerLandfallConfig.CurrentConfig.HorizonColorLerpSpeed;

            // Convert to RGB with density applied to alpha
            Color targetColor = ColorModulator.Modulate(new Color(0, 0, 0, 0), highEnergy * 0.8f, audioProcessor.generalBeat.averageEnergy * 1.2f, maxMidBeatStrength * 0.5f + maxLowBeatStrength * 0.5f, totalEnergy, 0f);

            // Apply the expanded color range 
            targetColor.r = ((targetColor.r * 1.5f) - 0.50f) * VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity;
            targetColor.g = ((targetColor.g * 1.5f) - 0.50f) * VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity;
            targetColor.b = ((targetColor.b * 1.5f) - 0.50f) * VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity;
            targetColor = ColorModulator.ApplyDensity(targetColor, maxHighBeatStrength * 1.2f);
            targetColor.a = ((targetColor.a * 0.8f) - 0.4f) * VisualizerLandfallConfig.CurrentConfig.HorizonColorDensity;
            _currentHorizonColor = Color.Lerp(_currentHorizonColor, targetColor, Time.deltaTime * horizonColorLerpSpeed);

            // Apply to shader
            Shader.SetGlobalColor(ShaderProperties.SkyColorHorizon, _currentHorizonColor);

            // Horizon remap - only using Y value in range 0.8 to 1.8
            Vector4 targetRemap = new Vector4(0f, 0.8f, 0f, 0f);

            // Calculate Y value based on high frequencies
            float edgeInfluence = Mathf.Clamp01(audioProcessor.generalBeat.averageEnergy * 0.5f + maxLowBeatStrength);
            targetRemap.y = Mathf.Lerp(1.4f, 2.8f, edgeInfluence);

            // Add subtle morphing effect
            float morphTime = Time.time * 0.3f;
            targetRemap.y += Mathf.Sin(morphTime) * 0.05f;


            // Smoothly transition to new values with slow lerp
            float remapLerpSpeed = 2f + Mathf.Sin(Time.time * audioProcessor.generalBeat.averageEnergy) * 4f; // Always slow

            _currentHorizonRemap = new Vector4(
                _currentHorizonRemap.x,
                Mathf.Lerp(_currentHorizonRemap.y, targetRemap.y, Time.deltaTime * remapLerpSpeed),
                _currentHorizonRemap.z,
                _currentHorizonRemap.w
            );

            // Apply to shader
            Shader.SetGlobalVector(ShaderProperties.HorizonRemap, _currentHorizonRemap);
        }


        // Star pulse
        if (VisualizerLandfallConfig.CurrentConfig.AffectStars)
        {
            Color starColor = Shader.GetGlobalColor(ShaderProperties.SkyColorStars);
            Color targetStarColor = ColorModulator.Modulate(initialStarsColor, maxLowBeatStrength, maxMidBeatStrength, maxHighBeatStrength, audioProcessor.generalBeat.currentEnergy, 0.5f);
            targetStarColor = ColorModulator.ApplyDensity(targetStarColor, highEnergy, 0.5f);
            starColor = Color.Lerp(starColor, targetStarColor, Time.deltaTime * (1 + maxBeatStrength));
            Shader.SetGlobalColor(ShaderProperties.SkyColorStars, starColor);
        }
    }


    // Soft-knee limiter for HDR-ish values. 'knee' is where the knee starts as a fraction of ceil.
    private static float SoftLimit(float x, float ceil, float knee = 0.6f)
    {
        ceil = Mathf.Max(1e-4f, ceil);
        knee = Mathf.Clamp01(knee);
        float kneeStart = ceil * knee;
        if (x <= kneeStart) return x;
        float t = (x - kneeStart) / Mathf.Max(1e-4f, ceil - kneeStart); // 0..1
                                                                        // Fast-exp knee
        return kneeStart + (1f - Mathf.Exp(-3f * t)) * (ceil - kneeStart);
    }

    // HSV blend that clamps value to avoid bleaching to white.
    // Also slightly boosts saturation when beats are strong.
    private static Color HSVBlendClamped(Color a, Color b, float t, float valueCap, float satBoost = 0f)
    {
        Color.RGBToHSV(a, out float ha, out float sa, out float va);
        Color.RGBToHSV(b, out float hb, out float sb, out float vb);

        // Lerp hue safely across wrap
        float hueA = ha * 360f;
        float hueB = hb * 360f;
        float hue = Mathf.LerpAngle(hueA, hueB, Mathf.Clamp01(t)) / 360f;

        float sat = Mathf.Lerp(sa, sb, t);
        sat = Mathf.Clamp01(sat * (1f + satBoost));

        float val = Mathf.Lerp(va, vb, t);
        // Bleach guard: cap value; allow more value if saturation is high
        float satInfluence = Mathf.Lerp(0.85f, 1.15f, sat); // more sat => permit more value
        val = Mathf.Min(val * satInfluence, valueCap);

        return Color.HSVToRGB(hue, sat, val);
    }

    // Build a beat-driven tint from bands with a warm→cool mapping
    private static Color BuildBeatTint(
        float subBass, float mainBass, float upperBass,
        float lowMids, float mids, float highMids,
        float presLow, float presHigh, float air)
    {
        float bassSum = subBass + mainBass + upperBass;         // warm
        float midSum = lowMids + mids + highMids;              // golden/neutral
        float highSum = presLow + presHigh + air;               // cool

        // Tuned tints (linear space)
        Color warm = new Color(1.00f, 0.66f, 0.38f) * (bassSum * 0.60f);
        Color gold = new Color(1.00f, 0.90f, 0.60f) * (midSum * 0.40f);
        Color cool = new Color(0.46f, 0.75f, 1.00f) * (highSum * 0.55f);

        // Sum and normalize to avoid runaway
        Color tint = warm + gold + cool;
        float maxC = Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b));
        if (maxC > 1.25f) tint /= (maxC / 1.25f); // keep under soft ceiling

        return tint;
    }


    private static float CalculateEnergy(int startBand, int endBand, float weight, float compression)
    {
        // Fast return if audio is silent
        if (audioProcessor != null && audioProcessor.IsSilent)
        {
            return 0f;
        }

        float energy = 0f;
        for (int i = startBand; i <= endBand; i++)
        {
            energy += smoothedBands[i];
        }
        energy /= (endBand - startBand + 1);

        // Apply a threshold to prevent very low values
        if (energy < 0.001f) return 0f;

        return Mathf.Pow(energy * weight, compression);
    }

    private static void NormalizeEnergies(ref float lowEnergy, ref float midEnergy, ref float highEnergy)
    {
        float maxEnergy = Mathf.Max(lowEnergy, Mathf.Max(midEnergy, highEnergy));
        if (maxEnergy > 0.01f)
        {
            lowEnergy /= maxEnergy;
            midEnergy /= maxEnergy;
            highEnergy /= maxEnergy;
        }
    }
    private static class SceneStateManager
    {
        private static Dictionary<string, SceneSettings> sceneSettings = new Dictionary<string, SceneSettings>();
        private static SceneSettings currentSettings;
    }
    public static void CaptureSceneSettings(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (sceneSettingsCache.ContainsKey(sceneName)) return;

        var snapshot = new SceneSettings
        {
            skyScale = Shader.GetGlobalFloat(ShaderProperties.SkyScale),
            cloudColor = Shader.GetGlobalColor(ShaderProperties.CloudColor),
            skyVolumeStrength = Shader.GetGlobalFloat(ShaderProperties.SkyVolumeStrength),
            starsColor = Shader.GetGlobalColor(ShaderProperties.SkyColorStars),
            horizonRemap = Shader.GetGlobalVector(ShaderProperties.HorizonRemap),
            horizonColor = Shader.GetGlobalColor(ShaderProperties.SkyColorHorizon),
            fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0.01f,
            ambientLight = RenderSettings.ambientLight,
            ambientEquatorColor = RenderSettings.ambientEquatorColor,
            ambientGroundColor = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            sunIntensity = sunLight != null ? sunLight.intensity : 1f,
            sunColor = sunLight != null ? sunLight.color : Color.white,
            sunRotation = sunLight != null ? sunLight.transform.localEulerAngles : Vector3.zero
        };

        sceneSettingsCache[sceneName] = snapshot;

        // Cloud extras
        Vector4 vShape = Shader.GetGlobalVector("_CloudShapeRemap");
        Vector4 vHighl = Shader.GetGlobalVector("_CloudHighlightRemap");
        Vector4 vVolum = Shader.GetGlobalVector("_CloudVolumeRemap");
        initialCloudShapeRemap = new Vector2(vShape.x, vShape.y);
        initialCloudHighlightRemap = new Vector2(vHighl.x, vHighl.y);
        initialCloudVolumeRemap = new Vector2(vVolum.x, vVolum.y);

        var sky = Object.FindFirstObjectByType<Skybox>();
        initialWind1 = sky != null ? sky.wind1 : 1f;
        initialWind2 = sky != null ? sky.wind2 : 2f;

        _currentCloudColor = initialCloudColor;
        _currentCloudShapeRemap = initialCloudShapeRemap;
        _currentHorizonRemap = initialHorizonRemap;
        _currentCloudHighlightRemap = initialCloudHighlightRemap;
        _currentCloudVolumeRemap = initialCloudVolumeRemap;
        _currentWind1 = initialWind1;
        _currentWind2 = initialWind2;
    }
    private static void InitializeForScene(Scene scene)
    {
        if (!scene.IsValid()) return;

        string sceneName = scene.name;

        // Always refresh sun reference for the new scene
        sunLight = RenderSettings.sun;

        // Ensure we have a cached profile for this scene
        if (!sceneSettingsCache.TryGetValue(sceneName, out SceneSettings settings))
        {
            CaptureSceneSettings(sceneName);
            settings = sceneSettingsCache[sceneName];
        }

        // Apply (no-op if settings is null)
        ApplySceneSettings(settings);

        // Update working baselines for this scene (so our effects modulate from the scene’s natural look)
        if (settings != null)
        {
            initialSkyScale = settings.skyScale;
            initialCloudColor = settings.cloudColor;
            initialSkyVolumeStrength = settings.skyVolumeStrength;
            _currentVolumeStrength = initialSkyVolumeStrength;
            initialStarsColor = settings.starsColor;
            initialHorizonRemap = settings.horizonRemap;
            initialHorizonColor = settings.horizonColor;
            initialFogDensity = settings.fogDensity;

            initialAmbientLight = settings.ambientLight;
            initialAmbientEquatorColor = settings.ambientEquatorColor;
            initialAmbientGroundColor = settings.ambientGroundColor;
            initialAmbientIntensity = settings.ambientIntensity;

            if (sunLight != null)
            {
                initialSunColor = settings.sunColor;
                initialSunIntensity = settings.sunIntensity;
                initialSunRotation = settings.sunRotation;
            }
        }

        // Refresh cloud extras from shader + skybox for this scene
        Vector4 vShape = Shader.GetGlobalVector("_CloudShapeRemap");
        Vector4 vHighl = Shader.GetGlobalVector("_CloudHighlightRemap");
        Vector4 vVolum = Shader.GetGlobalVector("_CloudVolumeRemap");
        initialCloudShapeRemap = new Vector2(vShape.x, vShape.y);
        initialCloudHighlightRemap = new Vector2(vHighl.x, vHighl.y);
        initialCloudVolumeRemap = new Vector2(vVolum.x, vVolum.y);

        var skyNow = Object.FindFirstObjectByType<Skybox>();
        initialWind1 = skyNow != null ? skyNow.wind1 : initialWind1;
        initialWind2 = skyNow != null ? skyNow.wind2 : initialWind2;

        // Reset runtime caches to this scene’s baseline
        _currentCloudColor = initialCloudColor;
        _currentCloudShapeRemap = initialCloudShapeRemap;
        _currentHorizonRemap = initialHorizonRemap;
        _currentCloudHighlightRemap = initialCloudHighlightRemap;
        _currentCloudVolumeRemap = initialCloudVolumeRemap;
        _currentWind1 = initialWind1;
        _currentWind2 = initialWind2;

        // Reset sun angle tracking to the scene’s baseline
        sunAngleTarget = initialSunRotation.x;
        sunAngleCurrent = initialSunRotation.x;
        transitioningToEdge = false;
    }

    public static void Initialize()
    {
        if (isInitialized) return;

        // Ensure single subscription
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (audioProcessor == null)
        {
            // Create host GameObject for our runtime processors
            GameObject processorObj = new("AudioSpectrumProcessor");
            Object.DontDestroyOnLoad(processorObj);

            // Audio analysis pipeline
            audioProcessor = processorObj.AddComponent<UnifiedAudioProcessor>();

            // On-screen debug (optional)
            debugRenderer = processorObj.AddComponent<DebugRenderer>();
            debugRenderer.SetAudioProcessor(audioProcessor);

            // Capture current global baselines (shader + ambient)
            initialFogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0.01f;
            initialSkyScale = Shader.GetGlobalFloat(ShaderProperties.SkyScale);
            initialCloudColor = Shader.GetGlobalColor(ShaderProperties.CloudColor);
            initialSkyVolumeStrength = Shader.GetGlobalFloat(ShaderProperties.SkyVolumeStrength);
            initialStarsColor = Shader.GetGlobalColor(ShaderProperties.SkyColorStars);
            initialHorizonRemap = Shader.GetGlobalVector(ShaderProperties.HorizonRemap);
            initialHorizonColor = Shader.GetGlobalColor(ShaderProperties.SkyColorHorizon);

            initialAmbientLight = RenderSettings.ambientLight;
            initialAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            initialAmbientGroundColor = RenderSettings.ambientGroundColor;
            initialAmbientIntensity = RenderSettings.ambientIntensity;

            // Sun reference
            sunLight = RenderSettings.sun;
            if (sunLight != null)
            {
                initialSunColor = sunLight.color;
                initialSunIntensity = sunLight.intensity;
                initialSunRotation = sunLight.transform.localEulerAngles;
            }

            // Cloud extras (read current shader globals and skybox component if present)
            Vector4 vShape = Shader.GetGlobalVector("_CloudShapeRemap");
            Vector4 vHighl = Shader.GetGlobalVector("_CloudHighlightRemap");
            Vector4 vVolum = Shader.GetGlobalVector("_CloudVolumeRemap");
            initialCloudShapeRemap = new Vector2(vShape.x, vShape.y);
            initialCloudHighlightRemap = new Vector2(vHighl.x, vHighl.y);
            initialCloudVolumeRemap = new Vector2(vVolum.x, vVolum.y);

            var sky = Object.FindFirstObjectByType<Skybox>();
            initialWind1 = sky != null ? sky.wind1 : 1f;
            initialWind2 = sky != null ? sky.wind2 : 2f;

            // Seed runtime caches
            _currentCloudColor = initialCloudColor;
            _currentCloudShapeRemap = initialCloudShapeRemap;
            _currentHorizonRemap = initialHorizonRemap;
            _currentCloudHighlightRemap = initialCloudHighlightRemap;
            _currentCloudVolumeRemap = initialCloudVolumeRemap;
            _currentWind1 = initialWind1;
            _currentWind2 = initialWind2;
        }

        // Initialize current scene profile and apply it
        InitializeForScene(SceneManager.GetActiveScene());

        isInitialized = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeForScene(scene);

        // Reset beat detection
        if (audioProcessor != null)
        {
            audioProcessor.ResetDetectors();
        }
    }





    private static void ApplySceneSettings(SceneSettings settings)
    {
        if (settings == null) return;

        // Shader globals (sky + clouds subset covered by our ShaderProperties)
        Shader.SetGlobalFloat(ShaderProperties.SkyScale, settings.skyScale);
        Shader.SetGlobalColor(ShaderProperties.CloudColor, settings.cloudColor);
        Shader.SetGlobalFloat(ShaderProperties.SkyVolumeStrength, settings.skyVolumeStrength);
        Shader.SetGlobalColor(ShaderProperties.SkyColorStars, settings.starsColor);
        Shader.SetGlobalVector(ShaderProperties.HorizonRemap, settings.horizonRemap);
        Shader.SetGlobalColor(ShaderProperties.SkyColorHorizon, settings.horizonColor);

        // Ambient lighting (RenderSettings)
        RenderSettings.ambientLight = settings.ambientLight;
        RenderSettings.ambientEquatorColor = settings.ambientEquatorColor;
        RenderSettings.ambientGroundColor = settings.ambientGroundColor;
        RenderSettings.ambientIntensity = settings.ambientIntensity;

        // Fog (respect whether fog is enabled in this scene)
        if (RenderSettings.fog)
        {
            RenderSettings.fogDensity = settings.fogDensity;
        }

        // Sun (if present)
        if (sunLight != null)
        {
            sunLight.intensity = settings.sunIntensity;
            sunLight.color = settings.sunColor;
            sunLight.transform.localEulerAngles = settings.sunRotation;
        }

        // Cloud extras: apply to current shader state if we’ve captured them
        // These are not stored inside SceneSettings (game’s class), so we use our cached “initial*” fields.
        // Safe no-ops if you didn’t initialize cloud extras.
        Shader.SetGlobalVector("_CloudShapeRemap", new Vector4(initialCloudShapeRemap.x, initialCloudShapeRemap.y, 0f, 0f));
        Shader.SetGlobalVector("_CloudHighlightRemap", new Vector4(initialCloudHighlightRemap.x, initialCloudHighlightRemap.y, 0f, 0f));
        Shader.SetGlobalVector("_CloudVolumeRemap", new Vector4(initialCloudVolumeRemap.x, initialCloudVolumeRemap.y, 0f, 0f));

        // Wind is on the Skybox component, not a shader global
        var sky = Object.FindFirstObjectByType<Skybox>();
        if (sky != null)
        {
            sky.wind1 = initialWind1;
            sky.wind2 = initialWind2;
        }
    }

    // Prime shader globals and our baselines directly from the scene's Skybox component
    private static void PrimeFromComponent(Skybox sky)
    {
        if (sky == null) return;

        // 1) Force shader globals to the scene's authored values
        Shader.SetGlobalFloat("_SkyScale", sky.scale);
        Shader.SetGlobalFloat("_SkyTextureInfluence", sky.skyTextureInfluence);
        Shader.SetGlobalFloat("_SkyVolumeStrength", sky.volumeStrength);
        Shader.SetGlobalFloat("_SkyTex1Scale", sky.tex1Scale);
        Shader.SetGlobalFloat("_SkyWind1", sky.wind1);
        Shader.SetGlobalFloat("_SkyTex2Scale", sky.tex2Scale);
        Shader.SetGlobalFloat("_SkyWind2", sky.wind2);
        Shader.SetGlobalFloat("_SkyTex2Strength", sky.tex2Strength);
        Shader.SetGlobalFloat("_SkyTexStarsScale", sky.skyTexStarsScale);
        Shader.SetGlobalVector("_SkyOffsetMinMax", new Vector4(sky.skyShapeOffset, sky.skyShapeMin, sky.skyShapeMax, 0f));
        Shader.SetGlobalVector("_CloudShapeRemap", new Vector4(sky.cloudShapeRemap.x, sky.cloudShapeRemap.y, 0f, 0f));
        Shader.SetGlobalVector("_CloudHighlightRemap", new Vector4(sky.highlightRemap.x, sky.highlightRemap.y, 0f, 0f));
        Shader.SetGlobalVector("_CloudVolumeRemap", new Vector4(sky.volumeRemap.x, sky.volumeRemap.y, 0f, 0f));
        Shader.SetGlobalColor("_SkyLightTint", sky.skyLightTint);
        Shader.SetGlobalColor("_SkyFinalTint", sky.skyFinalTint);
        Shader.SetGlobalColor("_SkyColorBright", sky.skyColorBright);
        Shader.SetGlobalColor("_SkyColorDark", sky.skyColorDark);
        Shader.SetGlobalColor("_CloudColor", sky.cloudColor);
        Shader.SetGlobalColor("_SkyColorStars", sky.skyColorStars);
        Shader.SetGlobalColor("_SkyColorHorizon", sky.skyColorHorizon);
        Shader.SetGlobalVector("_HorizonRemap", new Vector4(sky.horizonRemap.x, sky.horizonRemap.y, 0f, 0f));
        Shader.SetGlobalColor("_SkyColorGround", sky.skyColorGround);
        Shader.SetGlobalVector("_GroundRemap", new Vector4(sky.groundRemap.x, sky.groundRemap.y, 0f, 0f));
        Shader.SetGlobalFloat("_SunOffset", sky.sunOffset);

        // 2) Seed our baselines from the component (never from possibly polluted globals)
        initialSkyScale = sky.scale;
        initialCloudColor = sky.cloudColor;
        initialSkyVolumeStrength = Mathf.Clamp01(sky.volumeStrength);
        initialStarsColor = Shader.GetGlobalColor(ShaderProperties.SkyColorStars);
        initialHorizonRemap = new Vector4(sky.horizonRemap.x, sky.horizonRemap.y, 0f, 0f);
        initialHorizonColor = sky.skyColorHorizon;
        initialFogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0.01f;

        initialAmbientLight = RenderSettings.ambientLight;
        initialAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        initialAmbientGroundColor = RenderSettings.ambientGroundColor;
        initialAmbientIntensity = RenderSettings.ambientIntensity;

        sunLight = RenderSettings.sun;
        if (sunLight != null)
        {
            initialSunColor = sunLight.color;
            initialSunIntensity = sunLight.intensity;
            initialSunRotation = sunLight.transform.localEulerAngles;
        }

        // Cloud extras
        initialCloudShapeRemap = sky.cloudShapeRemap;
        initialCloudHighlightRemap = sky.highlightRemap;
        initialCloudVolumeRemap = sky.volumeRemap;
        initialWind1 = sky.wind1;
        initialWind2 = sky.wind2;

        // 3) Reset runtime caches to this baseline
        _currentCloudColor = initialCloudColor;
        _currentCloudShapeRemap = initialCloudShapeRemap;
        _currentCloudHighlightRemap = initialCloudHighlightRemap;
        _currentCloudVolumeRemap = initialCloudVolumeRemap;
        _currentHorizonRemap = initialHorizonRemap;
        _currentWind1 = initialWind1;
        _currentWind2 = initialWind2;
        _currentVolumeStrength = initialSkyVolumeStrength;

        // 4) Cache a fresh scene snapshot (safe)
        var sceneName = SceneManager.GetActiveScene().name;
        if (!sceneSettingsCache.ContainsKey(sceneName))
        {
            sceneSettingsCache[sceneName] = new SceneSettings
            {
                skyScale = initialSkyScale,
                cloudColor = initialCloudColor,
                skyVolumeStrength = initialSkyVolumeStrength,
                starsColor = initialStarsColor,
                horizonRemap = initialHorizonRemap,
                horizonColor = initialHorizonColor,
                fogDensity = initialFogDensity,
                ambientLight = initialAmbientLight,
                ambientEquatorColor = initialAmbientEquatorColor,
                ambientGroundColor = initialAmbientGroundColor,
                ambientIntensity = initialAmbientIntensity,
                sunIntensity = initialSunIntensity,
                sunColor = initialSunColor,
                sunRotation = initialSunRotation,
                CloudShapeRemap = initialCloudShapeRemap,
                CloudHighlightRemap = initialCloudHighlightRemap,
                CloudVolumeRemap = initialCloudVolumeRemap,
                Wind1 = initialWind1,
                Wind2 = initialWind2
            };
        }
    }

    private static Vector2 EnsureValidRemap(Vector2 remap, Vector2 initial, float minRange = 0.1f, float maxRange = 50f)
    {
        // Ensure X is always less than Y with a minimum range
        if (remap.x >= remap.y)
        {
            // If invalid, reset to initial values with safe range
            float center = (initial.x + initial.y) * 0.5f;
            float range = Mathf.Max(minRange, Mathf.Abs(initial.y - initial.x) * 0.5f);
            remap.x = center - range;
            remap.y = center + range;
        }

        // Ensure minimum range
        float currentRange = remap.y - remap.x;
        if (currentRange < minRange)
        {
            float center = (remap.x + remap.y) * 0.5f;
            remap.x = center - minRange * 0.5f;
            remap.y = center + minRange * 0.5f;
        }

        // Ensure maximum range
        if (currentRange > maxRange)
        {
            float center = (remap.x + remap.y) * 0.5f;
            remap.x = center - maxRange * 0.5f;
            remap.y = center + maxRange * 0.5f;
        }

        return remap;
    }

    // Put this inside DebugRenderer (or a shared utility if you prefer).
    private static float[] BandVisWeights;
    public static float[] GetBandVisWeights() => BandVisWeights;
    private static int _lastWeightBands = -1;

    // concave increase ~log-like: low bands near `low`, high bands near `high`
    private static float[] ComputeReverseLogWeights(int n, float low = 0.65f, float high = 1.90f, float gamma = 0.72f)
    {
        if (n <= 0) return new float[0];
        var w = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (n == 1) ? 1f : (i / (float)(n - 1));     // 0..1
            float s = Mathf.Pow(t, gamma);                      // concave ramp
            w[i] = Mathf.Lerp(low, high, s);
        }
        return w;
    }

    private static void EnsureWeights()
    {
        if (audioProcessor == null)
        {
            // Use default weights for 9 bands
            if (BandVisWeights == null || _lastWeightBands != 9)
            {
                BandVisWeights = ComputeReverseLogWeights(9);
                _lastWeightBands = 9;
            }
            return;
        }

        if (BandVisWeights == null || _lastWeightBands != audioProcessor.bands)
        {
            BandVisWeights = ComputeReverseLogWeights(audioProcessor.bands);
            _lastWeightBands = audioProcessor.bands;
        }
    }

    public static void PrimeCurrentScene()
    {
        // Find the active Skybox component in the loaded scene
        var sky = Object.FindFirstObjectByType<Skybox>();
        // Ensure our private priming routine was compiled 
        var method = typeof(SkyboxVisualizer).GetMethod("PrimeFromComponent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (sky != null && method != null)
        {
            method.Invoke(null, new object[] { sky });
            // mark as primed so PostfixUpdate will run visualization
            var primedField = typeof(SkyboxVisualizer).GetField("_scenePrimed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (primedField != null) primedField.SetValue(null, true);
        }
    }

    private static void RestoreInitialValues()
    {
        try
        {
            // Restore shader properties
            Shader.SetGlobalFloat(ShaderProperties.SkyScale, initialSkyScale);
            Shader.SetGlobalColor(ShaderProperties.CloudColor, initialCloudColor);
            Shader.SetGlobalFloat(ShaderProperties.SkyVolumeStrength, initialSkyVolumeStrength);
            Shader.SetGlobalColor(ShaderProperties.SkyColorStars, initialStarsColor);
            Shader.SetGlobalVector(ShaderProperties.HorizonRemap, initialHorizonRemap);
            Shader.SetGlobalColor(ShaderProperties.SkyColorHorizon, initialHorizonColor);

            // Restore cloud extras
            Shader.SetGlobalVector("_CloudShapeRemap", new Vector4(initialCloudShapeRemap.x, initialCloudShapeRemap.y, 0f, 0f));
            Shader.SetGlobalVector("_CloudHighlightRemap", new Vector4(initialCloudHighlightRemap.x, initialCloudHighlightRemap.y, 0f, 0f));
            Shader.SetGlobalVector("_CloudVolumeRemap", new Vector4(initialCloudVolumeRemap.x, initialCloudVolumeRemap.y, 0f, 0f));

            // Restore wind values if skybox component exists
            var sky = Object.FindFirstObjectByType<Skybox>();
            if (sky != null)
            {
                sky.wind1 = initialWind1;
                sky.wind2 = initialWind2;
            }

            // Restore lighting
            if (sunLight != null)
            {
                sunLight.color = initialSunColor;
                sunLight.intensity = initialSunIntensity;
                sunLight.transform.localEulerAngles = initialSunRotation;
            }

            // Restore ambient lighting
            RenderSettings.ambientLight = initialAmbientLight;
            RenderSettings.ambientEquatorColor = initialAmbientEquatorColor;
            RenderSettings.ambientGroundColor = initialAmbientGroundColor;
            RenderSettings.ambientIntensity = initialAmbientIntensity;

            // Restore fog
            if (RenderSettings.fog)
            {
                RenderSettings.fogDensity = initialFogDensity;
            }

            Debug.Log("Visualizer effects restored to initial values");
        }
        catch (UnityException e)
        {
            Debug.LogError($"Failed to restore initial values: {e.Message}");
        }
    }



    public static void Reset()
    {
        isInitialized = false;
        _scenePrimed = false;
        _lastSkybox = null;
        _lastSceneIndex = -1;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneSettingsCache.Clear();
        CaptureSceneSettings(SceneManager.GetActiveScene().name);
        sceneSettingsCache.Clear();

        if (audioProcessor != null)
            Object.Destroy(audioProcessor.gameObject);

        audioProcessor = null;
        debugRenderer = null;
        sunLight = null;

        // Restore fog density
        if (RenderSettings.fog)
        {
            RenderSettings.fogDensity = initialFogDensity;
        }
    }
}

