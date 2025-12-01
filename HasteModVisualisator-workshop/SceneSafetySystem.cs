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
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public class SceneSafetySystem : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Defer a frame so all scene objects (including Skybox) are present and Awake/Start have run
        StartCoroutine(PrimeNextFrame());
    }

    private IEnumerator PrimeNextFrame()
    {
        // Wait for two frames to ensure everything is initialized
        yield return null;
        yield return null;

        try
        {
            // Ensure visualizer is initialized once
            if (!SkyboxVisualizer.isInitialized)
                SkyboxVisualizer.Initialize();

            // Prime shader globals and baselines
            SkyboxVisualizer.PrimeCurrentScene();

            // Optional: ensure Post Processing Layer exists
            if (VisualizerLandfallConfig.CurrentConfig.EnableVisualizer)
                EnsurePostProcessingLayers();
        }
        catch (UnityException e)
        {
            Debug.LogError($"Error in scene priming: {e.Message}");
        }
    }

    // Creates a PostProcessLayer on all cameras if missing.
    // This mirrors your previous behavior; safe to keep or remove if you don’t need PPv2.
    private void EnsurePostProcessingLayers()
    {
        PostProcessResources resources = null;

        // Try to find PP resources (can be null in some builds; guard for safety)
        var allResources = Resources.FindObjectsOfTypeAll<PostProcessResources>();
        if (allResources != null && allResources.Length > 0)
            resources = allResources[0];

        foreach (var cam in Camera.allCameras)
        {
            var layer = cam.GetComponent<PostProcessLayer>();
            if (layer == null)
            {
                layer = cam.gameObject.AddComponent<PostProcessLayer>();
                if (resources != null)
                {
                    // Initialize resources if available (prevents null shaders in some setups)
                    layer.Init(resources);
                }
                // Keep AA off unless you explicitly want it (matches your earlier code)
                layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                // No volumes by default; you can manage them elsewhere
                layer.volumeLayer = LayerMask.GetMask("Nothing");
            }
        }
    }
}
