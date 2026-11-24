---
name: lint-rule-manager
description: >-
  Manages lint rules across the project's three-tier code style system. Supports
  three operations: add a new rule, change severity of an existing rule, or remove
  a rule. Updates .editorconfig, .ai/rules/code-style.md, and
  .ai/skills/lint-rule-manager/references/code-style-best-practices.md in sync.
  Use when: the user wants to enforce a new code style rule, promote a best-practice
  rule to enforced, change the severity of an existing rule, or remove a rule that
  is no longer needed.
---

# Lint Rule Manager

Manage lint rules across the project's three-tier code style system. This skill
ensures `.editorconfig`, `code-style.md`, and `code-style-best-practices.md`
stay in sync for every change.

## Three-Tier System

```
.editorconfig              -- Source of truth (what tooling enforces)
.ai/rules/code-style.md   -- Authored AI guidance (generates native rule mirrors)
.ai/skills/lint-rule-manager/references/code-style-best-practices.md  -- Roadmap / reference catalog (what to adopt next)
```

All three files must stay in sync. Every operation in this skill touches all three.

## Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| **action** | Yes | One of: `add`, `change`, `remove` |
| **rule_id** | Yes | The diagnostic or rule ID (e.g., `IDE0044`, `CA2000`, `SA1500`, `CS8602`) |
| **severity** | For `add`/`change` | Target severity: `error`, `warning`, `suggestion`, or `none` |

## How CI Lints (Pipeline Reference)

The CI lint job runs on merge requests only (`allow_failure: true` —
failures show as warnings but do not block merging, though this will change in future).

**Pipeline steps:**
1. Resolve dependencies
2. Restore each `.csproj` individually (<command to recursively get children> → `dotnet restore` per project)
3. Three-dot git diff against the target branch to find changed `.cs` and `.xaml` files:
   `git diff --name-only --diff-filter=ACMR "FETCH_HEAD...HEAD" -- "*.cs" "*.xaml"`
4. Run lint **only on changed files**:
   `dotnet format <solution-name>.sln --no-restore --verify-no-changes --verbosity diagnostic --include <file1> --include <file2> ...`
5. If no `.cs` files changed, lint is skipped entirely.

**Local vs CI differences:**
- CI runs on Windows (PowerShell); local/agent commands use bash on macOS
- CI restores individual projects from internal NuGet feeds (requires VPN to `nexus.ert.com`); locally,
  restore each project the same way before running `dotnet format --no-restore`:
  `find src test -name "*.csproj" | xargs -I{} dotnet restore {}`
- CI scopes lint to changed files via git diff; locally, scope to your changed files
  or run solution-wide for research purposes
- `--no-restore` requires individual project restores first — run the `find ... | xargs dotnet restore` command above before invoking `dotnet format --no-restore`

## Shared Steps (All Operations)

These steps apply to every operation. Run them first before branching into the
specific workflow.

### Step 1: Research the Rule

1. Check if the rule exists in `.editorconfig` and note its current severity (if any).
2. Check `.ai/rules/code-style.md` for a matching entry.
3. Check `references/code-style-best-practices.md` for the rule in the
   reference tables.
4. If the rule is unknown, look it up in Microsoft docs:
   - IDE rules: `https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/ideXXXX`
   - CA rules: `https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/caXXXX`
   - CS rules: compiler warnings (nullable, etc.)
   - SA rules: `https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/stable/documentation/SAXXXX.md`
5. Summarize the rule for the user: what it does, why it matters, current state
   in the project.

## Workflow: Add a Rule

**Precondition**: The rule does NOT exist in `.editorconfig`. If it does, suggest
the `change` workflow instead.

### Step 2a: Decide Severity

If the user did not provide a severity, ask them which they want: `error`, `warning`,
`suggestion`, or `none`.

Remind the user that:
- CI only lints **files changed in the MR** — it will not flag pre-existing violations
  in untouched files, even at `error` severity
- Any file they touch that already violates the new rule **will** fail CI lint
- Severity controls diagnostic reporting; the lint job currently uses
  `allow_failure: true`, so even a failing lint job does not block merging.

Ask the user to confirm the severity before proceeding.

### Step 3a: Add to `.editorconfig`

Add the rule to the appropriate section:

- **Naming conventions** (`dotnet_naming_rule.*`): Under `# --- Naming Conventions ---`
- **Code quality diagnostics** (`IDE*`, `CA*`): Under `# --- Code Quality - Bug Prevention ---`
- **Formatting rules** (`csharp_new_line_*`, `IDE0005`): Under `# --- Formatting - Readability ---`
- **Nullable reference types** (`CS86xx`): Under `# --- Nullable Reference Types ---`

Format for diagnostic rules:

```ini
# <Brief description of what the rule does>
dotnet_diagnostic.<RULE_ID>.severity = <severity>
```

Format for style/formatting rules:

```ini
# <Brief description>
<property> = <value>:<severity>
```

### Step 4a: Add to `code-style.md`

Add the rule to `.ai/rules/code-style.md` in the matching section:

- **Naming Conventions (Enforced)**: For naming rules at error severity
- **Code Quality Rules (CI Enforced)**: For IDE/CA diagnostics
- **Formatting Rules (Enforced)**: For formatting rules
- **Nullable Reference Types (Warnings)**: For CS86xx rules

Add a row to the relevant table:

```markdown
| `<RULE_ID>` | <Description> | <Why it matters> |
```

Include a code example showing correct and incorrect usage:

````csharp
// <RULE_ID> - <Severity> locally, CI lint will fail
<incorrect code>  // <explanation>

// Correct
<correct code>
````

If the rule was listed under "Rules NOT Currently Enforced", remove it from
that section.

### Step 5a: Update Best Practices

In `references/code-style-best-practices.md`:

- If the rule is already in the reference tables, no change needed.
- If the rule is **not** in best-practices, add it to the appropriate table
  (IDE rules in section 3, CA rules in section 4, StyleCop rules in section 5).

### Step 6a: Verify

Check that the changed `.editorconfig` doesn't cause unexpected format violations.
Scope to the files relevant to the new rule:

```bash
dotnet format PolarisApp.sln --no-restore --verify-no-changes --diagnostics <RULE_ID>
```

This checks all files against just the new rule — matching what CI would flag on any
changed file. If you want to limit to specific files:

```bash
dotnet format PolarisApp.sln --no-restore --verify-no-changes --include <path/to/file.cs>
```

Report results. If violations exist, tell the user the count and let them decide
whether to fix now or in a follow-up MR.

---

## Workflow: Change a Rule

**Precondition**: The rule MUST already exist in `.editorconfig`. If it doesn't,
suggest the `add` workflow instead.

### Step 2b: Confirm the Change

Show the user:
- Current severity (from `.editorconfig`)
- Requested new severity

Warn if:
- Raising severity (e.g., `suggestion` → `warning`) — any file they touch that
  already violates the rule will now fail CI lint
- Lowering to `none` — suggest the `remove` workflow instead, which also
  cleans up documentation

### Step 3b: Update `.editorconfig`

Change the severity value for the existing rule. Do not move it to a different
section unless the rule category has changed.

### Step 4b: Update `code-style.md`

Update the matching entry:
- Change the severity description in the table or prose
- Update code examples if the severity change affects the guidance (e.g.,
  promoting from "not enforced" to enforced means removing it from the
  "Rules NOT Currently Enforced" section)

### Step 5b: Update Best Practices

No change typically needed -- best-practices documents all rules regardless of
enforcement status. Only update if the severity column in a reference table
needs correcting.

### Step 6b: Verify

```bash
dotnet format PolarisApp.sln --no-restore --verify-no-changes --diagnostics <RULE_ID>
```

Report results.

---

## Workflow: Remove a Rule

**Precondition**: The rule MUST exist in `.editorconfig`. If it doesn't, tell the
user -- nothing to remove.

### Step 2c: Confirm Removal

Tell the user:
- What the rule does (from Step 1)
- Current severity
- What removing it means (the rule will no longer be enforced; existing code
  may drift)

Ask the user to confirm before proceeding.

### Step 3c: Remove from `.editorconfig`

Remove the diagnostic line **and** its comment. For naming convention rules,
remove the full block (`dotnet_naming_rule.*`, `dotnet_naming_symbols.*`,
`dotnet_naming_style.*`). For style rules, remove the property line and comment.

### Step 4c: Remove from `code-style.md`

Remove the matching entry:
- Remove the table row
- Remove any associated code examples
- If the rule was the only entry in a section, remove the section header too

Consider adding the rule to the "Rules NOT Currently Enforced" section if it's
a convention the team still aspires to but can't enforce yet.

### Step 5c: Update Best Practices

The rule should **stay** in the best-practices reference tables -- it's still a
valid best practice even if not currently enforced. No removal needed.

If the rule was removed because it's fundamentally wrong for this project (not
just deferred), then remove it from best-practices too.

### Step 6c: Verify

```bash
dotnet format PolarisApp.sln --no-restore --verify-no-changes --include <a-changed-file.cs>
```

This should pass (removing a rule can't introduce violations). If it fails,
something else changed — investigate.

---

## Validation

After editing the `.ai` rule, run `bash .ai/sync-ai-tools.sh` (or
`.ai\sync-ai-tools.cmd` on Windows) to refresh native instruction mirrors.

A successful operation produces:
- `.editorconfig` reflects the intended state (rule added, changed, or removed)
- `code-style.md` matches `.editorconfig` with human-readable descriptions
- `code-style-best-practices.md` has the rule in its reference catalog
- `dotnet format PolarisApp.sln --no-restore --verify-no-changes` passes for the
    affected files (or fails with a known, reported violation count that the user
    has accepted)

## References

- `references/code-style-best-practices.md` -- Full rule catalog with `.editorconfig` template, IDE/CA/SA/CS rule tables, and XAML conventions. Consult during Step 1 (Research) to look up rule IDs, descriptions, and recommended severities.

Project files managed by this skill:
- `.editorconfig` -- Source of truth for tooling enforcement
- `.ai/rules/code-style.md` -- Authored AI guidance (mirrored for native tools)
- `references/code-style-best-practices.md` -- Reference catalog and roadmap
