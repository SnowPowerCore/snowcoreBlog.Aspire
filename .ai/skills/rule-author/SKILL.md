---
name: rule-author
description: >-
  How-to for authoring a NEW rule file in snowcoreBlog — frontmatter contract
  (description + single-line inline globs), the rule body shape (Use-this-rule
  intro, Service Context, numbered rules with Why, Red Flags, Evidence),
  verification requirements, and the sync/registration steps afterwards. Use
  when asked to create a rule, add a rule for a new domain area, or when an
  existing rule should be split.
scope: project
---

# Rule Author

Use this skill to create a NEW `.ai/rules/<name>.md` file.

## When to Invoke

- "create a rule for X", "codify this convention", "agents keep doing Y wrong"
- Splitting an oversized rule into focused ones

## When NOT to Invoke

- Updating an EXISTING rule — use the `update-rules-skills-doc` skill
- Style/lint rules — `lint-rule-manager` owns that three-tier system
- Flow process rules — `git-flow.md` already exists; extend that instead of
  a parallel file

## Background

Rules are always-or-scoped instruction files loaded by Copilot
(`.github/instructions/<name>.instructions.md`, frontmatter `applyTo`) and
Claude (`.claude/rules/<name>.md`, frontmatter `paths`). They are generated
from `.ai/rules/<name>.md` by the sync script — see the
`update-rules-skills-doc` skill for the sync contracts (inline globs, mirror
behavior, commit policy).

## Step 1 — Verify the practice exists

A rule codifies what the repo DOES, not what it should do. Before writing:
find 2+ real code sites demonstrating the convention (grep + open them); if
the practice is inconsistent or you are unsure it is intentional, ASK the
developer (interactive question flow) instead of codifying a guess.

## Step 2 — Name + frontmatter

File: `.ai/rules/<kebab-name>-rules.md` (domain rules carry the `-rules`
suffix; process rules like `git-flow` and global/style rules
like `caveman-style`/`code-style` go unsuffixed). Frontmatter
contract — INLINE globs array, single line:

```yaml
---
description: <What it governs> — <concrete service/feature names>
globs: ["snowcoreBlog.Backend/**/*.cs", "snowcoreBlog.Backend/**/*.razor"]
alwaysApply: false
---
```

`globs` must match the real paths the rules discuss (they decide when Copilot
loads the file). Global rules use `globs: ["**"]` — or `globs: []`, which the
sync converts to `applyTo: "**"` / `paths: - "**"`. NOTE: the sync script never
reads `alwaysApply` and strips it from mirrors — an always-apply rule authored
with `alwaysApply: true` but NO globs line yields a frontmatter-less mirror
(same failure as the YAML-list trap). The globs line drives the mirrors;
`alwaysApply` survives only in the source for raw readers.

## Step 3 — Body shape (match the existing exemplars)

1. `# <Title>` + one-line "Use this rule when…" intro.
2. `## Service Context` — bullets naming the services/projects/versions involved.
3. Numbered rules. Each states the INVARIANT, shows a minimal verified
   snippet where it helps, and ends with a **Why:** line.
4. `## Red Flags` — observable symptoms reviewable in a diff.
5. `## Evidence` — real file paths (relative to repo root) backing every claim.

Domain rules follow all five parts. Process rules (`git-flow`) use a lighter
intro + sections shape — do not retrofit Service
Context/Red Flags/Evidence onto them.

## Step 4 — Verification bar

Open every path you cite; run every claim against the current code. Rules with
invented practices are worse than no rule. Cross-reference sibling rules in
plain Markdown links (`](git-flow.md)`) — the sync rewrites them for Copilot.

## Step 5 — Register

Run the sync (`.ai\sync-ai-tools.cmd` / `bash .ai/sync-ai-tools.sh`), confirm
`Done. N items synced.`, and spot-check
`.github/instructions/<name>.instructions.md` exists with the expected
`applyTo`. Update `AGENTS.md` if a section should point at the new rule. Full
post-steps: the `update-rules-skills-doc` skill.

## Red Flags

- YAML-list `globs` (mirror gets no frontmatter).
- A rule that restates `.editorconfig`/lint behavior (that is code-style +
  lint-rule-manager territory).
- Aspirational rules ("the team should…") without code evidence.
- Duplicating a rule that exists (search `.ai/rules/` first).

## Reference implementations

- Domain rule shape: `.ai/rules/endpoint-design-rules.md`, `.ai/rules/blog-content-rules.md`
- Process rule shape: `.ai/rules/git-flow.md`
- Sync/registration workflow: `.ai/skills/update-rules-skills-doc/SKILL.md`
