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
using Landfall.Modding;
using UnityEngine;
using Object = UnityEngine.Object;

[LandfallPlugin]
public class SkyboxVisualizerHaste
{
    private static Harmony _harmony;
    private static bool _booted;

    static SkyboxVisualizerHaste()
    {
        if (_booted) return;


        Debug.Log("Skybox Visualizer Haste Edition initialized!");

        _harmony = new Harmony("com.powrofgreed.skyboxvisualizer");
        _harmony.PatchAll();

        VisualizerLandfallConfig.Initialize();

        var vis = new GameObject("SkyboxVisualizerController");
        Object.DontDestroyOnLoad(vis);
        vis.AddComponent<SkyboxVisualizerController>();

        var safety = new GameObject("SceneSafetySystem");
        Object.DontDestroyOnLoad(safety);
        safety.AddComponent<SceneSafetySystem>();

        SkyboxVisualizer.Initialize();

        Debug.Log("Skybox Visualizer fully initialized!");
        _booted = true;
    }
}

// Controller to handle updates and settings window
public class SkyboxVisualizerController : MonoBehaviour
{
    private VisualizerSettingsWindow settingsWindow;
    private bool windowInitialized = false;
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            ToggleSettingsWindow();
        }
    }
    void ToggleSettingsWindow()
    {
        if (!windowInitialized)
        {
            settingsWindow = gameObject.AddComponent<VisualizerSettingsWindow>(); windowInitialized = true;
        }
        if (settingsWindow != null)
        {
            settingsWindow.ToggleVisibility();
        }
    }
}
