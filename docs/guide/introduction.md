# What is Lorex?

You use AI coding agents. Maybe more than one. And you've probably noticed that every time you start a session, you end up explaining the same things again — how your project is structured, what patterns to follow, what not to touch.

Then you switch agents, or a teammate joins, and you explain it all over again.

**Lorex fixes that.**

---

## The idea

Most AI agents already let you give them persistent context — Claude has skills, Cursor has rules, Copilot has instructions. The concept isn't new. The problem is that each tool stores it differently, in a different place, perhaps in a different format.

Lorex doesn't reinvent that. It gives you **one place** to author that context (a Markdown file called a skill), and then projects it into every agent's native format automatically.

You write it once. Every tool picks it up.

---

## You don't have to start from scratch

Skills can be installed from a team registry or any public Git repository. If your organization already has a registry, one command gets you everything your teammates use. If not, community tap sources let you pull in skills for popular frameworks, tools, or workflows that others have already written.

```bash
lorex tap add https://github.com/org/skills  # connect any public repo as a skill source
lorex install                          # browse and install from your registry or public skill source
```

## For solo developers

You write a skill (or ask your AI to write it for you). From that point on, every AI session in this project starts with that context already loaded. No more copy-pasting your conventions into every chat.

## For teams

Connect a Git repository as your team's skill registry. Publish a skill once and every developer on the team can install it with `lorex install`. When the skill evolves, `lorex sync` updates everyone. Skills go through the same Git review process as your code — PRs, diffs, history.

## For open-source projects

Commit your skills to the repo. Any contributor who has Lorex installed just runs `lorex refresh` after cloning and their AI agent immediately understands your project's conventions — no setup, no explanation needed.

---

That's the core of it. Ready to try it?

[Get Started](/guide/getting-started)
