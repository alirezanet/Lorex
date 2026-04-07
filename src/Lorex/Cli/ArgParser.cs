namespace Lorex.Cli;

/// <summary>Shared flag-value parsing utilities used by CLI commands.</summary>
internal static class ArgParser
{
    /// <summary>Returns the string value following <paramref name="flag"/>, or <c>null</c> if not found.</summary>
    internal static string? FlagValue(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    /// <summary>Returns the integer value following <paramref name="flag"/>, or <paramref name="defaultValue"/> if absent or non-numeric.</summary>
    internal static int IntFlagValue(string[] args, string flag, int defaultValue)
    {
        var raw = FlagValue(args, flag);
        return raw != null && int.TryParse(raw, out var n) && n >= 0 ? n : defaultValue;
    }
}
