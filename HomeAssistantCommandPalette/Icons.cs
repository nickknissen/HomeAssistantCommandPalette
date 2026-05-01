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

    // Service-call command icons (use the unbaked SVG; CmdPal renders them
    // in the palette's foreground color via currentColor).
    public static IconInfo Toggle => IconHelpers.FromRelativePath("Assets\\Icons\\toggle-switch-outline.svg");
    public static IconInfo TurnOn => IconHelpers.FromRelativePath("Assets\\Icons\\power-on.svg");
    public static IconInfo TurnOff => IconHelpers.FromRelativePath("Assets\\Icons\\power-off.svg");
    public static IconInfo Brightness => IconHelpers.FromRelativePath("Assets\\Icons\\lightbulb.svg");
    public static IconInfo Open => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-up-box.svg");
    public static IconInfo Close => IconHelpers.FromRelativePath("Assets\\Icons\\arrow-down-box.svg");
    public static IconInfo Stop => IconHelpers.FromRelativePath("Assets\\Icons\\stop.svg");
}
