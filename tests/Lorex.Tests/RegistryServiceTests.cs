using System.Diagnostics;
using System.Text.Json;
using Lorex.Core.Models;
using Lorex.Core.Services;

namespace Lorex.Tests;

public sealed class RegistryServiceTests
{
    [Fact]
    public void InitializeRegistryPolicy_BootstrapsEmptyBareRegistry()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-registry-{Guid.NewGuid():N}");
        var remotePath = Path.Combine(root, "remote.git");
        var clonePath = Path.Combine(root, "clone");
        var service = new RegistryService(new GitService());
        var policy = new RegistryPolicy
        {
            PublishMode = RegistryPublishModes.PullRequest,
            BaseBranch = "main",
            PrBranchPrefix = "lorex/",
        };

        var previousAuthorName = Environment.GetEnvironmentVariable("GIT_AUTHOR_NAME");
        var previousAuthorEmail = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");
        var previousCommitterName = Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME");
        var previousCommitterEmail = Environment.GetEnvironmentVariable("GIT_COMMITTER_EMAIL");

        try
        {
            Directory.CreateDirectory(root);
            RunGit(root, "init", "--bare", remotePath);

            Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", "Lorex Tests");
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", "lorex-tests@example.com");
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Lorex Tests");
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", "lorex-tests@example.com");

            var initialized = service.InitializeRegistryPolicy(remotePath, policy);

            Assert.Equal(policy, initialized);

            RunGit(root, "clone", "-b", policy.BaseBranch, remotePath, clonePath);

            var manifestPath = Path.Combine(clonePath, RegistryService.RegistryManifestFileName);
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(policy.PublishMode, document.RootElement.GetProperty("publishMode").GetString());
            Assert.Equal(policy.BaseBranch, document.RootElement.GetProperty("baseBranch").GetString());
            Assert.Equal(policy.PrBranchPrefix, document.RootElement.GetProperty("prBranchPrefix").GetString());

            var currentBranch = RunGit(clonePath, "branch", "--show-current").Trim();
            Assert.Equal(policy.BaseBranch, currentBranch);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", previousAuthorName);
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", previousAuthorEmail);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", previousCommitterName);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", previousCommitterEmail);

            TryDeleteDirectory(service.GetCachePath(remotePath));
            TryDeleteDirectory(service.GetWorktreeRoot(remotePath));
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void InitializeRegistryPolicy_ForceAddsManifestWhenRegistryIgnoresJsonFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"lorex-registry-{Guid.NewGuid():N}");
        var remotePath = Path.Combine(root, "remote.git");
        var seedPath = Path.Combine(root, "seed");
        var clonePath = Path.Combine(root, "clone");
        var service = new RegistryService(new GitService());
        var policy = new RegistryPolicy
        {
            PublishMode = RegistryPublishModes.PullRequest,
            BaseBranch = "main",
            PrBranchPrefix = "lorex/",
        };

        var previousAuthorName = Environment.GetEnvironmentVariable("GIT_AUTHOR_NAME");
        var previousAuthorEmail = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");
        var previousCommitterName = Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME");
        var previousCommitterEmail = Environment.GetEnvironmentVariable("GIT_COMMITTER_EMAIL");

        try
        {
            Directory.CreateDirectory(root);
            RunGit(root, "init", "--bare", remotePath);

            Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", "Lorex Tests");
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", "lorex-tests@example.com");
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Lorex Tests");
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", "lorex-tests@example.com");

            RunGit(root, "clone", remotePath, seedPath);
            RunGit(seedPath, "checkout", "-b", policy.BaseBranch);
            File.WriteAllText(Path.Combine(seedPath, ".gitignore"), "*.json\n");
            File.WriteAllText(Path.Combine(seedPath, "README.md"), "# Seed\n");
            RunGit(seedPath, "add", ".gitignore", "README.md");
            RunGit(seedPath, "commit", "-m", "seed registry");
            RunGit(seedPath, "push", "-u", "origin", policy.BaseBranch);
            RunGit(root, "--git-dir", remotePath, "symbolic-ref", "HEAD", $"refs/heads/{policy.BaseBranch}");

            var initialized = service.InitializeRegistryPolicy(remotePath, policy);

            Assert.Equal(policy, initialized);

            RunGit(root, "clone", "-b", policy.BaseBranch, remotePath, clonePath);

            var manifestPath = Path.Combine(clonePath, RegistryService.RegistryManifestFileName);
            Assert.True(File.Exists(manifestPath));

            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.Equal(policy.PublishMode, document.RootElement.GetProperty("publishMode").GetString());
            Assert.Equal(policy.BaseBranch, document.RootElement.GetProperty("baseBranch").GetString());
            Assert.Equal(policy.PrBranchPrefix, document.RootElement.GetProperty("prBranchPrefix").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", previousAuthorName);
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", previousAuthorEmail);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", previousCommitterName);
            Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", previousCommitterEmail);

            TryDeleteDirectory(service.GetCachePath(remotePath));
            TryDeleteDirectory(service.GetWorktreeRoot(remotePath));
            TryDeleteDirectory(root);
        }
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git process for test.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed (exit {process.ExitCode}): {stderr}{stdout}");

        return stdout;
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temp repos and caches created by the test.
        }
    }
}
