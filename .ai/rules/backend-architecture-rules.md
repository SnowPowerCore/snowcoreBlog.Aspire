---
description: Backend microservice architecture — gold folder structure, Program.cs startup extension order, worker services, serialization context registration, telemetry wiring
globs: ["snowcoreBlog.Backend/**/*.cs"]
alwaysApply: false
---

# Backend Architecture Rules

Use this rule when creating a new backend microservice, adding folders/projects
inside `snowcoreBlog.Backend`, wiring a service's `Program.cs`, registering
documents or DTOs for serialization, or deciding where a shared type belongs.

## Service Context

- **Business services** (domain-facing): `snowcoreBlog.Backend/BusinessServices/` — `Articles`, `AuthorsManagement`, `ReadersManagement`, `AdminsManagement`.
- **Shared/platform services**: `snowcoreBlog.Backend/Shared/Services/` — `IAM`, `Email`, `Push`, `Documents`, `Stripe`, `UrlShortener`, `ServiceNotifications`, `ApiAccessRestrictions`, `AspireYarpGateway`, ...
- **Shared code**: `Shared/Shared.Core` is the shared **kernel** (`BaseEntity`, `IRepository<T>`, compiled-query + hasher + step-checkpoint interfaces, auth/cookie constants, request-persistence entities) and `Shared/Infrastructure` holds base repositories, `CoreSerializationContext`, step-progress, durable-request persistence and OpenTelemetry extensions. Every service's domain-specific entities/events live in its **own `<Service>.Core` project** (`Shared/Articles.Core`, `Shared/ReadersManagement.Core`, `Shared/AuthorsManagement.Core`, `Shared/AdminsManagement.Core`, `Shared/ServiceNotifications.Core`, `Shared/Stripe.Core`, `Shared/Documents.Core`, `Shared/UrlShortener.Core`, `Shared/ApiAccessRestrictions.Core`), which references only the kernel.
- **Persistence**: Marten 9.x over PostgreSQL via Npgsql data sources (Aspire connection names). Most business services share `db-snowcore-blog-entities` (ReadersManagement, AuthorsManagement + platform ServiceNotifications/UrlShortener); some own dedicated DBs (`db-snowcore-blog-article-entities` for Articles, `db-admins-entities` for AdminsManagement); platform services own `db-iam-entities`, `db-documents-entities`, `db-ip-restrictions-entities`, `db-stripe-entities`.
- **Messaging**: MassTransit over RabbitMQ; **HTTP**: FastEndpoints 8.3; **pipelines**: MinimalStepifiedSystem 1.2.2.

## Architecture Rules

1. **Follow the gold folder structure inside every service** (see `ReadersManagement`): `Constants/ Context/ Delegates/ Endpoints/<Feature>[/<Op>]/` + `Endpoints/Root/` (ping), `ErrorResults/`, `Extensions/` (+ `Extensions/Startup/*ConfigurationExtensions.cs`), `Features/` (MassTransit consumers), `Interfaces/Services/`, `Interfaces/Repositories/Marten/`, `Options/`, `Repositories/Marten/`, `RequestPersistence/`, `Services/`, `Steps/<Feature>[/<Op>]/`.
   Why: the whole backend is navigated and wired by convention; a deviating layout hides registration points from both humans and tooling.

2. **`Program.cs` must be a flat chain of the named startup extensions, in this order** (verified in `ReadersManagement/Program.cs`):
   ```csharp
   var builder = WebApplication.CreateSlimBuilder(args);
   builder.UseHostingConfiguration();                       // DI validation only
   builder.UseServiceDefaultsConfiguration();               // KestrelHttps + AddServiceDefaults
   builder.Services.AddOpenTelemetryConfiguration();        // wraps ConnectBackendServices()
   builder.AddNpgsqlDataSourceConfiguration("db-<context>");
   builder.Services.AddMartenConfiguration();               // docs + repos (see repository-marten skill)
   builder.Services.AddPersistRequestProcessing(...);       // durable-request services only — also add
                                                            //   the *CheckpointJsonContext IJsonTypeInfoResolver
                                                            //   and IReplayAuthenticationProvider singletons
   builder.AddRedisClientConfiguration("cache");
   builder.Services.AddTickerQConfiguration(...);           // only when scheduled jobs exist
   builder.Services.AddMassTransitConfiguration(connStr);   // only when consuming/publishing
   // ... options/token/email configuration extensions ...
   builder.Services.AddHostingConfiguration();              // route options + JSON + cookies
   builder.Services.AddFastEndpointsConfiguration(signingKey);
   builder.Services.AddExternalAuthenticationConfiguration(builder.Configuration);   // only when external identity providers exist
   builder.Services.AddCoreServicesConfiguration();         // steps + services (AddScoped)
   builder.Services.AddFusionConfiguration();               // only when exposing Fusion compute services
   var app = builder.Build();
   app.UseApplicationMiddleware();                          // UseStepifiedSystem + FE + OpenApi chain
   app.UseFusionConfiguration();                            // maps /fusion/ws — MUST run before MapEndpoints
   app.MapEndpoints();
   await app.UseTickerQConfigurationAsync();                // seeds cron + starts scheduler — AFTER MapEndpoints
   await app.RunApplicationAsync();
   ```
   Why: every extension has a fixed home file (`Extensions/Startup/<Name>ConfigurationExtensions.cs`) and fixed ordering. TickerQ has BOTH a services registration and a mandatory app-level `await app.UseTickerQConfigurationAsync()` after `MapEndpoints()` (skipping it = cron never seeds); Fusion maps its websocket endpoint at the app level and must precede `MapEndpoints()` (skipping it = RPC endpoint never mapped).

3. **Worker services (IAM, Email, Push) expose no business endpoints** — their `MapEndpoints` is `MapDefaultEndpoints()` only, and their logic lives in `Features/<Area>/<Consumer>` consumers.
   Why: they exist to react to messages; adding HTTP surface to them blurs the service boundary and forces anonymous YARP routes that shouldn't exist.

4. **Every type the STJ source generator touches must be registered in `CoreSerializationContext`** (`snowcoreBlog.Backend/Shared/Infrastructure/Context/CoreSerializationContext.cs`): add `[JsonSerializable(typeof(...))]` entries for new DTOs, entities, MassTransit `DataResult<T>` payloads, SSE event types. Consumers: FastEndpoints `SerializerContext(CoreSerializationContext.Default)`, Marten serializer (`_serializer.UseTypeInfoResolver(CoreSerializationContext.Default)`), step-progress final frames.
   Why: an unregistered type fails **at runtime** (reflection-based serialization is trimmed away), not at compile time.

5. **Telemetry goes through the shared extension** — services call `builder.Services.AddOpenTelemetryConfiguration()`, which wraps `OpenTelemetryExtensions.ConnectBackendServices()` (FastEndpoints, Redis, Npgsql, MassTransit listener, Marten tracing/meters). Extend that shared method for new instrumented dependencies; never wire per-service ad-hoc tracing.
   Why: one place guarantees identical instrumentation across all services.

6. **Message contracts have fixed homes**: service-to-service **events** in the owning service's `<Service>.Core/Contracts/` (e.g. `ReadersManagement.Core/Contracts/ReaderAccountTempUserCreated`, `Stripe.Core/Contracts/Stripe/DonationPaid`, `Documents.Core/Contracts/DocumentUploaded`), IAM **request/response** contracts in `Shared/IAM.Core/Contracts/` (e.g. `ValidateAdminExists`, `CreateAdmin`). DTOs consumed by frontends go to the mirrored `snowcoreBlog.Universal/PublicApi` copies (see `git-flow` rule for the ×3 mirroring requirement).
   Why: contract location is how services discover what they may reference; misplaced contracts create cross-service compile dependencies.

7. **Do not create a new microservice without consulting the developer.** Prefer extending an existing service; the default stack for anything new is FastEndpoints + Marten + MassTransit + stepified processes.
   Why: each service costs an Aspire resource, a YARP route group, a logical database and mirrored PublicApi contracts.

## Red Flags

- A service folder layout that invents new top-level folders outside the gold set.
- `Program.cs` calling raw `builder.Services.AddFastEndpoints()` / `app.UseFastEndpoints()` instead of the named configuration extensions.
- A new DTO/entity shipping without a `CoreSerializationContext` entry.
- Business endpoints appearing in a worker service.
- A `[JsonSerializable]`-worthy type referenced from a step but "temporarily" serialized by reflection.

## Evidence

- Gold structure + Program.cs order: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Program.cs`
- Startup extension files: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/*.cs`
- Serialization context: `snowcoreBlog.Backend/Shared/Infrastructure/Context/CoreSerializationContext.cs`
- OTel: `snowcoreBlog.Backend/Shared/Infrastructure/Extensions/OpenTelemetryExtensions.cs`
- Contract homes: `snowcoreBlog.Backend/Shared/ReadersManagement.Core/Contracts/ReaderAccountTempUserCreated.cs`, `snowcoreBlog.Backend/Shared/IAM.Core/Contracts/ValidateAdminExists.cs`
