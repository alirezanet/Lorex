---
name: lorex
description: How to use lorex — the AI skill manager CLI — to install, manage, and author skills for AI coding agents.
version: 1.0.0
tags: lorex, ai-agents, skills, cli
owner: lorex
---

# lorex

Lorex is a CLI that manages **skills** — markdown files that give AI agents codebase knowledge. It injects a skill index into agent config files (Copilot, Cursor, Claude, Codex, etc.) so the agent knows what context is available and can load it on demand.

> **You are an AI agent reading this skill.** Use the information here to help users with lorex tasks and to create or manage skills yourself using the lorex CLI.

---

## When a User Asks You About Lorex

If the user asks "what is lorex?" or "what skills are available?":

1. Check `lorex status` to see what is installed and the registry URL
2. Check `.lorex/lorex.json` for the full config
3. List the skills under `.lorex/skills/` — each subfolder is an installed skill
4. Read any installed `skill.md` files the user asks about

---

## All Commands

| Command | Syntax | When to use |
|---|---|---|
| `init` | `lorex init [<url>] [--local] [--adapters a,b]` | First-time setup in a project; picks agent config targets and optional registry |
| `create` | `lorex create [<name>] [-d desc] [-t tags] [-o owner]` | Scaffold a new skill — creates the folder, writes the frontmatter, registers it, auto-refreshes the index. Alias: `generate` |
| `install` | `lorex install [<skill>…]` | Install skills from the registry into this project |
| `uninstall` | `lorex uninstall <skill>` | Remove an installed skill |
| `list` | `lorex list` | Browse skills available in the registry |
| `status` | `lorex status` | Show installed skills, their type (symlink/copy), and the registry URL |
| `sync` | `lorex sync` | Pull latest skill versions from the registry |
| `publish` | `lorex publish [<skill>…]` | Push a locally authored skill to the registry |
| `refresh` | `lorex refresh [--target adapter]` | Re-inject the skill index into agent config files after manual changes |

Every command works interactively (prompts for missing inputs) and non-interactively (all flags provided — useful when you are running the command on behalf of the user).

`list`, `install`, `sync`, and `publish` require a registry. `create`, `status`, and `refresh` work without one.

---

## Supported Adapters

| Key | File written |
|---|---|
| `copilot` | `.github/copilot-instructions.md` |
| `codex` | `AGENTS.md` |
| `openclaw` | `AGENTS.md` |
| `cursor` | `.cursorrules` |
| `claude` | `CLAUDE.md` |
| `windsurf` | `.windsurfrules` |
| `cline` | `.clinerules` |
| `roo` | `.roorules` |
| `gemini` | `GEMINI.md` |
| `opencode` | `opencode.md` |

---

## How to Create a Skill (For User or As Agent)

**Always use `lorex create` first.** It handles everything automatically: creates the folder, writes the frontmatter template, registers the skill in `.lorex/lorex.json`, and refreshes the index. You then edit the generated `skill.md` with the actual content.

```sh
# Scaffold a skill — lorex does the wiring
lorex create <skill-name> -d "One-line description" -t "tag1,tag2" -o "owner"

# Then write the content into the generated file
# .lorex/skills/<skill-name>/skill.md
```

**Do not** manually create folders, edit `lorex.json`, or run `lorex refresh` — `lorex create` does all of that for you. Only run `lorex refresh` if you edited a skill file manually after creation.

### Common user requests you will handle

**"Create a lorex skill about this project's architecture"**
1. Run `lorex create <name> -d "<description>"`
2. Write the architecture content into `.lorex/skills/<name>/skill.md` below the frontmatter
3. Confirm the skill index was updated (check the agent config file)

**"Summarise our last session into a lorex skill"**
1. Run `lorex create <name> -d "<description>"`
2. Distil the session into well-structured markdown and write it to the skill file
3. Done — no manual wiring needed

**"Install skills from the registry"**
```sh
lorex list                        # see what is available
lorex install <skill-name>        # install a specific skill
lorex install                     # interactive multi-select picker
```

**"Share this skill with the team"**
```sh
lorex publish <skill-name>        # pushes to the registry, converts to a symlink
```

---

## Skill File Format

A skill file is `skill.md` inside `.lorex/skills/<name>/`. The YAML frontmatter is at the top:

```markdown
---
name: my-skill
description: One sentence — shown in `lorex list` and the injected index.
version: 1.0.0
tags: topic, subtopic
owner: team-or-person
---

# My Skill

Free-form markdown. Write whatever the agent needs to know.
Headers, code blocks, tables, bullet lists — anything goes.
```

Field notes:
- `name` and `description` are required — `description` is what appears in the index the agent reads
- `version` is used by `lorex sync` to detect stale copies; bump it when content changes
- A skill folder can also contain scripts or other files the agent can invoke (e.g. `check-env.sh`)

---

## Project Layout

```
.lorex/
  lorex.json              ← project config: registry URL, adapters, installedSkills list
  skills/
    <skill-name>/
      skill.md            ← the skill content (symlink → registry cache, or local copy)
```

`~/.lorex/cache/<slug>/` — shared registry cache on this machine; symlinks point here.

---

## Registry

A lorex registry is a plain git repo:

```
skills/
  auth-overview/
    skill.md
  deployment/
    skill.md
    deploy.sh
```

Point a project at it with `lorex init <url>` or add `"registry": "<url>"` to `.lorex/lorex.json`.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| Skill not appearing in agent index | Run `lorex refresh`; check the skill name is in `installedSkills` in `.lorex/lorex.json` |
| `lorex list` / `install` say no registry | Run `lorex init <url>` to connect one, or use `lorex create` for local-only skills |
| Symlinks not working on Windows | Enable Developer Mode: Settings → System → For developers → Developer Mode |
| Skill content stale after registry update | Run `lorex sync` |
| Published skill still shows as local | Run `lorex status` — after publish it should show as `symlink` |
