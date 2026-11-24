---
description: Aspire AppHost wiring — service ProjectReferences, AddProject/WaitFor chains, logical databases, YARP ingress routes and auth-policy literals, custom container resources
globs: ["Program.cs", "snowcoreBlog.Aspire.csproj", "appsettings*.json", "Extensions/*.cs", "Resources/*.cs"]
alwaysApply: false
---

# Aspire AppHost Rules

Use this rule when wiring a new service into the AppHost, touching YARP
routes/policies, adding infrastructure resources, or debugging service startup
order in the root repo.

## Service Context

- The AppHost (`snowcoreBlog.Aspire.csproj`, SDK `Aspire.AppHost.Sdk/13.6.1`)
  references every app project explicitly — `<DefaultItemExcludes>` excludes
  all four submodule trees from globbing, so every service needs a manual
  `<ProjectReference>`. The gateway is special:
  `IsAspireProjectResource="false"` (resource-only, no Projects.* class).
- Resources: `AddRedis("cache").WithDataVolume().WithRedisInsight()`,
  `AddRabbitMQ("rabbitmq").WithManagementPlugin().WithImage("masstransit/rabbitmq")`,
  `AddPostgres("postgres").WithDataVolume(isReadOnly: false).WithPgWeb()`,
  `AddLocalSES("local-ses")` (custom container resource,
  `docker.io/dansyuqri/local-ses:latest`, HTTP port 8282 — defined in
  `Extensions/LocalSESResourceBuilderExtensions.cs` + `Resources/LocalSESResource.cs`).
  The ntfy resource exists (`Addntfy`, port 4010) but is **commented out** in
  `Program.cs`, same as the Console app registration.
- Six logical Postgres databases: `db-snowcore-blog-entities`,
  `db-snowcore-blog-article-entities`, `db-iam-entities`,
  `db-ip-restrictions-entities`, `db-documents-entities`, `db-admins-entities`.

## Wiring Rules

1. **A new service is wired in FOUR places, all in the same changeset**:
   `<ProjectReference>` in `snowcoreBlog.Aspire.csproj`; the
   `builder.AddProject<Projects.snowcoreBlog_Backend_X>("backend-x")` chain in
   `Program.cs`; a YARP cluster + route group in
   `appsettings.Development.json` (`ReverseProxy`); and the ingress
   `.WithReference(...).WaitFor(...)` entries. The canonical chain pattern:
   ```csharp
   var backendDocumentsProject = builder.AddProject<Projects.snowcoreBlog_Backend_Documents>("backend-documents")
       .WithReference(cache).WaitFor(cache)
       .WithReference(rabbitmq).WaitFor(rabbitmq)
       .WithReference(dbDocumentsEntitiesDb).WaitFor(dbDocumentsEntitiesDb);
   ```
   Why: omitting any of the four yields a service that builds but never starts,
   never resolves config, or is unreachable behind the ingress.

2. **Startup order is declared with `WaitFor(...)` only** — readers waits on
   authors; AdminsManagement waits on IAM (`// bootstrap CreateAdmin request
   targets the IAM queue`); both frontends and the ingress wait on their
   backing services. Why: `WaitFor` is the ONLY startup coordination; adding a
   dependency in code without a `WaitFor` gives race-y first requests.

3. **YARP routes live in `appsettings.Development.json` under `ReverseProxy`;
   auth policies live in `Program.cs` code** — `AddYarp("ingress")` calls
   `LoadFromConfiguration("ReverseProxy")` then `.WithAuthPolicies(...)`.
   Policies are STRING LITERALS kept in sync with backend constants by hand
   (the AppHost does not compile-reference service assemblies):
   ```csharp
   ("regularReader", policy => policy.RequireAuthenticatedUser()
       .RequireClaim("readerAccount", allowedValues: true.ToString().ToLower())),
   ("regularAdmin", policy => policy.RequireAuthenticatedUser()
       .AddAuthenticationSchemes("AdminScheme")
       .RequireClaim("adminAccount", allowedValues: "true")))
   ```
   Route-level policy via `"AuthorizationPolicy"` per route. Current state to
   preserve/consciously change: readers catch-all uses `regularReader`;
   `regularAdmin` is defined but used by NO route (admin surfaces are anonymous
   catch-alls guarded backend-side by `Claims(adminAccount)`); the articles
   catch-all has NO policy at all. Why: route/policy drift silently opens or
   closes the whole API — make policy changes deliberate, not accidental.

4. **Route shapes follow three templates**: API prefix strip
   (`Match.Path: "/api/<svc>/{**catch-all}"` + `Transforms: PathRemovePrefix
   /api/<svc>`), sub-routes BEFORE the catch-all (openapi/scalar/ping/antiforgerytoken/
   captcha — more specific routes match first), and frontend base-path forward
   (`/app/{**catch-all}` and `/admin/{**catch-all}` with `PathRemovePrefix` +
   `RequestHeader X-Forwarded-BasePath` set to the mount path — the Hosts apply
   it via `ApplyBasePathMiddleware`). Clusters are plain
   `"Address": "https://<resource-name>"`. Why: prefix stripping is what makes
   service-relative FastEndpoints routes (`Post("tokens/refresh")` →
   `/api/readers/tokens/refresh/v1`) work.

5. **Custom infrastructure = builder extension + resource class** in
   `Extensions/<Name>ResourceBuilderExtensions.cs` +
   `Resources/<Name>Resource.cs` (pattern: `ContainerResource` +
   `IResourceWithConnectionString`, connection string = endpoint HostAndPort —
   see LocalSES). Why: ad-hoc `AddContainer` calls scattered in Program.cs
   bypass the connection-string wiring services rely on.

6. **VS Code tasks/launch**: build/publish tasks target
   `snowcoreBlog.Frontend.WasmForDebugging` (not the AppHost); the AppHost is
   launched via the `C#: snowcoreBlog.Aspire [https]` launch config or
   `dotnet run --project snowcoreBlog.Aspire.csproj`. Why: pointing the build
   task at the AppHost surprises the WASM debug loop.

## Red Flags

- `AddProject` without matching `WaitFor` for new infra dependencies.
- A YARP route added without deciding `AuthorizationPolicy` explicitly.
- Policy literals edited in one place (AppHost vs backend `SecurityConstants`).
- A service cluster with no `WaitFor` from the ingress.
- Redis resource without `.WithDataVolume()` (sessions/refresh records die on restart).
- New container resource hardcoded inline instead of the Extensions/Resources pattern.

## Evidence

- Wiring + policies: `Program.cs` (root)
- Explicit references + excludes: `snowcoreBlog.Aspire.csproj`
- Routes/clusters: `appsettings.Development.json` (`ReverseProxy`)
- Custom resources: `Extensions/LocalSESResourceBuilderExtensions.cs`, `Resources/LocalSESResource.cs`, `Extensions/ntfyResourceBuilderExtensions.cs`
