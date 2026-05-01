using System;
using System.Linq;
using System.Reflection;

namespace HomeAssistantCommandPalette;

internal static class AppReleaseInfo
{
    private static readonly Assembly Assembly = typeof(AppReleaseInfo).Assembly;

    public static string Version => Assembly.GetName().Version?.ToString() ?? "Unknown";

    public static string InformationalVersion =>
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Version;

    public static string? CommitHash => Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => string.Equals(a.Key, "CommitHash", StringComparison.Ordinal))?
        .Value;

    public static string DisplayVersion => string.IsNullOrWhiteSpace(CommitHash)
        ? Version
        : $"{Version} ({ShortCommitHash})";

    public static string ShortCommitHash => string.IsNullOrWhiteSpace(CommitHash)
        ? "Unknown"
        : CommitHash![..Math.Min(7, CommitHash.Length)];
}
