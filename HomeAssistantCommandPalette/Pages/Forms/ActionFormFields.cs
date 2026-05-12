using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Pages.Forms;

/// <summary>
/// Shared parser / renderer for HA action `fields` schemas. Used by
/// both the script form (issue #8) and the generic Run Action form
/// (issue #16). The Adaptive Cards body is produced as a JSON
/// fragment; the host page wraps it in the surrounding card.
/// </summary>
internal static class ActionFormFields
{
    internal enum ActionFieldKind { Text, Number, Boolean, Select }

    internal sealed record ActionField(
        string Key,
        string Label,
        ActionFieldKind Kind,
        bool Required,
        object? Default,
        string? Description,
        string? Example,
        string[]? SelectOptions,
        double? Min,
        double? Max);

    public static IReadOnlyList<ActionField> Parse(IReadOnlyDictionary<string, object?> fields)
    {
        var result = new List<ActionField>();
        foreach (var (key, value) in fields)
        {
            if (value is not IReadOnlyDictionary<string, object?> spec) continue;

            var label = spec.TryGetValue("name", out var n) && n is string nm && !string.IsNullOrWhiteSpace(nm) ? nm : key;
            var description = spec.TryGetValue("description", out var d) && d is string ds ? ds : null;
            var example = spec.TryGetValue("example", out var ex) ? ex?.ToString() : null;
            var required = spec.TryGetValue("required", out var r) && r is bool rb && rb;
            spec.TryGetValue("default", out var defaultValue);

            var (kind, options, min, max) = InferKind(spec);
            result.Add(new ActionField(key, label, kind, required, defaultValue, description, example, options, min, max));
        }
        return result;
    }

    /// <summary>
    /// Returns the comma-separated list of Adaptive Card body elements
    /// for <paramref name="fields"/>, each one optionally preceded by a
    /// subtle description block. Caller wraps in `[ ... ]`.
    /// </summary>
    public static string RenderBody(IReadOnlyList<ActionField> fields)
    {
        var parts = new List<string>();
        foreach (var f in fields)
        {
            if (!string.IsNullOrWhiteSpace(f.Description))
            {
                parts.Add($$"""{ "type": "TextBlock", "text": {{Encode(f.Description!)}}, "wrap": true, "isSubtle": true, "size": "Small", "spacing": "None" }""");
            }
            parts.Add(RenderInput(f));
        }
        return string.Join(",\n            ", parts);
    }

    /// <summary>
    /// Reads the Adaptive Card form inputs and produces an HA service
    /// data dictionary with correctly-typed values. Returns false (and
    /// sets <paramref name="error"/>) when a required field is missing
    /// or a number fails to parse.
    /// </summary>
    public static bool TryBuildData(
        IReadOnlyList<ActionField> fields,
        JsonObject inputs,
        out IReadOnlyDictionary<string, object?> data,
        out string error)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var field in fields)
        {
            var raw = inputs[field.Key]?.ToString() ?? string.Empty;
            if (raw.Length == 0)
            {
                if (field.Kind == ActionFieldKind.Boolean)
                {
                    dict[field.Key] = false;
                    continue;
                }
                if (field.Required)
                {
                    data = new Dictionary<string, object?>();
                    error = $"Enter a value for {field.Label}.";
                    return false;
                }
                continue;
            }

            switch (field.Kind)
            {
                case ActionFieldKind.Number:
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    {
                        data = new Dictionary<string, object?>();
                        error = $"{field.Label} must be a number.";
                        return false;
                    }
                    dict[field.Key] = num;
                    break;
                case ActionFieldKind.Boolean:
                    dict[field.Key] = string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    dict[field.Key] = raw;
                    break;
            }
        }

        data = dict;
        error = string.Empty;
        return true;
    }

    public static string Encode(string value) => JsonSerializer.Serialize(value, HaJsonContext.Default.String);

    public static string FormatNum(double value)
        => value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string RenderInput(ActionField f)
    {
        var label = Encode(f.Label);
        var idAttr = Encode(f.Key);
        var required = f.Required ? ", \"isRequired\": true" : string.Empty;
        var requiredError = f.Required ? $", \"errorMessage\": {Encode($"Enter {f.Label}.")}" : string.Empty;

        switch (f.Kind)
        {
            case ActionFieldKind.Number:
                var nVal = f.Default is double dd
                    ? $", \"value\": {FormatNum(dd)}"
                    : f.Default is long ll
                        ? $", \"value\": {ll.ToString(CultureInfo.InvariantCulture)}"
                        : string.Empty;
                var minAttr = f.Min is { } mn ? $", \"min\": {FormatNum(mn)}" : string.Empty;
                var maxAttr = f.Max is { } mx ? $", \"max\": {FormatNum(mx)}" : string.Empty;
                return $$"""{ "type": "Input.Number", "id": {{idAttr}}, "label": {{label}}{{nVal}}{{minAttr}}{{maxAttr}}{{required}}{{requiredError}} }""";

            case ActionFieldKind.Boolean:
                var bDefault = f.Default is bool bd && bd ? ", \"value\": \"true\"" : string.Empty;
                return $$"""{ "type": "Input.Toggle", "id": {{idAttr}}, "title": {{label}}, "valueOn": "true", "valueOff": "false"{{bDefault}} }""";

            case ActionFieldKind.Select:
                var sDefault = f.Default is string ds && f.SelectOptions is { } opts && Array.IndexOf(opts, ds) >= 0
                    ? $", \"value\": {Encode(ds)}"
                    : string.Empty;
                var choices = string.Join(", ",
                    (f.SelectOptions ?? Array.Empty<string>())
                        .Select(o => $"{{ \"title\": {Encode(o)}, \"value\": {Encode(o)} }}"));
                return $$"""{ "type": "Input.ChoiceSet", "id": {{idAttr}}, "label": {{label}}, "style": "compact"{{sDefault}}{{required}}{{requiredError}}, "choices": [ {{choices}} ] }""";

            default:
                var tDefault = f.Default is string ts ? $", \"value\": {Encode(ts)}" : string.Empty;
                var placeholder = !string.IsNullOrWhiteSpace(f.Example) ? $", \"placeholder\": {Encode(f.Example!)}" : string.Empty;
                return $$"""{ "type": "Input.Text", "id": {{idAttr}}, "label": {{label}}{{tDefault}}{{placeholder}}{{required}}{{requiredError}} }""";
        }
    }

    private static (ActionFieldKind Kind, string[]? Options, double? Min, double? Max) InferKind(IReadOnlyDictionary<string, object?> spec)
    {
        if (!spec.TryGetValue("selector", out var s) || s is not IReadOnlyDictionary<string, object?> selector)
        {
            return (ActionFieldKind.Text, null, null, null);
        }

        if (selector.ContainsKey("boolean")) return (ActionFieldKind.Boolean, null, null, null);
        if (selector.TryGetValue("number", out var nv) && nv is IReadOnlyDictionary<string, object?> numCfg)
        {
            double? min = TryGetNumber(numCfg, "min");
            double? max = TryGetNumber(numCfg, "max");
            return (ActionFieldKind.Number, null, min, max);
        }
        if (selector.TryGetValue("select", out var sv) && sv is IReadOnlyDictionary<string, object?> selCfg
            && selCfg.TryGetValue("options", out var opts) && opts is List<object?> rawOptions)
        {
            var options = rawOptions.Select(o => o switch
            {
                string str => str,
                IReadOnlyDictionary<string, object?> dict when dict.TryGetValue("value", out var v) => v?.ToString() ?? string.Empty,
                _ => o?.ToString() ?? string.Empty,
            }).Where(o => !string.IsNullOrEmpty(o)).ToArray();
            return (ActionFieldKind.Select, options, null, null);
        }
        return (ActionFieldKind.Text, null, null, null);
    }

    private static double? TryGetNumber(IReadOnlyDictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) && v switch
        {
            double d => (double?)d,
            long l => l,
            int i => i,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        } is { } result ? result : null;
}
