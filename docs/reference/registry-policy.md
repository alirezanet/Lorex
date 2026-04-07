# Registry Policy

A registry policy controls how contributors publish skills to a shared registry. The policy is stored in `/.lorex-registry.json` at the root of the registry repository.

---

## Publish modes

### `pull-request` (recommended for teams)

```json
{
  "publishMode": "pull-request",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` creates a new branch (e.g. `lorex/auth-logic-20250401120000`), copies the skill, commits, pushes, and prints a pull request URL. The skill stays as a local directory until the PR is merged and `lorex sync` is run.

Best for: teams where skills should be reviewed before they become available to all users.

### `direct`

```json
{
  "publishMode": "direct",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` commits and pushes the skill straight to `baseBranch`. The local skill directory is immediately converted to a symlink pointing at the registry cache.

Best for: solo developers or small trusted teams.

### `read-only`

```json
{
  "publishMode": "read-only",
  "baseBranch": "main",
  "prBranchPrefix": "lorex/"
}
```

`lorex publish` is blocked entirely. Skills can be installed and synced but not contributed back.

Best for: curated registries where only maintainers publish.

---

## Policy fields

| Field | Default | Description |
| :--- | :--- | :--- |
| `publishMode` | `pull-request` | One of `direct`, `pull-request`, or `read-only` |
| `baseBranch` | `main` | The branch `lorex install`/`sync` reads from, and the base for PR branches |
| `prBranchPrefix` | `lorex/` | Prefix for branches created by `lorex publish` in `pull-request` mode |
| `recommendedTaps` | *(absent)* | Optional list of tap sources the registry recommends to all connected projects |

---

## Recommended taps

A registry can suggest read-only skill sources (taps) that complement its own skills. Add them to `.lorex-registry.json`:

```json
{
  "publishMode": "pull-request",
  "baseBranch": "main",
  "recommendedTaps": [
    { "name": "dotnet", "url": "https://github.com/dotnet/skills", "root": "plugins" },
    { "name": "security", "url": "https://github.com/example/security-skills" }
  ]
}
```

Each entry uses the same fields as a project tap:

| Field | Required | Description |
| :--- | :--- | :--- |
| `url` | yes | Git URL of the tap repository |
| `name` | yes | Short label shown in `lorex list`, `lorex status`, and TUI source columns |
| `root` | no | Subdirectory within the repo where skills live (defaults to repo root) |

**How lorex surfaces these:**

- **`lorex init`** — when connecting to a registry interactively, lorex checks for recommended taps not yet in the project and asks: *"This registry recommends N tap source(s): … Add them?"* The user must explicitly accept; taps are never added silently.
- **`lorex sync`** — after syncing, lorex compares the registry's current `recommendedTaps` against the project's configured taps. If new ones have been added to the registry since the project was initialised, lorex prints a notice and suggests `lorex tap add` or `lorex init`. No taps are modified automatically.

This design keeps the registry as a *suggestion authority*, not a *control authority* — team members decide which external sources land on their machine.

---

## Changing the policy

```bash
lorex registry
```

This opens an interactive prompt. What happens next depends on the *current* mode:

- **Current mode is `direct`** — the new policy is committed and pushed immediately.
- **Current mode is `pull-request`** — Lorex creates a branch for the policy change and prints a PR URL. The existing policy remains in effect until that PR is merged and `lorex sync` is run.

---

## Where the policy is stored

The policy is stored in the registry repository, not in the project. All projects connected to the same registry share the same policy. Lorex caches a copy of the policy in `.lorex/lorex.json` and refreshes it on `lorex sync` and `lorex init`.
