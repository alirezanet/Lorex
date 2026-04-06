namespace Lorex.Core.Services;

/// <summary>
/// Resolves the lorex project root from the current working directory or one of its ancestors.
/// </summary>
internal static class ProjectRootLocator
{
    private const string LorexConfigRelativePath = ".lorex/lorex.json";

    internal static string ResolveForExistingProject(string startDirectory)
    {
        var root = FindNearestInitializedRoot(startDirectory);
        if (root is not null)
            return root;

        throw new FileNotFoundException(
            "lorex is not initialised in this directory. Run `lorex init` first.",
            Path.Combine(Path.GetFullPath(startDirectory), ".lorex", "lorex.json"));
    }

    internal static string ResolveForInit(string startDirectory)
    {
        var root = FindNearestInitializedRoot(startDirectory);
        if (root is not null && !IsGlobalRoot(root))
            return root;

        return Path.GetFullPath(startDirectory);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> is the user's home directory,
    /// which is the project-root equivalent for global lorex operations.
    /// <c>lorex init</c> must not re-enter an existing global install when invoked inside a new
    /// project that lives under the home directory.
    /// </summary>
    private static bool IsGlobalRoot(string path)
    {
        var home = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return candidate.Equals(home, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? FindNearestInitializedRoot(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            var configPath = Path.Combine(current.FullName, ".lorex", "lorex.json");
            if (File.Exists(configPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
