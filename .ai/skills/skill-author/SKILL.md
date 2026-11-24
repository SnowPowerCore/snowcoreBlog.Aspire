---
name: skill-author
description: >-
  How-to for authoring a NEW skill in snowcoreBlog — folder + SKILL.md
  frontmatter contract (name, searchable description, scope), the body shape
  (When to Invoke / When NOT to Invoke / Background / ordered steps with
  verified code / Reference implementations), verification requirements, and
  the sync/registration steps afterwards. Use when asked to create a skill or
  package a recurring workflow for agents.
scope: project
---

# Skill Author

Use this skill to create a NEW `.ai/skills/<name>/SKILL.md`.

## When to Invoke

- "create a skill for X", "package this workflow", "agents need a how-to for Y"
- A multi-step workflow with repo-specific touch-points recurs across tasks

## When NOT to Invoke

- Updating an EXISTING skill — `update-rules-skills-doc`
- Style/lint workflows — `lint-rule-manager`
- One-off tasks (write code directly; skills are for RECURRING workflows)

## Background

Skills are agent-invocable instruction packages: Copilot discovers
`.github/skills/<name>/SKILL.md` (synced verbatim from `.ai/skills/<name>/`),
Claude gets `.claude/skills/<name>/`. Unlike rules, skills are not scoped by
globs — the `description` decides when the model reaches for them, so it must
contain the exact names/verbs a task will mention.

## Step 1 — Verify the workflow is real

Map the touch-points by reading the actual code paths the skill will teach
(existing exemplar files, real step/endpoint/service names). If the workflow
varies by case or is not yet established, ASK the developer before codifying.
A skill that invents steps is worse than none.

## Step 2 — Folder + frontmatter

Folder `.ai/skills/<kebab-name>/` with `SKILL.md`:

```yaml
---
name: <kebab-name>                    # must match the folder name
description: >-
  <What it does>. Use when: <exact type/route/task verbs an agent will search
  for>. Covers <the guarantee>.
scope: project
---
```

Author-side conventions: `-author` suffix for creation workflows
(`admin-auth-author`, `token-pair-auth-author`), `-check`/domain nouns for
validators. Optional `references/` subfolder rides along in the mirror.

## Step 3 — Body shape (match the existing exemplars)

1. `# <Title>` + "Use this skill to…" one-liner.
2. `## When to Invoke` — concrete triggers (quote real names/routes/verbs).
3. `## When NOT to Invoke` — near-misses with pointers to the right skill/rule.
4. `## Background` — why the pattern exists, what breaks without it.
5. Ordered steps (`## Step N — <action>`), each with a VERIFIED snippet from
   the current code and the exact file it came from.
6. `## Red Flags` — silent failures that still compile.
7. `## Reference implementations` — real paths, relative to repo root.

## Step 4 — Verification bar

Open every cited file this session; run every snippet against the current
code; keep snippets minimal (changed lines + context, not whole files) and
mark abbreviated ones. Cross-reference sibling skills by name
(`see the `fastendpoints-endpoints` skill`). If unsure about a practice,
consult the developer (interactive question flow) rather than guessing.

## Step 5 — Register

Sync (`.ai\sync-ai-tools.cmd` / `bash .ai/sync-ai-tools.sh`), confirm
`Done. N items synced.`, check `.github/skills/<name>/SKILL.md` exists. On
Windows/Git Bash, remember mirror cleanup rules for later renames/deletions
(see `update-rules-skills-doc`). Update `AGENTS.md` if a section should point
at the new skill.

## Red Flags

- Frontmatter `name` ≠ folder name.
- Description without concrete searchable names ("various backend tasks").
- Steps referencing files not opened this session.
- A skill duplicating an existing one (search `.ai/skills/` first — e.g.
  endpoint/step mechanics live in `fastendpoints-endpoints` /
  `stepified-process-handlers`, not in every new skill).
- Aspirational workflows the repo does not actually perform.

## Reference implementations

- Creation-workflow shape: `.ai/skills/admin-auth-author/SKILL.md`, `.ai/skills/token-pair-auth-author/SKILL.md`
- Infra/shape reference: `.ai/skills/tickerq-integration/SKILL.md`, `.ai/skills/shared-service-author/SKILL.md`
- Update workflow: `.ai/skills/update-rules-skills-doc/SKILL.md`
- Deferral exemplar: `.ai/skills/lint-rule-manager/SKILL.md`
