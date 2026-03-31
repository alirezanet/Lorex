using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class SymlinkTargetTests
{
    [Fact]
    public void GetSymlinkTarget_UsesRelativePathWithinProjectRoot()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        var projectRoot = Path.Combine(baseDir, "repo");
        var linkPath = Path.Combine(projectRoot, ".cline", "skills", "lorex", "SKILL.md");
        var targetPath = Path.Combine(projectRoot, ".lorex", "skills", "lorex", "SKILL.md");

        var target = AdapterService.GetSymlinkTarget(projectRoot, linkPath, targetPath);

        var expected = Path.Combine("..", "..", "..", ".lorex", "skills", "lorex", "SKILL.md");
        Assert.Equal(expected, target);
    }

    [Fact]
    public void GetSymlinkTarget_UsesAbsolutePathOutsideProjectRoot()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");
        var projectRoot = Path.Combine(baseDir, "repo");
        var linkPath = Path.Combine(projectRoot, ".lorex", "skills", "shared");
        var targetPath = Path.Combine(baseDir, "registry", "skills", "shared");

        var target = AdapterService.GetSymlinkTarget(projectRoot, linkPath, targetPath);

        Assert.Equal(Path.GetFullPath(targetPath), target);
    }
}
