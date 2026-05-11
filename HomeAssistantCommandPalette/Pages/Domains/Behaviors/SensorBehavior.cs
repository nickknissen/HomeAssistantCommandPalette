using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class SensorBehavior : DomainBehavior
{
    private const int SparklineBuckets = 30;
    private const string Blocks = "▁▂▃▄▅▆▇█";

    public override string Domain => "sensor";

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
            rows.Add(DomainHelpers.Row("Device class", dcs));
        if (entity.Attributes.TryGetValue("state_class", out var sc) && sc is string scs && !string.IsNullOrEmpty(scs))
            rows.Add(DomainHelpers.Row("State class", scs));

        if (TryBuildTrendRow(in ctx, out var trend))
        {
            rows.Add(DomainHelpers.Row("Trend (24h)", trend));
        }
    }

    private static bool TryBuildTrendRow(in DomainCtx ctx, out string trend)
    {
        trend = string.Empty;
        var entity = ctx.Entity;

        // Only numeric sensors should touch the history endpoint. HA uses
        // string states for everything, so parse the current state first to
        // avoid rows/fetches for text sensors such as sensor.next_alarm.
        if (!double.TryParse(entity.State, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        var history = ctx.Client.GetHistory(entity.EntityId, DateTimeOffset.UtcNow.AddHours(-24));
        if (history.Count < 2) return false;

        var samples = Downsample(history, SparklineBuckets);
        if (samples.Length < 2) return false;

        var min = samples.Min();
        var max = samples.Max();
        var sparkline = BuildSparkline(samples, min, max);
        var unit = entity.Attributes.TryGetValue("unit_of_measurement", out var u) && u is string us
            ? us
            : string.Empty;

        trend = $"{sparkline} {FormatValue(history[0].Value)} → {FormatValue(history[^1].Value)}{unit}";
        return true;
    }

    internal static string BuildSparkline(IReadOnlyList<double> values, double min, double max)
    {
        if (values.Count == 0) return string.Empty;
        if (Math.Abs(max - min) < double.Epsilon)
        {
            return new string(Blocks[Blocks.Length / 2], values.Count);
        }

        var chars = new char[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var normalized = Math.Clamp((values[i] - min) / (max - min), 0, 1);
            var idx = (int)Math.Floor(normalized * (Blocks.Length - 1));
            chars[i] = Blocks[idx];
        }
        return new string(chars);
    }

    private static double[] Downsample(IReadOnlyList<HaHistoryPoint> history, int bucketCount)
    {
        if (history.Count <= bucketCount)
        {
            return history.Select(p => p.Value).ToArray();
        }

        var values = new double[bucketCount];
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var start = bucket * history.Count / bucketCount;
            var end = (bucket + 1) * history.Count / bucketCount;
            var sum = 0d;
            for (var i = start; i < end; i++) sum += history[i].Value;
            values[bucket] = sum / (end - start);
        }
        return values;
    }

    private static string FormatValue(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}
