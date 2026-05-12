using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Forms;

/// <summary>
/// Generic form for HA's Run Action page (issue #16). Takes an
/// arbitrary <c>{domain}.{service}</c> plus its fields schema; if the
/// action targets an entity, a free-text <c>entity_id</c> input is
/// prepended. Submits via <see cref="IHaClient.TryCallService"/>.
/// </summary>
internal sealed partial class ActionFormContent : FormContent
{
    private const string EntityIdField = "__entity_id";

    private readonly IHaClient _client;
    private readonly Action _onSuccess;
    private readonly string _domain;
    private readonly string _service;
    private readonly bool _hasTarget;
    private readonly IReadOnlyList<ActionFormFields.ActionField> _fields;

    public ActionFormContent(
        IHaClient client,
        Action onSuccess,
        string domain,
        string service,
        IReadOnlyDictionary<string, object?> fields,
        bool hasTarget)
    {
        _client = client;
        _onSuccess = onSuccess;
        _domain = domain;
        _service = service;
        _hasTarget = hasTarget;
        _fields = ActionFormFields.Parse(fields);

        // FormContent leaves DataJson as "" by default; the host pipes
        // it through AdaptiveCardTemplate.Expand which throws on a
        // non-JSON string and leaves the page blank.
        DataJson = "{}";

        var bodyParts = new List<string>();
        if (hasTarget)
        {
            bodyParts.Add($$"""{ "type": "TextBlock", "text": "Target entity", "wrap": true, "weight": "Bolder" }""");
            bodyParts.Add($$"""{ "type": "Input.Text", "id": {{ActionFormFields.Encode(EntityIdField)}}, "label": "entity_id", "placeholder": "e.g. light.living_room", "isRequired": true, "errorMessage": "Enter an entity_id." }""");
        }
        var rendered = ActionFormFields.RenderBody(_fields);
        if (!string.IsNullOrEmpty(rendered))
        {
            bodyParts.Add(rendered);
        }
        if (bodyParts.Count == 0)
        {
            // No target, no fields — a one-button "Run" form.
            bodyParts.Add($$"""{ "type": "TextBlock", "text": {{ActionFormFields.Encode($"Call {domain}.{service}")}}, "wrap": true }""");
        }

        var body = string.Join(",\n            ", bodyParts);
        TemplateJson = $$"""
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            {{body}}
          ],
          "actions": [ { "type": "Action.Submit", "title": "Run" } ]
        }
        """;
    }

    public override CommandResult SubmitForm(string inputs)
    {
        var formInput = JsonNode.Parse(inputs)?.AsObject();
        if (formInput is null)
        {
            return Error("Invalid form input.");
        }

        string entityId = string.Empty;
        if (_hasTarget)
        {
            entityId = formInput[EntityIdField]?.ToString() ?? string.Empty;
            if (entityId.Length == 0)
            {
                return Error("Enter an entity_id.");
            }
        }

        if (!ActionFormFields.TryBuildData(_fields, formInput, out var data, out var validationError))
        {
            return Error(validationError);
        }

        if (_client.TryCallService(_domain, _service, entityId, data, out var error))
        {
            _onSuccess();
            return CommandResult.GoBack();
        }

        return Error("Failed: " + error);
    }

    private static CommandResult Error(string message)
        => CommandResult.ShowToast(new ToastArgs
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
}
