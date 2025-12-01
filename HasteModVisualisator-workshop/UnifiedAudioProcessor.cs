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
using Landfall.Haste.Music;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Processes audio spectrum data and detects beats in multiple frequency bands
/// </summary>
/// <remarks>
/// This class handles the audio processing pipeline including FFT analysis,
/// band separation, and beat detection across multiple frequency ranges.
/// </remarks>
public class UnifiedAudioProcessor : MonoBehaviour
{
    // ========================
    // CONFIGURATION PARAMETERS
    // ========================
    [Tooltip("Number of samples to capture from audio spectrum")]
    public int sampleCount = 1024;

    [Tooltip("Number of frequency bands to analyze")]
    public int bands = 9;

    [Tooltip("Speed at which band values decay when no new energy is detected")]
    public float falloffSpeed = 0.08f;

    [Tooltip("Overall sensitivity multiplier for audio processing")]
    public float sensitivity = 1f;

    [Tooltip("Audio source to analyze (auto-detected if not set)")]
    public AudioSource audioSource;

    [Tooltip("Index of the most energetic frequency band")]
    public int DominantBand { get; private set; } = -1;

    // Labels for visualization/debugging
    public string[] bandLabels = new string[]
    {
        "SubBass", "LowBass", "HighBass", "LowMid", "Mid", "HighMid", "PresenceLow", "PresenceHigh", "Air"
    };

    public bool IsSilent { get; private set; }

    // ===================
    // BAND WEIGHT CACHING
    // ===================
    private static float[] _bandWeights;   // shared across instances (depends only on 'bands' policy)
    private static float _weightsNorm = 1f;
    private static int _weightsForBands = -1; // remember for which 'bands' count the weights were computed

    // ===================
    // Gaussian Smoothing
    // ===================
    private float[] gaussianKernel; // Cache the kernel for performance.
    private int lastKernelSize = -1; // Cache last kernel size to avoid recomputation.
    private float lastKernelIntensity = -1f; // Cache last intensity.


    // =================
    // PROCESSING STATE
    // =================
    private float[] _currentEnergies;              // Current energy values for each band + total
    public BeatDetector[] bandDetectors { get; private set; }           // Beat detectors for individual bands
    public BeatDetector generalBeat { get; private set; }               // Beat detector for overall energy
    private (int start, int end)[] _bandRanges;    // Precomputed spectrum indices for each band

    private float[] rawSpectrum;       // Raw FFT spectrum data
    private float[] bandBuffer;        // Buffered band values for smooth decay
    private float[] bufferDecrease;    // Per-band decay rates
    private float[] bandValues;        // Final processed band values
    private float[] _bandEma;      // init to small >0
    private float[] _bandGain;     // per-band normalization gain
    [SerializeField] private float emaHalfLife = 1.2f; // seconds, tweakable
    [SerializeField] private float targetBandLevel = 0.15f; // desired median
    private float[] _noiseEma;
    [SerializeField] private float noiseTau = 2.0f;

    // ==================
    // BEAT COORDINATION
    // ==================
    public BeatCoordinator beatCoordinator = new BeatCoordinator();
    private TrackChangeDetector _trackChangeDetector;
    private float _beatUpdateTimer;
    private readonly List<BeatDetector> _coordinatorBuffer = new List<BeatDetector>(16);

    // Frequency ranges for each band (in Hz)
    private static readonly float[] frequencyRanges = {
        65f,    // 0-65Hz (Sub-bass: deep kicks, rumble)
        130f,   // 65-130Hz (Bass: main kick/bassline)
        261f,   // 130-261Hz (Low mids: snares/toms)
        523f,   // 261-523Hz (Midrange: lower vocals)
        1046f,  // 523-1046Hz (Upper mids: vocals/guitars)
        2093f,  // 1046-2093Hz (Presence: attack of instruments)
        4186f,  // 2093-4186Hz (Brilliance: hi-hats/cymbals)
        8372f,  // 4186-8372Hz (Ultra high: sharp transients)
        16000f  // 8372-16000Hz (Air: subtle harmonics)
    };

    private bool _initialized;
    void Start()
    {
        // Initialize immediately for critical components
        InitializeArrays();
        CacheBandRanges();
        FindAudioSource();

        // Create detectors immediately
        bandDetectors = new BeatDetector[bands];
        for (int i = 0; i < bands; i++)
            bandDetectors[i] = new BeatDetector();

        _currentEnergies = new float[bands + 1];

        // Configure immediately
        ConfigureBeatDetectors();

        // Create general beat detector
        generalBeat = new BeatDetector();
        ConfigureGeneralBeat();

        // Add track detector
        _trackChangeDetector = gameObject.AddComponent<TrackChangeDetector>();
        RecomputeBandWeights(bands, force: true);
        if (beatCoordinator != null && _bandWeights != null)
            beatCoordinator.SetBandWeights(_bandWeights);

        _initialized = true;
    }

    /// <summary>
    /// Applies Gaussian smoothing to the raw spectrum data.
    /// </summary>
    private void ApplyGaussianSmoothing()
    {
        if (rawSpectrum == null || rawSpectrum.Length == 0) return;

        int kernelSize = VisualizerLandfallConfig.CurrentConfig.GaussianKernelSize;
        float intensity = VisualizerLandfallConfig.CurrentConfig.GaussianKernelIntensity;

        // Ensure kernel size is odd and within bounds
        if (kernelSize % 2 == 0) kernelSize++; // Force odd size
        kernelSize = Mathf.Clamp(kernelSize, 3, 11);

        // Recompute kernel only if size or intensity has changed
        if (gaussianKernel == null || kernelSize != lastKernelSize || !Mathf.Approximately(intensity, lastKernelIntensity))
        {
            gaussianKernel = GenerateGaussianKernel(kernelSize, intensity);
            lastKernelSize = kernelSize;
            lastKernelIntensity = intensity;
        }

        // Temporary buffer to store smoothed data
        float[] smoothedSpectrum = new float[rawSpectrum.Length];

        // Apply Gaussian kernel
        int halfSize = kernelSize / 2;
        for (int i = 0; i < rawSpectrum.Length; i++)
        {
            float smoothedValue = 0f;
            for (int k = -halfSize; k <= halfSize; k++)
            {
                int sampleIndex = Mathf.Clamp(i + k, 0, rawSpectrum.Length - 1); // Handle edge cases
                smoothedValue += rawSpectrum[sampleIndex] * gaussianKernel[k + halfSize];
            }
            smoothedSpectrum[i] = smoothedValue;
        }

        // Copy smoothed data back to rawSpectrum
        System.Array.Copy(smoothedSpectrum, rawSpectrum, rawSpectrum.Length);
    }

    /// <summary>
    /// Generates a Gaussian kernel with the specified size and intensity.
    /// </summary>
    /// <param name="size">The size of the kernel (odd number).</param>
    /// <param name="intensity">The spread/intensity of the smoothing (higher = more smoothing).</param>
    /// <returns>Array representing the Gaussian kernel.</returns>
    private float[] GenerateGaussianKernel(int size, float intensity)
    {
        float[] kernel = new float[size];
        int halfSize = size / 2;
        float sigma = intensity;
        float twoSigmaSq = 2f * sigma * sigma;
        float sqrtTwoPiSigma = Mathf.Sqrt(2f * Mathf.PI) * sigma;

        for (int i = -halfSize; i <= halfSize; i++)
        {
            kernel[i + halfSize] = Mathf.Exp(-i * i / twoSigmaSq) / sqrtTwoPiSigma;
        }

        // Normalize kernel to ensure the sum is 1
        float sum = 0f;
        foreach (float val in kernel) sum += val;
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        return kernel;
    }

    /// <summary>
    /// Computes perceptual weights for each band and a normalization factor,
    /// only when the 'bands' count changes or when forced.
    /// </summary>
    public static void RecomputeBandWeights(int bands, bool force = false)
    {
        if (!force && _bandWeights != null && _weightsForBands == bands)
            return;

        if (bands <= 0) bands = 1;

        // Allocate and compute
        _bandWeights = new float[bands];
        float sum = 0f;

        for (int i = 0; i < bands; i++)
        {
            // Perceptual weighting curve (log-like)
            float w = 1.0f - (0.15f * i);

            // Bass emphasis for first 3 bands
            if (i < 3) w *= 1.2f;

            // Clamp to reasonable range
            w = Mathf.Clamp(w, 0.5f, 1.3f);

            _bandWeights[i] = w;
            sum += w;
        }

        // Normalize so average weight ~1.0
        _weightsNorm = bands / Mathf.Max(0.0001f, sum);
        _weightsForBands = bands;
    }

    void ConfigureGeneralBeat()
    {
        if (generalBeat != null)
        {
            generalBeat.adaptiveThreshold = new AdaptiveThreshold
            {
                minThreshold = 0.06f,
                decayRate = 5.91f,
                sensitivity = 1.3f
            };
            generalBeat.beatThresholdMultiplier = 1.23f;
            generalBeat.beatCooldownTime = 0.14f;
            generalBeat.MinExceed = 0.06f;
            generalBeat.beatDecay = 0.75f;
        }
    }

    private IEnumerator InitializeCoroutine()
    {
        InitializeArrays();
        CacheBandRanges();
        FindAudioSource();

        // Wait until next frame to create detectors
        yield return null;

        bandDetectors = new BeatDetector[bands];
        for (int i = 0; i < bands; i++)
            bandDetectors[i] = new BeatDetector();

        _currentEnergies = new float[bands + 1];
        ConfigureBeatDetectors();

        // Wait another frame before adding track detector
        yield return null;
        _trackChangeDetector = gameObject.AddComponent<TrackChangeDetector>();

        _initialized = true;
    }

    void OnDestroy()
    {
        if (_trackChangeDetector != null)
            Destroy(_trackChangeDetector);
    }

    /// <summary>
    /// Precomputes spectrum index ranges for each frequency band
    /// </summary>
    private void CacheBandRanges()
    {
        float nyquist = AudioSettings.outputSampleRate / 2f;
        float binSize = nyquist / sampleCount;
        _bandRanges = new (int start, int end)[bands];

        for (int band = 0; band < bands; band++)
        {
            // Calculate frequency range for this band
            float startFreq = band == 0 ? 0 : frequencyRanges[band - 1];
            float endFreq = frequencyRanges[band];

            // Convert frequencies to spectrum indices
            int startIndex = Mathf.FloorToInt(startFreq / binSize);
            int endIndex = Mathf.FloorToInt(endFreq / binSize);

            // Clamp to valid range
            startIndex = Mathf.Clamp(startIndex, 0, sampleCount - 1);
            endIndex = Mathf.Clamp(endIndex, 0, sampleCount - 1);

            // Handle edge cases
            if (endIndex < startIndex) endIndex = startIndex;
            _bandRanges[band] = (startIndex, endIndex);
        }
    }

    /// <summary>
    /// Locates the active audio source (MusicPlayer or fallback)
    /// </summary>
    private void FindAudioSource()
    {
        try
        {
            // First try to use the game's music player
            if (MusicPlayer.Instance != null && MusicPlayer.Instance.m_AudioSourceCurrent != null)
            {
                audioSource = MusicPlayer.Instance.m_AudioSourceCurrent;
                Debug.Log($"[AudioSpectrum] Using MusicPlayer audio source: {audioSource.name}");
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[AudioSpectrum] MusicPlayer error: {e.Message}");
        }

        // Fallback to any available audio source
        audioSource = FindFirstObjectByType<AudioSource>();
        if (audioSource != null)
            Debug.Log($"[AudioSpectrum] Using fallback audio source: {audioSource.name}");
        else
            Debug.LogWarning("[AudioSpectrum] No audio source found");
    }

    /// <summary>
    /// Initializes processing arrays
    /// </summary>
    public void InitializeArrays()
    {
        rawSpectrum = new float[sampleCount];
        bandBuffer = new float[bands];
        bufferDecrease = new float[bands];
        bandValues = new float[bands];

        // Clear arrays to initial state
        System.Array.Clear(rawSpectrum, 0, rawSpectrum.Length);
        System.Array.Clear(bandBuffer, 0, bandBuffer.Length);
        System.Array.Clear(bufferDecrease, 0, bufferDecrease.Length);
        System.Array.Clear(bandValues, 0, bandValues.Length);
        _bandEma = new float[bands];
        _bandGain = new float[bands];
        for (int i = 0; i < bands; i++) { _bandEma[i] = 0.05f; _bandGain[i] = 1f; }
        _noiseEma = new float[bands];
    }

    private void UpdateNoiseFloor(int i, float val)
    {
        // only update when energy is low to avoid bias
        if (val < _bandEma[i] * 0.7f)
        {
            float a = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.1f, noiseTau));
            _noiseEma[i] = Mathf.Lerp(_noiseEma[i], val, a);
        }
    }

    private void UpdateBandAdaptiveGain(int i, float value)
    {
        float lambda = Mathf.Exp(-Mathf.Log(2f) * Time.deltaTime / Mathf.Max(0.05f, emaHalfLife));
        _bandEma[i] = Mathf.Lerp(value, _bandEma[i], lambda);
        float g = targetBandLevel / Mathf.Max(0.0005f, _bandEma[i]);
        _bandGain[i] = Mathf.Clamp(g, 0.5f, 3.0f); // keep it sane
    }

    void Update()
    {
        if (!_initialized) return;


        bool gotAudio = false;
        IsSilent = true; // Assume silent until proven otherwise
        // Capture spectrum data from current audio source
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.GetSpectrumData(rawSpectrum, 0, FFTWindow.BlackmanHarris);
            gotAudio = true;

            // Check if audio is actually silent
            float sum = 0f;
            for (int i = 0; i < rawSpectrum.Length; i++)
            {
                sum += rawSpectrum[i];
            }
            IsSilent = sum < 0.001f; // Adjust threshold as needed
        }
        else  // Fallback to global audio listener
        {
            AudioListener.GetSpectrumData(rawSpectrum, 0, FFTWindow.BlackmanHarris);
            gotAudio = true;

            // Check if audio is actually silent
            float sum = 0f;
            for (int i = 0; i < rawSpectrum.Length; i++)
            {
                sum += rawSpectrum[i];
            }
            IsSilent = sum < 0.001f; // Adjust threshold as needed
        }

        if (gotAudio)
        {
            ApplyGaussianSmoothing(); // Smooth raw spectrum data.
            ProcessBands();           // Analyze captured audio data.
            UpdateBeatDetectors();
        }

        // Safe coordinator update
        if (beatCoordinator != null)
        {
            _coordinatorBuffer.Clear();

            if (bandDetectors != null)
            {
                for (int i = 0; i < bandDetectors.Length; i++)
                {
                    if (bandDetectors[i] != null)
                        _coordinatorBuffer.Add(bandDetectors[i]);
                }
            }

            if (generalBeat != null)
                _coordinatorBuffer.Add(generalBeat);

            if (_coordinatorBuffer.Count > 0)
                beatCoordinator.Update(_coordinatorBuffer);
        }
    }

    /// <summary>
    /// Updates all beat detectors with current energy values
    /// </summary>
    private void UpdateBeatDetectors()
    {
        // Update individual band detectors
        if (bandDetectors != null)
        {
            int len = Mathf.Min(bandDetectors.Length, bands);
            for (int i = 0; i < len; i++)
                bandDetectors[i].Detect(_currentEnergies[i], i, IsSilent);
        }

        // General beat detector with average energy
        if (generalBeat != null)
            generalBeat.Detect(_currentEnergies[bands], bands, IsSilent);
    }

    /// <summary>
    /// Processes raw spectrum data into frequency bands
    /// </summary>
    private void ProcessBands()
    {
        if (rawSpectrum == null || rawSpectrum.Length == 0) return;

        int bandsToProcess = bands;

        // Ensure perceptual weights are ready
        RecomputeBandWeights(bandsToProcess);

        float maxBandValue = 0f;
        DominantBand = -1;

        for (int band = 0; band < bandsToProcess; band++)
        {
            // Validate band range
            if (_bandRanges == null || band >= _bandRanges.Length)
            {
                bandValues[band] = 0f;
                _currentEnergies[band] = 0f;
                continue;
            }

            var (startIndex, endIndex) = _bandRanges[band];
            int sampleRange = endIndex - startIndex + 1;
            if (sampleRange <= 0)
            {
                bandValues[band] = 0f;
                _currentEnergies[band] = 0f;
                continue;
            }

            // Sum spectrum values within this band (with improved weighting curve)
            float sum = 0f;
            for (int j = startIndex; j <= endIndex; j++)
            {
                if (j < 0 || j >= rawSpectrum.Length) continue;
                float freqPosition = (j - startIndex) / (float)sampleRange;

                // Adjusted frequency weighting curve (logarithmic-like)
                float freqWeight = Mathf.Lerp(1f, 1.2f, Mathf.Pow(freqPosition, 0.8f));
                sum += rawSpectrum[j] * freqWeight;
            }

            // Raw average energy for the band
            float average = sum / sampleRange;

            // Apply cached perceptual band weighting
            float w = (band < _bandWeights.Length ? _bandWeights[band] : 1f) * _weightsNorm;
            average *= w;

            // Noise floor on raw band energy (pre-log, pre-gain)
            UpdateNoiseFloor(band, average);

            // Gate using the same domain
            float noiseGate = _noiseEma[band] * (band < 3 ? 1.3f : 1.6f);
            if (average < noiseGate) average = 0f;

            // Inverse-log compression (improved for higher bands)
            float startFreq = band == 0 ? 0 : frequencyRanges[band - 1];
            float endFreq = frequencyRanges[band];
            float centerHz = (startFreq + endFreq) * 0.5f;
            // Inverse-log pre-gain
            float exp = Mathf.Lerp(1.0f, 0.75f, Mathf.InverseLerp(60f, 16000f, centerHz));
            float preGain = Mathf.Pow(average, exp);

            // Feed normalization on pre-gain/pre-sensitivity
            UpdateBandAdaptiveGain(band, preGain);

            // Apply sensitivity and gain after
            float scaled = preGain * sensitivity * _bandGain[band];

            // Band-specific falloff adjustment
            bufferDecrease[band] = Mathf.Lerp(falloffSpeed, falloffSpeed * 3f, bandBuffer[band] / 5f);
            if (band < 3)
            {
                // Boost bass bands
                scaled *= 2.0f;
                bufferDecrease[band] *= 0.8f; // Slow decay for bass
            }
            else if (band > 6)
            {
                // Increase decay for higher bands
                scaled *= 1.2f;
                bufferDecrease[band] *= 1.1f;
            }
            float prev = bandBuffer[band];
            float riseLimit = (band < 3) ? 2.2f : (band <= 6 ? 1.8f : 1.5f);
            if (prev > 0f && scaled > prev * riseLimit)
                scaled = prev * riseLimit;

            // Buffer update
            if (scaled > bandBuffer[band])
                bandBuffer[band] = Mathf.Clamp(scaled, 0f, 5f);
            else
                bandBuffer[band] = Mathf.Max(0f, bandBuffer[band] - bufferDecrease[band]);

            // Final logarithmic scaling
            float k = (band < 3) ? 6f : 9f; // Less compressive feed for bass
            float scale = (band < 3) ? 1.8f : 1.5f; // Slightly boost bass display
            bandValues[band] = Mathf.Clamp(Mathf.Log10(1f + bandBuffer[band] * k) * scale, 0.001f, 2f);

            _currentEnergies[band] = bandValues[band];

            // Track dominant band
            if (bandValues[band] > maxBandValue)
            {
                maxBandValue = bandValues[band];
                DominantBand = band;
            }
        }

        // Total energy for general beat detector
        float total = 0f;
        for (int i = 0; i < bandsToProcess; i++) total += _currentEnergies[i];
        if (_currentEnergies.Length > bands)
            _currentEnergies[bands] = total / Mathf.Max(bandsToProcess, 1);
    }


    /// <summary>
    /// Configures beat detection parameters for each band
    /// </summary>
    void ConfigureBeatDetectors()
    {
        float[] defaultMinThresholds = {
            0.16f, 0.13f, 0.12f, 0.11f, 0.10f, 0.09f, 0.07f, 0.06f, 0.055f
        };

        float[] defaultDecayRates = {
            0.96f, 0.96f, 0.96f, 1.19f, 1.25f, 1.37f, 0.91f, 0.84f, 0.81f
        };

        float[] defaultSensitivities = {
            1.55f, 1.42f, 1.31f, 1.1f, 1.08f, 1.04f, 1f, 1.11f, 1.22f
        };

        float[] defaultThresholdMultipliers = {
            0.99f, 0.98f, 0.97f, 1.35f, 1.3f, 1.26f, 1.15f, 0.97f, 0.96f
        };

        float[] defaultCooldowns = {
            0.200f, 0.194f, 0.188f, 0.181f, 0.175f, 0.169f, 0.163f, 0.156f, 0.150f
        };

        float[] defaultMinExceed = {
            0.11f, 0.09f, 0.08f, 0.075f, 0.07f, 0.06f, 0.05f, 0.04f, 0.03f
        };

        float[] defaultBeatDecays = {
            0.98f, 0.97f, 0.96f, 0.86f, 0.87f, 0.88f, 0.89f, 0.90f, 0.91f
        };


        int expected = bands;

        float[] mins = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_MinThresholds ?? "", expected, defaultMinThresholds);
        float[] decays = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_DecayRates ?? "", expected, defaultDecayRates);
        float[] sens = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_Sensitivities ?? "", expected, defaultSensitivities);
        float[] thrMult = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_ThresholdMultipliers ?? "", expected, defaultThresholdMultipliers);
        float[] cooldowns = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_Cooldowns ?? "", expected, defaultCooldowns);
        float[] MinExceed = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_MinExceed ?? "", expected, defaultMinExceed);
        float[] beatDecays = VisualizerLandfallConfig.ParseCsvFloats(VisualizerLandfallConfig.CurrentConfig.Band_BeatDecays ?? "", expected, defaultBeatDecays);

        if (mins == null || mins.Length != expected) mins = defaultMinThresholds;
        if (decays == null || decays.Length != expected) decays = defaultDecayRates;
        if (sens == null || sens.Length != expected) sens = defaultSensitivities;
        if (thrMult == null || thrMult.Length != expected) thrMult = defaultThresholdMultipliers;
        if (cooldowns == null || cooldowns.Length != expected) cooldowns = defaultCooldowns;
        if (MinExceed == null || MinExceed.Length != expected) MinExceed = defaultMinExceed;
        if (beatDecays == null || beatDecays.Length != expected) beatDecays = defaultBeatDecays;

        if (bandDetectors == null || bandDetectors.Length < bands)
        {
            Debug.LogError("Band detectors not properly initialized");
            return;
        }

        // Apply defaults to each band detector
        for (int i = 0; i < bands; i++)
        {
            // Skip if detector slot is null
            if (bandDetectors[i] == null)
            {
                Debug.LogWarning($"Band detector {i} is null, creating new");
                bandDetectors[i] = new BeatDetector();
            }

            var d = bandDetectors[i];
            if (d == null)
            {
                Debug.LogError($"Beat detector for band {i} is null");
                continue;
            }

            d.adaptiveThreshold = new AdaptiveThreshold
            {
                minThreshold = Mathf.Max(0.0001f, mins[i]),
                decayRate = Mathf.Clamp01(decays[i]),
                sensitivity = Mathf.Max(0.001f, sens[i])
            };

            d.beatThresholdMultiplier = Mathf.Max(0.1f, thrMult[i]);
            d.beatCooldownTime = Mathf.Max(0.01f, cooldowns[i]);
            d.MinExceed = Mathf.Max(0.01f, MinExceed[i]);
            d.beatDecay = Mathf.Clamp01(beatDecays[i]);

        }


        // Configure general beat detector (overall energy)
        if (generalBeat != null)
        {
            ConfigureGeneralBeat();
        }
    }



    /// <summary>
    /// Resets all beat detectors (called on track change)
    /// </summary>
    public void ResetDetectors()
    {
        if (bandDetectors != null)
        {
            foreach (var d in bandDetectors)
            {
                if (d != null) d.Reset();
            }
        }

        if (generalBeat != null) generalBeat.Reset();
        ConfigureBeatDetectors();
        Debug.Log("Beat detectors reset");
    }

    /// <summary>
    /// Adaptive threshold structure for beat detection
    /// </summary>
    public struct AdaptiveThreshold
    {
        public float minThreshold;     // Minimum required energy
        public float currentThreshold;  // Current dynamic threshold
        public float decayRate;         // Threshold decay speed
        public float sensitivity;      // Detection sensitivity
    }

    /// <summary>
    /// Beat detection for individual frequency bands
    /// </summary>
    public class BeatDetector
    {
        // Energy tracking
        public float[] _energyHistory { get; private set; }  // Circular buffer of recent energy values
        private int _historyIndex;      // Current position in history buffer
        private float _runningTotal;    // Sum of values in history
        private float _runningTotalSq;  // Sum of squares for variance calculation
        private float _previousEnergy;  // Previous frame's energy for smoothing
        private float _lastBeatDuration = 0.1f;
        private bool _inRefractory = false;   // Are we in "energy must drop" state
        private float _beatStartTime = 0f;

        // Dynamic reset threshold (hysteresis + time-gated "cooldown")
        private bool _needsReset = false;     // must drop below _resetThreshold before new beat
        private float _resetThreshold = 0f;   // moving lower threshold
        private float _resetElapsed = 0f;     // time since last beat, for rise curve
        private float _resetRiseSpeed;
        private float _lastEnergyAverage;
        private bool _inResetPhase;

        // Expose for debug overlay
        public float ResetThresholdDebug => _resetThreshold;

        // Tunables 
        private const float kResetMaxFactor = 0.98f; // upper bound (just under main threshold)
        private const float kBaseResetTau = 0.12f;   // seconds, how fast reset rises by default


        // Detection parameters
        public float beatThresholdMultiplier = 1.5f;  // Threshold multiplier
        public float beatDecay = 0.97f;              // Beat strength decay rate
        public float thresholdSensitivity = 1f;       // Sensitivity adjustment
        public float MinExceed = 0.1f;                  // Minimum beat strength
        public float beatCooldownTime = 0.10f;        // Minimum time between beats
        public float thresholdDecay = 0.97f;          // Threshold decay rate

        // --- Configurable detection params ---
        public float beatThreshold = 1.3f;   // How much above EMA energy to count as a beat
        public int minBeatIntervalMs = 200;  // Cooldown in ms to avoid double-trigger
        public float emaDecay = 0.15f;       // Lower = slower EMA response

        // --- Internal state ---
        private float _energyEMA = 0f;       // Running smoothed average energy
        private float _lastBeatTime = 0f;    // Timestamp of last beat
        private bool _beatDetected = false;  // Flag for external consumers
        private float _currentEnergy = 0f;   // Total or band-specific energy

        // Optional: restrict detection to a range of bands
        public int minDetectBand = 0;
        public int maxDetectBand = -1; // -1 means last band

        // Beat state
        public float beatStrength { get; private set; }        // Current beat strength (0-1)
        public float beatTimer { get; private set; }           // Time remaining for current beat
        private float _currentCooldown;   // Time until next beat can be detected
        public float peakBeatStrength = 0f;    // Peak strength of current beat
        private float _peakHoldTimer;     // Time remaining at peak strength

        // Current analysis state
        public float currentEnergy { get; private set; }       // Current compressed energy
        private float effectiveThreshold;
        public float EffectiveThresholdDebug => effectiveThreshold;
        public float currentThreshold { get; private set; }    // Current detection threshold
        public float averageEnergy = 1f;       // Average energy over history
        public float variance = 1f;            // Energy variance
        public float varianceMultiplier = 1.5f;  // Variance impact on threshold
        public float Confidence { get; private set; }  // Beat detection confidence (0-1)

        public AdaptiveThreshold adaptiveThreshold;  // Adaptive threshold settings




        // Debug state
        private bool Snapped = false;
        public string beatActivationSnapshot = "no beat detected";
        public string activationSnapshot = "no beat detected";
        public float lastBeatTimer = 0f;

        // Initial reset drop factor after a beat (deeper drop for stronger, bassier beats)
        private float InitialResetFactor(float strength, float bandIndex)
        {
            // Band shaping: bass needs deeper reset, highs a lighter one
            bool isBass = bandIndex <= 2f;
            bool isMid = bandIndex >= 3f && bandIndex <= 6f;

            float bandMul = isBass ? 0.85f : isMid ? 0.92f : 0.96f;

            // Strength shaping: stronger beats require deeper dips
            //  r0 ≈ 0.95 for weak, down to ≈ 0.60 for very strong (before bandMul)
            float r0 = Mathf.Lerp(0.95f, 0.60f, Mathf.Clamp01(strength));

            float f = r0 * bandMul;
            return Mathf.Clamp(f, 0.50f, 0.95f);
        }

        // Time constant for reset rising toward main threshold
        private float ComputeResetTau(float strength, float bandIndex)
        {
            // Start from base + per-band bias, then scale with strength
            bool isBass = bandIndex <= 2f;
            bool isHigh = bandIndex >= 7f;

            float bandMul = isBass ? 1.35f : isHigh ? 0.85f : 1.00f;
            float strengthMul = Mathf.Lerp(1.0f, 1.8f, Mathf.Clamp01(strength)); // strong beats rise slower

            // You can blend in beatCooldownTime if you want it to taste like your old cooldown:
            // float baseTau = Mathf.Max(kBaseResetTau, beatCooldownTime * 0.75f);
            float baseTau = kBaseResetTau;

            return Mathf.Clamp(baseTau * bandMul * strengthMul, 0.05f, 0.40f);
        }


        public BeatDetector()
        {
            Reset();
        }

        /// <summary>
        /// Resets detector to initial state
        /// </summary>
        public void Reset()
        {
            // Calculate history size based on config
            float historySeconds = VisualizerLandfallConfig.CurrentConfig.HistoryDuration * 0.25f;
            float fps = 1f / Mathf.Max(Time.deltaTime, 0.0166f); // fallback ≈60 FPS on first frame
            int historySize = Mathf.RoundToInt(historySeconds * fps);

            int minCount = Mathf.RoundToInt(historySeconds * 30f);
            int maxCount = Mathf.RoundToInt(historySeconds * 120f);
            historySize = Mathf.Clamp(historySize, minCount, maxCount);

            // Initialize history buffer
            _energyHistory = new float[historySize];
            _historyIndex = 0;

            // Set initial values to avoid false positives
            float initValue = Mathf.Max(0.0001f, adaptiveThreshold.minThreshold);
            for (int i = 0; i < _energyHistory.Length; i++) _energyHistory[i] = initValue;
            _runningTotal = initValue * _energyHistory.Length;
            _runningTotalSq = initValue * initValue * _energyHistory.Length;

            // Reset state variables
            beatStrength = 0f;
            beatTimer = 0f;
            _currentCooldown = 0f;
            currentEnergy = initValue;
            currentThreshold = initValue;
            averageEnergy = initValue;
            _previousEnergy = initValue;
            variance = 0f;
            beatActivationSnapshot = activationSnapshot = "no beat detected";
            peakBeatStrength = 0f;
            _peakHoldTimer = 0f;
            Snapped = false;
            lastBeatTimer = 0f;
            Confidence = 1f;
            _resetThreshold = 0f;
            _resetElapsed = 0f;
            _needsReset = false;
            _inResetPhase = false;
            _lastEnergyAverage = 0f;
        }

        /// <summary>
        /// Detects beats based on current energy value
        /// </summary>
        /// <param name="energy">Raw energy input for this frame</param>
        /// <param name="bandIndex">Index of the band being processed (used for customization)</param>
        public void Detect(float energy, float bandIndex, bool isSilent = false)
        {
            if (isSilent)
            {
                // Fast decay during silence
                beatStrength = Mathf.Lerp(beatStrength, 0f, Time.deltaTime * 8f);
                beatTimer = Mathf.Max(0f, beatTimer - Time.deltaTime * 2f);
                currentEnergy = 0f;
            }
            float epsilon = 1e-6f;
            float beatDuration = 0f;
            bool beatDetected = false;

            // Get threshold parameters
            float minThresh = Mathf.Max(adaptiveThreshold.minThreshold, 0.0001f);
            float decayRate = Mathf.Clamp01(adaptiveThreshold.decayRate);
            float sensitivityAdj = Mathf.Max(adaptiveThreshold.sensitivity, 0.001f) * VisualizerLandfallConfig.CurrentConfig.BeatSensitivity;

            // Dynamic compression for energy
            float dynamicCompression = Mathf.Lerp(1.0f, 0.6f, averageEnergy);
            float compressedEnergy = energy > 0f ? Mathf.Log10(1f + energy * 50f * dynamicCompression) / 2f : 0f;

            // Early exit for very low energy levels
            if (compressedEnergy < 0.002f)
            {
                float dur = Mathf.Max(_lastBeatDuration, 0.08f);
                beatStrength = Mathf.Lerp(beatStrength, 0f, Time.deltaTime * 6f * dur);
                beatStrength = Mathf.Lerp(beatStrength, beatStrength * beatDecay, Time.deltaTime);
                beatTimer = Mathf.Max(0f, beatTimer - Time.deltaTime);
                lastBeatTimer = beatTimer;
                Confidence = Mathf.Lerp(Confidence, beatDetected ? 1f : 0.5f, Time.deltaTime * 5f);
                return;
            }

            // Adaptive smoothing for energy
            float smoothingFactor = Mathf.Lerp(0.4f, 0.2f, compressedEnergy);
            float smoothedEnergy = Mathf.Lerp(_previousEnergy, compressedEnergy, smoothingFactor);

            bool isBass = bandIndex <= 2f;
            bool isMid = bandIndex >= 3f && bandIndex <= 6f;
            bool isHigh = bandIndex >= 7f;

            float powExp = isBass ? 1.0f : 0.85f; // preserve bass transients more
            float liveEnergy = Mathf.Pow(Mathf.Max(0f, smoothedEnergy), powExp) * sensitivityAdj;
            _previousEnergy = smoothedEnergy;

            // History buffer and statistics
            float oldest = _energyHistory[_historyIndex];
            _runningTotal -= oldest;
            _runningTotalSq -= oldest * oldest;

            _energyHistory[_historyIndex] = smoothedEnergy;
            _runningTotal += smoothedEnergy;
            _runningTotalSq += smoothedEnergy * smoothedEnergy;
            _historyIndex = (_historyIndex + 1) % _energyHistory.Length;

            averageEnergy = _runningTotal / Mathf.Max(_energyHistory.Length, 1);
            float varianceCalc = (_runningTotalSq / Mathf.Max(_energyHistory.Length, 1)) - (averageEnergy * averageEnergy);
            variance = Mathf.Max(0f, varianceCalc);


            // Stability factor and dynamic multiplier
            float stabilityFactor = averageEnergy > epsilon ? Mathf.Clamp01(variance / (averageEnergy + epsilon)) : 0f;
            float dynamicMultiplier = Mathf.Lerp(1.5f, 4.5f, stabilityFactor);

            // Calculate target threshold
            float varianceThreshold = Mathf.Clamp(1.55f - 15f * variance, 0.1f, 2.5f);
            float targetThreshold = Mathf.Lerp(
                averageEnergy + variance * dynamicMultiplier,
                averageEnergy * varianceThreshold,
                stabilityFactor * 1.1f
            );

            if (variance > 0.5f * averageEnergy)
            {
                targetThreshold *= Mathf.Lerp(1.1f, 1.5f, stabilityFactor);
            }

            float k = Mathf.Min(decayRate * Time.deltaTime * (1 + averageEnergy), 0.35f);
            currentThreshold = Mathf.Lerp(currentThreshold <= 0f ? targetThreshold : currentThreshold, targetThreshold, k);

            if (bandIndex <= 2 && currentEnergy > averageEnergy * 1.05f && currentEnergy > effectiveThreshold)
            {
                float stickLerp = 1f - Mathf.Exp(-Time.deltaTime / 0.05f); // ~fast 50ms half-life
                currentThreshold = Mathf.Lerp(currentThreshold, currentEnergy * 0.98f, stickLerp);
            }

            currentThreshold = Mathf.Max(currentThreshold, minThresh);



            // Apply sensitivity and threshold scaling
            effectiveThreshold = currentThreshold * VisualizerLandfallConfig.CurrentConfig.BeatThreshold * Mathf.Max(thresholdSensitivity, 0.001f);
            effectiveThreshold *= Mathf.Max(beatThresholdMultiplier, 0.01f);

            // Final energy after sensitivity adjustment
            currentEnergy = liveEnergy;

            // ==================================================================
            // DUAL-THRESHOLD BEAT DETECTION SYSTEM
            // ==================================================================


            // 1. Handle reset phase logic
            if (_inResetPhase)
            {
                // Dynamic rise speed based on average energy (faster response on high energy)
                // Rise speed depends on average energy in this band: loud = faster rise
                float targetReset;
                if (currentEnergy >= effectiveThreshold) targetReset = Mathf.Max(epsilon, currentEnergy * 0.96f);
                else targetReset = Mathf.Max(minThresh, effectiveThreshold * 0.95f);
                float avgFactor = Mathf.Clamp01(averageEnergy * 2f); // scale to ~0..1
                float tau = Mathf.Lerp(0.20f, 0.05f, avgFactor); // seconds to reach ~63% of target gap

                // Exponential rise toward target
                float t = 1f - Mathf.Exp(-Time.deltaTime / tau);
                _resetThreshold = Mathf.Lerp(_resetThreshold, targetReset, t);

                // Exit reset phase when energy drops below reset threshold
                if (currentEnergy <= _resetThreshold)
                {
                    _inResetPhase = false;
                    activationSnapshot = "Reset cleared";
                }
            }

            // 2. Detect new beats only when not in reset phase
            if (!_inResetPhase)
            {
                float thresholdExcess = currentEnergy - effectiveThreshold;
                if (thresholdExcess > MinExceed)
                {
                    float denom = Mathf.Max(effectiveThreshold, minThresh, epsilon);

                    // Relative excess in 0..1 range
                    float relExcess = Mathf.Clamp01(thresholdExcess / denom);

                    // Weight: give big hits >150% threshold a strong pop, but compress >200%
                    float transientFactor = Mathf.Pow(Mathf.Clamp01(relExcess / 1.5f), 0.6f);

                    // Energy context: fade back when overall averageEnergy is constantly high
                    float energyDamp = 1f - Mathf.Clamp01(averageEnergy * 0.65f);

                    // Blend into final strength
                    float newStrength = transientFactor * (0.85f + averageEnergy * 0.5f) * energyDamp;

                    // Keep HDR headroom for downstream visuals
                    newStrength = Mathf.Clamp(newStrength, 0f, 3f);

                    // Duration scaling by band type
                    float baseDur = isBass ? 0.12f : isMid ? 0.10f : 0.08f;
                    beatDuration = Mathf.Clamp(baseDur + newStrength * 0.10f, baseDur, baseDur * 2f);

                    beatStrength = newStrength;
                    peakBeatStrength = newStrength;
                    beatTimer = beatDuration;
                    _lastBeatDuration = beatDuration;
                    _peakHoldTimer = beatDuration * 0.5f;
                    _currentCooldown = Mathf.Max(0.01f, beatCooldownTime);
                    _beatStartTime = Time.time;
                    Confidence = Mathf.Clamp01(0.6f + newStrength * 0.35f);

                    activationSnapshot = VisualizerLandfallConfig.CurrentConfig.ShowDebug
                        ? $"{currentEnergy:F2}/{effectiveThreshold:F2}\nS:{beatStrength:F2}"
                        : $"S:{beatStrength:F2}";

                    // Initialize reset phase
                    _inResetPhase = true;
                    _resetThreshold = effectiveThreshold * Mathf.Lerp(0.2f, 0.9f, beatStrength);  // Start below current level
                    beatDetected = true;
                }
            }
            // 3. Continuous strength updates during reset phase 
            else if (currentEnergy > effectiveThreshold)
            {

                // Calculate temporary beat strength without triggering new beat
                float thresholdExcess = currentEnergy - effectiveThreshold;

                // Relative excess in 0..1 range
                float relExcess = Mathf.Clamp01(thresholdExcess / effectiveThreshold);

                // Weight: give big hits >150% threshold a strong pop, but compress >200%
                float transientFactor = Mathf.Pow(Mathf.Clamp01(relExcess / 1.5f), 0.6f);

                // Energy context: fade back when overall averageEnergy is constantly high
                float energyDamp = 1f - Mathf.Clamp01(averageEnergy * 0.65f);

                // Blend into final strength
                float tempStrength = transientFactor * (0.85f + averageEnergy * 0.5f) * energyDamp;

                // Keep HDR headroom for downstream visuals
                tempStrength = Mathf.Clamp(tempStrength, 0f, 3f);
                float energyFactor = Mathf.Clamp01(currentEnergy / effectiveThreshold);

                beatStrength = Mathf.Lerp(beatStrength, tempStrength, 0.5f + energyFactor * 0.5f);
                beatTimer = Mathf.Max(beatTimer, isBass ? 0.07f : 0.05f);
                activationSnapshot = $"Sustained: {tempStrength:F2}";
            }
            // ==================================================================

            // Update timers
            beatTimer = Mathf.Max(0, beatTimer - Time.deltaTime);
            _peakHoldTimer = Mathf.Max(0, _peakHoldTimer - Time.deltaTime);
            float durForDecay = (beatTimer > 0f ? Mathf.Max(beatDuration, 0.08f) : Mathf.Max(_lastBeatDuration, 0.08f));

            // Manage beat strength decay
            if (beatTimer > 0)
            {
                if (_peakHoldTimer > 0)
                {
                    beatStrength = Mathf.Lerp(beatStrength, peakBeatStrength, Time.deltaTime * 20f);
                }
                else
                {
                    float decaySpeed = 8f * durForDecay;
                    beatStrength = Mathf.Lerp(beatStrength, 0f, Time.deltaTime * decaySpeed);
                    beatStrength = Mathf.Lerp(beatStrength, beatStrength * beatDecay, Time.deltaTime);
                }
            }
            else
            {
                beatStrength = Mathf.Lerp(beatStrength, 0f, Time.deltaTime * 6f * durForDecay);
                beatStrength = Mathf.Lerp(beatStrength, beatStrength * beatDecay, Time.deltaTime);
                peakBeatStrength = Mathf.Lerp(peakBeatStrength, peakBeatStrength * beatDecay, Time.deltaTime * 0.9f);

                float near = Mathf.InverseLerp(effectiveThreshold * 0.9f, effectiveThreshold * 1.1f, currentEnergy);
                Confidence = Mathf.Clamp01(Mathf.Lerp(Confidence, 0.5f + (near - 0.5f) * 0.4f, Time.deltaTime * 2f));
            }

            // Update debug snapshot
            if (beatDetected && VisualizerLandfallConfig.CurrentConfig.ShowDebug && !Snapped)
            {
                beatActivationSnapshot = activationSnapshot;
                Snapped = true;
            }
            else if (beatTimer <= 0f && Snapped)
            {
                Snapped = false;
            }

            lastBeatTimer = beatTimer;
        }

    }

    /// <summary>
    /// Coordinates multiple beat detectors into unified output
    /// </summary>
    public class BeatCoordinator
    {
        public float combinedBeatStrength;  // Unified beat strength (0-1)
        public float beatPulse;             // Pulsing effect value

        private float[] _bandWeights;       // Importance weights for each band

        public void SetBandWeights(float[] weights)
        {
            _bandWeights = weights;
        }

        /// <summary>
        /// Updates coordinator with current detector states
        /// </summary>
        public void Update(IList<BeatDetector> detectors)
        {
            if (detectors == null) return;

            float weightedSum = 0f;
            float totalWeight = 0f;

            for (int i = 0; i < detectors.Count; i++)
            {
                if (detectors[i] == null) continue;

                float weight = _bandWeights != null && i < _bandWeights.Length ?
                    _bandWeights[i] : 1f;

                // Null check for peakBeatStrength
                weightedSum += detectors[i].beatStrength * weight;
                totalWeight += weight;
            }

            // Handle case where all weights are zero
            if (Mathf.Approximately(totalWeight, 0f))
            {
                combinedBeatStrength = 0f;
                beatPulse = Mathf.Lerp(beatPulse, 1f, Time.deltaTime * 5f);
                return;
            }

            float maxStrength = 0f;
            for (int i = 0; i < detectors.Count; i++)
            {
                if (detectors[i] != null && detectors[i].beatStrength > maxStrength)
                {
                    maxStrength = detectors[i].beatStrength;
                }
            }
            combinedBeatStrength = maxStrength;
            float pulseTarget = 1 + combinedBeatStrength * 2;
            float pulseSpeed = combinedBeatStrength > 0.3f ? 25f : 8f;
            beatPulse = Mathf.Lerp(beatPulse, pulseTarget, Time.deltaTime * pulseSpeed);
        }
    }

    /// <summary>
    /// Detects when audio source or track changes
    /// </summary>
    public class TrackChangeDetector : MonoBehaviour
    {
        private AudioSource _lastAudioSource;
        private string _lastClipName;
        private float _lastCheckTime;
        private UnifiedAudioProcessor _processor;

        void Start()
        {
            _processor = GetComponent<UnifiedAudioProcessor>();
            if (_processor != null && _processor.audioSource != null)
            {
                _lastAudioSource = _processor.audioSource;
                _lastClipName = _processor.audioSource.clip?.name ?? "";
            }
        }

        void Update()
        {
            // Only check every 0.5 seconds
            if (Time.time - _lastCheckTime < 0.5f) return;
            _lastCheckTime = Time.time;

            if (_processor == null) return;

            // Check for audio source changes
            AudioSource currentSource = GetCurrentAudioSource();
            if (currentSource != _lastAudioSource)
            {
                OnAudioSourceChanged(currentSource);
                return;
            }

            // Check for track changes on same source
            if (currentSource != null && currentSource.clip != null)
            {
                string currentClipName = currentSource.clip.name;
                if (currentClipName != _lastClipName)
                {
                    OnTrackChanged(currentSource);
                }
            }
        }

        private AudioSource GetCurrentAudioSource()
        {
            try
            {
                // Prefer the game's music player
                if (MusicPlayer.Instance != null && MusicPlayer.Instance.m_AudioSourceCurrent != null)
                {
                    return MusicPlayer.Instance.m_AudioSourceCurrent;
                }
                return _processor.audioSource;
            }
            catch
            {
                return _processor.audioSource;
            }
        }

        private void OnAudioSourceChanged(AudioSource newSource)
        {
            _lastAudioSource = newSource;
            _lastClipName = newSource?.clip?.name ?? "";

            if (_processor != null)
            {
                _processor.audioSource = newSource;
                _processor.ResetDetectors();
                UnifiedAudioProcessor.RecomputeBandWeights(_processor.bands, force: true);
                if (_processor.beatCoordinator != null && UnifiedAudioProcessor._bandWeights != null)
                    _processor.beatCoordinator.SetBandWeights(UnifiedAudioProcessor._bandWeights);
                Debug.Log($"Audio source changed to: {newSource?.name ?? "None"}");
            }


        }

        private void OnTrackChanged(AudioSource source)
        {
            if (source == null || source.clip == null) return;

            _lastClipName = source.clip.name;

            if (_processor != null)
            {
                _processor.ResetDetectors();
                SkyboxVisualizer.NewTrackForSun(SkyboxVisualizer.lastTrackProgress, SkyboxVisualizer.lastTrackLength);
                UnifiedAudioProcessor.RecomputeBandWeights(_processor.bands, force: true);
                if (_processor.beatCoordinator != null && UnifiedAudioProcessor._bandWeights != null)
                    _processor.beatCoordinator.SetBandWeights(UnifiedAudioProcessor._bandWeights);

            }
        }
    }

    // ==================
    // PUBLIC ACCESSORS
    // ==================

    /// <summary>
    /// Gets current band values
    /// </summary>
    public float[] GetBandValues() => bandValues;

    /// <summary>
    /// Gets value for specific band
    /// </summary>
    public float GetBandValue(int index)
    {
        if (index >= 0 && index < bands && bandValues != null)
            return bandValues[index];
        return 0f;
    }
}