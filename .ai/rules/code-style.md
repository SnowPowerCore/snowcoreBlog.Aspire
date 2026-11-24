---
description: Code style conventions enforced by .editorconfig and CI lint check
globs: ["**/*.cs"]
alwaysApply: true
---

# Code Style Rules

This project enforces code style via `.editorconfig` and a CI lint job (`dotnet format --verify-no-changes`).

## Naming Conventions (Enforced)

### Private Fields
- **Pattern**: `_camelCase` (underscore prefix + camelCase)
- **Severity**: Error (CI will fail)

```csharp
// Correct
private readonly IService _service;
private string _userName;
private int _count;

// Incorrect - will fail lint
private readonly IService service;
private string userName;
private int m_count;
```

### Interfaces
- **Pattern**: `IInterfaceName` (I prefix + PascalCase)
- **Severity**: Error (CI will fail)

```csharp
// Correct
public interface IUserService;
public interface IRepository<T>;

// Incorrect - will fail lint
public interface UserService { }
public interface userService { }
```

## Code Quality Rules (CI Enforced)

These rules catch real bugs. They appear as **warnings** locally but are **enforced by the CI lint job**:

| Rule | Description | Why It Matters |
|------|-------------|----------------|
| `IDE0051` | Remove unused private members | Dead code clutters the codebase |
| `IDE0052` | Remove unread private members | Fields written but never read = logic bug |
| `CA1001` | Disposable fields need IDisposable | Prevents memory leaks |
| `CA2016` | Forward CancellationToken | Async operations can be cancelled |

```csharp
// IDE0051 - Warning locally, CI lint will fail
private void UnusedMethod() { }  // Remove this

// IDE0052 - Warning locally, CI lint will fail
private int _counter;
public void Increment() { _counter++; }  // _counter is never read

// CA1001 - Warning locally, CI lint will fail
public class MyService
{
    private readonly HttpClient _client = new();  // Must implement IDisposable
}

// CA2016 - Warning locally, CI lint will fail
public async Task DoWorkAsync(CancellationToken ct)
{
    await _service.ProcessAsync();  // Should pass ct here
}
```

## Null Check Pattern (Suggestion)

Null checks on reference types use the default-value constant pattern with the left-hand
side's type — not the equality operator, and not the bare `null` pattern:

| Rule | Description | Severity |
|------|-------------|----------|
| `IDE0041` | Bans `ReferenceEquals(x, null)` and the casted `(object)x == null` form | Suggestion |

```csharp
// Incorrect - equality operator or reference-equality method
if (entity == null) { }
if (list != null) { }
if (ReferenceEquals(entity, null)) { }

// Incorrect - bare null pattern (this project's form is default(T))
if (entity is null) { }
if (list is not null) { }

// Correct - default-value constant pattern with the left-hand side's type
if (_form is default(NotificationFormModel)) { }
if (_form is not default(NotificationFormModel)) { }
if (data is not default(List<AdminDto>)) { }
if (invite is default(AdminInviteEntity) || !invite.IsUsable) { }
```

**Why `default(T)`**:
- Constant patterns never invoke a user-defined `==` operator — the check compares the
  value against the type's default (`null` for reference types) with no surprise operator
  semantics.
- Nullable flow analysis recognizes the pattern fully (a dereference after
  `is not default(T)` produces no warning).
- Established codebase style.

**Scope**: reference types only. The compiler rejects `default(T)` constant patterns for
value types and `Nullable<T>` (CS9135) — those need their own checks.

**Apply while writing code, not in a later pass**: no analyzer flags a plain typed
`x == null` (a `==` operator may be custom), and `IDE0041`'s code fixer rewrites to the
bare `is null` form — if you accept such a fix, change it to `is default(T)`. The only
exception to the rule is a type whose custom `==` operator semantics are actually intended
(rare — leave a comment when used).

## Formatting Rules (Enforced)

### Indentation
- **Style**: Spaces (not tabs)
- **Size**: 4 spaces for C# files
- **Size**: 2 spaces for XML/YAML files (`.csproj`, `.yml`)

### Unused Usings
- **Rule**: `IDE0005`
- **Severity**: Warning locally, enforced by CI lint job
- Remove all unused `using` directives

### Brace Style (Allman)
- **Severity**: Enforced
- New line before all braces (`{`), `else`, `catch`, `finally`

```csharp
// Correct - Allman style
public void DoSomething()
{
    if (condition)
    {
        // code
    }
    else
    {
        // code
    }
}

// Incorrect - K&R style (will fail)
public void DoSomething() {
    if (condition) {
        // code
    } else {
        // code
    }
}
```

### Empty Type Bodies (Convention)
- A type with no members (marker class, attribute, serializer context, nested action
  record, empty interface) uses a **semicolon body** — never empty braces.
- Applies to `class`, `struct`, `interface`, `record`, and `enum` declarations.
- No analyzer backs this (`dotnet format` will not flag `{ }` on types) — apply at
  code-writing time; reviewers reject empty-brace bodies.

```csharp
// Correct - semicolon body
public class ApplicationLaunchResult;
public sealed class CheckpointedStepAttribute : Attribute;
public static class EventHandlers;
public interface IRepository<T>;
public record Positional(int A);

// Incorrect - empty braces
public class ApplicationLaunchResult { }
public sealed class CheckpointedStepAttribute : Attribute
{
}
```

**Scope**: type declarations only. Members are exempt — an empty method or property
body `{ }` must never become `;` (that would change its meaning to abstract/interface
form and break compilation). Braces are also kept when the body contains a comment.

## Nullable Reference Types (Warnings)

These rules surface null-safety issues as warnings (not errors yet):

| Rule | Description |
|------|-------------|
| `CS8602` | Possible null dereference |
| `CS8603` | Possible null return |
| `CS8604` | Possible null argument |

```csharp
// CS8602 warning - possible null dereference
string? name = GetName();
Console.WriteLine(name.Length);  // Warning: name might be null

// Fix with null check
if (name is not default(string?))
{
    Console.WriteLine(name.Length);
}
```

## Rules NOT Currently Enforced

These patterns exist in the codebase but are not enforced by lint:

### Constants
- Convention: Mixed (`UPPER_CASE` and `PascalCase` both used)
- Status: Not enforced (inconsistent patterns in codebase)

### Whitespace
- Trailing whitespace and final newlines are NOT enforced

## CI Enforcement

The `lint` job runs on merge requests (changed files only):
- Command: `dotnet format <solution-name>.slnx --no-restore --verify-no-changes --verbosity diagnostic --include [changed-files]`
- `dotnet format --verify-no-changes` treats **all** rule violations equally — both error-severity (naming) and warning-severity (IDE0051, CA2016, etc.) cause the job to fail with a non-zero exit code
- Current status: `allow_failure: true` in CI, so the job shows as an orange warning but does not block the pipeline by default. However, depending on the project's merge request settings (e.g., "Pipelines must succeed"), the lint failure may still prevent merging.
- All rules listed in this document — naming, code quality, formatting, and unused usings — are enforced equally by the lint job

## Adding New Code

When writing new C# code, always follow:

1. **Naming**
   - Use `_camelCase` for private fields
   - Use `I` prefix for interfaces
   - Use `Async` suffix for methods, which return `Task`

2. **Code Quality**
   - Remove unused private members
   - Implement `IDisposable` if class has disposable fields
   - Forward `CancellationToken` to async methods. In case `CancellationToken` should not be forwarded by purpose, forward `CancellationToken.None` or ask developer using interactive questions interface

3. **Formatting**
   - Use 4-space indentation
   - Remove unused `using` directives
   - Use Allman brace style (new line before `{`)

4. **Null Safety**
   - Handle nullable references properly (warnings will appear)
   - Use `is default(<type>)` / `is not default(<type>)` with the left-hand side's type for
     reference-type null checks — never `== null` / `!= null`, and never bare
     `is null` / `is not null`

## Related Documents

- [code-organization.md](code-organization.md) — code organization & infrastructure best
  practices applied at authoring time (buffer types — Span/Memory first, representation
  choices at code-generation time).
- [code-style-best-practices.md](../skills/lint-rule-manager/references/code-style-best-practices.md) —
  full `.editorconfig` template, StyleCop/CA analyzer details, XAML conventions, and
  rationale behind the enforced rules.
