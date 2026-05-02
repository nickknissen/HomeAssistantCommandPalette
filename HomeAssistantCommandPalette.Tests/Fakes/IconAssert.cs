using Microsoft.CommandPalette.Extensions.Toolkit;
using Xunit;

namespace HomeAssistantCommandPalette.Tests.Fakes;

/// <summary>
/// Helpers for asserting <see cref="IconInfo"/> identity by underlying
/// path. Reference equality doesn't work because <c>Icons.X</c>
/// properties build a fresh <see cref="IconInfo"/> on every access via
/// <see cref="IconHelpers.FromRelativePath(string)"/>, but the resulting
/// <see cref="IconInfo.Light"/> / <see cref="IconInfo.Dark"/>
/// <see cref="IconData.Icon"/> string paths are stable.
/// </summary>
internal static class IconAssert
{
    /// <summary>
    /// Asserts <paramref name="actual"/> resolves to the same icon path
    /// (Light + Dark) as <paramref name="expected"/>.
    /// </summary>
    public static void Same(IconInfo expected, IconInfo actual)
    {
        Assert.Equal(expected.Light.Icon, actual.Light.Icon);
        Assert.Equal(expected.Dark.Icon, actual.Dark.Icon);
    }

    public static string PathOf(IconInfo i) => i.Light.Icon ?? string.Empty;
}
