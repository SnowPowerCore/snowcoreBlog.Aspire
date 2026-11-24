---
name: shared-service-author
description: >-
  How-to for scaffolding a new shared backend microservice — csproj package
  mirror, gold Program.cs startup chain, startup extension files to copy, Marten
  + serialization + PublicApi DTO registrations, YARP + AppHost wiring, and
  solution file updates. Use when creating a new service under
  snowcoreBlog.Backend/Shared/Services (or a new BusinessService).
scope: project
---

# Shared Service Author

Use this skill to create a new backend microservice that matches the repo's
gold conventions from commit one.

## When to Invoke

- "create a new service X", "scaffold a microservice"
- A service folder must be added under `Shared/Services/` or `BusinessServices/`

## When NOT to Invoke

- Adding endpoints/features to an EXISTING service
- Deciding WHETHER a new service is warranted — that is an architecture
  decision: consult the developer first (see `backend-architecture-rules`)

## Background

Every service follows the gold structure (reference: ReadersManagement; newest
scaffolds: Documents, Stripe, ServiceNotifications). A service is a Nest of
conventions, not just a csproj: startup extensions, Marten registration,
serialization context, PublicApi contracts, YARP routes, AppHost resources, and
solution entries all must land together or the service is unreachable/unserializable.

## Step 1 — csproj (mirror the closest exemplar)

For FastEndpoints services, mirror `Documents/Stripe` csproj: FastEndpoints
(+ Security/Generator/Swagger), MassTransit.RabbitMQ, Marten (+ SourceGenerator),
MinimalStepifiedSystem Core+SourceGen, Scalar, Riok.Mapperly, OpenTelemetry
extensions; project refs: ServiceDefaults, Infrastructure, Core, PublicApi
(the backend mirror). net10.0. Skip `PublishTrimmed` for reflection-heavy
SDKs (Stripe.net is explicitly not trim-safe — commented in its csproj).

## Step 2 — Folder skeleton + startup extensions

Gold folders: `Constants/ Context/ Delegates/ Endpoints/<Feature>/ +
Endpoints/Root/ ErrorResults/ Extensions/Startup/ Features/ Interfaces/
Options/ Repositories/ Services/ Steps/`. Copy/adapt the startup extension
files from ServiceNotifications: `HostingConfigurationExtensions`,
`TelemetryConfigurationExtensions` (wraps `ConnectBackendServices()`),
`LocalizationConfigurationExtensions`, `MiddlewareConfigurationExtensions`,
`NpgsqlConfigurationExtensions`, `OpenApiConfigurationExtensions`,
`ApplicationConfigurationExtensions` (`UseApplicationMiddleware` /
`MapEndpoints` / `RunApplicationAsync`), `CoreServicesConfigurationExtensions`.

## Step 3 — Program.cs gold chain

Follow `backend-architecture-rules` exactly: `CreateSlimBuilder` →
`UseHostingConfiguration` → `UseServiceDefaultsConfiguration` →
`AddOpenTelemetryConfiguration` → `AddNpgsqlDataSourceConfiguration("<db>")` →
`AddMartenConfiguration` → (persist/redis/tickerq/masstransit as needed) →
options → `AddHostingConfiguration` → `AddFastEndpointsConfiguration(signingKey)` → `AddCoreServicesConfiguration` → build → `UseApplicationMiddleware` →
`MapEndpoints` → `RunApplicationAsync`. Worker-style services (no business
endpoints): `MapEndpoints` = `MapDefaultEndpoints()` only.

## Step 4 — Domain registrations that always get forgotten

- Marten: `options.RegisterDocumentType<T>()` per entity +
  `_serializer.UseTypeInfoResolver(CoreSerializationContext.Default)` +
  `AllDocumentsSoftDeleted()` + `UseLightweightSessions()` + `UseNpgsqlDataSource()`.
- `CoreSerializationContext`: `[JsonSerializable]` for every entity/DTO/event.
- PublicApi DTOs + `ApiTagConstants.<Service>` — mirrored ×3 (Backend +
  Frontend + Console copies).
- Repositories: interface extends `IRepository<T>`; impl inherits
  `BaseMartenRepository<T>` (see `repository-marten` skill).

## Step 5 — Gateway + AppHost

- `appsettings.Development.json` (`ReverseProxy`): cluster
  `backend-<name>` (`"Address": "https://backend-<name>"`) + full-path routes
  `/api/<name>/openapi/{**catch-all}`, `/api/<name>/scalar/{**catch-all}`,
  `/api/<name>/ping/{**catch-all}`, then the `"/api/<name>/{**catch-all}"
  with `PathRemovePrefix /api/<name>` — decide `AuthorizationPolicy`
  explicitly (`anonymous` is the current default for new shared services, with
  an in-code TODO to restrict later).
- Root `Program.cs`: `postgres.AddDatabase("db-<name>-entities")` (if owned),
  `builder.AddProject<Projects.snowcoreBlog_Backend_<Name>>("backend-<name>")`
  with `.WithReference/.WaitFor` chains, ingress `.WithReference/.WaitFor`,
  and the `<ProjectReference>` in `snowcoreBlog.Aspire.csproj`.
- Solutions: add to `snowcoreBlog.Backend.sln` AND `snowcoreBlog.Aspire.slnx`
  (solution folders `/Backend/` + `/Tests/`).

## Step 6 — Namespace hazards

If the service namespace can shadow an SDK namespace (Stripe does: namespace
`snowcoreBlog.Backend.Stripe` shadows `Stripe`), qualify SDK types with
`global::` and keep the SDK `using` set minimal — otherwise CS0234 binds to
your own types.

## Red Flags

- Missing `RegisterDocumentType` / `CoreSerializationContext` entry (runtime
  serialization failure).
- YARP cluster without routes, or routes with an undecided
  `AuthorizationPolicy`.
- Solution files not updated (service builds but isn't in the build graph).
- DTOs defined service-locally when the frontend will need them (PublicApi ×3).

## Reference implementations

- Newest full scaffold: `snowcoreBlog.Backend/Shared/Services/Documents/` (+ its `.github` era docs),
  `Shared/Services/Stripe/`
- Gold service: `.../BusinessServices/ReadersManagement/`
- AppHost wiring: root `Program.cs`, `snowcoreBlog.Aspire.csproj`, `appsettings.Development.json`
