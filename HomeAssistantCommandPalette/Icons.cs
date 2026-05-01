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
