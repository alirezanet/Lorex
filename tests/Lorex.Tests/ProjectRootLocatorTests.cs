using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class ProjectRootLocatorTests
{
    [Fact]
    public void ResolveForExistingProject_FindsNearestAncestorWithLorexConfig()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-root-{Guid.NewGuid():N}");
        var nested = Path.Combine(root, "src", "Feature", "Deep");

        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".lorex"));
            File.WriteAllText(Path.Combine(root, ".lorex", "lorex.json"), "{}");
            Directory.CreateDirectory(nested);

            var resolved = ProjectRootLocator.ResolveForExistingProject(nested);

            Assert.Equal(root, resolved);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveForInit_UsesCurrentDirectoryWhenNoInitializedProjectExists()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-root-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(root);

            var resolved = ProjectRootLocator.ResolveForInit(root);

            Assert.Equal(root, resolved);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveForInit_DoesNotResolveToGlobalRootWhenProjectHasNoLorexConfig()
    {
        // Regression test: when global lorex is initialized (~/.lorex/lorex.json exists),
        // running `lorex init` in a new project directory must resolve to the project
        // directory — not to ~ — even though FindNearestInitializedRoot walks up and
        // finds ~/.lorex/lorex.json.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var globalConfig = Path.Combine(home, ".lorex", "lorex.json");

        // Only meaningful when the global config exists; skip otherwise.
        if (!File.Exists(globalConfig))
            return;

        var root = Path.Combine(Path.GetTempPath(), $"lorex-root-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(root);

            var resolved = ProjectRootLocator.ResolveForInit(root);

            Assert.Equal(root, resolved);
            Assert.NotEqual(home, resolved, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
