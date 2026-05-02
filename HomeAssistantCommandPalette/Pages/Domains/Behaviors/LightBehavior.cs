using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class LightBehavior : DomainBehavior
{
    // Preset palette for the "Set color…" submenu. RGB triplets are pure
    // primaries / secondaries so each option behaves the same on every
    // RGB-capable bulb regardless of its colour-mode (rgb / rgbw / hs /
    // xy — HA converts internally).
    private static readonly (string Name, int[] Rgb)[] ColorPalette =
    [
        ("Red",    new[] { 255,   0,   0 }),
        ("Orange", new[] { 255, 128,   0 }),
        ("Yellow", new[] { 255, 255,   0 }),
        ("Green",  new[] {   0, 255,   0 }),
        ("Cyan",   new[] {   0, 255, 255 }),
        ("Blue",   new[] {   0,   0, 255 }),
        ("Purple", new[] { 128,   0, 255 }),
        ("Pink",   new[] { 255,   0, 128 }),
        ("White",  new[] { 255, 255, 255 }),
    ];

    public override string Domain => "light";

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new CallServiceCommand(
            ctx.Client, "light", "toggle", ctx.Entity.EntityId,
            $"Toggle {ctx.Entity.FriendlyName}",
            icon: Icons.Toggle, onSuccess: ctx.OnSuccess);

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;

        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "light", "turn_on", entity.EntityId,
            $"Turn on {name}", icon: Icons.TurnOn, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "light", "turn_off", entity.EntityId,
            $"Turn off {name}", icon: Icons.TurnOff, onSuccess: ctx.OnSuccess)));

        // `in` parameters can't be captured by lambdas — bind locally.
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;

        // Brightness presets — calls light.turn_on with brightness_pct
        // (HA turns the light on at that level whether it was on or off).
        items.Add(new CommandContextItem(new NoOpCommand())
        {
            Title = "Set brightness…",
            Icon = Icons.Brightness,
            MoreCommands = new IContextItem[]
            {
                BrightnessPreset(client, entityId, onSuccess, 25),
                BrightnessPreset(client, entityId, onSuccess, 50),
                BrightnessPreset(client, entityId, onSuccess, 75),
                BrightnessPreset(client, entityId, onSuccess, 100),
            },
        });

        // RGB color picker — only when the bulb declares an RGB-capable
        // colour mode. HA exposes capability via `supported_color_modes`
        // (a list of strings). Modes that accept rgb_color: rgb, rgbw,
        // rgbww, hs, xy. Older single-channel bulbs would error on rgb_color.
        if (entity.Attributes.TryGetValue("supported_color_modes", out var modes)
            && modes is List<object?> modeList
            && modeList.OfType<string>().Any(m =>
                m is "rgb" or "rgbw" or "rgbww" or "hs" or "xy"))
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set color…",
                Icon = Icons.Brightness,
                MoreCommands = ColorPalette
                    .Select(c => (IContextItem)ColorPreset(client, entityId, onSuccess, c.Name, c.Rgb))
                    .ToArray(),
            });
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        // brightness in HA states is 0..255
        if (entity.Attributes.TryGetValue("brightness", out var b) && b is long br && br > 0)
        {
            rows.Add(DomainHelpers.Row("Brightness", $"{(int)Math.Round(br / 255.0 * 100)}%"));
        }
        if (entity.Attributes.TryGetValue("color_temp_kelvin", out var ctk) && ctk is long k && k > 0)
        {
            rows.Add(DomainHelpers.Row("Color temp", $"{k}K"));
        }
        // Min / max Kelvin range — only when both are reported. Helps the
        // user know the bulb's tunable-white window without round-tripping
        // to the HA UI. Some firmwares report only the legacy mireds pair,
        // so fall back to mireds → kelvin (k = 1_000_000 / mired).
        var minK = entity.Attributes.TryGetValue("min_color_temp_kelvin", out var mnk) && mnk is long mnki ? mnki : 0;
        var maxK = entity.Attributes.TryGetValue("max_color_temp_kelvin", out var mxk) && mxk is long mxki ? mxki : 0;
        if (minK == 0 && maxK == 0
            && entity.Attributes.TryGetValue("min_mireds", out var mnm) && mnm is long mnmi && mnmi > 0
            && entity.Attributes.TryGetValue("max_mireds", out var mxm) && mxm is long mxmi && mxmi > 0)
        {
            // k = 1_000_000 / mired. Smaller mired → higher kelvin, so the
            // mired pair swaps: min Kelvin comes from max mireds.
            minK = 1_000_000 / mxmi;
            maxK = 1_000_000 / mnmi;
        }
        if (minK > 0 && maxK > 0)
        {
            rows.Add(DomainHelpers.Row("Color temp range", $"{minK}K – {maxK}K"));
        }
        // rgb_color is reported as a 3-element list of ints (sometimes
        // longs after JSON deserialization). Stringify each component so
        // we don't end up rendering the .NET list's type name.
        if (entity.Attributes.TryGetValue("rgb_color", out var rgb) && rgb is List<object?> rgbList && rgbList.Count == 3)
        {
            var parts = rgbList.Select(c => c switch
            {
                long l => l.ToString(CultureInfo.InvariantCulture),
                int i => i.ToString(CultureInfo.InvariantCulture),
                double d => ((int)Math.Round(d)).ToString(CultureInfo.InvariantCulture),
                _ => c?.ToString() ?? "?",
            });
            rows.Add(DomainHelpers.Row("RGB", string.Join(", ", parts)));
        }
        if (entity.Attributes.TryGetValue("color_mode", out var mode) && mode is string m && !string.IsNullOrEmpty(m))
        {
            rows.Add(DomainHelpers.Row("Color mode", m));
        }
        if (entity.Attributes.TryGetValue("effect", out var fx) && fx is string fxs && !string.IsNullOrEmpty(fxs)
            && !string.Equals(fxs, "none", StringComparison.OrdinalIgnoreCase))
        {
            rows.Add(DomainHelpers.Row("Effect", fxs));
        }
    }

    private static CommandContextItem BrightnessPreset(IHaClient client, string entityId, Action onSuccess, int pct)
        => new(new CallServiceCommand(
            client, "light", "turn_on", entityId,
            $"{pct}%", icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["brightness_pct"] = pct },
            onSuccess: onSuccess));

    private static CommandContextItem ColorPreset(IHaClient client, string entityId, Action onSuccess, string name, int[] rgb)
        => new(new CallServiceCommand(
            client, "light", "turn_on", entityId,
            name, icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["rgb_color"] = rgb },
            onSuccess: onSuccess));
}
