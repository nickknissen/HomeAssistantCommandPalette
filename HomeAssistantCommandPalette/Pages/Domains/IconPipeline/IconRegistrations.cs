using HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Single source of truth for entity-icon dispatch. Each line registers
/// a rule for one HA domain. Trivial domains use <c>Stateless</c> /
/// <c>OnOff</c> sugar (data); domains with rich state machines or
/// attribute-driven rules register a sealed <see cref="IDomainIconRule"/>.
/// Unregistered domains fall back to <see cref="Icons.ForDomain(string, string)"/>.
/// </summary>
internal static class IconRegistrations
{
    public static void Configure(IconRegistry r)
    {
        // ── Stateless ── one icon, optional unavailable variant.
        r.Stateless("scene",         Icons.Scene,        Icons.SceneUnavailable);
        r.Stateless("button",        Icons.Button,       Icons.ButtonUnavailable);
        r.Stateless("input_button",  Icons.InputButton);
        r.Stateless("counter",       Icons.Counter,      Icons.CounterUnavailable);
        r.Stateless("zone",          Icons.Zone,         Icons.ZoneUnavailable);
        r.Stateless("input_text",    Icons.InputText);
        r.Stateless("input_select",  Icons.InputSelect);
        r.Stateless("input_number",  Icons.InputNumber);
        r.Stateless("camera",        Icons.Camera,       Icons.CameraUnavailable);

        // ── OnOff ── three-state palette driven by HaEntity.IsOn
        // (which already covers state == "on" / "open" / "playing").
        r.OnOff("switch",        Icons.SwitchOn,        Icons.SwitchOff,        Icons.SwitchUnavailable);
        r.OnOff("input_boolean", Icons.InputBooleanOn,  Icons.InputBooleanOff,  Icons.InputBooleanUnavailable);
        r.OnOff("automation",    Icons.AutomationOn,    Icons.AutomationOff,    Icons.AutomationUnavailable);
        r.OnOff("script",        Icons.ScriptOn,        Icons.ScriptOff,        Icons.ScriptUnavailable);
        r.OnOff("fan",           Icons.FanOn,           Icons.FanOff,           Icons.FanUnavailable);
        r.OnOff("update",        Icons.UpdateOn,        Icons.UpdateOff,        Icons.UpdateUnavailable);
        r.OnOff("media_player",  Icons.MediaPlayerPlaying, Icons.MediaPlayerIdle, Icons.MediaPlayerUnavailable);

        // ── Rich ── state machines, attribute-driven dispatch.
        r.Rich("light",          new LightIconRule());
        r.Rich("climate",        new ClimateIconRule());
        r.Rich("cover",          new CoverIconRule());
        r.Rich("sensor",         new SensorIconRule());
        r.Rich("binary_sensor",  new BinarySensorIconRule());
        r.Rich("weather",        new WeatherIconRule());
        r.Rich("vacuum",         new VacuumIconRule());
        r.Rich("person",         new PersonIconRule());
        r.Rich("timer",          new TimerIconRule());
        r.Rich("input_datetime", new InputDateTimeIconRule());
        // sun.sun is the only entity in the `sun` domain — register at
        // the domain level rather than threading entity-id overrides.
        r.Rich("sun",            new SunIconRule());
    }
}
