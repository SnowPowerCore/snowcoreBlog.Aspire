# AGENTS.md -- snowcoreBlog

## First-time setup

After cloning, enable hooks with `git config core.hooksPath .githooks`, then run `bash .ai/sync-ai-tools.sh` (or `.ai\sync-ai-tools.cmd` on Windows) to sync AI skills/rules.

## What this is

snowcoreBlog is a self-hosted blogging platform built as an
Aspire-orchestrated distributed application (net10.0). One command brings up
the whole system:

- **Aspire AppHost** (this repo) — wires the containers (Redis with data
  volume, RabbitMQ + management plugin, PostgreSQL with six logical
  databases), the LocalSES email emulator, and every app service.
  `aspire.config.json` points at `snowcoreBlog.Aspire.csproj`.
- **YARP ingress** — single HTTPS entry point; route-level auth policies
  (`regularReader`, `regularAdmin`) whose claim/scheme literals must stay in
  sync with backend security constants.
- **Backend** — FastEndpoints microservices: Marten over PostgreSQL,
  MassTransit over RabbitMQ, Redis-backed sessions/refresh tokens, stepified
  request pipelines (MinimalStepifiedSystem).
- **Frontend** — public Blazor web app for readers and authors.
- **AdminFrontend** — Blazor admin portal (FluentUI, InteractiveServer),
  served under `/admin`.
- **Console** — RazorConsole terminal client.

## For AI agents

> **Do not read from `.ai/`.** It is the human authoring source — your native
> folder is auto-mirrored from it and may reshape files (e.g. rules are renamed
> to `*.instructions.md` for some tools).
>
> If a rule or skill referenced in this file is missing from your native folder,
> the sync has not run on this clone. Ask the user to run it rather than falling
> back to `.ai/`.

## Keeping this file current

AI agents must update **this file** whenever a change makes any part of it
stale — new/removed services or projects, build/test/lint commands, package
versions, layering conventions, container topology, or the `.ai/` harness
itself. Update it in the same changeset as the change; do not leave that to a
follow-up. Unlike rules and skills, `AGENTS.md` is edited directly — no sync
or mirror regeneration is needed.

## Multi-repo architecture

The root repo is the orchestrator; the products live in git submodules
(github.com/SnowPowerCore/*) pinned by SHA from `main`:

| Path | Repo | Contents |
|---|---|---|
| `snowcoreBlog.Backend` | snowcoreBlog.Backend | `BusinessServices/` (Articles, AuthorsManagement, ReadersManagement, AdminsManagement), `Shared/Services/` (IAM, Email, Push, Documents, Stripe, UrlShortener, ServiceNotifications, ApiAccessRestrictions, AspireYarpGateway, ...), `Shared/Shared.Core` (shared kernel) + per-service `Shared/<Service>.Core` projects (Articles.Core, ReadersManagement.Core, AuthorsManagement.Core, AdminsManagement.Core, ServiceNotifications.Core, Stripe.Core, Documents.Core, UrlShortener.Core, ApiAccessRestrictions.Core) |
| `snowcoreBlog.Frontend` | snowcoreBlog.Frontend | `Host`, `Client`, `ClientShared`, `Core`, `Infrastructure`, `Modules`, `WasmForDebugging` + per-area `*.Tests` |
| `snowcoreBlog.Console` | snowcoreBlog.Console | `App` (RazorConsole TUI) |
| `snowcoreBlog.App` | snowcoreBlog.App | mobile client (declared in `.gitmodules`; clone only when needed) |

`AdminFrontend` (single server-only Blazor Web App project — no WASM
client) lives **in this root repo**.

Cross-repo contracts to keep in mind:

- `snowcoreBlog.Universal` (shared kernel) and `snowcoreBlog.Universal/PublicApi`
  (service DTOs + endpoint contracts) are **duplicated per consumer repo**
  (Backend, Frontend, Console). A contract change must be mirrored into **all
  three** — the frontend compiles against its own copy.
- The root `.csproj` holds direct `ProjectReference`s into submodule projects;
  the gateway is referenced with `IsAspireProjectResource="false"`.
- `Gateway.Middleware`/`AspireYarpGateway` couple to `ApiAccessRestrictions`
  contracts; root `Program.cs` auth-policy literals mirror backend
  `SecurityConstants` by hand.
- **Backend consumes cross-service code as NuGet packages from the local feed.**
  `snowcoreBlog.Backend/pack-shared-packages.ps1` packs into
  `..\..\local-nuget-feed` (one level above the Aspire root, registered in the
  root `nuget.config` under `local`, mapped to the `snowcoreBlog.*` pattern):
  `snowcoreBlog.Backend.Shared.Core`, `snowcoreBlog.PublicApi`,
  `snowcoreBlog.Backend.Infrastructure`, `snowcoreBlog.ServiceDefaults`,
  `snowcoreBlog.ApplicationLaunch(.Implementations)`,
  `snowcoreBlog.Backend.{IAM,Email,AspireYarpGateway}.Core`.
  A microservice `PackageReference`s ServiceDefaults/PublicApi/Infrastructure,
  the ApplicationLaunch module and any cross-service `<Service>.Core`; the only
  remaining `ProjectReference`s are its own `<Service>.Core` (tests keep
  ProjectReferences too). One exception: `Gateway.Middleware` still
  ProjectReferences the full `ApiAccessRestrictions` service (the gateway runs
  its gateway-integration implementations in-process).
  Bump `Version` in `pack-shared-packages.ps1` + the `PackageVersion` entries in
  the root `Directory.Packages.props` **together** whenever a packed project
  changes, then re-run the pack script.

## First-time setup

New developer joining the project? Walk through these steps. The AI agent can help diagnose any step that fails — paste the error and ask.

### 1. Install prerequisites

- **.NET SDK 10** — `global.json` pins `10.0.100` (`rollForward: latestFeature`)
  and enables the Microsoft.Testing.Platform runner repo-wide.
- **Git (CLI)** — hooks and the AI-tools sync run from plain git operations.
- **Container runtime** — Docker Desktop or Podman; Aspire hosts Redis,
  RabbitMQ and PostgreSQL as containers.
- **PowerShell** (Windows) — the `.cmd` wrapper works without pwsh;
  PowerShell 7+ recommended.
- **IDE** — VS Code (repo carries `.vscode` build/debug tasks), Visual Studio
  or Rider.
- **NuGet** — `nuget.config` clears all sources except nuget.org; versions
  are centralized in `Directory.Packages.props` (CPM).

### 2. Clone all submodules

```bash
git submodule update --init --recursive
git submodule foreach --recursive 'git checkout main'
```

Submodules come back in detached-HEAD state; the second command moves each to
`main` so new branches start from the right commit. Verify:

```bash
git submodule status   # no leading '-' or '+' on any line
```

### 3. Enable git hooks (auto-sync of AI tools)

Hooks live in `.githooks/` and auto-sync `.ai/` into `.claude/` and `.github/` on every branch switch or pull. Enable them once:

```bash
git config core.hooksPath .githooks
```

Verify it was set:
```bash
git config --get core.hooksPath   # should print: .githooks
```

### 4. Run the AI-tools sync once manually

This populates `.claude/` and `.github/` so editors and AI agents see the rules and skills immediately.

**macOS / Linux**:
```bash
bash .ai/sync-ai-tools.sh
```

**Windows** (from cmd, PowerShell, or Windows Terminal):
```cmd
.ai\sync-ai-tools.cmd
```

Expected output ends with `Done. N items synced.` Skills use symlinks on macOS/Linux and copies on Windows; rules are generated files with tool-specific frontmatter.

### 5. Verify the setup

Run these checks and confirm each one succeeds:

```bash
# Hooks enabled
git config --get core.hooksPath              # -> .githooks

# AI tools synced
ls .claude/skills .claude/rules               # -> non-empty
ls .github/skills .github/instructions        # -> non-empty

# Build chain (requires VPN + sibling repos cloned)
dotnet restore <solution-name>.slnx
```

### Windows-specific gotchas

- **Line endings**: `.gitattributes` forces LF on `*.sh` and `.githooks/*` so they work under Git Bash. If you cloned before this file was added and hooks fail with cryptic errors, re-checkout with `git checkout --force HEAD` or run `git rm --cached -r . && git reset --hard` to re-apply line ending rules.
- **PowerShell execution policy**: The `.cmd` wrapper bypasses it. If you prefer running `pwsh .ai\sync-ai-tools.ps1` directly, you may need `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` once.
- **IDE git operations**: Visual Studio / Rider / some VS Code git operations bypass hooks. Run the sync manually after IDE-driven branch switches that touched `.ai/`.

## Build

Run the whole system through the AppHost:

```bash
dotnet run --project snowcoreBlog.Aspire.csproj   # or: aspire run / F5 in VS Code
```

This starts the containers, all backend services, both frontends and the YARP
ingress (admin portal under `/admin`). Startup order is declared with
`WaitFor(...)` in `Program.cs`.

Build a single repo solution when working inside one product:

```bash
dotnet build snowcoreBlog.Aspire.slnx                 # root (AppHost + AdminFrontend)
dotnet build snowcoreBlog.Backend/snowcoreBlog.Backend.sln
dotnet build snowcoreBlog.Frontend/snowcoreBlog.Frontend.sln
dotnet build snowcoreBlog.Console/snowcoreBlog.Console.sln
```

Frontend WASM debugging without the full stack uses
`snowcoreBlog.Frontend/snowcoreBlog.Frontend.WasmForDebugging` (the VS Code
build/publish tasks target it; see `.vscode/settings.json`).

## Testing

Framework: **xUnit v3** + **NSubstitute** + **FluentAssertions** (+ **bUnit**
for components) on the native Microsoft.Testing.Platform runner. Test methods
are named `MethodUnderTest_Scenario_ExpectedResult` and follow
Arrange-Act-Assert with `// Arrange` (when needed), `// Act`, `// Assert`
comments — see the **unit-test-author** skill and `docs/UnitTesting.md`.

Tests are run per-project, not solution-wide:

```bash
# Single test project
dotnet test <test-project>.csproj
```

## Linting

CI runs `dotnet format` on changed `.cs` and `.xaml` files in MRs:

```bash
dotnet format <solution-name>.slnx --verify-no-changes --include <files>
```

Currently `allow_failure: true` -- lint failures don't block the pipeline yet, but will in the future.

## Code style (enforced by .editorconfig)

Rules live in **`.ai/rules/code-style.md`** (synced into your tool's native
folder); the `lint-rule-manager` skill holds the full analyzer/template
reference. Highlights:

- 4-space indent (spaces, not tabs), UTF-8, trailing whitespace trimmed.
- Private fields `_camelCase`; interfaces `I`-prefixed — CI-enforced.
- CI quality rules (warnings locally, lint job fails on them): `IDE0051`,
  `IDE0052`, `CA1001`, `CA2016` — always forward your `CancellationToken`.
- Reference-type null checks use `is default(<type>)` / `is not default(<type>)`,
  never `== null` / `!= null`.
- One type per file — never append a second top-level class/record/struct/enum/
  interface to an existing `.cs` file; new type → new file named after it.
- Code organization & infrastructure best practices (one type per file, buffer
  types — Span/Memory first, null-less logic — no nullable fields/properties, no
  null returns (`IMaybe<T>`/`DataResult<T>` instead), safe non-null defaults,
  representation choices at code-generation time) live
  in **`.ai/rules/code-organization.md`**.

## Project layering rules

**Backend microservices** follow the gold structure (`ReadersManagement` is
the reference — do not restructure it):

```
Constants/ Context/ Delegates/ Endpoints/<Feature>[/<Op>] + Endpoints/Root (ping)
ErrorResults/ Extensions/ (+ Extensions/Startup/*ConfigurationExtensions)
Features/ (MassTransit consumers)   Interfaces/Services   Interfaces/Repositories/Marten
Models/ Options/ Repositories/Marten   RequestPersistence/   Services/   Steps/<Feature>[/<Op>]
```

- Endpoints are thin: build a step context, invoke the stepified delegate
  (`[StepifiedProcess]`), send. Steps implement
  `IStep<TDelegate, TContext, IMaybe<TResult>>` and are registered `AddScoped`.
- Simple flows return `IMaybe<Dto>` converted with `ToApiResponse`; error
  results are `[None] public partial record XError;` source-generated records.
- Repositories extend `BaseMartenRepository<TEntity>`; every entity/DTO the
  Marten serializer touches must be registered in `CoreSerializationContext`.
- Worker services (IAM, Email, Push) expose no business endpoints.
- Startup extension naming: `UseHostingConfiguration`,
  `UseServiceDefaultsConfiguration`, `AddHostingConfiguration`,
  `AddCoreServicesConfiguration`, `MapEndpoints`, `UseApplicationMiddleware`,
  `RunApplicationAsync`.

**Frontend** layering: `Core` (state/models) → `Infrastructure`
(HTTP/services) → `ClientShared` (pages/components) → `Client` (WASM entry)
→ `Host` (ASP.NET host); feature work goes through `Modules/`.
`AdminFrontend` is a single server-only project (`Components/Pages/`,
`InteractiveServer` pinned, `Extensions/AddAdminClient()` for FluentUI +
`AddAdminDataClients()` for the host-side Apizr managers) — no WASM client.

**Cross-cutting**: `snowcoreBlog.Universal` + `PublicApi` are duplicated per
consumer repo — mirror changes into all three (see Multi-repo architecture).

## Branching

For branch naming rules (allowed namespaces, release branch shape, personal dev handles), see **`.ai/rules/branch-naming.md`**.

For change flow rules, merge vs cherry-pick policy, and propagation guidelines, see **`.ai/rules/git-flow.md`**.

## Key dependencies

Versions are centralized in the root `Directory.Packages.props` (CPM):

| Concern | Packages |
|---|---|
| Orchestration | Aspire 13.6 (AppHost + Redis/PostgreSQL/RabbitMQ hosting), YARP ingress |
| HTTP APIs | FastEndpoints 8.3 (+ Security/Swagger), NSwag + Scalar |
| Persistence | Marten 9.x over PostgreSQL (Npgsql), Redis (StackExchange) |
| Messaging | MassTransit + RabbitMQ transport |
| Pipelines | MinimalStepifiedSystem (stepified processes), MaybeResults `[None]` error records, AspectInjector (checkpoint aspects) |
| Realtime/state | ActualLab.Fusion 15.x, TimeWarp.State 11.x, SSE step progress |
| UI | Microsoft FluentUI 4.13 (Blazor), RazorConsole (terminal TUI) |
| Mapping/serialization | Riok.Mapperly, MessagePack / MemoryPack, System.Text.Json source-gen contexts |
| Auth/security | Fido2 (passkeys), Otp.NET + QRCoder (TOTP), JwtBearer, Sodium, Altcha |
| Integrations | Stripe.net, SendGrid + AWS SES (LocalSES emulator), ntfy (NtfyCator), Markdig |
| Jobs/resilience | TickerQ 10.4 (**pinned** — last OSS-licensed release), Polly |
| Telemetry | OpenTelemetry (OTLP exporter; AspNetCore/Http/Runtime/Redis instrumentation) |
| Weaving | Fody + ConfigureAwait.Fody (all `await`s, repo-wide) |
| Testing | xunit.v3, NSubstitute, FluentAssertions, bUnit, coverlet |

## Authoring rules and skills

> AI agents should read from their native folder per [For AI agents](#for-ai-agents) above. When explicitly asked to author or update rules/skills, edit the `.ai/` source and regenerate the native mirrors.

`.ai/` is the **single source of truth** for project-wide rules and skills.
The sync script mirrors it into each tool's native folder so each agent reads
from the place its own tooling expects.

```
.ai/rules/<name>.md          authored here
.ai/skills/<name>/SKILL.md   authored here
        │
        │  (sync-ai-tools.sh / .cmd)
        ▼
.claude/rules/<name>.md                          Claude Code
.claude/skills/<name>/                           Claude Code + OpenCode
.github/instructions/<name>.instructions.md      GitHub Copilot
.github/skills/<name>/                           GitHub Copilot
```

Rule frontmatter is converted from `globs` to Claude `paths` or Copilot
`applyTo`. Copilot peer-rule links use `.instructions.md`; both tools also
receive the rule `references/` directory. Skills retain their source content.

### When to run the sync

Run the sync after **creating, renaming, or deleting** any file in `.ai/rules/` or `.ai/skills/`. Hooks (`post-checkout`, `post-merge`) run it automatically on branch switches and pulls if enabled — see [First-time setup](#first-time-setup) step 3.

**macOS / Linux** (creates symlinks):
```bash
bash .ai/sync-ai-tools.sh
```

**Windows** (creates copies; bypasses PowerShell execution policy):
```cmd
.ai\sync-ai-tools.cmd
```

### Editing rules

Edit files **only in `.ai/`**. The mirrors are regenerated on every sync — local edits to `.claude/`, `.github/`, etc. will be silently overwritten.

> **Windows + IDE users**: Hooks fire from CLI git operations only. Visual Studio's built-in Git, Rider's VCS, and some VS Code Git operations bypass hooks. Run `.ai\sync-ai-tools.cmd` manually after IDE-driven branch switches that touched `.ai/`.
