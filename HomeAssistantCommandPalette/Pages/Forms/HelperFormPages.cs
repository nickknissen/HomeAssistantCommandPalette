using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Forms;

internal sealed partial class HelperFormPage : ContentPage
{
    private readonly FormContent _form;

    public HelperFormPage(HaEntity entity, FormContent form, string title, IconInfo icon)
        : this(form, title, "ha.form:" + entity.EntityId, icon)
    {
    }

    public HelperFormPage(FormContent form, string title, string id, IconInfo icon)
    {
        _form = form;
        Title = title;
        Name = title;
        Id = id;
        Icon = icon;
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class InputTextFormContent : HelperServiceFormContent
{
    public InputTextFormContent(HaEntity entity, IHaClient client, Action onSuccess)
        : base(entity, client, onSuccess)
    {
        TemplateJson = """
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            { "type": "Input.Text", "id": "value", "label": "Value" }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Submit" } ]
        }
        """;
    }

    protected override bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error)
    {
        var value = inputs["value"]?.ToString() ?? string.Empty;
        if (value.Length == 0)
        {
            data = EmptyData;
            error = "Enter a value.";
            return false;
        }

        data = new Dictionary<string, object?> { ["value"] = value };
        error = string.Empty;
        return true;
    }

    protected override string Domain => "input_text";

    protected override string Service => "set_value";
}

internal sealed partial class InputNumberFormContent : HelperServiceFormContent
{
    public InputNumberFormContent(HaEntity entity, IHaClient client, Action onSuccess)
        : base(entity, client, onSuccess)
    {
        // Input.Number's `value` / `min` / `max` are numbers, not strings —
        // emit them bare, or omit when missing / non-numeric, since
        // quoting makes the renderer fail silently.
        var valueAttr = double.TryParse(Entity.State, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? $", \"value\": {FormatNum(v)}"
            : string.Empty;
        var minAttr = TryGetDouble(entity, "min", out var minValue) ? $", \"min\": {FormatNum(minValue)}" : string.Empty;
        var maxAttr = TryGetDouble(entity, "max", out var maxValue) ? $", \"max\": {FormatNum(maxValue)}" : string.Empty;

        TemplateJson = $$"""
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            { "type": "Input.Number", "id": "value", "label": "Value"{{valueAttr}}{{minAttr}}{{maxAttr}} }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Submit" } ]
        }
        """;
    }

    protected override bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error)
    {
        var raw = inputs["value"]?.ToString();
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            data = EmptyData;
            error = "Enter a valid number.";
            return false;
        }

        data = new Dictionary<string, object?> { ["value"] = value };
        error = string.Empty;
        return true;
    }

    protected override string Domain => "input_number";

    protected override string Service => "set_value";
}

internal sealed partial class InputSelectFormContent : HelperServiceFormContent
{
    public InputSelectFormContent(HaEntity entity, IHaClient client, Action onSuccess)
        : base(entity, client, onSuccess)
    {
        var options = (entity.Attributes.TryGetValue("options", out var opts) && opts is List<object?> list
            ? list.OfType<string>()
            : Enumerable.Empty<string>()).ToArray();

        var choices = string.Join(",\n            ",
            options.Select(o => $"{{ \"title\": {Encode(o)}, \"value\": {Encode(o)} }}"));
        var valueAttr = options.Contains(entity.State, StringComparer.Ordinal)
            ? $", \"value\": {Encode(entity.State)}"
            : string.Empty;

        TemplateJson = $$"""
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            { "type": "Input.ChoiceSet", "id": "option", "label": "Option", "style": "compact", "isRequired": true, "errorMessage": "Select an option"{{valueAttr}}, "choices": [ {{choices}} ] }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Submit" } ]
        }
        """;
    }

    protected override bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error)
    {
        var option = inputs["option"]?.ToString() ?? string.Empty;
        if (option.Length == 0)
        {
            data = EmptyData;
            error = "Select an option.";
            return false;
        }

        data = new Dictionary<string, object?> { ["option"] = option };
        error = string.Empty;
        return true;
    }

    protected override string Domain => "input_select";

    protected override string Service => "select_option";
}

internal sealed partial class InputDateTimeFormContent : HelperServiceFormContent
{
    private readonly bool _hasDate;
    private readonly bool _hasTime;

    public InputDateTimeFormContent(HaEntity entity, IHaClient client, Action onSuccess)
        : base(entity, client, onSuccess)
    {
        _hasDate = !entity.Attributes.TryGetValue("has_date", out var hd) || hd is not bool hasDate || hasDate;
        _hasTime = !entity.Attributes.TryGetValue("has_time", out var ht) || ht is not bool hasTime || hasTime;

        var dateColumn = _hasDate ? $$"""
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "Input.Date", "id": "date", "label": "Date", "value": {{Encode(DateValue(entity.State))}}, "isRequired": true, "errorMessage": "Enter a date" }
              ]
            }
        """ : string.Empty;
        var timeColumn = _hasTime ? $$"""
            {{(_hasDate ? "," : string.Empty)}}
            {
              "type": "Column",
              "width": "stretch",
              "items": [
                { "type": "Input.Text", "id": "time", "label": "Time (HH:MM)", "value": {{Encode(TimeValue(entity.State))}}, "regex": "^([01][0-9]|2[0-3]):[0-5][0-9]$", "isRequired": true, "errorMessage": "Enter time as HH:MM (24-hour)" }
              ]
            }
        """ : string.Empty;

        TemplateJson = $$"""
        {
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "type": "AdaptiveCard",
          "version": "1.5",
          "body": [
            { "type": "TextBlock", "text": {{Encode(Entity.FriendlyName)}}, "weight": "Bolder", "size": "Medium", "wrap": true },
            {
              "type": "ColumnSet",
              "columns": [
                {{dateColumn}}{{timeColumn}}
              ]
            }
          ],
          "actions": [ { "type": "Action.Submit", "title": "Submit" } ]
        }
        """;
    }

    protected override bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error)
    {
        var dict = new Dictionary<string, object?>();
        var date = inputs["date"]?.ToString() ?? string.Empty;
        var time = inputs["time"]?.ToString() ?? string.Empty;

        if (_hasDate)
        {
            if (date.Length == 0)
            {
                data = EmptyData;
                error = "Enter a date.";
                return false;
            }
            dict["date"] = date;
        }

        if (_hasTime)
        {
            if (time.Length == 0)
            {
                data = EmptyData;
                error = "Enter a time.";
                return false;
            }
            dict["time"] = time.Length == 5 ? time + ":00" : time;
        }

        data = dict;
        error = string.Empty;
        return true;
    }

    protected override string Domain => "input_datetime";

    protected override string Service => "set_datetime";

    private static string DateValue(string state)
        => DateTimeOffset.TryParse(state, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string TimeValue(string state)
    {
        if (TimeSpan.TryParse(state, CultureInfo.InvariantCulture, out var time))
        {
            return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }
        return DateTimeOffset.TryParse(state, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value)
            ? value.ToString("HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;
    }
}

internal sealed partial class ScriptFormContent : HelperServiceFormContent
{
    private readonly IReadOnlyList<ActionFormFields.ActionField> _fields;
    private readonly string _objectId;

    public ScriptFormContent(HaEntity entity, IHaClient client, Action onSuccess, IReadOnlyDictionary<string, object?> fields)
        : base(entity, client, onSuccess)
    {
        _objectId = ObjectIdOf(entity.EntityId);
        _fields = ActionFormFields.Parse(fields);

        TemplateJson = $$"""
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            {{ActionFormFields.RenderBody(_fields)}}
          ],
          "actions": [ { "type": "Action.Submit", "title": "Run" } ]
        }
        """;
    }

    protected override bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error)
        => ActionFormFields.TryBuildData(_fields, inputs, out data, out error);

    protected override string Domain => "script";

    protected override string Service => _objectId;

    private static string ObjectIdOf(string entityId)
    {
        var dot = entityId.IndexOf('.');
        return dot < 0 ? entityId : entityId[(dot + 1)..];
    }
}

internal abstract partial class HelperServiceFormContent : FormContent
{
    protected static readonly IReadOnlyDictionary<string, object?> EmptyData = new Dictionary<string, object?>();

    protected HelperServiceFormContent(HaEntity entity, IHaClient client, Action onSuccess)
    {
        Entity = entity;
        Client = client;
        OnSuccess = onSuccess;

        // FormContent leaves DataJson as "" by default, but the host
        // pipes it through AdaptiveCardTemplate.Expand, which throws on
        // a non-JSON string. That leaves the page blank. We don't use
        // template bindings, so an empty object is enough.
        DataJson = "{}";
    }

    protected HaEntity Entity { get; }

    protected IHaClient Client { get; }

    protected Action OnSuccess { get; }

    protected abstract string Domain { get; }

    protected abstract string Service { get; }

    public override CommandResult SubmitForm(string inputs)
    {
        var formInput = JsonNode.Parse(inputs)?.AsObject();
        if (formInput is null)
        {
            return Error("Invalid form input.");
        }

        if (!TryBuildServiceData(formInput, out var data, out var validationError))
        {
            return Error(validationError);
        }

        if (Client.TryCallService(Domain, Service, Entity.EntityId, data, out var error))
        {
            OnSuccess();
            return CommandResult.GoBack();
        }

        return Error("Failed: " + error);
    }

    protected abstract bool TryBuildServiceData(JsonObject inputs, out IReadOnlyDictionary<string, object?> data, out string error);

    protected static string Encode(string value) => JsonSerializer.Serialize(value, HaJsonContext.Default.String);

    protected static bool TryGetDouble(HaEntity entity, string key, out double value)
    {
        if (!entity.Attributes.TryGetValue(key, out var raw))
        {
            value = 0;
            return false;
        }

        return raw switch
        {
            double d => Set(out value, d),
            long l => Set(out value, l),
            int i => Set(out value, i),
            string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value),
            _ => Set(out value, 0, success: false),
        };
    }

    protected static string FormatNum(double value)
        => value == Math.Floor(value)
            ? ((long)value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static bool Set(out double target, double value, bool success = true)
    {
        target = value;
        return success;
    }

    private static CommandResult Error(string message)
        => CommandResult.ShowToast(new ToastArgs
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
}
