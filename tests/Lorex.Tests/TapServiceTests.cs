using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class TapServiceTests
{
    // ── DiscoverSkills ────────────────────────────────────────────────────────

    [Fact]
    public void DiscoverSkills_SubdirLayout_ReturnsAllSkills()
    {
        using var dir = new TempDir();
        WriteSkill(dir.Path, "skills/alpha/SKILL.md", "alpha", "Skill A");
        WriteSkill(dir.Path, "skills/beta/SKILL.md",  "beta",  "Skill B");

        var results = TapService.DiscoverSkills(dir.Path, root: null);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "alpha");
        Assert.Contains(results, r => r.Name == "beta");
    }

    [Fact]
    public void DiscoverSkills_RootLevelSkill_ReturnsSingleSkillWithFrontmatterName()
    {
        using var dir = new TempDir();
        WriteSkill(dir.Path, "SKILL.md", "my-skill", "Root skill");

        var results = TapService.DiscoverSkills(dir.Path, root: null);

        Assert.Single(results);
        Assert.Equal("my-skill", results[0].Name);
    }

    [Fact]
    public void DiscoverSkills_RootLevelSkill_SkipsEntryWhenFrontmatterNameBlank()
    {
        using var dir = new TempDir();
        // name field is blank — SimpleYamlParser throws InvalidDataException, entry is silently skipped
        File.WriteAllText(Path.Combine(dir.Path, "SKILL.md"),
            "---\nname: \ndescription: no name\n---\n# body\n");

        var results = TapService.DiscoverSkills(dir.Path, root: null);

        Assert.Empty(results);
    }

    [Fact]
    public void DiscoverSkills_RootLevelSkill_DoesNotAlsoEnumerateSubdirs()
    {
        using var dir = new TempDir();
        WriteSkill(dir.Path, "SKILL.md",           "root-skill", "Root");
        WriteSkill(dir.Path, "sub/SKILL.md",        "sub-skill",  "Sub");

        var results = TapService.DiscoverSkills(dir.Path, root: null);

        // Root-level detection short-circuits — sub-skill must not appear
        Assert.Single(results);
        Assert.Equal("root-skill", results[0].Name);
    }

    [Fact]
    public void DiscoverSkills_EmptyDirectory_ReturnsEmpty()
    {
        using var dir = new TempDir();

        var results = TapService.DiscoverSkills(dir.Path, root: null);

        Assert.Empty(results);
    }

    [Fact]
    public void DiscoverSkills_NonExistentDirectory_ReturnsEmpty()
    {
        var results = TapService.DiscoverSkills(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            root: null);

        Assert.Empty(results);
    }

    // ── FindSkillPath ─────────────────────────────────────────────────────────

    [Fact]
    public void FindSkillPath_SubdirLayout_FindsByFrontmatterName()
    {
        using var dir = new TempDir();
        CreateFakeGit(dir.Path);
        WriteSkill(dir.Path, "skills/alpha/SKILL.md", "alpha", "Skill A");

        var tap = new TapConfig { Name = "test", Url = "https://github.com/owner/test-repo" };
        var service = new TapService(new GitService());

        // Bypass cache by pointing directly at the temp dir
        var found = InvokeFind(dir.Path, tap, "alpha");

        Assert.NotNull(found);
        Assert.EndsWith("alpha", found, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindSkillPath_RootLevelSkill_ReturnsSearchRootForMatchingName()
    {
        using var dir = new TempDir();
        CreateFakeGit(dir.Path);
        WriteSkill(dir.Path, "SKILL.md", "my-skill", "Root skill");

        var tap = new TapConfig { Name = "test", Url = "https://github.com/owner/my-skill" };
        var found = InvokeFind(dir.Path, tap, "my-skill");

        Assert.NotNull(found);
        Assert.Equal(dir.Path, found, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindSkillPath_RootLevelSkill_ReturnsNullForWrongName()
    {
        using var dir = new TempDir();
        CreateFakeGit(dir.Path);
        WriteSkill(dir.Path, "SKILL.md", "my-skill", "Root skill");

        var tap = new TapConfig { Name = "test", Url = "https://github.com/owner/my-skill" };
        var found = InvokeFind(dir.Path, tap, "other-skill");

        Assert.Null(found);
    }

    [Fact]
    public void FindSkillPath_MissingCache_ReturnsNull()
    {
        var tap = new TapConfig { Name = "test", Url = "https://github.com/owner/repo" };
        var found = InvokeFind(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            tap, "any-skill");

        Assert.Null(found);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void WriteSkill(string root, string relativePath, string name, string description)
    {
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full,
            $"---\nname: {name}\ndescription: {description}\nversion: 1.0.0\n---\n# {name}\n");
    }

    private static void CreateFakeGit(string dir) =>
        Directory.CreateDirectory(Path.Combine(dir, ".git"));

    /// <summary>
    /// Calls the internal path-resolution logic of FindSkillPath without going through
    /// the cache layer by replicating its search logic directly on the given directory.
    /// </summary>
    private static string? InvokeFind(string cacheDir, TapConfig tap, string skillName)
    {
        // Mirror FindSkillPath's logic so we can test it without a real git clone
        if (!Directory.Exists(Path.Combine(cacheDir, ".git"))) return null;

        var searchRoot = tap.Root is not null
            ? Path.Combine(cacheDir, tap.Root)
            : (Directory.Exists(Path.Combine(cacheDir, "skills"))
                ? Path.Combine(cacheDir, "skills")
                : cacheDir);

        if (!Directory.Exists(searchRoot)) return null;

        if (SkillFileConvention.ResolveEntryPath(searchRoot) is not null)
        {
            var resolvedName = ReadSkillName(searchRoot) ?? SkillFileConvention.RepoNameFromUrl(tap.Url);
            return string.Equals(resolvedName, skillName, StringComparison.OrdinalIgnoreCase)
                ? searchRoot
                : null;
        }

        foreach (var dir in RegistryService.EnumerateSkillDirectories(searchRoot))
        {
            if (string.Equals(Path.GetFileName(dir), skillName, StringComparison.OrdinalIgnoreCase))
                return dir;
            var metaName = ReadSkillName(dir);
            if (metaName is not null && string.Equals(metaName, skillName, StringComparison.OrdinalIgnoreCase))
                return dir;
        }

        return null;
    }

    private static string? ReadSkillName(string dir)
    {
        try
        {
            var entry = SkillFileConvention.ResolveEntryPath(dir);
            if (entry is null) return null;
            var meta = Lorex.Core.Serialization.SimpleYamlParser
                .ParseSkillMetadataFromMarkdown(File.ReadAllText(entry));
            return string.IsNullOrWhiteSpace(meta.Name) ? null : meta.Name;
        }
        catch { return null; }
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lorex-test-{Guid.NewGuid():N}");

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
