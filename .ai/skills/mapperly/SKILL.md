---
name: mapperly
description: >-
  How-to for Riok.Mapperly source-generated mappers in the snowcoreBlog backend
  — [Mapper] partial classes in the service's Extensions/ folder, ToDto/ToEntity
  conventions, MapProperty/MapPropertyFromSource overrides, and regeneration
  gotchas. Use when mapping between entities and DTOs in backend services
  (frontend PublicApi mirrors are DTO-only — no mappers there).
scope: project
---

# Mapperly

Use this skill to create or modify object mappings between entities and DTOs.

## When to Invoke

- "map X to a DTO", "convert entity → response model"
- A mapping compiles but produces wrong property values
- A new PublicApi DTO needs an entity counterpart mapping

## When NOT to Invoke

- Manual one-off field picking inside a lambda where Mapperly already generates
  the type mapping — extend the mapper instead (see Rule 1)
- JSON (de)serialization concerns — those are `CoreSerializationContext` +
  `ToApiResponse`/`ToData` territory, not mappers

## Background

Riok.Mapperly 4.3.1 generates mapping bodies at compile time from `partial`
method declarations — no reflection, trim/AOT-safe, and consistent with the
repo's source-gen-first philosophy (FastEndpoints gen, Marten static codegen,
STJ contexts). Mapperly is used in the **Backend only**; the Frontend/Console
PublicApi mirrors carry DTOs but no mappers.

## Rule 1 — `[Mapper]` on a partial static class in the service's `Extensions/` folder

There is no `Mappers/` folder convention — real mapper classes live in each
service's `Extensions/` (file names are either `*Extensions.cs` or `*Mapper.cs`,
e.g. `AdminAccountMapper.cs`, `UserExtensions.cs`, `UrlMappingExtensions.cs`):

```csharp
using Riok.Mapperly.Abstractions;
using snowcoreBlog.Backend.AdminsManagement.Core.Entities.Admin;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;

[Mapper]
public static partial class AdminAccountMapper
{
    public static partial AdminDto ToDto(this AdminEntity entity);
}
```

Call sites read naturally and are already the repo norm:

```csharp
var items = await AdminRepository.SearchAsync(search, page, pageSize, ct);
var result = Maybe.Create(items.Select(static a => a.ToDto()).ToList());
```

## Rule 2 — Fix mismatches with attributes, not manual bodies

`[MapProperty]` for name mismatches; `[MapPropertyFromSource(nameof(targetMember))]`
names the TARGET member that receives an additional method-parameter source —
real usage in `Shared/Services/IAM/Extensions/Fido2Extensions.cs`
(`[MapPropertyFromSource(nameof(RequestNewCredentialParams.User))]`). A real
name-mismatch mapping from `ReadersManagement/Extensions/UserExtensions.cs`:

```csharp
[MapProperty(nameof(ConfirmCreateReaderAccountDto.VerificationToken), nameof(CreateUser.TempUserVerificationToken))]
public static partial CreateUser ToCreateUser(this ConfirmCreateReaderAccountDto createReaderAccountDto, string attestationOptionsJson);
```

Note the second parameter — `attestationOptionsJson` is a method-parameter
source mapped in addition to the DTO, which is exactly the case
`[MapPropertyFromSource(nameof(targetMember))]` covers.

Only drop to a hand-written mapping when Mapperly genuinely cannot express it
(conditional defaults, multi-source composition); keep such methods adjacent to
the generated ones.

## Rule 3 — Regenerate after shape changes

Mapperly output is emitted at build time — after changing an entity or DTO
shape, rebuild the owning project and fix mapping compile errors (missing
member, nullability). Generated code does not silently drift, but stale local
builds can mislead: rebuild before diagnosing.

## Red Flags

- Hand-written `new Dto { A = e.A, B = e.B, ... }` bodies in steps/endpoints
  where a `[Mapper]` type exists or belongs.
- Mapping logic inside `CoreSerializationContext`-registered DTO constructors.
- Non-partial mapper classes or non-partial methods (generator cannot emit).
- Entity ↔ DTO cycles without `[MapperIgnoreTarget]`/`[MapperIgnoreSource]` guidance.

## Reference implementations

- Real mapper class: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Extensions/AdminAccountMapper.cs`
- Attribute usage (`MapPropertyFromSource`): `snowcoreBlog.Backend/Shared/Services/IAM/Extensions/Fido2Extensions.cs`
- DTO→IAM-command mapping (`ToCreateUser`, `ToLoginUser`, `ToEntity(UserCreationResult)`): `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/UserExtensions.cs`
- Mapper call site in an endpoint: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Admins/SearchAdminAccountsEndpoint.cs` (`a.ToDto()`)
