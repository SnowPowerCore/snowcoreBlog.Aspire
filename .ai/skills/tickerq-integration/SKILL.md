---
name: tickerq-integration
description: >-
  How-to for TickerQ 10.4 background jobs in snowcoreBlog — ITickerFunction +
  MapTicker<T> interface registration (chosen over [TickerFunction] for
  class-library safety), 6-field cron seeding, in-memory persistence decision,
  AddTickerQConfiguration/UseTickerQConfigurationAsync split, dashboard, and
  the version pin. Use when adding a scheduled job or debugging ticker
  execution.
scope: project
---

# TickerQ Integration

Use this skill to add scheduled background work or debug jobs that don't fire.

## When to Invoke

- "run X every N seconds/minutes", "scheduled job", "cron"
- A ticker is seeded but throws "Ticker function 'X' was not found"
- Wiring TickerQ into a new service

## When NOT to Invoke

- MassTransit consumers (event reactions, not schedules)
- Startup-only work — use `IApplicationLaunchService` / hosted services, not cron

## Background

TickerQ is **pinned to 10.4.0** in `Directory.Packages.props` — the last
MIT/Apache dual-licensed release (10.5+ is commercial; its APIs like
`UseApplicationNamespace` do NOT exist here). The source generator embedded in
10.4 emits a `[ModuleInitializer]` in the assembly owning `[TickerFunction]`
classes, and module initializers only run when the assembly is EXECUTED —
referencing or DI-registering it is not enough. This repo therefore uses
**interface registration**, which registers at DI time with no source gen.

## Step 1 — Job class implementing `ITickerFunction`

`Shared/Infrastructure/RequestPersistence/PersistRequestRecoveryJobs.cs` is the
reference:

```csharp
public sealed class PersistRequestRecoveryJob(
    IServiceScopeFactory scopeFactory,
    IOptions<PersistRequestOptions> options,
    ILogger<PersistRequestRecoveryJob> logger) : ITickerFunction
{
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken cancellationToken = default)
    {
        try { await RecoverDueAsync(cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* host shutdown */ }
        catch (Exception ex) { logger.LogError(ex, "Recovery pass failed; retrying on next poll."); }
    }
}
```

Constructor DI works; instances are resolved per execution (NOT at startup —
eager side effects must be explicit in Program.cs). Resolve scoped services
inside via `IServiceScopeFactory`.

## Step 2 — Register + configure (two-phase)

`Extensions/Startup/TickerQConfigurationExtensions.cs` pattern:

```csharp
public static IServiceCollection AddTickerQConfiguration(this IServiceCollection services, IConfiguration configuration)
{
    services.MapTicker<PersistRequestRecoveryJob>();      // interface registration — registers at DI time
    services.AddTickerQ(options => { ConfigureDashboard(options, configuration, requireAuthentication: false); });
    return services;
}

public static async Task UseTickerQConfigurationAsync(this WebApplication app)
{
    _ = app.Services.GetRequiredService<StepCheckpointCoordinator>();   // eager resolve (TickerQ won't build job classes at startup)
    await PersistRequestTickerQ.SeedRecoveryCronAsync(app.Services);     // runtime-config-driven cron
    app.UseTickerQ();                                                    // starts scheduler + dashboard branch
}
```

`Program.cs` calls BOTH: `builder.Services.AddTickerQConfiguration(...)` before
build and `await app.UseTickerQConfigurationAsync();` AFTER `app.MapEndpoints()`.
Function name for interface-registered jobs: `TickerFunctionProvider.GetFunctionName<TJob>()`.

## Step 3 — Cron

Cron is **6-field (seconds first)**: `*/15 * * * * *` = every 15 seconds.
`MapTicker<T>()` registers with empty cron; recurrence comes from either
`builder.MapTicker<T>().WithCron(expr)` (initializer migrates registry crons on
startup) or explicit seeding via
`ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>.MigrateDefinedCronTickers([(name, expr)])`
(upsert by function name — safe on every startup) when the interval is
config-driven, as `PersistRequestTickerQ.SeedRecoveryCronAsync` does from
`PersistRequestOptions.RecoveryPollInterval`. The gateway's snapshot-refresh
job (`ApiAccessRestrictionsSnapshotRefreshJob`) is seeded `*/30 * * * * *` in
`YarpResourceExtensions.BuildYarpWebApplicationAsync`.

## Step 4 — Persistence is IN-MEMORY — deliberately

Do NOT add `AddStackExchangeRedis` (the 10.4 Redis provider uses HARDCODED keys
`tq:*` with no per-app namespace, and registers an unkeyed
`IConnectionMultiplexer` that hijacks the Aspire Redis client — two hosts on the
shared cache caused cross-app ticker pickup). In-memory exactly reproduces the
old hosted-service semantics because crons re-seed every startup. Dashboard:
`ConfigureDashboard` (`/tickerq`, basic auth optional from `TickerQ:Dashboard`;
the gateway enables it only with configured auth — never expose it
unauthenticated on the public ingress).

## Red Flags

- `[TickerFunction]` attribute in a CLASS LIBRARY (module initializer never
  runs → seeded but "function not found" at execution).
- Redis persistence attempt on the shared `cache`.
- `UseTickerQ()` called without the pre-build `AddTickerQ` registration, or
  `UseTickerQConfigurationAsync` skipped (cron never seeds).
- Jobs with external side effects relying on exactly-once (no outbox; retries
  re-execute — make `ExecuteAsync` idempotent).
- Upgrading TickerQ past 10.4.0 (commercial license + API breaks).

## Reference implementations

- Job + seeding: `snowcoreBlog.Backend/Shared/Infrastructure/RequestPersistence/PersistRequestRecoveryJobs.cs`, `PersistRequestTickerQ.cs`
- Service wiring: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/TickerQConfigurationExtensions.cs`
- Gateway job: `snowcoreBlog.Backend/Shared/Services/ApiAccessRestrictions/GatewayIntegration/ApiAccessRestrictionsSnapshotRefreshJob.cs`
- Version pin: root `Directory.Packages.props` (`TickerQ` 10.4.0 with license comment)
