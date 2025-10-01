```instructions
This repository is an Aspire-based monorepo for the snowcoreBlog sample platform. Use these notes to be productive immediately when authoring or changing code.

High-level architecture (read `Program.cs` and `*.csproj`):
- The top-level host is `snowcoreBlog.Aspire` (see `Program.cs`) which uses the Aspire SDK (Aspire.Hosting.*) to wire up local infra: Redis (cache), RabbitMQ, PostgreSQL, Local SES and YARP reverse proxy.
- Backend services live under `snowcoreBlog.Backend/BusinessServices` and `snowcoreBlog.Backend/Shared/Services` (IAM, Email, Push, AspireYarpGateway, etc.). Frontend projects are under `snowcoreBlog.Frontend/**` (Host, Client, ClientShared, WasmForDebugging).
- Services communicate via RabbitMQ (MassTransit), shared Postgres databases (added in `Program.cs` as `db-snowcore-blog-entities` and `db-iam-entities`), Redis cache, and HTTP via YARP.

Developer workflows (commands / tasks):
- Use the workspace VS Code tasks (labels: `build`, `publish`, `watch`) which call `dotnet build`, `dotnet publish`, and `dotnet watch run` against the frontend Wasm project respectively. You can run these from the Run Task palette.
- Typical CLI commands:
  - Run the Aspire host: `dotnet run --project ./snowcoreBlog.Aspire.csproj`
  - Build a specific project: `dotnet build ./path/to/project.csproj`
  - Run frontend in watch mode (matches workspace task): `dotnet watch run --project ./snowcoreBlog.Frontend/snowcoreBlog.Frontend.WasmForDebugging/snowcoreBlog.Frontend.WasmForDebugging.csproj`
  - Run tests (per-project): `dotnet test ./path/to/YourProject.Tests.csproj`

Project conventions and patterns to follow (observable in repo):
- Aspire conventions: `builder.AddProject<Projects.xxx>("name")` appears in `Program.cs`; projects are wired with `.WithReference(...)` and `.WaitFor(...)`—follow this pattern when adding new services/resources.
- Use Fody weaving (see `FodyWeavers.xml` and `Fody` references in `snowcoreBlog.Aspire.csproj`)—do not remove or duplicate weaver config without checking existing files.
- Microservices commonly reuse packages and patterns: MassTransit consumers, FastEndpoints, Marten, FluentValidation, and OpenTelemetry are used across services (see microservice-level `.github/copilot-instructions.md` under `snowcoreBlog.Backend/Shared/Services/*`).
- Public API / cross-service contracts live under the universal/PublicApi areas (see `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi`)—update these first when changing shared DTOs or endpoints.
- Projects sometimes set `IsAspireProjectResource` and other custom metadata in csproj; prefer matching existing project reference attributes.

Integration points and external systems to be aware of:
- RabbitMQ (MassTransit) — messaging backbone for events/commands between services.
- PostgreSQL — multiple logical databases (see `Program.cs` db registrations). Check each service for Marten or EF usage.
- Redis — used as cache and configured by Aspire builder in the host.
- YARP — ingress/reverse proxy configured in the host; routes are loaded from configuration (`ReverseProxy`).

Where to look for examples:
- Root wiring: `Program.cs` (how projects/resources are registered).
- Project list & packaging: `snowcoreBlog.Aspire.csproj` and the solution file `snowcoreBlog.Aspire.sln`.
- Fody / weaving: `FodyWeavers.xml` and `FodyWeavers.xsd` in repo root.
- Service-level instructions: `snowcoreBlog.Backend/Shared/Services/IMS/.github/copilot-instructions.md` and `.../Stripe/.github/copilot-instructions.md` — follow these microservice notes when editing those services.

Agent-specific guidance (do this automatically when suggesting edits):
- When adding/renaming DTOs used across services, update the PublicApi project and run a repo-wide build.
- Prefer reusing existing MassTransit consumer patterns found under `BusinessServices/*/` and `Shared/Services/*`.
- Avoid adding new direct database connections in frontend projects; prefer using the backend services or the Aspire host wiring.
- If you change project references or AddProject entries, update `Program.cs` wiring and ensure `.WithReference(...).WaitFor(...)` ordering stays consistent.

If something isn't discoverable in the codebase, ask for these clarifications before large changes:
- Which environment is authoritative for secrets/connection strings (user secrets vs environment)?
- Are there expected message contracts in RabbitMQ that other teams depend on?

Files to check when in doubt: `Program.cs`, `snowcoreBlog.Aspire.csproj`, `FodyWeavers.xml`, `snowcoreBlog.Backend/**`, `snowcoreBlog.Frontend/**`, and microservice README/Copilot files under `Shared/Services/*/.github/`.

Please ask if any area needs more detail or if you'd like me to extract additional examples (consumers, endpoints, or DTOs) from specific subfolders.
```
