---
name: mayberesults-used
description: >-
  How-to for the MaybeResults pattern (NuGet WTorricos.Results, namespace
  MaybeResults): [None] source-generated typed errors, Maybe.Create successes,
  IMaybe/Some/INone semantics, the ToApiResponse/ToStatusCode envelope mapping
  and its caveats (no 404, ICollection DataCount), and DataResult<T> payloads
  over MassTransit. Use when modeling operation results or errors anywhere in
  the backend.
scope: project
---

# MaybeResults

Use this skill to model operation results and errors in backend code — the
repo-wide pattern for anything that can succeed or fail without throwing.

## When to Invoke

- Writing steps/services/delegates that return `IMaybe<T>`
- Creating or reusing error types
- Deciding what an endpoint should return for a failure

## When NOT to Invoke

- Frontend-side unwrapping (use `ToData<T>` on `IApizrResponse<ApiResponse>` —
  see `frontend-api-clients` skill)

## Background

`WTorricos.Results` 2.0.4 provides `IMaybe<T>` with two shapes:
`Some<T>` (success, carries `Value`) and `INone<T>` (typed failure). The
`[None]` attribute triggers a source generator that turns a marker record into
a GENERIC class with a static `Create` factory. The envelope extensions live
in `snowcoreBlog.Universal/PublicApi/Extensions/` (`ApiResponseExtensions.cs`,
`StatusCodeExtensions.cs`).

## Rule 1 — Declare typed errors with `[None]`, reuse before creating

```csharp
using MaybeResults;

namespace snowcoreBlog.Backend.ReadersManagement;

[None]
public partial record CreateReaderAccountError;
```

The generator emits a generic class. Call sites supply the target DTO type:

```csharp
return CreateReaderAccountError<RequestReaderAccountCreationResultDto>.Create(
    "Reader account could not be created.",            // message (required)
    response.Message.Errors);                           // optional error details
```

**Never** `XError.Create<T>(...)` (non-generic method — CS0308), never
stringly-typed or exception-based expected failures. Grep `ErrorResults/` for
an existing record before adding a new one; 404-vs-400 distinction is an
endpoint concern (see below), not an error-type concern.

## Rule 2 — Successes via `Maybe.Create`

```csharp
return Maybe.Create<RequestReaderAccountCreationResultDto>(new(responseObj.Id));
return Maybe.Create(items.Select(static a => a.ToDto()).ToList());   // collections OK
```

## Rule 3 — Know the envelope mapping exactly

`ToApiResponse<T>(this IMaybe<T>, JsonSerializerOptions?)`:
- `Some<T>` → `new ApiResponse(data: Value.ToJsonDocument(options), dataCount: Value is ICollection c ? c.Count : 1, statusCode: 0)`.
- `INone<T>` → `new ApiResponse(default, 0, -1, errors)` where errors are `"{Code}: {Description}"` per detail, falling back to `Message` when details are empty.
- Neither shape (null result) → the `UnknownError` envelope (`ApiResponseConstants.UnknownError`).

`ToStatusCode<T>(this IMaybe<T>)`: `Some<T>` → **200**, `INone<T>` → **400**,
anything else → **500**. There is **no 404** — send it manually when needed
(reference: `GetNotificationByIdEndpoint`).

**`DataCount` caveat:** only `ICollection` is counted. A lazy `IEnumerable<T>`
reports `1` — materialize (`ToList()`) before `Maybe.Create` for collections.

## Rule 4 — MassTransit payloads use `DataResult<T>`

Cross-service request/response messages carry `DataResult<T>` — a plain record
with `Value` + `Errors: IReadOnlyList<NoneDetail>?`; success is `Errors == null`
(`result.Message.IsSuccess`). Beware: a non-null but EMPTY `Errors` list is
neither success (`IsSuccess == false`) nor `IsFailed == true`, so do not treat
`IsFailed` as the negation of `IsSuccess`. Steps consume it via
`IRequestClient<T>.GetResponse<DataResult<TResult>>(...)` and translate failures
into typed `[None]` errors for their own flow.

```csharp
var response = await client.GetResponse<DataResult<TempUserCreationResult>>(
    context.CreateRequest.ToCreateTempUser(), token);

if (!response.Message.IsSuccess)
    return CreateUserForReaderAccountError<RequestReaderAccountCreationResultDto>.Create(
        ReaderAccountUserConstants.TempUserForReaderAccountUnableToCreateUpdateError,
        response.Message.Errors);
```

## Serialization

Any DTO/`DataResult` flowing through STJ must have a
`[JsonSerializable]` entry in `CoreSerializationContext`
(`snowcoreBlog.Backend/Shared/Infrastructure/Context/CoreSerializationContext.cs`)
— the runtime has no reflection fallback.

## Reference implementations

- Error record + usage: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/ErrorResults/CreateReaderAccountError.cs`, `Steps/ReaderAccount/Request/CreateReaderAccountTempUserStep.cs`
- Extensions: `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi/Extensions/ApiResponseExtensions.cs`, `StatusCodeExtensions.cs`
- Inline maybe in a direct endpoint: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Admins/SearchAdminAccountsEndpoint.cs`
