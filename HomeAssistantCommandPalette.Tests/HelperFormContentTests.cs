using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Forms;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

public class HelperFormContentTests
{
    [Fact]
    public void Input_text_form_calls_set_value()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("input_text.note", "old");
        var form = new InputTextFormContent(entity, client, () => { });

        form.SubmitForm("{\"value\":\"hello\"}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("input_text", call.Domain);
        Assert.Equal("set_value", call.Service);
        Assert.Equal("input_text.note", call.EntityId);
        Assert.Equal("hello", call.ExtraData!["value"]);
    }

    [Fact]
    public void Input_number_form_calls_set_value_with_number()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("input_number.target", "1");
        var form = new InputNumberFormContent(entity, client, () => { });

        form.SubmitForm("{\"value\":42.5}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("input_number", call.Domain);
        Assert.Equal("set_value", call.Service);
        Assert.Equal(42.5, Assert.IsType<double>(call.ExtraData!["value"]));
    }

    [Fact]
    public void Input_select_form_calls_select_option()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("input_select.mode", "auto", attributes: new Dictionary<string, object?>
        {
            ["options"] = new List<object?> { "auto", "manual", "off" },
        });
        var form = new InputSelectFormContent(entity, client, () => { });

        form.SubmitForm("{\"option\":\"manual\"}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("input_select", call.Domain);
        Assert.Equal("select_option", call.Service);
        Assert.Equal("input_select.mode", call.EntityId);
        Assert.Equal("manual", call.ExtraData!["option"]);
    }

    [Fact]
    public void Script_form_calls_object_id_service_with_typed_fields()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("script.goodnight", "off");
        var fields = new Dictionary<string, object?>
        {
            ["temperature"] = new Dictionary<string, object?>
            {
                ["name"] = "Temperature",
                ["required"] = true,
                ["selector"] = new Dictionary<string, object?>
                {
                    ["number"] = new Dictionary<string, object?> { ["min"] = 15.0, ["max"] = 30.0 },
                },
            },
            ["enable"] = new Dictionary<string, object?>
            {
                ["selector"] = new Dictionary<string, object?> { ["boolean"] = new Dictionary<string, object?>() },
            },
            ["note"] = new Dictionary<string, object?>(),
        };
        var form = new ScriptFormContent(entity, client, () => { }, fields);

        form.SubmitForm("{\"temperature\":\"22.5\",\"enable\":\"true\",\"note\":\"hello\"}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("script", call.Domain);
        Assert.Equal("goodnight", call.Service);
        Assert.Equal("script.goodnight", call.EntityId);
        Assert.Equal(22.5, Assert.IsType<double>(call.ExtraData!["temperature"]));
        Assert.True(Assert.IsType<bool>(call.ExtraData!["enable"]));
        Assert.Equal("hello", call.ExtraData!["note"]);
    }

    [Fact]
    public void Action_form_calls_target_service_with_entity_id_and_fields()
    {
        var client = new RecordingHaClient();
        var fields = new Dictionary<string, object?>
        {
            ["brightness"] = new Dictionary<string, object?>
            {
                ["name"] = "Brightness",
                ["selector"] = new Dictionary<string, object?>
                {
                    ["number"] = new Dictionary<string, object?> { ["min"] = 0.0, ["max"] = 255.0 },
                },
            },
        };
        var form = new ActionFormContent(client, () => { }, "light", "turn_on", fields, hasTarget: true);

        form.SubmitForm("{\"__entity_id\":\"light.kitchen\",\"brightness\":\"200\"}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("light", call.Domain);
        Assert.Equal("turn_on", call.Service);
        Assert.Equal("light.kitchen", call.EntityId);
        Assert.Equal(200.0, Assert.IsType<double>(call.ExtraData!["brightness"]));
    }

    [Fact]
    public void Action_form_without_target_omits_entity_id()
    {
        var client = new RecordingHaClient();
        var form = new ActionFormContent(client, () => { }, "homeassistant", "restart", new Dictionary<string, object?>(), hasTarget: false);

        form.SubmitForm("{}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("homeassistant", call.Domain);
        Assert.Equal("restart", call.Service);
        Assert.Equal(string.Empty, call.EntityId);
    }

    [Fact]
    public void Action_form_requires_entity_id_when_target_set()
    {
        var client = new RecordingHaClient();
        var form = new ActionFormContent(client, () => { }, "light", "turn_off", new Dictionary<string, object?>(), hasTarget: true);

        form.SubmitForm("{\"__entity_id\":\"\"}");

        Assert.Empty(client.Calls);
    }

    [Fact]
    public void Script_form_rejects_missing_required_field()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("script.example", "off");
        var fields = new Dictionary<string, object?>
        {
            ["name"] = new Dictionary<string, object?> { ["required"] = true },
        };
        var form = new ScriptFormContent(entity, client, () => { }, fields);

        form.SubmitForm("{\"name\":\"\"}");

        Assert.Empty(client.Calls);
    }

    [Fact]
    public void Input_datetime_form_calls_set_datetime_with_date_and_time()
    {
        var client = new RecordingHaClient();
        var entity = TestEntities.Make("input_datetime.alarm", "2026-01-02 03:04:00", attributes: new Dictionary<string, object?>
        {
            ["has_date"] = true,
            ["has_time"] = true,
        });
        var form = new InputDateTimeFormContent(entity, client, () => { });

        form.SubmitForm("{\"date\":\"2026-05-11\",\"time\":\"12:34\"}");

        var call = Assert.Single(client.Calls);
        Assert.Equal("input_datetime", call.Domain);
        Assert.Equal("set_datetime", call.Service);
        Assert.Equal("2026-05-11", call.ExtraData!["date"]);
        Assert.Equal("12:34:00", call.ExtraData!["time"]);
    }
}
