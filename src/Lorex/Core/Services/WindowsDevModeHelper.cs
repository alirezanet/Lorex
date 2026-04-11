using System.Diagnostics;

namespace Lorex.Core.Services;

/// <summary>
/// Detects whether Windows Developer Mode is enabled (required for unprivileged symlink creation)
/// and provides a junction fallback for environments where symlinks are unavailable.
/// </summary>
internal static class WindowsDevModeHelper
{
    private static bool? _cached;

    /// <summary>
    /// Returns true when symlinks are available without elevation:
    ///   - Always true on non-Windows platforms
    ///   - True when Windows Developer Mode is enabled
    ///   - True when running as Administrator (elevation grants SeCreateSymbolicLinkPrivilege)
    /// </summary>
    public static bool IsSymlinkAvailable()
    {
        if (!OperatingSystem.IsWindows()) return true;
        if (_cached.HasValue) return _cached.Value;

        _cached = IsDevModeEnabled() || IsElevated();
        return _cached.Value;
    }

    /// <summary>
    /// Creates a directory junction at <paramref name="linkPath"/> pointing to
    /// <paramref name="targetPath"/>. Junctions require an absolute target path and
    /// work on any Windows account without Developer Mode or administrator elevation.
    /// Returns false on any failure.
    /// </summary>
    public static bool TryCreateJunction(string linkPath, string targetPath)
    {
        if (!OperatingSystem.IsWindows()) return false;

        // Junctions require an absolute target path.
        var absoluteTarget = Path.GetFullPath(targetPath);

        // Strip any trailing separator — "path\" confuses cmd.exe argument parsing.
        absoluteTarget = absoluteTarget.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // mklink is a cmd.exe built-in, not a standalone executable.
        var psi = new ProcessStartInfo("cmd.exe",
            $"/c mklink /J \"{linkPath}\" \"{absoluteTarget}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            proc.WaitForExit();
            return proc.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }

    // ── internals ─────────────────────────────────────────────────────────────

    private static bool IsDevModeEnabled()
    {
        // Query the Windows registry via reg.exe — keeps the binary AOT-safe (no reflection).
        try
        {
            var psi = new ProcessStartInfo(
                "reg",
                @"query HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock /v AllowDevelopmentWithoutDevLicense")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output.Contains("0x1", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsElevated()
    {
        try
        {
            // A quick probe: try to create a symlink in the temp directory.
            var test = Path.Combine(Path.GetTempPath(), $"lorex-symtest-{Guid.NewGuid():N}");
            var target = Path.GetTempPath();
            Directory.CreateSymbolicLink(test, target);
            Directory.Delete(test);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
