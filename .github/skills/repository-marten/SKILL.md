---
name: repository-marten
description: >-
  How-to for Marten persistence in snowcoreBlog backend services: IRepository<T>
  contract, BaseMartenRepository<TEntity> inheritance, interface/implementation
  placement, DI registration, AddMartenConfiguration (document registration,
  CoreSerializationContext serializer, soft delete, lightweight sessions),
  compiled queries, and entity placement. Use when adding or changing data
  access in any backend service.
scope: project
---

# Repository + Marten

Use this skill to add or modify persistence in a backend service.

## When to Invoke

- "add a repository", "persist X", "query X from the database"
- Adding a new document/entity type
- Debugging Marten serialization or soft-delete behavior

## When NOT to Invoke

- Cross-service DTOs for frontends (those belong in the mirrored
  `snowcoreBlog.Universal/PublicApi`, not in repositories)

## Background

Persistence is Marten 9.x over PostgreSQL (Npgsql data source from Aspire).
Services own their documents; the shared base repo and `IRepository<T>` live in
`Shared/Infrastructure` and `Shared/Shared.Core` respectively. All documents are soft-deleted
(`options.Policies.AllDocumentsSoftDeleted()`) and sessions are lightweight.

## Rule 1 — Interface: `Interfaces/Repositories/Marten/`, extends `IRepository<TEntity>`

```csharp
using snowcoreBlog.Backend.AdminsManagement.Core.Entities.Admin;
using snowcoreBlog.Backend.Shared.Core.Interfaces.Repositories;

namespace snowcoreBlog.Backend.AdminsManagement.Interfaces.Repositories.Marten;

public interface IAdminRepository : IRepository<AdminEntity>
{
    Task<AdminEntity?> GetActiveByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyByEmailAsync(string email, CancellationToken ct = default);
    Task<List<AdminEntity>> SearchAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<List<AdminEntity>> GetActiveExceptAsync(Guid exceptId, CancellationToken ct = default);
}
```

`IRepository<TEntity> where TEntity : notnull, BaseEntity` (in
`Shared/Shared.Core/Interfaces/Repositories/IRepository.cs`) already provides:
`GetAllAsync`, `GetAllByQueryAsync(ICompiledQueriesProvider, ...)`, `GetByIdAsync(Guid, ...)`,
`GetOneByQueryAsync(...)`, `AnyByQueryAsync(...)`, `CountByQueryAsync(...)`,
`AddOrUpdateAsync(entity, id?, saveChange = true, ...)`, `AddRangeAsync(...)`,
`RemoveAsync(entity, saveChange = true, ...)`.

## Rule 2 — Implementation: primary constructor + `BaseMartenRepository<TEntity>`

```csharp
public class ReaderRepository(IDocumentSession session) : BaseMartenRepository<ReaderEntity>(session), IReaderRepository
{
    // only specialized queries that compiled queries/base methods cannot express
}
```

`BaseMartenRepository<TEntity>(IDocumentSession)` is in
`Shared/Infrastructure/Repositories/Marten/Base/BaseMartenRepository.cs` and
implements the whole `IRepository<T>` surface over the session. Add custom
methods (e.g. `SearchAsync`, `GetActiveExceptAsync`) only when a compiled query
cannot express them.

## Rule 3 — Documents register in `MartenConfigurationExtensions`; repos may register there OR in `CoreServicesConfigurationExtensions`

Documents always register in `Extensions/Startup/MartenConfigurationExtensions.cs`:

```csharp
private static readonly SystemTextJsonSerializer _serializer = new();   // class-level field

services.AddMarten(options =>
{
    options.RegisterDocumentType<ReaderEntity>();
    options.RegisterDocumentType<AltchaStoredChallengeEntity>();
    options.AddPersistRequestDocuments();   // durable-request services only — registers
                                            // PersistedRequestEntity + RequestCheckpointEntity
                                            // (claims + step checkpoints, unique idempotency-key index)

    _serializer.UseTypeInfoResolver(CoreSerializationContext.Default);   // static STJ metadata
    options.Serializer(_serializer);
    options.Policies.AllDocumentsSoftDeleted();
})
    .UseLightweightSessions()
    .UseNpgsqlDataSource();
```

Schema customization (unique indexes, FKs, per-document soft delete) chains into
the same `options.Schema.For<T>()` builder — see AdminsManagement's
`MartenConfigurationExtensions.cs` (`.ForeignKey<AdminEntity>(...)`,
`.Index(x => x.TokenHash, ...) { IsUnique = true }`, `.SoftDeleted()`); DB-enforced
uniqueness pairs with the soft-delete red flag below.

**Repository DI placement follows the target service's existing convention:**
ReadersManagement registers repo pairs inside `AddMartenConfiguration` (and, as a
known harmless wart, again in `AddCoreServicesConfiguration` — last registration
wins); IAM/Stripe/UrlShortener register theirs in `AddCoreServicesConfiguration`
(`CoreServicesConfigurationExtensions.cs`); AdminsManagement registers via a
sibling `AddAdminRepositoriesConfiguration` in the same file. Match the service
you are editing.

## Rule 4 — Entities live in the owning service's `<Service>.Core` project; IAM has its own Core project

Every service has a dedicated Core class library (`Shared/<Service>.Core/`) that
references only the `Shared/Shared.Core` kernel and owns its entities/events:
`ReaderEntity` → `Shared/ReadersManagement.Core/Entities/Reader/`, `ArticleEntity` →
`Shared/Articles.Core/Entities/Article/`, `AdminEntity` →
`Shared/AdminsManagement.Core/Entities/Admin/`, Stripe entities/events →
`Shared/Stripe.Core/`, and so on. **IAM entities live in
`Shared/IAM.Core/Entities/`** (`ApplicationUserEntity`, `Fido2DevicePublicKeyEntity`,
...). Service-only documents stay in the service. Every entity consumed by the
serializer needs a `[JsonSerializable]` entry in `CoreSerializationContext` — an
unregistered entity fails at runtime, not compile time.

## Rule 5 — Queries: compiled queries over inline filters

The `IRepository<T>` query members take an `ICompiledQueriesProvider` (the base
repository resolves Marten `ICompiledQuery`/`ICompiledListQuery` from it via
`GetQuery<...>()`). Shared query interfaces live in
`snowcoreBlog.Backend.Shared.Core.Interfaces.CompiledQueries`; grep existing
`ICompiledQueriesProvider` implementations before inventing a new mechanism, and
add a specialized repository method only when a compiled query genuinely cannot
express the lookup.

## Red Flags

- Injecting `IDocumentSession` directly into endpoints/steps instead of a repository.
- A repository interface NOT extending `IRepository<T>`, or an impl NOT inheriting `BaseMartenRepository<T>`.
- New `RegisterDocumentType` missing while a step persists that entity.
- Hardcoded SQL / `FilterDefinition` strings where a compiled query provider exists.
- Forgetting soft delete: removed documents remain (tombstoned) — uniqueness checks must filter active state.

## Reference implementations

- Interface: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Interfaces/Repositories/Marten/IAdminRepository.cs`
- Implementation: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Repositories/Marten/ReaderRepository.cs`
- Marten config: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/MartenConfigurationExtensions.cs`
- Base repo: `snowcoreBlog.Backend/Shared/Infrastructure/Repositories/Marten/Base/BaseMartenRepository.cs`
