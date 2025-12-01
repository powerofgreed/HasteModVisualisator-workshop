using UnityEngine;

public class VisualizerSettingsWindow : MonoBehaviour
{
    private bool _showSettings = false;
    private Rect _windowRect = new Rect(100, 100, 650, 900);
    private Vector2 _scrollPosition = Vector2.zero;
    private float _uiScale = 1.0f;
    private Matrix4x4 _originalMatrix;
    private bool _styleInitialized = false;
    private static GUIStyle _headerStyle;
    private static GUIStyle _buttonStyle;

    // Cursor state management
    private bool _wasCursorVisible;
    private CursorLockMode _previousCursorLockState;
    private bool _cursorStateForced = false;

    void Update()
    {
        //// Force cursor state every frame while window is open
        //if (_showSettings && !_cursorStateForced)
        //{
        //    ForceCursorState();
        //}
    }

    void OnGUI()
    {
        CalculateUIScale();
        _originalMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));

        if (!_showSettings) return;

        if (!_styleInitialized) InitializeStyles();

        // Force cursor state at the beginning of OnGUI
        if (!_cursorStateForced)
        {
            ForceCursorState();
        }

        _windowRect = GUI.Window(10000, _windowRect, DrawSettingsWindow, "Skybox Visualizer Settings <color=green>F9</color>");

        // Restore matrix
        GUI.matrix = _originalMatrix;

        // Prevent clicks from going through to the game
        if (Event.current.type == EventType.MouseDown && _windowRect.Contains(Event.current.mousePosition))
        {
            Event.current.Use();
        }

        // Additional cursor forcing at the end of OnGUI
        if (_showSettings)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    // Also handle when the component is destroyed
    void OnDestroy()
    {
        if (_cursorStateForced)
        {
            RestoreCursorState();
        }
    }
    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _showSettings)
        {
            // Re-force cursor state when application regains focus
            ForceCursorState();
        }
    }

    public void ToggleVisibility()
    {
        _showSettings = !_showSettings;

        if (_showSettings)
        {
            // Save current cursor state
            _wasCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;

            // Immediately force cursor state
            ForceCursorState();

        }
        else
        {
            // Restore cursor state
            RestoreCursorState();
        }
    }
    private void ForceCursorState()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _cursorStateForced = true;
    }

    private void RestoreCursorState()
    {
        Cursor.visible = _wasCursorVisible;
        Cursor.lockState = _previousCursorLockState;
        _cursorStateForced = false;
        VisualizerLandfallConfig.SaveConfig();
        
    }

    private void CalculateUIScale()
    {
        float screenHeight = Screen.height;
        _uiScale = Mathf.Max(screenHeight / 1080f, 0.75f);
    }

    void DrawSettingsWindow(int id)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(false));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

        DrawVisualizerSection();
        DrawBeatDetectionSection();
        DrawColorSection();
        DrawLightSection();
        DrawCloudSection();
        DrawHorizonSection();
        //DrawEffectsSection();
        DrawAdvancedSection();
        //tab cycle fix
        if (
                (Event.current.type == EventType.KeyDown || Event.current.type == EventType.KeyUp) &&
                Event.current.keyCode == KeyCode.Tab)
        {
            // Skip focus
            GUI.FocusControl(null);
            Event.current.Use();
        }

        GUILayout.EndScrollView();

        // Bottom buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save & Close", _buttonStyle, GUILayout.Height(30)))
        {
            VisualizerLandfallConfig.SaveConfig();
            _showSettings = false;
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset to Defaults", _buttonStyle, GUILayout.Height(30)))
        {
            ResetToDefaults();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close", _buttonStyle, GUILayout.Height(30)))
        {
            _showSettings = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
    }

    void DrawVisualizerSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ VISUALIZER ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);

        VisualizerLandfallConfig.CurrentConfig.EnableVisualizer = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.EnableVisualizer,
            "Enabled - Enable audio-reactive skybox effects");

        // Intensity slider
        GUILayout.BeginHorizontal();
        GUILayout.Label("Intensity:", GUILayout.Width(150));
        VisualizerLandfallConfig.CurrentConfig.Intensity = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.Intensity, 0.01f, 3f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.Intensity.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();


        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawBeatDetectionSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ BEAT DETECTION ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);

        // Beat Sensitivity
        GUILayout.BeginHorizontal();
        GUILayout.Label("Sensitivity !!KEEP CLOSE TO 1.0!!:", GUILayout.Width(200));
        VisualizerLandfallConfig.CurrentConfig.BeatSensitivity = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.BeatSensitivity, 0.1f, 1.5f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.BeatSensitivity.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Beat Threshold
        GUILayout.BeginHorizontal();
        GUILayout.Label("Threshold !!KEEP CLOSE TO 1.0!!:", GUILayout.Width(200));
        VisualizerLandfallConfig.CurrentConfig.BeatThreshold = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.BeatThreshold, 0.01f, 5f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.BeatThreshold.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawColorSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ COLOR ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);

        // Color Intensity
        GUILayout.BeginHorizontal();
        GUILayout.Label("Intensity:", GUILayout.Width(150));
        VisualizerLandfallConfig.CurrentConfig.ColorIntensity = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.ColorIntensity, -1f, 5f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.ColorIntensity.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Color Saturation
        GUILayout.BeginHorizontal();
        GUILayout.Label("Saturation:", GUILayout.Width(150));
        VisualizerLandfallConfig.CurrentConfig.ColorSaturation = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.ColorSaturation, 0.01f, 5f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.ColorSaturation.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        // Beat Response
        GUILayout.BeginHorizontal();
        GUILayout.Label("Beat Response:", GUILayout.Width(150));
        VisualizerLandfallConfig.CurrentConfig.BeatResponse = GUILayout.HorizontalSlider(
            VisualizerLandfallConfig.CurrentConfig.BeatResponse, 0.01f, 5f);
        GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.BeatResponse.ToString("F2"), GUILayout.Width(40));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawLightSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ LIGHT ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);

        VisualizerLandfallConfig.CurrentConfig.AffectLighting = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.AffectLighting,
            "Enable Lighting Effects - Enable light intensity and color effects");

        if (VisualizerLandfallConfig.CurrentConfig.AffectLighting)
        {
            // Light Intensity Multiplier
            GUILayout.BeginHorizontal();
            GUILayout.Label("Intensity Multiplier:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.LightIntensityMultiplier = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.LightIntensityMultiplier, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.LightIntensityMultiplier.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Light Color Multiplier
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Multiplier:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.LightColorMultiplier = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.LightColorMultiplier, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.LightColorMultiplier.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Light Color Lerp Speed
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Lerp Speed:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.LightColorLerpSpeed = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.LightColorLerpSpeed, 0.01f, 10f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.LightColorLerpSpeed.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Ambient Lighting
            VisualizerLandfallConfig.CurrentConfig.AffectAmbient = GUILayout.Toggle(
                VisualizerLandfallConfig.CurrentConfig.AffectAmbient,
                "Enable Ambient Effects - Enable ambient light color and intensity effects");

            if (VisualizerLandfallConfig.CurrentConfig.AffectAmbient)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                // Ambient Intensity Multiplier
                GUILayout.BeginHorizontal();
                GUILayout.Label("Ambient Intensity:", GUILayout.Width(150));
                VisualizerLandfallConfig.CurrentConfig.AmbientIntensityMultiplier = GUILayout.HorizontalSlider(
                    VisualizerLandfallConfig.CurrentConfig.AmbientIntensityMultiplier, 0.01f, 3f);
                GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.AmbientIntensityMultiplier.ToString("F2"), GUILayout.Width(40));
                GUILayout.EndHorizontal();

                // Ambient Color Multiplier
                GUILayout.BeginHorizontal();
                GUILayout.Label("Ambient Color:", GUILayout.Width(150));
                VisualizerLandfallConfig.CurrentConfig.AmbientColorMultiplier = GUILayout.HorizontalSlider(
                    VisualizerLandfallConfig.CurrentConfig.AmbientColorMultiplier, 0.01f, 3f);
                GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.AmbientColorMultiplier.ToString("F2"), GUILayout.Width(40));
                GUILayout.EndHorizontal();

                // Ambient Color Lerp Speed
                GUILayout.BeginHorizontal();
                GUILayout.Label("Ambient Lerp Speed:", GUILayout.Width(150));
                VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed = GUILayout.HorizontalSlider(
                    VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed, 0.01f, 10f);
                GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.AmbientColorLerpSpeed.ToString("F2"), GUILayout.Width(40));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            // Sun Angle Preset
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sun Angle Preset:", GUILayout.Width(150));
            string[] presets = { "Disable", "TrackLength", "TotalEnergy" };
            int currentPreset = System.Array.IndexOf(presets, VisualizerLandfallConfig.CurrentConfig.SunAnglePreset);
            int newPreset = GUILayout.SelectionGrid(currentPreset, presets, 3);
            if (newPreset != currentPreset)
            {
                VisualizerLandfallConfig.CurrentConfig.SunAnglePreset = presets[newPreset];
            }
            GUILayout.EndHorizontal();

            // Sun Angle Lerp Speed
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sun Angle Speed:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.SunAngleLerpSpeedMultiplier.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawCloudSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ CLOUDS ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.BeginVertical(GUI.skin.box);

        VisualizerLandfallConfig.CurrentConfig.AffectClouds = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.AffectClouds,
            "Enable Cloud Effects - Enable cloud pulsing effect");

        if (VisualizerLandfallConfig.CurrentConfig.AffectClouds)
        {
            // Cloud Color Intensity
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Intensity:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.CloudColorIntensity = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.CloudColorIntensity, 0.01f, 10f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.CloudColorIntensity.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Cloud Color Lerp Speed
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Lerp Speed:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.CloudColorLerpSpeed = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.CloudColorLerpSpeed, 0.01f, 10f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.CloudColorLerpSpeed.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Crazy Cloud Color
            VisualizerLandfallConfig.CurrentConfig.CrazyCloudColor = GUILayout.Toggle(
                VisualizerLandfallConfig.CurrentConfig.CrazyCloudColor,
                "Enable Crazy Color - Enable very toxic skybox cloud coloring effects");

            if (VisualizerLandfallConfig.CurrentConfig.CrazyCloudColor)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                // Crazy Cloud Color Intensity
                GUILayout.BeginHorizontal();
                GUILayout.Label("Crazy Color Intensity:", GUILayout.Width(150));
                VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorIntensity = GUILayout.HorizontalSlider(
                    VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorIntensity, 0.01f, 10f);
                GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorIntensity.ToString("F2"), GUILayout.Width(40));
                GUILayout.EndHorizontal();

                // Crazy Cloud Color Lerp Speed
                GUILayout.BeginHorizontal();
                GUILayout.Label("Crazy Color Lerp Speed:", GUILayout.Width(150));
                VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorLerpSpeed = GUILayout.HorizontalSlider(
                    VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorLerpSpeed, 0.01f, 10f);
                GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.CrazyCloudColorLerpSpeed.ToString("F2"), GUILayout.Width(40));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            // Sky Volume Strength
            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume Strength:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.SkyVolumeStrength = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.SkyVolumeStrength, 0f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.SkyVolumeStrength.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Jump Scale Multiplier
            GUILayout.BeginHorizontal();
            GUILayout.Label("Jump Scale:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.JumpScaleMultiplier = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.JumpScaleMultiplier, 0.00f, 10f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.JumpScaleMultiplier.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Cloud Wind Beat Scale
            GUILayout.BeginHorizontal();
            GUILayout.Label("Wind Beat Scale:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.CloudWindBeatScale = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.CloudWindBeatScale, 0f, 6f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.CloudWindBeatScale.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            //// Volume Strength Min Multiplier (commented in original but included for completeness)
            //GUILayout.BeginHorizontal();
            //GUILayout.Label("Volume Min Multiplier:", GUILayout.Width(150));
            //VisualizerLandfallConfig.CurrentConfig.VolumeStrengthMinMultiplier = GUILayout.HorizontalSlider(
            //    VisualizerLandfallConfig.CurrentConfig.VolumeStrengthMinMultiplier, 0.1f, 1.0f);
            //GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.VolumeStrengthMinMultiplier.ToString("F2"), GUILayout.Width(40));
            //GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawHorizonSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ HORIZON ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);
        // Affect Stars
        VisualizerLandfallConfig.CurrentConfig.AffectStars = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.AffectStars,
            "Affect Stars - Enable star brightness effect");

        VisualizerLandfallConfig.CurrentConfig.AffectHorizon = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.AffectHorizon,
            "Enable Horizon Effects - Enable horizon color and position effects");

        if (VisualizerLandfallConfig.CurrentConfig.AffectHorizon)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            // Horizon Color Intensity
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Intensity:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonColorIntensity.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Horizon Color Density
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Density:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.HorizonColorDensity = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonColorDensity, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonColorDensity.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Horizon Color Lerp Speed
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color Lerp Speed:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.HorizonColorLerpSpeed = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonColorLerpSpeed, 0.01f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonColorLerpSpeed.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Horizon Remap Values
            GUILayout.Label("Horizon Remap Values:");
            GUILayout.BeginHorizontal();
            GUILayout.Label("X:", GUILayout.Width(20));
            VisualizerLandfallConfig.CurrentConfig.HorizonRemapX = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonRemapX, -4.0f, 4.0f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonRemapX.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Y:", GUILayout.Width(20));
            VisualizerLandfallConfig.CurrentConfig.HorizonRemapY = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonRemapY, -4.0f, 4.0f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonRemapY.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Z:", GUILayout.Width(20));
            VisualizerLandfallConfig.CurrentConfig.HorizonRemapZ = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonRemapZ, -4.0f, 4.0f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonRemapZ.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("W:", GUILayout.Width(20));
            VisualizerLandfallConfig.CurrentConfig.HorizonRemapW = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HorizonRemapW, -4.0f, 4.0f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HorizonRemapW.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawEffectsSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label("------ EFFECTS ------", _headerStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);



        // Screen Effects (commented in original but included for completeness)
        VisualizerLandfallConfig.CurrentConfig.EnableScreenEffects = GUILayout.Toggle(
            VisualizerLandfallConfig.CurrentConfig.EnableScreenEffects,
            "Screen Effects - Enable additional screen effects");

        if (VisualizerLandfallConfig.CurrentConfig.EnableScreenEffects)
        {
            // Blur Intensity
            GUILayout.BeginHorizontal();
            GUILayout.Label("Blur Intensity:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.BlurIntensity = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.BlurIntensity, 0.1f, 5f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.BlurIntensity.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(10);
    }

    void DrawAdvancedSection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        VisualizerLandfallConfig.CurrentConfig.EnableAdvanced = GUILayout.Toggle(
    VisualizerLandfallConfig.CurrentConfig.EnableAdvanced,
    "<color=red>------ ADVANCED ------</color>");
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (VisualizerLandfallConfig.CurrentConfig.EnableAdvanced)
        {

            GUILayout.BeginVertical(GUI.skin.box);

            // Debug Toggles
            VisualizerLandfallConfig.CurrentConfig.ShowDebug = GUILayout.Toggle(
                VisualizerLandfallConfig.CurrentConfig.ShowDebug,
                "Show Debug - Show visualizer debug information");

            VisualizerLandfallConfig.CurrentConfig.ShowCSVDebug = GUILayout.Toggle(
                VisualizerLandfallConfig.CurrentConfig.ShowCSVDebug,
                "Show CSV Debug - Show/hide CSV values");

            // History Duration
            GUILayout.BeginHorizontal();
            GUILayout.Label("History Duration:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.HistoryDuration = (int)GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.HistoryDuration, 4, 12);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.HistoryDuration.ToString(), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // Gaussian Kernel Size
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gaussian Kernel Size:", GUILayout.Width(150));
            string[] kernelSizes = { "3", "5", "7", "9", "11" };
            int currentKernel = System.Array.IndexOf(kernelSizes, VisualizerLandfallConfig.CurrentConfig.GaussianKernelSize.ToString());
            int newKernel = GUILayout.SelectionGrid(currentKernel, kernelSizes, 5);
            if (newKernel != currentKernel)
            {
                VisualizerLandfallConfig.CurrentConfig.GaussianKernelSize = int.Parse(kernelSizes[newKernel]);
            }
            GUILayout.EndHorizontal();

            // Gaussian Kernel Intensity
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gaussian Kernel Intensity:", GUILayout.Width(150));
            VisualizerLandfallConfig.CurrentConfig.GaussianKernelIntensity = GUILayout.HorizontalSlider(
                VisualizerLandfallConfig.CurrentConfig.GaussianKernelIntensity, 0.1f, 5.0f);
            GUILayout.Label(VisualizerLandfallConfig.CurrentConfig.GaussianKernelIntensity.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            // CSV Band Settings
            GUILayout.Label("PER-BAND CSV SETTINGS:", _headerStyle);

            DrawCSVField("Min Thresholds:", ref VisualizerLandfallConfig.CurrentConfig.Band_MinThresholds);
            DrawCSVField("Decay Rates:", ref VisualizerLandfallConfig.CurrentConfig.Band_DecayRates);
            DrawCSVField("Sensitivities:", ref VisualizerLandfallConfig.CurrentConfig.Band_Sensitivities);
            DrawCSVField("Threshold Multipliers:", ref VisualizerLandfallConfig.CurrentConfig.Band_ThresholdMultipliers);
            DrawCSVField("Cooldowns:", ref VisualizerLandfallConfig.CurrentConfig.Band_Cooldowns);
            DrawCSVField("Min Exceed:", ref VisualizerLandfallConfig.CurrentConfig.Band_MinExceed);
            DrawCSVField("Beat Decays:", ref VisualizerLandfallConfig.CurrentConfig.Band_BeatDecays);

            GUILayout.EndVertical();
            GUILayout.Space(10);
        }
    }

    void DrawCSVField(string label, ref string csvValue)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        string newValue = GUILayout.TextField(csvValue, GUILayout.ExpandWidth(true));
        if (newValue != csvValue)
        {
            csvValue = newValue;
        }
        GUILayout.EndHorizontal();
    }

    void ResetToDefaults()
    {
        VisualizerLandfallConfig.CurrentConfig = new VisualizerLandfallConfig.VisualizerConfigData();
        VisualizerLandfallConfig.SaveConfig();
        Debug.Log("Visualizer settings reset to defaults");
    }

    void InitializeStyles()
    {
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                normal = { textColor = Color.yellow }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                padding = new RectOffset(10, 10, 5, 5)
            };
        }
        _styleInitialized = true;
    }
}