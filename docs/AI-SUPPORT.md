# AI Support Guide — snowcoreBlog (Aspire monorepo)

This document is written to help both humans and AI agents implement features consistently in this repository.

Goals:
- Preserve architectural decisions (why things are wired the way they are)
- Maximize reuse of existing patterns and dependencies
- Reduce “new patterns per feature” drift

If you’re looking for the primary local wiring source of truth, start with the Aspire host:
- `Program.cs` (Aspire AppHost resource graph)
- `appsettings.Development.json` (YARP reverse proxy routes, dev ingress)
- `Directory.Packages.props` (central NuGet versions)

---

## 1) Repository map (where things live)

Top-level (this folder):
- `snowcoreBlog.Aspire/` (Aspire host / AppHost)
  - Owns local infra + service orchestration (Redis, RabbitMQ, Postgres, Local SES, YARP ingress)

Backend:
- `snowcoreBlog.Backend/BusinessServices/*` (domain-facing microservices)
  - Examples: `Articles`, `AuthorsManagement`, `ReadersManagement`
- `snowcoreBlog.Backend/Shared/Services/*` (cross-cutting / platform microservices)
  - Examples: `IAM`, `Email`, `Push`, `NotificationsManagement`, `AspireYarpGateway`, `RegionalIpRestriction`
- `snowcoreBlog.Backend/Shared/*` (shared backend libraries)
  - `Infrastructure` contains reusable instrumentation + repositories, middleware, utilities

Frontend:
- `snowcoreBlog.Frontend/*` (Blazor-based frontend projects)
- `snowcoreBlog.Frontend/snowcoreBlog.Universal/PublicApi` (typed HTTP clients and shared DTOs for frontend)

Console:
- `snowcoreBlog.Console/*` (console host + screens/steps)

Universal contracts:
- There are three copies of `snowcoreBlog.Universal/PublicApi` (under Backend, Frontend, Console).
  - They are currently structurally identical and should stay in sync.
  - When changing shared DTOs/contracts, update all affected copies (at minimum: Backend + Frontend).

---

## 2) Local runtime architecture (Aspire host)

### 2.1 Resources
The Aspire host (`snowcoreBlog.Aspire/Program.cs`) wires these resources:
- Redis (`cache`) + RedisInsight
- RabbitMQ (`rabbitmq`) + management plugin (image `masstransit/rabbitmq`)
- PostgreSQL (`postgres`) + PGWeb + persisted volume
- Local SES (`local-ses`) for local email testing
- YARP ingress (`ingress`) loading routes from configuration `ReverseProxy`

### 2.2 Logical databases
The host registers multiple logical databases on the same Postgres resource:
- `db-snowcore-blog-entities`
- `db-snowcore-blog-article-entities`
- `db-iam-entities`

Services connect using `builder.AddNpgsqlDataSource(connectionName: "...")` (and then Marten uses `.UseNpgsqlDataSource()`).

### 2.3 Service inventory & wiring intent
AppHost project wiring uses the Aspire pattern:
- `builder.AddProject<Projects.X>("name")`
- `.WithReference(resourceOrProject)` + `.WaitFor(resourceOrProject)`

Notable dependency edges:
- `backend-readersmanagement` waits for and references `backend-authorsmanagement`
- `frontend-apphost` waits for backend services (authors/readers/articles)
- `ingress` references backend services + `frontend-apphost` and enforces auth policies

---

## 3) HTTP ingress & routing (YARP)

The development ingress lives in `snowcoreBlog.Aspire/appsettings.Development.json` under `ReverseProxy`.

### 3.1 Route conventions
Routes are grouped by service and remove a service-specific prefix:
- `/api/articles/{**catch-all}` → backend articles (prefix removed: `/api/articles`)
- `/api/authors/{**catch-all}` → backend authors management
- `/api/readers/{**catch-all}` → backend readers management
- `/api/notifications/{**catch-all}` → backend notifications management

Frontend is mounted under:
- `/app/{**catch-all}` → `frontend-apphost` with `X-Forwarded-BasePath: /app`

### 3.2 Auth policy conventions
The AppHost sets YARP auth policies in code when building ingress:
- `regularReader` policy requires an authenticated user with claim `readerAccount == "true"`

In `ReverseProxy` routes, `AuthorizationPolicy` is set per route:
- Many “bootstrap” routes are `anonymous` (OpenAPI, scalar UI, antiforgery token, captcha challenge)
- Protected routes (e.g., `/api/readers/{**catch-all}`) can require `regularReader`

When adding a new route:
- Prefer following existing service prefixes (`/api/<service>/...`)
- Decide explicitly whether it is `anonymous` or protected
- If it needs a new policy, add it to the ingress `.WithAuthPolicies(...)` list

---

## 4) Backend microservice baseline (what “normal” looks like here)

Most backend services follow a consistent structure (see `BusinessServices/Articles/Program.cs` or `Shared/Services/NotificationsManagement/Program.cs`).

### 4.1 Web host setup
Common setup patterns:
- `WebApplication.CreateSlimBuilder(args)`
- `builder.Host.UseDefaultServiceProvider(... ValidateScopes/ValidateOnBuild ...)`
- Oakton: `builder.Host.ApplyOaktonExtensions()` then `await app.RunOaktonCommands(args)`
- `builder.WebHost.UseKestrelHttpsConfiguration()`
- `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`

### 4.2 JSON serialization (source-gen first)
This repo strongly prefers source-generated System.Text.Json metadata:
- `ConfigureHttpJsonOptions(... options.SerializerOptions.SetJsonSerializationContext())`
- FastEndpoints endpoints frequently call `SerializerContext(CoreSerializationContext.Default)`
- MassTransit + RabbitMQ config calls `ConfigureJsonSerializerOptions(... SetJsonSerializationContext())`
- Marten uses `UseSystemTextJsonForSerialization(... SetJsonSerializationContext())`

Practical guidance:
- When adding/renaming DTOs that cross project boundaries, expect to update the serialization context.
- If you see runtime serialization failures, check that the DTO type is included in the generated context chain.

### 4.3 HTTP endpoints: FastEndpoints + Swagger/Scalar
- FastEndpoints provides the HTTP API surface (`Endpoints/**`)
- Source generator discovered types are registered: `options.SourceGeneratorDiscoveredTypes.AddRange(<Service>.DiscoveredTypes.All)`
- OpenAPI is generated via `SwaggerDocument(...)`
- Dev experience: NSwag JSON at `/openapi/{documentName}.json` and Scalar UI configured in development

### 4.4 Multi-step request flows: MinimalStepifiedSystem
Complex workflows are composed from steps rather than large endpoint methods:
- `app.UseStepifiedSystem()`
- Endpoints can be decorated with stepified attributes and delegate pipelines
- Steps are registered in DI and placed under `Steps/**`

When implementing a workflow feature:
- Prefer creating reusable steps rather than duplicating validation/business logic.

### 4.5 Persistence: Marten on PostgreSQL
The typical persistence stack:
- Marten registered via `builder.Services.AddMarten(...)`
- Documents registered explicitly (`options.RegisterDocumentType<T>()`)
- Static codegen enabled (`options.GeneratedCodeMode = TypeLoadMode.Static`)
- Soft delete enabled (`options.Policies.AllDocumentsSoftDeleted()`)
- Sessions: commonly `.UseLightweightSessions()`

Repository pattern:
- Shared base repository exists: `snowcoreBlog.Backend.Infrastructure.Repositories.Marten.Base.BaseMartenRepository<TEntity>`
- Some services define their own repository interfaces under `Interfaces/Repositories/Marten/**`
- Some queries use “compiled query providers” (see shared interfaces under `snowcoreBlog.Backend.Core.Interfaces.CompiledQueries`)

### 4.6 Messaging: MassTransit over RabbitMQ
Standard MassTransit wiring:
- `builder.Services.AddMassTransit(...)`
- `busConfigurator.ConfigureHttpJsonOptions(... SetJsonSerializationContext())`
- `UsingRabbitMq((context, config) => { config.Host(builder.Configuration.GetConnectionString("rabbitmq")); config.ConfigureEndpoints(context); })`

Consumer guidance:
- Add consumers in a `Consumers` folder (pattern used in services like ReadersManagement)
- Register consumers with `AddConsumer<T>()` and ensure endpoint configuration is consistent

### 4.7 Auth & security primitives
Observed patterns:
- JWT bearer auth is used in some services via FastEndpoints.Security (`AddAuthenticationJwtBearer(...)`)
- Signing key is provided via configuration: `Security:Signing:User:SigningKey`
- Antiforgery is enabled for JSON calls: `UseAntiforgeryFE(additionalContentTypes: ["application/json"])`
- Altcha is used for captcha/attestation flows (e.g., ReadersManagement/Articles)

Guidance:
- Don’t invent new auth mechanisms per service; reuse the existing JWT + policy approach.
- If adding a new protected route, ensure the ingress policy and backend auth expectations align.

### 4.8 Observability: OpenTelemetry everywhere
Backend services commonly call:
- `builder.Services.AddOpenTelemetry().ConnectBackendServices()`

`ConnectBackendServices` currently wires:
- FastEndpoints instrumentation
- Redis instrumentation
- Npgsql instrumentation
- MassTransit diagnostic listener (`DiagnosticHeaders.DefaultListenerName`)
- Marten tracing source + metrics

When adding a new dependency with telemetry support:
- Prefer extending the shared OpenTelemetry extension rather than wiring ad-hoc per service.

---

## 5) Frontend integration patterns (what to reuse)

### 5.1 Typed HTTP clients via PublicApi + Apizr
The universal PublicApi projects provide typed API interfaces (e.g., `IArticlesApi`, `IReaderAccountManagementApi`) using Apizr.

Guidance:
- When adding a new backend endpoint that the frontend will call, add it to the appropriate Apizr interface in the PublicApi project.
- Prefer Apizr-managed APIs over hand-rolled `HttpClient` calls.

### 5.2 UI & state management
The repo central package list includes:
- `Microsoft.FluentUI.AspNetCore.Components` (UI)
- `TimeWarp.State` (state management)

Guidance:
- Prefer existing state patterns and avoid introducing another state framework.

---

## 6) Dependency management and “don’t duplicate” rules

### 6.1 Central package versions
This repo uses Central Package Management:
- `Directory.Packages.props` has the versions.

Rules:
- Don’t add explicit package versions into individual `.csproj` files.
- When adding a new shared dependency, add the version once in `Directory.Packages.props`.

### 6.2 Fody weaving
Fody + `ConfigureAwait.Fody` are used across projects.
- Don’t remove or duplicate Fody configuration without checking existing `FodyWeavers.xml`.

---

## 7) “Where do I implement X?” (fast lookup)

### Add a new backend HTTP endpoint
- Location: `<Service>/Endpoints/**`
- Prefer: FastEndpoints endpoint class + stepified process (if multi-step)
- Ensure: `SerializerContext(CoreSerializationContext.Default)` and consistent versioning/prefix conventions

### Add a new workflow step
- Location: `<Service>/Steps/**`
- Also: register the step with DI in that service’s `Program.cs`

### Add a new document type / persistence model
- Location: `<Service>/Entities/**` (or `Backend.Core/Entities/**` if truly shared)
- Also: register with Marten in `Program.cs` via `options.RegisterDocumentType<T>()`

### Add a new MassTransit consumer
- Location: `<Service>/Consumers/**`
- Also: register in `AddMassTransit(...)` and keep JSON context wiring consistent

### Add or change a cross-service DTO or shared API contract
- Location(s): `snowcoreBlog.*.Universal/PublicApi/**`
- Rule: keep the Backend + Frontend (and Console if used) copies in sync
- Expect: you may need to update serialization context generation references

### Add a new service behind ingress
- Add service project reference to `snowcoreBlog.Aspire.csproj` if not already present
- Wire it in `snowcoreBlog.Aspire/Program.cs` with `.WithReference(...)` and `.WaitFor(...)`
- Add `ReverseProxy` cluster + route entries in `snowcoreBlog.Aspire/appsettings.Development.json`

---

## 8) Testing patterns

The repo centrally references:
- xUnit + `Microsoft.NET.Test.Sdk`
- FluentAssertions
- NSubstitute
- bUnit (frontend)

Guidance:
- Add tests alongside the service/project you’re changing (matching existing `*.Tests` projects).
- Prefer FluentAssertions for readability.

---

## 9) Practical “AI agent” guardrails

If you’re using an AI agent to implement a feature request, these guardrails reduce churn:
- Prefer extending existing services rather than creating new microservices.
- Prefer the established stack: FastEndpoints + Marten + MassTransit + OpenTelemetry + Stepified.
- Keep YARP routes and auth policies explicit (don’t default to anonymous).
- Don’t bypass PublicApi when a change is meant to be consumed by other services or the frontend.
- Keep the three PublicApi copies in sync unless explicitly scoped otherwise.
