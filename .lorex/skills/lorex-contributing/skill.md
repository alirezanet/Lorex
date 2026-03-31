---
name: lorex-contributing
description: Lorex project overview, architecture, and contribution guide for developers working on the lorex codebase.
version: 1.0.0
tags: lorex, contributing, architecture, internals
owner: lorex
---

# lorex-contributing

> **You are an AI agent reading this skill.** Use it to help contributors understand the lorex codebase, answer architecture questions, implement changes correctly, and know which files to update after any change.

This skill covers working **on** lorex itself. For using lorex to manage skills in other projects, see the `lorex` skill.

---

## What Is Lorex (For Contributors)

Lorex is a **native AOT CLI tool** written in C#/.NET 10 — a single self-contained binary with no runtime dependency. It packages engineering knowledge as versioned markdown "skills" and injects a skill index into AI agent config files so agents arrive informed.

- **Language / framework:** C# / .NET 10 (`net10.0`)
- **Key dependency:** `Spectre.Console` (core only — no `.Cli` package; dispatch is manual)
- **Distribution:** `PackAsTool=true` (dotnet global tool) + native AOT binaries per platform
- **Status:** Early beta (v0.0.x stabilisation)
- **Repo:** https://github.com/alirezanet/lorex

---

## When a User Asks You to Help With the Codebase

**"Where is X implemented?"** → use the Repository Layout and Architecture sections below to orient yourself, then read the relevant file.

**"How do I add a new adapter?"** → see [Adding a New Adapter](#adding-a-new-adapter).

**"How do I add a new command?"** → see [Adding a New Command](#adding-a-new-command).

**"What files do I need to update after this change?"** → see [Skill File Update Checklist](#skill-file-update-checklist).

**"How do I build / run / test?"** → see [Build & Run](#build--run).

**"Why isn't my change working?"** → see [Pitfalls](#pitfalls).

---

## Repository Layout

```
lorex/
├── src/Lorex/
│   ├── Program.cs                      ← entry point; manual switch dispatch on args[0]
│   ├── Lorex.csproj
│   ├── Commands/                       ← one static class per CLI command
│   │   ├── InitCommand.cs              ← lorex init
│   │   ├── InstallCommand.cs           ← lorex install
│   │   ├── UninstallCommand.cs         ← lorex uninstall
│   │   ├── ListCommand.cs              ← lorex list
│   │   ├── StatusCommand.cs            ← lorex status
│   │   ├── SyncCommand.cs              ← lorex sync
│   │   ├── CreateCommand.cs            ← lorex create (scaffold + register + refresh)
│   │   ├── PublishCommand.cs           ← lorex publish
│   │   ├── RefreshCommand.cs           ← lorex refresh
│   │   └── ServiceFactory.cs           ← lazy singleton service locator
│   ├── Core/
│   │   ├── Adapters/                   ← one file per supported AI tool
│   │   │   ├── IAdapter.cs             ← Key, ConfigFile, InjectIndex, RemoveIndex, IsPresent
│   │   │   ├── CopilotAdapter.cs       ← .github/copilot-instructions.md
│   │   │   ├── CodexAdapter.cs         ← AGENTS.md
│   │   │   ├── OpenClawAdapter.cs      ← AGENTS.md
│   │   │   ├── CursorAdapter.cs        ← .cursorrules
│   │   │   ├── ClaudeAdapter.cs        ← CLAUDE.md
│   │   │   ├── WindsurfAdapter.cs      ← .windsurfrules
│   │   │   ├── ClineAdapter.cs         ← .clinerules
│   │   │   ├── RooAdapter.cs           ← .roorules
│   │   │   ├── GeminiAdapter.cs        ← GEMINI.md
│   │   │   └── OpenCodeAdapter.cs      ← opencode.md
│   │   ├── Services/
│   │   │   ├── AdapterService.cs       ← KnownAdapters dict; Compile (injects index); auto-detect
│   │   │   ├── SkillService.cs         ← install / uninstall / sync / scaffold / publish
│   │   │   ├── BuiltInSkillService.cs  ← reads EmbeddedResources; InstallAll on lorex init
│   │   │   ├── RegistryService.cs      ← git clone/pull to ~/.lorex/cache/<slug>/
│   │   │   ├── GitService.cs           ← thin Process wrapper around git
│   │   │   └── WindowsDevModeHelper.cs ← symlink availability check + guidance
│   │   ├── Models/
│   │   │   ├── LorexConfig.cs          ← project config (registry?, adapters[], installedSkills[])
│   │   │   ├── GlobalConfig.cs         ← user-level config (~/.lorex/config.json)
│   │   │   └── SkillMetadata.cs        ← parsed YAML frontmatter (name, description, version…)
│   │   └── Serialization/
│   │       ├── LorexJsonContext.cs     ← AOT-safe source-gen JSON context
│   │       └── SimpleYamlParser.cs     ← handwritten frontmatter parser (no external deps)
│   └── Resources/
│       └── lorex.md                    ← EmbeddedResource; auto-installed on lorex init
├── tests/Lorex.Tests/                  ← xUnit unit tests
├── .lorex/skills/
│   ├── lorex/                          ← built-in skill (auto-managed; gitignored)
│   └── lorex-contributing/             ← this file (committed to repo)
├── .github/workflows/
│   ├── ci.yml                          ← build + test on every push/PR
│   └── release.yml                     ← AOT binaries + nupkg + NuGet publish on v* tag
├── install.cs                          ← dev installer (dotnet C# script)
└── lorex.slnx
```

---

## Architecture

### CLI Dispatch
`Program.cs` uses a `switch(args[0])` to route to static `Run(string[] rest)` methods. No command framework. Help text and argument parsing are handwritten per command.

### Services
`ServiceFactory` is a lazy singleton locator. Commands call `ServiceFactory.Skills`, `ServiceFactory.Registry`, `ServiceFactory.Adapters`, etc. All services are constructed once per process.

### Adapters
Each adapter implements `IAdapter`:
- `Key` — the string used in `lorex init` (e.g. `"copilot"`)
- `TargetFilePath(projectRoot)` — full path to the config file written
- `InjectIndex(projectRoot, index)` — writes/updates the `<!-- lorex:start -->…<!-- lorex:end -->` block
- `RemoveIndex(projectRoot)` — strips the block
- `DetectExisting(projectRoot)` — returns true if config file is already present (used for auto-select on init)

All adapters are registered in `AdapterService.KnownAdapters` (`Dictionary<string, IAdapter>`). `AdapterService.Compile(projectRoot, config)` calls `InjectIndex` on every enabled adapter.

### Skill Lifecycle
| Type | Location | Symlink? | Publishable? |
|---|---|---|---|
| Registry skill | `~/.lorex/cache/<slug>/skills/<name>/` → symlink at `.lorex/skills/<name>` | Yes (copy fallback on Windows without Dev Mode) | Already in registry |
| Local skill | `.lorex/skills/<name>/` (real directory) | No | Yes via `lorex publish` |
| Built-in skill | Embedded in binary → extracted to `.lorex/skills/<name>/` on init | No | No (blocked by guard) |

`SkillService.LocalOnlySkills()` returns real directories excluding built-ins — the candidates for `lorex publish`.

### Config Model
```json
{ "registry": "https://…", "adapters": ["copilot", "codex"], "installedSkills": ["lorex", "my-skill"] }
```
- `registry` is `string?` — null = local-only mode
- Skills must be in `installedSkills` to appear in the injected index
- `lorex create` / `SkillService.ScaffoldSkill` adds the name automatically

### Skill Index Injection Format
```
<!-- lorex:start -->
## Lorex Skill Index
- **skill-name**: description → `.lorex/skills/skill-name/skill.md`
<!-- lorex:end -->
```
Each adapter writes this block into its own config file.

---

## Build & Run

```bash
dotnet build                                                # build
dotnet run --project src/Lorex -- <args>                   # run from source
dotnet run install.cs                                       # build -dev nupkg + install as global tool
dotnet test                                                 # run unit tests

# Native AOT (must run on the matching platform)
dotnet publish src/Lorex /p:PublishProfile=win-x64   -c Release
dotnet publish src/Lorex /p:PublishProfile=linux-x64 -c Release
dotnet publish src/Lorex /p:PublishProfile=osx-arm64 -c Release
```

`install.cs` uninstalls the existing global tool before reinstalling — this works around `dotnet tool update` being a no-op when the version hasn't changed.

### CI / Release
- **CI** (`ci.yml`) — triggers on push/PR to master: restore → build → test
- **Release** (`release.yml`) — triggers on `v*` tag: test → 6× native AOT builds (parallel) → pack nupkg → create draft GitHub release → push to NuGet
- Version comes from the tag (`v0.1.0` → `0.1.0`), not the csproj

```bash
git tag v0.1.0 && git push origin v0.1.0   # triggers full release pipeline
```

---

## Adding a New Adapter

1. Create `src/Lorex/Core/Adapters/<Name>Adapter.cs` implementing `IAdapter`
2. Register in `AdapterService.KnownAdapters`
3. ✏️ `src/Lorex/Resources/lorex.md` — add row to Supported Adapters table
4. ✏️ `.lorex/skills/lorex-contributing/skill.md` — update adapter list in repo layout
5. ✏️ `README.md` — Works With Every Major AI Tool table

## Adding a New Command

1. Create `src/Lorex/Commands/<Name>Command.cs` with `public static int Run(string[] args)`
2. Add case to `switch` in `Program.cs`
3. Add row to `PrintHelp()` grid in `Program.cs`
4. ✏️ `src/Lorex/Resources/lorex.md` — All Commands table + any agent workflow guidance
5. ✏️ `README.md` — All Commands section

---

## Skill File Update Checklist

After any change, update the relevant files so agents reading only skill files get accurate information:

| Changed area | Files to update |
|---|---|
| New / changed CLI command | `src/Lorex/Resources/lorex.md`, `README.md` |
| New / changed adapter | `lorex.md`, `lorex-contributing/skill.md`, `README.md` |
| Skill format / frontmatter | `lorex.md`, `README.md` |
| Build, install, or release workflow | `lorex-contributing/skill.md`, `README.md` |
| Architecture / project structure | `lorex-contributing/skill.md` |

> **Rule:** `src/Lorex/Resources/lorex.md` is the embedded binary copy. After editing it, rebuild with `dotnet run install.cs` so the installed tool carries the updated skill.

---

## Pitfalls

- **Spectre.Console markup:** `[tag]` is parsed as markup. Escape literal brackets as `[[`. Any dynamic content rendered inside markup must go through `Markup.Escape()`.
- **AOT compatibility:** no `dynamic`, no `Reflection.Emit`, no runtime-discovered types. Add new model types to `LorexJsonContext` for AOT-safe JSON serialisation.
- **Null registry:** `LorexConfig.Registry` is `string?`. Commands that need a registry (`install`, `list`, `sync`, `publish`) must guard for null and print a helpful message — see existing commands for the pattern.
- **`git ls-remote -- <url>`** validates a registry URL against empty repos. Do not use `--exit-code` or `--heads` — they behave differently across git versions.
- **`install.cs` version:** the script reads the version from the csproj and appends `-dev`. Since the csproj no longer has a hardcoded version, it defaults to `0.0.0-dev`. This is intentional for local dev.
- **Built-in skill sync:** `src/Lorex/Resources/lorex.md` (binary source) must be kept manually in sync with the agent-facing knowledge. There is no automation for this — it's your responsibility on every relevant change.


---

## Project Overview

Lorex is a **native AOT CLI tool** written in C#/.NET 10. It is distributed as a single self-contained binary with no runtime dependency. It lets developers package engineering knowledge as versioned markdown "skills" and inject them into AI agent config files.

- **Version:** 0.0.1 (early beta)
- **Target framework:** `net10.0`
- **Key dependency:** `Spectre.Console` (core only, no `.Cli` package — dispatch is manual)
- **Tool packaging:** `PackAsTool=true`, command name `lorex`
- **AOT:** `InvariantGlobalization=true`, `EnableTrimAnalyzer=true`; all native publish profiles live in `src/Lorex/Properties/PublishProfiles/`

---

## Repository Layout

```
lorex/
├── src/
│   └── Lorex/
│       ├── Program.cs                     ← entry point; manual switch dispatch
│       ├── Lorex.csproj
│       ├── Commands/                      ← one file per CLI command
│       │   ├── InitCommand.cs
│       │   ├── InstallCommand.cs
│       │   ├── UninstallCommand.cs
│       │   ├── ListCommand.cs
│       │   ├── StatusCommand.cs
│       │   ├── SyncCommand.cs
│       │   ├── CreateCommand.cs
│       │   ├── PublishCommand.cs
│       │   ├── RefreshCommand.cs
│       │   └── ServiceFactory.cs          ← singleton service locator
│       ├── Core/
│       │   ├── Adapters/                  ← one file per AI tool adapter
│       │   │   ├── IAdapter.cs
│       │   │   ├── CopilotAdapter.cs
│       │   │   ├── CodexAdapter.cs
│       │   │   ├── OpenClawAdapter.cs
│       │   │   ├── CursorAdapter.cs
│       │   │   ├── ClaudeAdapter.cs
│       │   │   ├── WindsurfAdapter.cs
│       │   │   ├── ClineAdapter.cs
│       │   │   ├── RooAdapter.cs
│       │   │   ├── GeminiAdapter.cs
│       │   │   └── OpenCodeAdapter.cs
│       │   ├── Services/
│       │   │   ├── AdapterService.cs      ← KnownAdapters registry; auto-detect
│       │   │   ├── SkillService.cs        ← install/uninstall/scaffold/publish/sync
│       │   │   ├── BuiltInSkillService.cs ← reads embedded resources; auto-install on init
│       │   │   ├── RegistryService.cs     ← git clone/pull of registry to ~/.lorex/cache/
│       │   │   ├── GitService.cs          ← thin Process wrapper around git
│       │   │   └── WindowsDevModeHelper.cs
│       │   ├── Models/                    ← LorexConfig, GlobalConfig, SkillMeta
│       │   └── Serialization/             ← LorexJsonContext (AOT-safe source-gen JSON)
│       └── Resources/
│           └── lorex.md                   ← EmbeddedResource; auto-installed on lorex init
├── tests/
│   └── Lorex.Tests/                       ← xUnit unit tests
├── .lorex/
│   └── skills/                            ← locally-managed skills (committed to repo)
│       ├── lorex/                         ← built-in usage skill (auto-managed by lorex)
│       └── lorex-contributing/            ← this file
├── install.cs                             ← dev installer (dotnet script)
├── lorex.slnx
└── README.md
```

---

## Architecture — Key Concepts

### Dispatch
`Program.cs` uses a `switch(args[0])` to route to static `Run(string[] rest)` methods on each command class. There is no command framework. Help text and argument parsing are handwritten.

### Services
`ServiceFactory` is a lazy singleton locator. Commands call `ServiceFactory.Skills`, `ServiceFactory.Registry`, etc. Services are constructed once per process.

### Adapters
Each adapter implements `IAdapter`:
- `Key` — the string used in `lorex init` (e.g., `"copilot"`)
- `ConfigFile` — relative path to the file written (e.g., `.github/copilot-instructions.md`)
- `InjectIndex(string projectRoot, string index)` — writes or updates the `<!-- lorex:start -->…<!-- lorex:end -->` block
- `RemoveIndex(string projectRoot)` — strips the block
- `IsPresent(string projectRoot)` — returns true if the config file already exists (used for auto-detect on init)

All known adapters are registered in `AdapterService.KnownAdapters` (a `Dictionary<string, IAdapter>`).

### Skill Lifecycle
1. **Registry skills** — cloned to `~/.lorex/cache/<registry-hash>/skills/<name>/`; installed as a directory symlink at `.lorex/skills/<name>` → cache path.
2. **Local skills** — authored directly in `.lorex/skills/<name>/` by the AI/user, or scaffolded by `lorex create`; real directory (not a symlink); publishable via `lorex publish`.
3. **Built-in skills** — embedded as `EmbeddedResource` in the binary (`Resources/*.md`); extracted to `.lorex/skills/<name>/` on `lorex init`; not publishable.

`SkillService.LocalOnlySkills()` returns skills that are real directories and not built-in — i.e., candidates for `lorex publish`.

### Config
`.lorex/lorex.json` — project-level config:
```json
{ "registry": "https://…", "adapters": ["copilot"], "installedSkills": ["lorex"] }
```
Skills must be in `installedSkills` to appear in the injected index. `ScaffoldSkill` adds the name automatically. For manually created skill folders, add the name here and run `lorex refresh`.

### Skill Index Injection
Each adapter's `InjectIndex` writes a fenced block into its config file:
```
<!-- lorex:start -->
## Lorex Skill Index
- **skill-name**: description → `.lorex/skills/skill-name/skill.md`
<!-- lorex:end -->
```
The agent reads this block and knows which skill files to load on demand.

---

## Build & Run

```bash
# Build
dotnet build

# Run from source
dotnet run --project src/Lorex -- <args>

# Dev install (builds -dev nupkg + installs as global tool)
dotnet install.cs

# Run tests
dotnet test

# Native AOT publish (requires platform matching the profile)
dotnet publish src/Lorex /p:PublishProfile=win-x64   -c Release
dotnet publish src/Lorex /p:PublishProfile=linux-x64 -c Release
dotnet publish src/Lorex /p:PublishProfile=osx-arm64 -c Release
```

The dev installer (`install.cs`) uninstalls any existing `lorex` global tool, bumps the version with a `-dev` suffix, packs, and re-installs. It requires .NET 10 SDK.

---

## Common Contribution Tasks

### Adding a new adapter

1. Create `src/Lorex/Core/Adapters/<Name>Adapter.cs` implementing `IAdapter`.
2. Register it in `AdapterService.KnownAdapters` dictionary.
3. ✏️ Update `src/Lorex/Resources/lorex.md` — add the key and file to the Supported Adapters table.
4. ✏️ Update `.lorex/skills/lorex/skill.md` — same table.
5. ✏️ Update `README.md` — Supported AI Tools table.
6. ✏️ Update this file (`.lorex/skills/lorex-contributing/skill.md`) if the architecture changed.

### Adding a new command

1. Create `src/Lorex/Commands/<Name>Command.cs` with a static `Run(string[] args)` method.
2. Add a case to the `switch` in `Program.cs`.
3. Add a row to the `PrintHelp()` grid in `Program.cs`.
4. ✏️ Update `src/Lorex/Resources/lorex.md` — All Commands table and AI workflow guidance if relevant.
5. ✏️ Update `.lorex/skills/lorex/skill.md` — All Commands table and AI workflow guidance if relevant.
6. ✏️ Update `README.md` — How It Works command list.

### Changing the skill file format (frontmatter fields, layout conventions)

1. ✏️ Update `src/Lorex/Resources/lorex.md` — Skill File Format section.
2. ✏️ Update `.lorex/skills/lorex/skill.md` — Skill File Format section.
3. ✏️ Update `README.md` — Skill Format section.
4. Consider updating `ScaffoldSkill` in `SkillService.cs` to reflect the new template.

### Changing the skill index format (the injected block)

1. Update the relevant adapter(s) in `Core/Adapters/`.
2. ✏️ Update `src/Lorex/Resources/lorex.md`.
3. ✏️ Update `.lorex/skills/lorex/skill.md`.

---

## Skill File Update Checklist

When you finish a change, ask yourself:

| Changed area | Files to update |
|---|---|
| New or changed CLI command | `lorex.md`, `lorex/skill.md`, `README.md` |
| New or changed adapter | `lorex.md`, `lorex/skill.md`, `README.md`, this file |
| Skill format / frontmatter | `lorex.md`, `lorex/skill.md`, `README.md` |
| Build / install workflow | This file (`lorex-contributing/skill.md`), `README.md` |
| Project structure / architecture | This file (`lorex-contributing/skill.md`) |
| Any of the above | Run `lorex refresh` to re-inject the updated index |

> **Rule of thumb:** if an AI agent reading only the skill files would now have wrong or missing information, the skill files need updating. Treat `lorex.md` (the embedded resource) and `.lorex/skills/lorex/skill.md` as a pair — they should always be identical.

---

## Notes & Pitfalls

- `Spectre.Console` markup uses `[tag]` syntax. To output a literal bracket include it as `[[`. Skill content injected into markup must be escaped with `Markup.Escape()` or wrapped in `[[ ]]`.
- The `lorex` built-in skill lives in two places: `src/Lorex/Resources/lorex.md` (source, compiled into binary) and `.lorex/skills/lorex/skill.md` (installed copy). Keep them in sync manually — there is currently no automation for this.
- AOT compatibility: no `dynamic`, no `System.Reflection.Emit`, no unregistered `System.Text.Json` types. Add new model types to `LorexJsonContext`.
- **`LorexConfig.Registry`** is `string?` — null means local-only mode.
- `InstallCommand`, `SyncCommand`, `ListCommand`, `PublishCommand` guard against null registry and print a helpful message.
- `SkillService.InstallSkill`, `SyncSkills`, `PublishSkill` throw `InvalidOperationException` if registry is null (commands guard before reaching them).
- `StatusCommand` shows `(none — local-only mode)` when registry is null.
- `InitCommand` accepts `--local` flag (non-interactive skip) or empty Enter press (interactive skip).
- `git ls-remote -- <url>` validates a registry URL (works on empty repos; avoid `--exit-code` or `--heads` flags which behave differently across git versions).
- `install.cs` uses the `dotnet` C# scripting runner (`#!/usr/bin/env dotnet`). It uninstalls before installing to work around `dotnet tool update` no-op on same version.
