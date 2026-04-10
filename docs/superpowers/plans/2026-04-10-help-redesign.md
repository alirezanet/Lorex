# Help Command Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `--help` to all 11 lorex commands using a shared `HelpPrinter` helper, and redesign the global `lorex --help` layout.

**Architecture:** A new `Cli/HelpPrinter.cs` takes structured data (usage, description, options, examples, subcommands) and renders consistent output via Spectre.Console. Each command's `PrintHelp()` calls it. The global help in `Program.cs` gets a flat COMMANDS + FLAGS layout, dropping the old 3-column grid.

**Tech Stack:** .NET 10, Spectre.Console, xUnit

**Spec:** `docs/superpowers/specs/2026-04-10-help-redesign-design.md`

**Rule:** Fix edge cases and missing details inline as you find them — don't block on them.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/Lorex/Cli/HelpPrinter.cs` | Renders USAGE/DESCRIPTION/OPTIONS/EXAMPLES/SUBCOMMANDS |
| Modify | `src/Lorex/Program.cs` | Redesigned global `PrintHelp()` |
| Modify | `src/Lorex/Commands/InitCommand.cs` | Refactor existing `PrintHelp()` to use `HelpPrinter` |
| Modify | `src/Lorex/Commands/RegistryCommand.cs` | Refactor existing `PrintHelp()` to use `HelpPrinter` |
| Modify | `src/Lorex/Commands/TapCommand.cs` | Refactor existing `PrintHelp()` to use `HelpPrinter` |
| Modify | `src/Lorex/Commands/InstallCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/UninstallCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/CreateCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/PublishCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/ListCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/StatusCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/SyncCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `src/Lorex/Commands/RefreshCommand.cs` | Add `--help` check + `PrintHelp()` |
| Modify | `tests/Lorex.Tests/CommandArgumentTests.cs` | Add `--help` exit-code-0 tests for all commands |
| Modify | `docs/reference/commands.md` | Note `--help` available on all commands |

---

## Task 1: Create `HelpPrinter`

**Files:**
- Create: `src/Lorex/Cli/HelpPrinter.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to the bottom of `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void HelpPrinter_Print_DoesNotThrowWithMinimalArgs()
{
    // Should not throw when only required args are provided
    var result = HelpPrinter.Print("lorex test [args]", "Test description.");
    Assert.Equal(0, result);
}

[Fact]
public void HelpPrinter_Print_DoesNotThrowWithAllSections()
{
    var result = HelpPrinter.Print(
        "lorex test [args]",
        "First line.\nSecond line.",
        options:
        [
            ("--flag", "A flag"),
            ("-h, --help", "Show help"),
        ],
        examples:
        [
            ("With a comment", "lorex test --flag"),
            ("", "lorex test"),
        ],
        subcommands:
        [
            ("sub <arg> [-g]", "A subcommand"),
        ]
    );
    Assert.Equal(0, result);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Lorex.Tests --filter "HelpPrinter_Print" -v minimal
```

Expected: FAIL — `HelpPrinter` does not exist yet.

- [ ] **Step 3: Create `src/Lorex/Cli/HelpPrinter.cs`**

```csharp
using Spectre.Console;

namespace Lorex.Cli;

/// <summary>
/// Renders consistent USAGE / DESCRIPTION / OPTIONS / EXAMPLES help output for lorex commands.
/// </summary>
internal static class HelpPrinter
{
    /// <summary>Prints a formatted help page and returns 0 for use as a command return value.</summary>
    /// <param name="usage">Usage synopsis, e.g. <c>lorex install [&lt;skill&gt;…] [--all] [-g]</c>.</param>
    /// <param name="description">One or two sentences. Use <c>\n</c> for line breaks.</param>
    /// <param name="options">Option rows shown in an OPTIONS section. Null = section omitted.</param>
    /// <param name="examples">Example rows. Empty <c>Comment</c> = no comment line printed. Null = section omitted.</param>
    /// <param name="subcommands">Subcommand rows shown in a SUBCOMMANDS section before OPTIONS. Null = section omitted.</param>
    public static int Print(
        string usage,
        string description,
        (string Flags, string Description)[]? options = null,
        (string Comment, string Command)[]? examples = null,
        (string Signature, string Description)[]? subcommands = null)
    {
        AnsiConsole.MarkupLine("[bold]USAGE[/]");
        AnsiConsole.MarkupLine("  {0}", Markup.Escape(usage));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[bold]DESCRIPTION[/]");
        foreach (var line in description.Split('\n'))
            AnsiConsole.MarkupLine("  {0}", Markup.Escape(line.TrimEnd()));
        AnsiConsole.WriteLine();

        if (subcommands is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]SUBCOMMANDS[/]");
            var subWidth = Math.Max(subcommands.Max(s => s.Signature.Length) + 4, 20);
            var subGrid = new Grid()
                .AddColumn(new GridColumn().Width(subWidth))
                .AddColumn();
            foreach (var (sig, desc) in subcommands)
                subGrid.AddRow(
                    $"  [bold deepskyblue3]{Markup.Escape(sig)}[/]",
                    $"[dim]{Markup.Escape(desc)}[/]");
            AnsiConsole.Write(subGrid);
            AnsiConsole.WriteLine();
        }

        if (options is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]OPTIONS[/]");
            var optWidth = Math.Max(options.Max(o => o.Flags.Length) + 4, 24);
            var optGrid = new Grid()
                .AddColumn(new GridColumn().Width(optWidth))
                .AddColumn();
            foreach (var (flags, desc) in options)
                optGrid.AddRow(
                    $"  [bold]{Markup.Escape(flags)}[/]",
                    $"[dim]{Markup.Escape(desc)}[/]");
            AnsiConsole.Write(optGrid);
            AnsiConsole.WriteLine();
        }

        if (examples is { Length: > 0 })
        {
            AnsiConsole.MarkupLine("[bold]EXAMPLES[/]");
            foreach (var (comment, command) in examples)
            {
                if (!string.IsNullOrEmpty(comment))
                    AnsiConsole.MarkupLine("  [dim]# {0}[/]", Markup.Escape(comment));
                AnsiConsole.MarkupLine("  {0}", Markup.Escape(command));
            }
            AnsiConsole.WriteLine();
        }

        return 0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Lorex.Tests --filter "HelpPrinter_Print" -v minimal
```

Expected: PASS (2 tests).

- [ ] **Step 5: Build to confirm no errors**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Lorex/Cli/HelpPrinter.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): add HelpPrinter helper"
```

---

## Task 2: Redesign global help (`Program.cs`)

**Files:**
- Modify: `src/Lorex/Program.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void Program_HelpFlag_ReturnsZero()
{
    // lorex --help should always return 0 — tested via the static method directly
    // since Program.cs uses top-level statements we route through the dispatch table
    var result = InstallCommand.Run(["--help"]);  // placeholder — will pass after Task 4
    // The global help is verified manually; this test is a smoke-check via --version
    Assert.Equal(0, result);
}
```

> Note: `Program.cs` uses top-level statements and can't be called directly from tests.
> The global `PrintHelp()` is effectively tested by running `lorex --help` manually.
> We'll add a smoke-check test that at least verifies the dispatch table returns 0 for `--help`.
> Replace the body above with:

```csharp
[Fact]
public void InitCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, InitCommand.Run(["--help"]));
    Assert.Equal(0, InitCommand.Run(["-h"]));
}
```

> (This test may already pass since `InitCommand` has `--help`. That's fine — it documents the contract.)

- [ ] **Step 2: Run test to verify current state**

```bash
dotnet test tests/Lorex.Tests --filter "InitCommand_HelpFlag_ReturnsZero" -v minimal
```

Expected: PASS (since `InitCommand` already handles `--help`).

- [ ] **Step 3: Redesign `PrintHelp()` in `src/Lorex/Program.cs`**

Replace the entire `PrintHelp()` static method (lines 64–120). Keep `GetVersion()`, `PrintVersion()`, and `UnknownCommand()` unchanged.

```csharp
static int PrintHelp()
{
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new FigletText("lorex").Color(Color.Blue));
    AnsiConsole.MarkupLine($"[dim]v{GetVersion()} — Teach your AI agents once. Reuse everywhere.[/]");
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]USAGE[/]  lorex [dim]<command> [[args]][/]");
    AnsiConsole.WriteLine();

    AnsiConsole.Write(new Rule("[bold]COMMANDS[/]").LeftJustified().RuleStyle("blue dim"));
    var commandsGrid = new Grid()
        .AddColumn(new GridColumn().Width(12))
        .AddColumn();
    commandsGrid.AddRow("  [bold deepskyblue3]init[/]",      "[dim]Configure lorex for this project or globally[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]install[/]",   "[dim]Install skills from the registry, taps, or a URL[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]uninstall[/]", "[dim]Remove installed skills[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]create[/]",    "[dim]Scaffold a new skill for authoring[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]list[/]",      "[dim]Browse and filter available skills[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]status[/]",    "[dim]Show installed skills and their state[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]sync[/]",      "[dim]Pull latest skill versions from registry and taps[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]publish[/]",   "[dim]Push local skills to the registry[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]refresh[/]",   "[dim]Re-project skills into native agent locations[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]registry[/]",  "[dim]Configure the connected registry policy[/]");
    commandsGrid.AddRow("  [bold deepskyblue3]tap[/]",       "[dim]Manage read-only skill sources[/]");
    AnsiConsole.Write(commandsGrid);
    AnsiConsole.WriteLine();

    AnsiConsole.Write(new Rule("[bold]FLAGS[/]").LeftJustified().RuleStyle("blue dim"));
    var flagsGrid = new Grid()
        .AddColumn(new GridColumn().Width(20))
        .AddColumn();
    flagsGrid.AddRow("  [bold]-g[/][dim], --global[/]",  "[dim]Operate on the global lorex root ([bold]~/.lorex/[/])[/]");
    flagsGrid.AddRow("  [bold]-h[/][dim], --help[/]",    "[dim]Show help for a command[/]");
    flagsGrid.AddRow("  [bold]-v[/][dim], --version[/]", "[dim]Show version[/]");
    AnsiConsole.Write(flagsGrid);
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[dim]Run [bold]lorex <command> --help[/] for command-specific flags and examples.[/]");
    AnsiConsole.WriteLine();
    return 0;
}
```

Also remove the now-unused `MakeGrid`, `Row`, and `Section` helper lambdas (lines 73–88 in the original).

- [ ] **Step 4: Build to confirm no errors**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Smoke-test manually**

```bash
dotnet run --project src/Lorex -- --help
```

Expected: FigletText banner, tagline, COMMANDS section, FLAGS section, footer line.

- [ ] **Step 6: Commit**

```bash
git add src/Lorex/Program.cs
git commit -m "feat(help): redesign global lorex --help layout"
```

---

## Task 3: Refactor `InitCommand` and `RegistryCommand`

These commands already handle `--help` — we're replacing their inline rendering with `HelpPrinter.Print`.

**Files:**
- Modify: `src/Lorex/Commands/InitCommand.cs`
- Modify: `src/Lorex/Commands/RegistryCommand.cs`

- [ ] **Step 1: Replace `InitCommand.PrintHelp()`**

Find the private `PrintHelp()` method in `src/Lorex/Commands/InitCommand.cs` (currently lines 332–364) and replace the entire method body:

```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex init [<url>] [--local] [--adapters <a,b>] [--global]",
    "Configure lorex for this project (or globally with --global).\nRunning without arguments launches an interactive setup wizard.",
    options:
    [
        ("<url>",                "Registry URL (HTTPS/SSH) or local absolute path"),
        ("--local",              "Skip registry setup; manage skills locally"),
        ("-a, --adapters <a,b>", "Comma-separated adapters to enable (e.g. claude,copilot)"),
        ("-g, --global",         "Initialise the global lorex root (~/.lorex)"),
        ("-h, --help",           "Show this help"),
    ],
    examples:
    [
        ("Interactive setup",            "lorex init"),
        ("Connect to a remote registry", "lorex init https://github.com/org/skills --adapters claude,copilot"),
        ("Use a local path as registry", "lorex init /path/to/registry --adapters claude"),
        ("Local-only, no registry",      "lorex init --local --adapters claude"),
        ("Global install",               "lorex init --global https://github.com/org/skills"),
    ]);
```

- [ ] **Step 2: Replace `RegistryCommand.PrintHelp()`**

Find the private `PrintHelp()` method in `src/Lorex/Commands/RegistryCommand.cs` (currently lines 149–156) and replace:

```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex registry",
    "Interactively update the connected registry's publish policy and recommended taps.\nDirect registries update immediately; pull-request registries prepare a review branch.",
    options:
    [
        ("-h, --help", "Show this help"),
    ]);
```

Also simplify the `--help` check at the top of `RegistryCommand.Run()`. Replace:

```csharp
if (args.Length > 0 && args.Any(arg => arg is "--help" or "-h"))
    return PrintHelp();
```

with:

```csharp
if (args.Any(a => a is "--help" or "-h"))
    return PrintHelp();
```

- [ ] **Step 3: Run existing `--help` tests**

```bash
dotnet test tests/Lorex.Tests --filter "InitCommand_HelpFlag_ReturnsZero" -v minimal
```

Expected: PASS.

- [ ] **Step 4: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Lorex/Commands/InitCommand.cs src/Lorex/Commands/RegistryCommand.cs
git commit -m "refactor(help): migrate init and registry to HelpPrinter"
```

---

## Task 4: Add `--help` to `InstallCommand` and `UninstallCommand`

**Files:**
- Modify: `src/Lorex/Commands/InstallCommand.cs`
- Modify: `src/Lorex/Commands/UninstallCommand.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void InstallCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, InstallCommand.Run(["--help"]));
    Assert.Equal(0, InstallCommand.Run(["-h"]));
}

[Fact]
public void UninstallCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, UninstallCommand.Run(["--help"]));
    Assert.Equal(0, UninstallCommand.Run(["-h"]));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Lorex.Tests --filter "InstallCommand_HelpFlag|UninstallCommand_HelpFlag" -v minimal
```

Expected: FAIL — both commands throw because `--help` hits project-root resolution before being handled.

- [ ] **Step 3: Update `InstallCommand.Run()`**

In `src/Lorex/Commands/InstallCommand.cs`, add the `--help` check as the very first line of `Run()` (before `WantsGlobal`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var isGlobal = WantsGlobal(args);
    // ... rest unchanged
```

Add at the bottom of the class (before the closing `}`):
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex install [<skill>…] [--all] [--recommended] [--search <text>] [--tag <tag>] [-g]",
    "Install skills from the registry, taps, or a URL.\nRunning without arguments opens an interactive picker.",
    options:
    [
        ("<skill>…",           "Skill names or URLs to install"),
        ("--all",              "Install all available skills"),
        ("--recommended",      "Install skills recommended for this project"),
        ("--search <text>",    "Pre-filter the picker by name or description"),
        ("--tag <tag>",        "Pre-filter the picker by tag"),
        ("-g, --global",       "Operate on the global lorex root (~/.lorex)"),
        ("-h, --help",         "Show this help"),
    ],
    examples:
    [
        ("Interactive picker",        "lorex install"),
        ("Install a specific skill",  "lorex install my-skill"),
        ("Install recommended skills","lorex install --recommended"),
        ("Install all available",     "lorex install --all"),
        ("Install from a URL",        "lorex install https://github.com/org/skill-repo"),
    ]);
```

- [ ] **Step 4: Update `UninstallCommand.Run()`**

In `src/Lorex/Commands/UninstallCommand.cs`, add the `--help` check as the very first line of `Run()` (before `WantsGlobal`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var isGlobal = args.Any(a =>
    // ... rest unchanged
```

Add at the bottom of the class:
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex uninstall [<skill>…] [--all] [-g]",
    "Remove installed skills. Running without arguments opens an interactive picker.",
    options:
    [
        ("<skill>…",     "Skill names to uninstall"),
        ("--all",        "Uninstall all installed skills"),
        ("-g, --global", "Operate on the global lorex root (~/.lorex)"),
        ("-h, --help",   "Show this help"),
    ],
    examples:
    [
        ("Interactive picker",      "lorex uninstall"),
        ("Remove a specific skill", "lorex uninstall my-skill"),
        ("Remove all skills",       "lorex uninstall --all"),
    ]);
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Lorex.Tests --filter "InstallCommand_HelpFlag|UninstallCommand_HelpFlag" -v minimal
```

Expected: PASS (4 assertions across 2 tests).

- [ ] **Step 6: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Lorex/Commands/InstallCommand.cs src/Lorex/Commands/UninstallCommand.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): add --help to install and uninstall"
```

---

## Task 5: Add `--help` to `CreateCommand` and `PublishCommand`

**Files:**
- Modify: `src/Lorex/Commands/CreateCommand.cs`
- Modify: `src/Lorex/Commands/PublishCommand.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void CreateCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, CreateCommand.Run(["--help"]));
    Assert.Equal(0, CreateCommand.Run(["-h"]));
}

[Fact]
public void PublishCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, PublishCommand.Run(["--help"]));
    Assert.Equal(0, PublishCommand.Run(["-h"]));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Lorex.Tests --filter "CreateCommand_HelpFlag|PublishCommand_HelpFlag" -v minimal
```

Expected: FAIL.

- [ ] **Step 3: Update `CreateCommand.Run()`**

In `src/Lorex/Commands/CreateCommand.cs`, add the `--help` check as the very first line of `Run()` (before `ProjectRootLocator`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var projectRoot = ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());
    // ... rest unchanged
```

Add at the bottom of the class:
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex create [<name>] [-d <desc>] [-t <tags>] [-o <owner>]",
    "Scaffold a new skill in .lorex/skills/ for local authoring.\nRunning without arguments prompts interactively.",
    options:
    [
        ("<name>",               "Skill name (kebab-case)"),
        ("-d, --description",    "One-line description shown in lorex list"),
        ("-t, --tags <a,b>",     "Comma-separated tags for discovery"),
        ("-o, --owner <name>",   "Team or individual name"),
        ("-h, --help",           "Show this help"),
    ],
    examples:
    [
        ("Interactive",     "lorex create"),
        ("Non-interactive", "lorex create auth-overview -d \"Auth patterns for this repo\" -t auth,security"),
    ]);
```

- [ ] **Step 4: Update `PublishCommand.Run()`**

In `src/Lorex/Commands/PublishCommand.cs`, add the `--help` check as the very first line of `Run()` (before `ProjectRootLocator`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var projectRoot = ProjectRootLocator.ResolveForExistingProject(cwd ?? Directory.GetCurrentDirectory());
    // ... rest unchanged
```

Add at the bottom of the class:
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex publish [<skill>…]",
    "Push local skills to the registry. Running without arguments opens an interactive picker.\nDirect registries publish immediately; pull-request registries prepare a review branch.",
    options:
    [
        ("<skill>…",   "Skill names to publish"),
        ("-h, --help", "Show this help"),
    ],
    examples:
    [
        ("Interactive picker",       "lorex publish"),
        ("Publish a specific skill", "lorex publish my-skill"),
    ]);
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Lorex.Tests --filter "CreateCommand_HelpFlag|PublishCommand_HelpFlag" -v minimal
```

Expected: PASS.

- [ ] **Step 6: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Lorex/Commands/CreateCommand.cs src/Lorex/Commands/PublishCommand.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): add --help to create and publish"
```

---

## Task 6: Add `--help` to `ListCommand` and `StatusCommand`

**Files:**
- Modify: `src/Lorex/Commands/ListCommand.cs`
- Modify: `src/Lorex/Commands/StatusCommand.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void ListCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, ListCommand.Run(["--help"]));
    Assert.Equal(0, ListCommand.Run(["-h"]));
}

[Fact]
public void StatusCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, StatusCommand.Run(["--help"]));
    Assert.Equal(0, StatusCommand.Run(["-h"]));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Lorex.Tests --filter "ListCommand_HelpFlag|StatusCommand_HelpFlag" -v minimal
```

Expected: FAIL.

- [ ] **Step 3: Update `ListCommand.Run()`**

In `src/Lorex/Commands/ListCommand.cs`, add the `--help` check as the very first line of `Run()` (before `WantsGlobal`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var isGlobal = args.Any(a =>
    // ... rest unchanged
```

Add at the bottom of the class (before the closing `}`):
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex list [--search <text>] [--tag <tag>] [--page <n>] [--page-size <n>] [-g]",
    "Browse skills available in the registry and taps.\nOpens an interactive TUI in a terminal; outputs a plain table when piped.",
    options:
    [
        ("--search <text>", "Filter by name or description"),
        ("--tag <tag>",     "Filter by tag"),
        ("--page <n>",      "Page number (default: 1)"),
        ("--page-size <n>", "Results per page (default: 25; use 0 to show all)"),
        ("-g, --global",    "Operate on the global lorex root (~/.lorex)"),
        ("-h, --help",      "Show this help"),
    ],
    examples:
    [
        ("Interactive TUI",         "lorex list"),
        ("Filter by keyword",       "lorex list --search auth"),
        ("Filter by tag",           "lorex list --tag security"),
        ("Paginate non-interactively", "lorex list --page 2 --page-size 10"),
    ]);
```

- [ ] **Step 4: Update `StatusCommand.Run()`**

In `src/Lorex/Commands/StatusCommand.cs`, add the `--help` check as the very first line of `Run()` (before `WantsGlobal`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var isGlobal = args.Any(a =>
    // ... rest unchanged
```

Add at the bottom of the class (before the closing `}`):
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex status [-g]",
    "Show the registry, adapters, and installed skill link states for this project.",
    options:
    [
        ("-g, --global", "Show global lorex state (~/.lorex) instead of the current project"),
        ("-h, --help",   "Show this help"),
    ],
    examples:
    [
        ("", "lorex status"),
        ("", "lorex status --global"),
    ]);
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Lorex.Tests --filter "ListCommand_HelpFlag|StatusCommand_HelpFlag" -v minimal
```

Expected: PASS.

- [ ] **Step 6: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Lorex/Commands/ListCommand.cs src/Lorex/Commands/StatusCommand.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): add --help to list and status"
```

---

## Task 7: Add `--help` to `SyncCommand` and `RefreshCommand`

**Files:**
- Modify: `src/Lorex/Commands/SyncCommand.cs`
- Modify: `src/Lorex/Commands/RefreshCommand.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void SyncCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, SyncCommand.Run(["--help"]));
    Assert.Equal(0, SyncCommand.Run(["-h"]));
}

[Fact]
public void RefreshCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, RefreshCommand.Run(["--help"]));
    Assert.Equal(0, RefreshCommand.Run(["-h"]));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Lorex.Tests --filter "SyncCommand_HelpFlag|RefreshCommand_HelpFlag" -v minimal
```

Expected: FAIL.

- [ ] **Step 3: Update `SyncCommand.Run()`**

In `src/Lorex/Commands/SyncCommand.cs`, add the `--help` check as the very first line of `Run()` (before `WantsGlobal`), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null, string? homeRoot = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    var isGlobal = args.Any(a =>
    // ... rest unchanged
```

Add at the bottom of the class (before the closing `}`):
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex sync [-g]",
    "Pull the latest skill versions from the registry and all taps,\nand restore any missing symlinks (e.g. after a fresh clone).",
    options:
    [
        ("-g, --global", "Operate on the global lorex root (~/.lorex)"),
        ("-h, --help",   "Show this help"),
    ],
    examples:
    [
        ("", "lorex sync"),
        ("", "lorex sync --global"),
    ]);
```

- [ ] **Step 4: Update `RefreshCommand.Run()`**

In `src/Lorex/Commands/RefreshCommand.cs`, add the `--help` check as the very first line of `Run()` (before the target parsing loop), and add a `PrintHelp()` method:

At the top of `Run()`:
```csharp
public static int Run(string[] args, string? cwd = null)
{
    if (args.Any(a => a is "--help" or "-h"))
        return PrintHelp();

    // Parse optional --target <adapter>
    string? target = null;
    // ... rest unchanged
```

Add at the bottom of the class (before the closing `}`):
```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex refresh [--target <adapter>]",
    "Re-project lorex skills into native agent locations without fetching from the registry.\nUseful after adding a new adapter or when projections are out of sync.",
    options:
    [
        ("-t, --target <adapter>", "Re-project a single adapter only"),
        ("-h, --help",             "Show this help"),
    ],
    examples:
    [
        ("Refresh all adapters",        "lorex refresh"),
        ("Refresh only Claude adapter", "lorex refresh --target claude"),
    ]);
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/Lorex.Tests --filter "SyncCommand_HelpFlag|RefreshCommand_HelpFlag" -v minimal
```

Expected: PASS.

- [ ] **Step 6: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Lorex/Commands/SyncCommand.cs src/Lorex/Commands/RefreshCommand.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): add --help to sync and refresh"
```

---

## Task 8: Update `TapCommand` and add `RegistryCommand` test

**Files:**
- Modify: `src/Lorex/Commands/TapCommand.cs`
- Modify: `tests/Lorex.Tests/CommandArgumentTests.cs`

- [ ] **Step 1: Write the failing tests**

Add to `tests/Lorex.Tests/CommandArgumentTests.cs`:

```csharp
[Fact]
public void RegistryCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, RegistryCommand.Run(["--help"]));
    Assert.Equal(0, RegistryCommand.Run(["-h"]));
}

[Fact]
public void TapCommand_HelpFlag_ReturnsZero()
{
    Assert.Equal(0, TapCommand.Run(["--help"]));
    Assert.Equal(0, TapCommand.Run(["-h"]));
    Assert.Equal(0, TapCommand.Run([]));   // no args → show help
}
```

- [ ] **Step 2: Run tests to verify state**

```bash
dotnet test tests/Lorex.Tests --filter "RegistryCommand_HelpFlag|TapCommand_HelpFlag" -v minimal
```

Expected: `RegistryCommand_HelpFlag` PASSES (already implemented). `TapCommand_HelpFlag` PASSES (already dispatches to `PrintHelp()`). Confirm all pass before proceeding.

- [ ] **Step 3: Refactor `TapCommand.PrintHelp()`**

Find the `PrintHelp()` method in `src/Lorex/Commands/TapCommand.cs` (currently lines 346–359) and replace:

```csharp
private static int PrintHelp() => HelpPrinter.Print(
    "lorex tap <subcommand> [args]",
    "Manage read-only skill sources (taps). Skills from taps appear\nalongside registry skills in lorex list and lorex install.",
    subcommands:
    [
        ("add <url> [--name <n>] [--root <p>] [-g]", "Add a tap"),
        ("remove <name> [-g]",                       "Remove a tap"),
        ("list [-g]",                                "List configured taps"),
        ("sync [<name>] [-g]",                       "Pull latest from taps"),
        ("promote [<name>]",                         "Add tap(s) to registry recommended taps"),
    ],
    options:
    [
        ("-g, --global", "Operate on the global lorex root (~/.lorex)"),
        ("-h, --help",   "Show this help"),
    ],
    examples:
    [
        ("Add a tap",              "lorex tap add https://github.com/org/skills"),
        ("Add with a custom name", "lorex tap add https://github.com/org/skills --name myorg"),
        ("",                       "lorex tap list"),
        ("",                       "lorex tap sync"),
        ("",                       "lorex tap remove myorg"),
        ("Promote to registry",    "lorex tap promote myorg"),
    ]);
```

- [ ] **Step 4: Run all help tests**

```bash
dotnet test tests/Lorex.Tests --filter "HelpFlag" -v minimal
```

Expected: All tests PASS.

- [ ] **Step 5: Run the full test suite**

```bash
dotnet test tests/Lorex.Tests -v minimal
```

Expected: All tests pass, 0 failures.

- [ ] **Step 6: Build**

```bash
dotnet build src/Lorex
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Lorex/Commands/TapCommand.cs tests/Lorex.Tests/CommandArgumentTests.cs
git commit -m "feat(help): migrate tap to HelpPrinter, add registry and tap help tests"
```

---

## Task 9: Update docs

**Files:**
- Modify: `docs/reference/commands.md`

- [ ] **Step 1: Add a note at the top of `docs/reference/commands.md`**

Find the opening paragraph (lines 1–7) and add a note about `--help` availability. Change the opening to:

```markdown
# Command Reference

All commands resolve the project root by walking **up** from the current directory to the nearest ancestor that contains `.lorex/lorex.json`. You never need to `cd` to the repo root before running a lorex command.

Commands that accept `--global` bypass project-root discovery entirely and operate on `~/.lorex/` instead. See [Global Skills](#global-skills) for the workflow.

Every command supports `--help` / `-h` for command-specific usage, flags, and examples:

```bash
lorex <command> --help
```

---
```

- [ ] **Step 2: Verify the commands that previously lacked `--help` documentation now have their flags described**

Scan `docs/reference/commands.md` for each of these commands and confirm each section has a `--help` / `-h` row in its flags table: `install`, `uninstall`, `create`, `publish`, `sync`, `list`, `status`, `refresh`. If any are missing, add them.

The flag row to add (where missing):

```markdown
| `--help` | `-h` | Show command-specific help and exit. |
```

- [ ] **Step 3: Run full test suite one final time**

```bash
dotnet test tests/Lorex.Tests -v minimal
```

Expected: All tests pass, 0 failures.

- [ ] **Step 4: Commit**

```bash
git add docs/reference/commands.md
git commit -m "docs(help): note --help available on all commands"
```

---

## Self-Review

**Spec coverage:**
- [x] `HelpPrinter` helper in `Cli/` — Task 1
- [x] Global help redesign (COMMANDS + FLAGS layout) — Task 2
- [x] `InitCommand` refactored to use `HelpPrinter` — Task 3
- [x] `RegistryCommand` refactored to use `HelpPrinter` — Task 3
- [x] `TapCommand` refactored to use `HelpPrinter` — Task 8
- [x] `InstallCommand` --help — Task 4
- [x] `UninstallCommand` --help — Task 4
- [x] `CreateCommand` --help — Task 5
- [x] `PublishCommand` --help — Task 5
- [x] `ListCommand` --help — Task 6
- [x] `StatusCommand` --help — Task 6
- [x] `SyncCommand` --help — Task 7
- [x] `RefreshCommand` --help — Task 7
- [x] Tests for `--help` exit-code-0 on all commands — Tasks 4–8
- [x] `docs/reference/commands.md` updated — Task 9

**Type consistency:** `HelpPrinter.Print` returns `int`. Every `PrintHelp()` delegates via `=> HelpPrinter.Print(...)` which returns 0. Consistent across all tasks.

**Edge cases handled:**
- `--help` check placed before project-root resolution in every command, so `lorex install --help` works from any directory with no `.lorex/lorex.json`.
- `lorex tap` no-args case (`args.Length == 0 ? "--help"`) already dispatches to `PrintHelp()` — no change needed.
- Multi-line descriptions use `\n` as delimiter; `HelpPrinter` splits and renders each line with consistent indentation.
