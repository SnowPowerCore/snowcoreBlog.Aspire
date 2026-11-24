---
name: update-rules-skills-doc
description: >-
  Procedural workflow for updating existing skills, rules, and documentation in
  snowcoreBlog so they match the current code. Use when: code changes make an
  existing `.ai/skills/*/SKILL.md`, `.ai/rules/*.md`, `AGENTS.md` section, or
  `docs/*.md` stale (paths, type names, routes, behavior contracts, trigger
  text, frontmatter), when a rule/skill must be renamed or deleted, or when
  mirrors in `.claude/` / `.github/` must be regenerated after `.ai/` source
  changes. Covers impact detection, source-first editing, the inline-globs
  frontmatter contract, sync, and mirror verification.
scope: project
---

# Update Rules, Skills and Docs

Use this skill to revise existing `.ai/` instructions and documentation so they
match the current code and still trigger at the right time.

## When to Invoke

- Code changes make an existing rule or skill stale: cited file paths, type
  names, routes, step lists, endpoints, or behavior contracts no longer match.
- A rule's `description`/`globs` or a skill's trigger text no longer matches
  the code paths it should fire on.
- A new rule/skill/doc was created (or one renamed/deleted) and mirrors must be
  regenerated + cross-references updated.
- `AGENTS.md` sections or `docs/*.md` (e.g. `docs/UnitTesting.md`) drifted from
  reality — AGENTS.md explicitly requires same-changeset updates ("Keeping this
  file current").
- Mirrors under `.claude/` / `.github/` need regeneration after `.ai/` edits.

## When NOT to Invoke

- Lint/style-rule additions or severity changes — use the `lint-rule-manager`
  skill instead (it owns the `.editorconfig` + `code-style.md` + references
  three-tier sync).
- One-off code fixes with no instruction impact.
- Never hand-edit the mirrors (`.claude/**`, `.github/instructions/**`,
  `.github/skills/**`) — they are generated and the next sync overwrites the
  edit silently. The `.ai/` source is the contract.

## Background: how instruction sync works here

`.ai/` is the single source of truth (see AGENTS.md "Authoring rules and
skills"). `.ai\sync-ai-tools.cmd` (Windows) / `bash .ai/sync-ai-tools.sh`:

- **Rules** (`.ai/rules/<name>.md`) → `.claude/rules/<name>.md` (frontmatter
  `globs:` → `paths:`) and `.github/instructions/<name>.instructions.md`
  (`globs:` → `applyTo:`, peer links `](name.md)` rewritten to
  `](name.md.instructions.md)`). ALL mirror rule `.md` files are deleted and
  regenerated every sync — deletions/renames of rules clean up automatically.
- **Skills** (`.ai/skills/<name>/`) → copied VERBATIM to `.claude/skills/<name>/`
  and `.github/skills/<name>/` (with their `references/` folders).
- `.ai/rules/references/` (shared folder) is copied to both rule mirrors.
- Output ends with `Done. N items synced.`.

Two mechanical contracts the sync script imposes:

1. **Rule frontmatter `globs` MUST be a single-line inline array** —
   `globs: ["snowcoreBlog.Backend/**/*.cs"]`. The script's parser reads only
   the inline value; a YAML list (`globs:` + indented `- item` lines) silently
   writes the mirror with NO frontmatter block at all — the rule never matches
   any path. `globs: []` means always-apply (`**`).
2. **Deleting or renaming a SKILL leaves its old mirror folders behind on
   Windows/Git Bash** (copy mode prunes only mirror entries whose name matches
   a current source entry). Remove `.claude/skills/<old-name>/` and
   `.github/skills/<old-name>/` manually in the same change. On macOS/Linux
   (symlink mode) skill renames/deletions self-clean — every symlink targeting
   `.ai/skills` is pruned and recreated each sync. Rule renames/deletions
   self-clean on BOTH platforms (all mirror rule `.md` files regenerate).

Hooks `.githooks/post-checkout` + `post-merge` re-run the sync on branch
switches/pulls — but only when `git config core.hooksPath .githooks` is set,
and IDE-driven git operations bypass hooks: after IDE branch switches that
touched `.ai/`, run the sync manually.

## Ordered Touch-Points

### 1. Map the changed code to affected instructions

Start from the CODE that changed, never from the old instruction text.
`grep` `.ai/rules/` and `.ai/skills/` for the renamed/moved/changed symbols,
paths, routes, and step names; check `AGENTS.md` sections and `docs/*.md` too.
A change affecting a recurring workflow invalidates EVERY rule/skill teaching
that workflow in the same pass.

### 2. Update rule frontmatter

```yaml
---
description: <What it governs> — <concrete service/feature names>
globs: ["<real path glob>"]       # inline array ONLY (see contract above)
alwaysApply: false                # true only for genuinely global rules
---
```

`description` names concrete services/features (not topic labels); `globs`
cover the real paths the rules talk about.

### 3. Update skill frontmatter

```yaml
---
name: <skill-name>
description: >-
  <What it does>. Use when: <exact type/route/task verbs an agent will search
  for>. Covers <the guarantee>.
scope: project
---
```

`Use when:` must contain the exact searchable names — paraphrases do not
trigger reliably.

### 4. Realign bodies with the current code

Rules keep the shape: intro "Use this rule when…", Service Context bullets,
numbered rules each ending in a **Why**, Red Flags (observable review
symptoms), Evidence (real paths). Skills keep: "Use this skill to…", When to
Invoke / When NOT to Invoke, Background, ordered steps with verified code,
Reference implementations (real paths).

Hard quality bar (repo convention, enforced by review):
- Every cited path must exist in the workspace and every snippet must match
  the current code — open the files before writing; never carry a snippet over
  from the old text without re-verifying it.
- No invented practices: if the repo does not do it, it does not go in.
- Rules state invariants that survive the next change; gotchas describe silent
  failures that still compile.
- Peer references between rules use plain relative links
  (`see the `git-flow` rule` or `](git-flow.md)`) — the sync converts them for
  Copilot.

### 5. Realign docs and AGENTS.md

`docs/*.md` and the relevant `AGENTS.md` sections go in the SAME changeset.
AGENTS.md is edited directly (no sync involvement); its "Keeping this file
current" section makes stale-section updates mandatory, not optional.

### 6. Sync and verify mirrors

```powershell
.\.ai\sync-ai-tools.cmd        # or: bash .ai/sync-ai-tools.sh
```

Confirm output ends `Done. N items synced.`, then spot-check: the mirror file
exists and STILL HAS a frontmatter block (missing block = the inline-globs
contract was violated), `paths:` / `applyTo:` show the expected globs, and
peer links inside `.github/instructions/` use `.instructions.md`. If you
deleted/renamed a skill on Windows/Git Bash, delete the stale mirror folders
yourself (contract 2).

**Committing:** commit the `.ai/` source only. Mirrors under `.claude/**` and
`.github/**` are gitignored (root `.gitignore`) — never `git add` them; they
regenerate locally via sync/hooks on every clone.

## Red Flags

- Editing a mirror file and considering the job done.
- YAML-list `globs` frontmatter (silently empty mirrors).
- Descriptions without concrete searchable names ("stuff about articles").
- Evidence/Evidence-section paths that were not opened this session.
- A skill deletion without mirror cleanup (Windows/Git Bash copy mode).
- `git add`ing mirror directories (gitignored — local artifacts only).
- AGENTS.md left stale while rules/skills changed underneath it.

## Reference implementations

- Rule shape: `.ai/rules/endpoint-design-rules.md`, `.ai/rules/auth-and-token-rules.md`
- Skill shape: `.ai/skills/admin-auth-author/SKILL.md`, `.ai/skills/article-content-author/SKILL.md`
- Specialized deferral: `.ai/skills/lint-rule-manager/SKILL.md`
- Sync scripts (the contract source): `.ai/sync-ai-tools.ps1`, `.ai/sync-ai-tools.sh`
- Direct-edit doc: `AGENTS.md`; long-form doc example: `docs/UnitTesting.md`
