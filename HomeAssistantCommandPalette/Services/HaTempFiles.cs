using System;
using System.IO;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Transport-agnostic helpers for the temp files the extension caches to
/// disk (camera snapshots, entity pictures). Lives outside any concrete
/// <see cref="IHaClient"/> implementation so the provider can sweep stale
/// files at startup regardless of which transport is active.
/// </summary>
internal static class HaTempFiles
{
    /// <summary>
    /// Sweeps temp snapshot / entity-picture files older than one hour.
    /// Called once at extension startup — the in-memory cache resets on
    /// restart, so any file from a previous session is unreachable. The
    /// 1 h threshold leaves a safety margin if a second CmdPal process
    /// is racing the cleanup. Best-effort: a failure is silent.
    /// </summary>
    public static void CleanupStaleSnapshots()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
        foreach (var sub in new[] { "camera", "picture" })
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "HomeAssistantCommandPalette", sub);
                if (!Directory.Exists(dir)) continue;
                foreach (var path in Directory.EnumerateFiles(dir))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff)
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                        // File in use, locked, or vanished — skip it.
                    }
                }
            }
            catch
            {
                // Permission or I/O error reading the dir — skip the sweep.
            }
        }
    }
}
