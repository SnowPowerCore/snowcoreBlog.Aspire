---
description: Code organization & infrastructure best practices applied while authoring — one type per file, buffer types (Span/Memory), null-less logic (no nullable fields/properties, no null returns — IMaybe/DataResult instead, safe non-null defaults), representation choices at code-generation time
globs: ["**/*.cs", "**/*.razor"]
---

# Code Organization & Infrastructure Best Practices

Use this rule when writing or reviewing new code and infrastructure: it concentrates the
practices that decide *how code is organized and what representations it uses* — not pure
C# syntax (those live in [code-style.md](code-style.md)).

## Principle: decide at code-generation time

Representations and organization are chosen while writing the code — not as a later
refactor pass. Defaulting to the convenient form first and "optimizing later" bakes
decisions in that are expensive to unwind; agents and reviewers must make the choice at
authoring time.

## 1. Buffer Types — Span & Memory first

Decide the buffer representation while writing new code — not as a later refactor:

| Representation | Use for |
|----------------|---------|
| `ReadOnlySpan<T>` / `Span<T>` | Synchronous, stack-lifetime access: parsing, slicing, searching |
| `ReadOnlyMemory<T>` / `Memory<T>` | Buffers held across `await` boundaries or stored in fields, async I/O overloads |
| `T[]` | Only when ownership, mutability, or storage beyond the call frame is genuinely required |
| `string` / string APIs | Only when the API requires `string`; parse with `AsSpan()` instead of `Substring` |

```csharp
// Incorrect - copies before parsing
var code = input.Substring(start, length);
if (int.TryParse(code, out var value)) { }

// Correct - zero-copy span slice
if (int.TryParse(input.AsSpan(start, length), out var value)) { }
```

**Why**:
- Spans slice without copying (`text.AsSpan(start, len)` instead of `Substring`), avoiding
  allocations on hot paths.
- `Memory<T>` exists precisely for spans that must cross `await` or be stored as fields.
- Defaulting to arrays/strings first and "optimizing later" bakes allocation churn in —
  the representation is chosen at code-generation time.

Pair with pooling for larger or repeated buffers (`ArrayPool<byte>.Shared.Rent` / `Return`),
as the existing encoders do.

## 2. One Type Per File

A physical `.cs` file holds **exactly one top-level type** — `class`, `record`,
`struct`, `enum` or `interface`. Never write a second top-level type into an
existing file; create a new file named after the type instead.

```csharp
// Incorrect - two top-level types in Options/ExternalAuthenticationOptions.cs
public sealed class ExternalAuthenticationOptions { /* ... */ }
public sealed class ExternalProviderOptions { /* ... */ }

// Correct - ExternalProviderOptions lives in its own
// Options/ExternalProviderOptions.cs, named after the type
public sealed class ExternalProviderOptions { /* ... */ }
```

**Why**:
- File-per-type keeps navigation, git history/blame and code review honest — a
  diff to `DonationFailed.cs` is a diff to one message contract, not to a mixed
  contract grab-bag.
- The `PublicApi`/`Universal` mirrors are per-consumer copies; a mixed file
  drifts between copies easily, while a file named after its type is trivially
  diffable across the three mirrors.
- Discoverability: every file in `PublicApi/BusinessObjects/Dto` is findable by
  type name (`Ctrl+P`, `AdminInviteDto.cs`) instead of remembering which
  plural "Dtos" file holds it.

Mechanics:
- The file name matches the type: `PaymentRefunded.cs` contains
  `PaymentRefunded` and nothing else at top level.
- Generic types cannot carry `<T>` in a file name — use the `OfT` suffix
  (`UserServiceOfT.cs` for `UserService<TSignInPayload>`).
- A `private` (or `private protected`) nested helper class owned by exactly one
  outer type stays nested — it is not a second top-level type.
- `file`-scoped types (`file class`) are single-file by definition; when a split
  forces one out, widen to `internal` in its own file and note it in the MR.
- Splitting never changes the namespace: same namespace means no `using` or
  call-site edits — references keep resolving. Marten / System.Text.Json /
  MemoryPack / MassTransit contracts are keyed by namespace + type name, so a
  file split is serialization- and contract-neutral.
- A DTO/request/result type never shares a file with its endpoint, step,
  service or validator — each goes to its own layer folder.

## 3. Null-less Logic — no nullable fields, no null returns, no null defaults

Nullability is a representation choice made while writing the code. New code declares no
nullable reference-type fields/properties and never returns `null` — absence and failure
are modeled explicitly with the result types the repo already has:

| Situation | Instead of | Use |
|---|---|---|
| Field/property that may "not have a value yet" | `public string? NickName { get; set; }` | Infer the best safe non-null default and initialize it: `= string.Empty;`, `= [];`, `= new();` |
| Method reporting failure by returning null | `return null;` | `IMaybe<T>` — `Maybe.Create(result)` / `XError<T>.Create(...)` (see the `mayberesults-used` skill) |
| Cross-service message reporting failure | `Value = null` and a caller-side prayer | `DataResult<T>` payload (`IsSuccess` / non-empty `Errors`) |
| Lookup missed / "not found" | `null` return | `IMaybe<T>` with a typed error; the endpoint decides 404-vs-400 |
| Collection result with no items | `return null;` | the empty collection — `[]` |
| Optional parameter | `void M(string? opt = null)` | an overload, or a non-null sentinel the domain defines |

```csharp
// Incorrect - nullable fields, null defaults, null as the failure path
public string? EffectiveNickName { get; set; }
public IReadOnlyList<string>? Permissions { get; set; } = null;

StepResult? ProcessAsync(string input) => validated
    ? new StepResult(input)
    : null;

// Correct - non-null fields with inferred safe defaults, Maybe result
public string EffectiveNickName { get; set; } = string.Empty;
public IReadOnlyList<string> Permissions { get; set; } = [];

IMaybe<StepResult> ProcessAsync(string input) => validated
    ? Maybe.Create(new StepResult(input))
    : ProcessFailed<StepResult>.Create("Validation failed.");
```

**Why**:
- A nullable field pushes the "is it set?" question onto every consumer forever; a safe
  non-null default answers it once, at the declaration site, and deletes the guard code.
- `null` returns are an untyped failure channel — no message, no code, and no
  compile-time forcing of the check. `IMaybe<T>` / `DataResult<T>` carry the failure
  information and the compiler forces the unwrap.
- Nulls escaping to the serialization/frontend boundary surface as runtime NREs or as
  CS86xx `!` suppressions downstream; non-null defaults keep the nullable-warning budget
  at zero so real warnings stay visible.

Mechanics:
- Pick the default from type and role, in this order: empty value (`string.Empty`, `[]`,
  `new()`), a neutral domain value (first enum member, empty key), then restructure
  (`required` members, constructor parameters) when no non-null default is honest.
- Normalize at boundaries: config, HTTP input and deserialization may hand null —
  coalesce immediately (`?? string.Empty`, `?? []`) on the boundary line and keep the
  rest of the flow non-null. This is the one place a `null` literal may appear in new
  code.
- Null checks that remain (boundary defense, external values) follow the
  [code-style.md](code-style.md) pattern: `is default(<type>)` / `is not default(<type>)`.
- Other absence cases: "not yet computed" → compute eagerly or hold a factory — not a
  nullable field; "provider/config absent" → an empty options object with safe defaults;
  "optional behavior" → a no-op implementation, not a null.
- The only nullable members allowed: envelope/transport records whose wire contract
  models absence explicitly (`DataResult<TValue>.Errors` — success is `Errors == null`)
  and members a framework forces; state the reason in the MR.

## When Writing New Code

1. **Buffer Types**
   - Choose `Span<T>` / `Memory<T>` representations at code-generation time (see
     "Buffer Types" above) — parse and slice with `AsSpan()`, not `Substring`
2. **One Type Per File**
   - New type → new file named after it (generic types via the `OfT` suffix);
     never append a type to an existing file
3. **Null-less Logic**
   - No nullable reference-type fields/properties — infer and initialize the best safe
     non-null default (`string.Empty`, `[]`, `new()`); never `= null`
   - Methods never return `null` — failures and not-found go through `IMaybe<T>` /
     `DataResult<T>`; collections return empty; no `= null` parameter defaults

## Red Flags

- `Substring` / string concatenation used for parsing where `AsSpan()` slicing would do.
- `byte[]` allocated per call inside a hot path that could `stackalloc` or pool.
- A `Span<T>` / `ReadOnlySpan<T>` forced across an `await` (does not compile — use
  `Memory<T>`).
- Buffers copied "for safety" without an ownership reason.
- A second top-level `class`/`record`/`struct`/`enum`/`interface` appended below
  the file's primary type (the classic "extension class + marker record +
  options class" pile-up).
- Plural grab-bag file names: `*Dtos.cs`, `*Events.cs`, `*Contracts.cs`,
  `*Options.cs` holding several types.
- An interface and its implementation (or a record and its derived exception)
  co-located "for convenience".
- `public string?` / `T?` field or property on a new type (outside the sanctioned
  envelope contracts) instead of a safe non-null default.
- `return null;` / `=> null;` as a failure or not-found path; a collection-typed method
  that can return null instead of an empty collection.
- Optional parameters defaulted to `null`.
- `?.` chains and `??` fallbacks smeared through business logic instead of normalizing
  once at the boundary.
- CS86xx warnings silenced with `!` instead of replaced by a non-null default.

## Evidence

- `snowcoreBlog.Backend/Shared/Infrastructure/Utilities/Base64UrlEncoder.cs` — span +
  stackalloc-threshold encode/decode over pooled buffers.
- `snowcoreBlog.Backend/Shared/Infrastructure/Processors/AltchaVerificationProcessor.cs`,
  `snowcoreBlog.Backend/Shared/Services/AspireYarpGateway/Utilities/JwtTokenCreator.cs` —
  ArrayPool rent with span decode.
- `snowcoreBlog.Frontend/snowcoreBlog.Frontend.Infrastructure/Extensions/CookieExtensions.cs` —
  span-based Set-Cookie parsing with `scoped` spans.
- `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi/Extensions/StringExtensions.cs` —
  span-based string helpers.
- `snowcoreBlog.Backend/Shared/Stripe.Core/Contracts/Stripe/DonationPaid.cs` /
  `DonationFailed.cs` — Stripe event contracts split one-per-file (was
  `DonationEvents.cs`); same shape for `PaymentPaid`/`PaymentFailed`/
  `PaymentRefunded` and `EnsureStripeCustomer`/`StripeCustomerEnsured`.
- `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi/BusinessObjects/Dto/AdminDto.cs`
  + ten sibling per-DTO files (`AdminInviteDto.cs`, `RevokeAdminInviteResultDto.cs`,
  …) — the `AdminManagementDtos.cs` grab-bag split identically into the Backend,
  Frontend and Console `Universal/PublicApi` mirrors in one changeset.
- `snowcoreBlog.Backend/Shared/Services/AspireYarpGateway/Resources/YarpResource.cs`
  + `Resources/ResourceLoggerProvider.cs` — resource class and its former
  `file class` logger provider split out of `YarpResourceExtensions.cs`
  (`file` widened to `internal`).
- `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Options/ExternalProviderOptions.cs`
  + `ExternalAuthenticationOptions.cs` — non-nullable option properties with
  `= string.Empty` / nested `= new()` safe defaults.
- `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Options/ReaderAccountTokenRequirementOptions.cs`
  — `= []` collection defaults; `AdminsManagement/Context/AcceptAdminInviteContext.cs`
  — step context with `= string.Empty` defaults, no nullable members.
- `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi/Utilities/DataResult/DataResult.cs`
  (mirrored in Frontend/Console `Universal` copies) — the sanctioned
  absence-modeling envelope; its nullable members (`Value`, `Errors`) are the wire
  contract, and business code stays null-less behind it.
- `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Steps/ReaderAccount/Request/CreateReaderAccountTempUserStep.cs`
  — step returning `IMaybe<T>` with typed `[None]` errors instead of null on any
  failure path.
