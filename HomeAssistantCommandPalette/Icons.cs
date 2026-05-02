using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette;

/// <summary>
/// Centralized icon lookup. SVGs are sourced from the Raycast Home
/// Assistant extension under Assets/Icons (MIT-licensed there too).
/// </summary>
internal static class Icons
{
    public static IconInfo App => IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-200.png");

    // State-tinted entity icons. SVGs are pre-baked variants of lightbulb.svg
    // (currentColor → Raycast palette: yellow for on, blue for off, grey for
    // unavailable). CmdPal's IconInfo has no runtime tint, so we ship the
    // three variants and pick at row-build time.
    public static IconInfo LightOn => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-on.svg");
    public static IconInfo LightOff => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-off.svg");
    public static IconInfo LightUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-unavailable.svg");

    public static IconInfo LightGroupOn => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-group-on.svg");
    public static IconInfo LightGroupOff => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-group-off.svg");
    public static IconInfo LightGroupUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb-group-unavailable.svg");

    // Cover state-tinted icons: window-open / window-closed / arrow-up /
    // arrow-down. Raycast tints all cover state icons with PrimaryIconColor
    // (blue) — only unavailable is grey.
    public static IconInfo CoverOpen => IconHelpers.FromRelativePath("Assets\\Icons\\window-open-off.svg");
    public static IconInfo CoverClosed => IconHelpers.FromRelativePath("Assets\\Icons\\window-closed-off.svg");
    public static IconInfo CoverOpening => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-up-box-off.svg");
    public static IconInfo CoverClosing => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-down-box-off.svg");
    public static IconInfo CoverUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\window-closed-unavailable.svg");

    // Media player state-tinted icons. Yellow when playing, blue when
    // paused / idle / off, grey when unavailable.
    public static IconInfo MediaPlayerPlaying => IconHelpers.FromRelativePath("Assets\\Icons\\cast-connected-on.svg");
    public static IconInfo MediaPlayerIdle => IconHelpers.FromRelativePath("Assets\\Icons\\cast-connected-off.svg");
    public static IconInfo MediaPlayerUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\cast-connected-unavailable.svg");

    // Climate state-tinted icons. Yellow when actively heating/cooling,
    // blue when off, grey when unavailable. Auto/heat_cool gets the
    // dedicated thermostat-auto glyph.
    public static IconInfo ClimateActive => IconHelpers.FromRelativePath("Assets\\Icons\\thermostat-on.svg");
    public static IconInfo ClimateOff => IconHelpers.FromRelativePath("Assets\\Icons\\thermostat-off.svg");
    public static IconInfo ClimateAuto => IconHelpers.FromRelativePath("Assets\\Icons\\thermostat-auto-on.svg");
    public static IconInfo ClimateUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\thermostat-unavailable.svg");

    public static IconInfo Thermometer => IconHelpers.FromRelativePath("Assets\\Icons\\thermometer.svg");
    public static IconInfo Thermostat => IconHelpers.FromRelativePath("Assets\\Icons\\thermostat.svg");
    public static IconInfo Fan => IconHelpers.FromRelativePath("Assets\\Icons\\fan.svg");

    // Fan state-tinted icons. Yellow when on, blue when off, grey when
    // unavailable.
    public static IconInfo FanOn => IconHelpers.FromRelativePath("Assets\\Icons\\fan-on.svg");
    public static IconInfo FanOff => IconHelpers.FromRelativePath("Assets\\Icons\\fan-off.svg");
    public static IconInfo FanUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\fan-unavailable.svg");

    // Vacuum state-tinted icons. Yellow when cleaning, blue when docked /
    // idle / paused / returning, grey when unavailable.
    public static IconInfo VacuumCleaning => IconHelpers.FromRelativePath("Assets\\Icons\\robot-vacuum-on.svg");
    public static IconInfo VacuumIdle => IconHelpers.FromRelativePath("Assets\\Icons\\robot-vacuum-off.svg");
    public static IconInfo VacuumUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\robot-vacuum-unavailable.svg");

    public static IconInfo Home => IconHelpers.FromRelativePath("Assets\\Icons\\home.svg");

    // Automation state-tinted icons. Yellow when enabled (state="on"),
    // blue when disabled, grey when unavailable.
    public static IconInfo AutomationOn => IconHelpers.FromRelativePath("Assets\\Icons\\robot-on.svg");
    public static IconInfo AutomationOff => IconHelpers.FromRelativePath("Assets\\Icons\\robot-off.svg");
    public static IconInfo AutomationUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\robot-unavailable.svg");

    // "Trigger automation" — lightning bolt suggests "fire now".
    public static IconInfo Trigger => IconHelpers.FromRelativePath("Assets\\Icons\\flash.svg");

    // Helper-domain icons. Mirrors the Raycast extension's MDI choices:
    //   input_boolean → toggle-switch (slider position differs by state)
    //   timer         → av-timer (yellow when active)
    //   input_select  → format-list-bulleted
    //   input_button  → gesture-tap-button
    //   input_number  → ray-vertex
    //   input_text    → form-textbox
    //   input_datetime → calendar-clock / calendar / clock-time-four
    //                    depending on has_date / has_time
    public static IconInfo InputBooleanOn => IconHelpers.FromRelativePath("Assets\\Icons\\input-boolean-on.svg");
    public static IconInfo InputBooleanOff => IconHelpers.FromRelativePath("Assets\\Icons\\input-boolean-off.svg");
    public static IconInfo InputBooleanUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\input-boolean-unavailable.svg");
    public static IconInfo TimerOn => IconHelpers.FromRelativePath("Assets\\Icons\\av-timer-on.svg");
    public static IconInfo TimerOff => IconHelpers.FromRelativePath("Assets\\Icons\\av-timer-off.svg");
    public static IconInfo TimerUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\av-timer-unavailable.svg");
    public static IconInfo InputSelect => IconHelpers.FromRelativePath("Assets\\Icons\\input-select.svg");
    public static IconInfo InputButton => IconHelpers.FromRelativePath("Assets\\Icons\\input-button.svg");
    public static IconInfo InputNumber => IconHelpers.FromRelativePath("Assets\\Icons\\input-number.svg");
    public static IconInfo InputText => IconHelpers.FromRelativePath("Assets\\Icons\\input-text.svg");
    public static IconInfo InputDateTime => IconHelpers.FromRelativePath("Assets\\Icons\\input-datetime.svg");
    public static IconInfo InputDate => IconHelpers.FromRelativePath("Assets\\Icons\\input-datetime-date.svg");
    public static IconInfo InputTime => IconHelpers.FromRelativePath("Assets\\Icons\\input-datetime-time.svg");

    // Switch state-tinted icons. Toggle-position changes between on/off
    // (slider on the right vs left); colour mirrors the standard palette.
    public static IconInfo SwitchOn => IconHelpers.FromRelativePath("Assets\\Icons\\switch-on.svg");
    public static IconInfo SwitchOff => IconHelpers.FromRelativePath("Assets\\Icons\\switch-off.svg");
    public static IconInfo SwitchUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\switch-unavailable.svg");

    // Script state-tinted icons. Yellow while running (state="on"), blue
    // when idle, grey when unavailable. Reuses the play.svg geometry.
    public static IconInfo ScriptOn => IconHelpers.FromRelativePath("Assets\\Icons\\script-on.svg");
    public static IconInfo ScriptOff => IconHelpers.FromRelativePath("Assets\\Icons\\script-off.svg");
    public static IconInfo ScriptUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\script-unavailable.svg");

    // Update state-tinted icons. Yellow when an update is available
    // (state="on"), blue when up-to-date, grey when unavailable.
    public static IconInfo UpdateOn => IconHelpers.FromRelativePath("Assets\\Icons\\update-on.svg");
    public static IconInfo UpdateOff => IconHelpers.FromRelativePath("Assets\\Icons\\update-off.svg");
    public static IconInfo UpdateUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\update-unavailable.svg");

    // Person state-tinted icons. Yellow when home, blue otherwise, grey
    // when unavailable. The full picture from `entity_picture` is used at
    // the call site when present.
    public static IconInfo PersonOn => IconHelpers.FromRelativePath("Assets\\Icons\\person-on.svg");
    public static IconInfo PersonOff => IconHelpers.FromRelativePath("Assets\\Icons\\person-off.svg");
    public static IconInfo PersonUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\person-unavailable.svg");

    // sun.sun has two valid states: "above_horizon" (yellow sun) and
    // "below_horizon" (blue moon). Grey for unavailable.
    public static IconInfo SunDay => IconHelpers.FromRelativePath("Assets\\Icons\\sun-on.svg");
    public static IconInfo SunNight => IconHelpers.FromRelativePath("Assets\\Icons\\sun-off.svg");
    public static IconInfo SunUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\sun-unavailable.svg");

    // Stateless single-blue domain icons.
    public static IconInfo Scene => IconHelpers.FromRelativePath("Assets\\Icons\\scene-off.svg");
    public static IconInfo SceneUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\scene-unavailable.svg");
    public static IconInfo Zone => IconHelpers.FromRelativePath("Assets\\Icons\\zone-off.svg");
    public static IconInfo ZoneUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\zone-unavailable.svg");
    public static IconInfo Camera => IconHelpers.FromRelativePath("Assets\\Icons\\camera-off.svg");
    public static IconInfo CameraUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\camera-unavailable.svg");
    public static IconInfo Button => IconHelpers.FromRelativePath("Assets\\Icons\\button-off.svg");
    public static IconInfo ButtonUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\button-unavailable.svg");
    public static IconInfo Counter => IconHelpers.FromRelativePath("Assets\\Icons\\counter-off.svg");
    public static IconInfo CounterUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\counter-unavailable.svg");
    // Weather fallback for unknown conditions (sunny shape, blue / grey).
    public static IconInfo Weather => IconHelpers.FromRelativePath("Assets\\Icons\\weather-off.svg");
    public static IconInfo WeatherUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\weather-unavailable.svg");

    /// <summary>
    /// Generic state-tinted icon dispatch. Used by the
    /// <see cref="HomeAssistantCommandPalette.Pages.Domains.DomainBehavior"/>
    /// default — concrete behaviors override <c>BuildIcon</c> when they
    /// need richer rules (sub-state palettes, device_class dispatch,
    /// supported_features gating, …). Domains land here as their PRs
    /// migrate them off the legacy <c>IconForEntity</c> dispatcher.
    /// </summary>
    public static IconInfo ForDomain(string domain, string state)
    {
        var unavailable = string.Equals(state, "unavailable", System.StringComparison.OrdinalIgnoreCase);
        var on = string.Equals(state, "on", System.StringComparison.OrdinalIgnoreCase);
        return domain switch
        {
            "switch"        => unavailable ? SwitchUnavailable        : on ? SwitchOn        : SwitchOff,
            "input_boolean" => unavailable ? InputBooleanUnavailable  : on ? InputBooleanOn  : InputBooleanOff,
            "script"        => unavailable ? ScriptUnavailable        : on ? ScriptOn        : ScriptOff,
            "scene"         => unavailable ? SceneUnavailable         : Scene,
            "button"        => unavailable ? ButtonUnavailable        : Button,
            "input_button"  => InputButton,
            "counter"       => unavailable ? CounterUnavailable       : Counter,
            _               => unavailable ? ShapeUnavailable         : Shape,
        };
    }

    /// <summary>
    /// Returns the icon for an HA weather condition string. Mirrors the
    /// MDI weather glyph set the Home Assistant frontend uses
    /// (weather-sunny / weather-cloudy / weather-rainy / …). Unknown
    /// conditions fall back to the generic sunny icon.
    /// </summary>
    public static IconInfo WeatherForCondition(string condition, bool unavailable)
    {
        var stem = condition?.ToLowerInvariant() switch
        {
            "clear-night" => "weather-clear-night",
            "cloudy" => "weather-cloudy",
            "exceptional" => "weather-exceptional",
            "fog" => "weather-fog",
            "hail" => "weather-hail",
            "lightning" => "weather-lightning",
            "lightning-rainy" => "weather-lightning-rainy",
            "partlycloudy" => "weather-partly-cloudy",
            "pouring" => "weather-pouring",
            "rainy" => "weather-rainy",
            "snowy" => "weather-snowy",
            "snowy-rainy" => "weather-snowy-rainy",
            "sunny" => "weather-sunny",
            "windy" => "weather-windy",
            "windy-variant" => "weather-windy-variant",
            _ => null,
        };
        if (stem is null) return unavailable ? WeatherUnavailable : Weather;
        var suffix = unavailable ? "unavailable" : "off";
        return IconHelpers.FromRelativePath($"Assets\\Icons\\{stem}-{suffix}.svg");
    }

    // device_class icons for binary_sensor / sensor entities. State-bearing
    // ones (door / window / motion / connectivity / plug) follow the
    // yellow/blue/grey palette; the others are always blue with a grey
    // unavailable fallback.
    public static IconInfo DoorOpen => IconHelpers.FromRelativePath("Assets\\Icons\\door-on.svg");
    public static IconInfo DoorClosed => IconHelpers.FromRelativePath("Assets\\Icons\\door-off.svg");
    public static IconInfo DoorUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\door-unavailable.svg");
    public static IconInfo WindowOpen => IconHelpers.FromRelativePath("Assets\\Icons\\window-binary-on.svg");
    public static IconInfo WindowClosed => IconHelpers.FromRelativePath("Assets\\Icons\\window-binary-off.svg");
    public static IconInfo WindowUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\window-binary-unavailable.svg");
    public static IconInfo MotionDetected => IconHelpers.FromRelativePath("Assets\\Icons\\motion-on.svg");
    public static IconInfo MotionClear => IconHelpers.FromRelativePath("Assets\\Icons\\motion-off.svg");
    public static IconInfo MotionUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\motion-unavailable.svg");
    public static IconInfo ConnectivityOn => IconHelpers.FromRelativePath("Assets\\Icons\\connectivity-on.svg");
    public static IconInfo ConnectivityOff => IconHelpers.FromRelativePath("Assets\\Icons\\connectivity-off.svg");
    public static IconInfo ConnectivityUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\connectivity-unavailable.svg");
    public static IconInfo PlugOn => IconHelpers.FromRelativePath("Assets\\Icons\\plug-on.svg");
    public static IconInfo PlugOff => IconHelpers.FromRelativePath("Assets\\Icons\\plug-off.svg");
    public static IconInfo PlugUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\plug-unavailable.svg");

    public static IconInfo Temperature => IconHelpers.FromRelativePath("Assets\\Icons\\temperature-off.svg");
    public static IconInfo TemperatureUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\temperature-unavailable.svg");
    public static IconInfo Humidity => IconHelpers.FromRelativePath("Assets\\Icons\\humidity-off.svg");
    public static IconInfo HumidityUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\humidity-unavailable.svg");
    public static IconInfo Pressure => IconHelpers.FromRelativePath("Assets\\Icons\\pressure-off.svg");
    public static IconInfo PressureUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\pressure-unavailable.svg");
    public static IconInfo Energy => IconHelpers.FromRelativePath("Assets\\Icons\\energy-off.svg");
    public static IconInfo EnergyUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\energy-unavailable.svg");
    public static IconInfo PowerFactor => IconHelpers.FromRelativePath("Assets\\Icons\\power-factor-off.svg");
    public static IconInfo PowerFactorUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\power-factor-unavailable.svg");
    public static IconInfo CarbonDioxide => IconHelpers.FromRelativePath("Assets\\Icons\\carbon-dioxide-off.svg");
    public static IconInfo CarbonDioxideUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\carbon-dioxide-unavailable.svg");
    // Battery fallback when the entity state can't be parsed as a percentage.
    public static IconInfo Battery => IconHelpers.FromRelativePath("Assets\\Icons\\battery-off.svg");
    public static IconInfo BatteryUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\battery-unavailable.svg");

    /// <summary>
    /// Returns a battery icon whose shape reflects the charge level (10
    /// buckets of 10% plus an "outline" empty and a "full" 100% bucket)
    /// and whose tint reflects urgency: red ≤ 20%, yellow ≤ 30%, blue
    /// otherwise. Unavailable always wins. Mirrors Raycast's
    /// getBatteryIconFromState in src/components/battery/utils.ts.
    /// </summary>
    public static IconInfo BatteryForLevel(double percent, bool unavailable)
    {
        // Clamp into the 11 baked buckets: 0 → empty outline; 10 → full.
        var bucket = (int)System.Math.Floor(percent / 10.0);
        if (bucket < 0) bucket = 0;
        if (bucket > 10) bucket = 10;

        var stem = bucket switch
        {
            0 => "battery-outline",
            10 => "battery-full",
            _ => $"battery-{bucket * 10}",
        };

        // Match the Raycast urgency thresholds. The bake step only produced
        // the (bucket × colour) combinations that can actually occur, so
        // these branches must stay aligned with that matrix in
        // scripts/bake-icons.ps1.
        string suffix;
        if (unavailable) suffix = "unavailable";
        else if (percent <= 20) suffix = "low";
        else if (percent <= 30) suffix = "on";
        else suffix = "off";

        return IconHelpers.FromRelativePath($"Assets\\Icons\\{stem}-{suffix}.svg");
    }

    // Generic fallback when no domain or device_class match is found.
    public static IconInfo Shape => IconHelpers.FromRelativePath("Assets\\Icons\\shape-off.svg");
    public static IconInfo ShapeUnavailable => IconHelpers.FromRelativePath("Assets\\Icons\\shape-unavailable.svg");

    // Service-call command icons (use the unbaked SVG; CmdPal renders them
    // in the palette's foreground color via currentColor).
    public static IconInfo Toggle => IconHelpers.FromRelativePath("Assets\\Icons\\toggle-switch-outline.svg");
    public static IconInfo TurnOn => IconHelpers.FromRelativePath("Assets\\Icons\\power-on.svg");
    public static IconInfo TurnOff => IconHelpers.FromRelativePath("Assets\\Icons\\power-off.svg");
    public static IconInfo Brightness => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb.svg");
    public static IconInfo Open => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-up-box.svg");
    public static IconInfo Close => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-down-box.svg");
    public static IconInfo Stop => IconHelpers.FromRelativePath("Assets\\Icons\\stop.svg");
    public static IconInfo Play => IconHelpers.FromRelativePath("Assets\\Icons\\play.svg");
    public static IconInfo Pause => IconHelpers.FromRelativePath("Assets\\Icons\\pause.svg");
    public static IconInfo PlayPause => IconHelpers.FromRelativePath("Assets\\Icons\\play-pause.svg");
    public static IconInfo Next => IconHelpers.FromRelativePath("Assets\\Icons\\skip-next.svg");
    public static IconInfo Previous => IconHelpers.FromRelativePath("Assets\\Icons\\skip-previous.svg");
    public static IconInfo Volume => IconHelpers.FromRelativePath("Assets\\Icons\\volume.svg");
    public static IconInfo VolumeUp => IconHelpers.FromRelativePath("Assets\\Icons\\volume-plus.svg");
    public static IconInfo VolumeDown => IconHelpers.FromRelativePath("Assets\\Icons\\volume-minus.svg");
    public static IconInfo VolumeMute => IconHelpers.FromRelativePath("Assets\\Icons\\volume-off.svg");
}
