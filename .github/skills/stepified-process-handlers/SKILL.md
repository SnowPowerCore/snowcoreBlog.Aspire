---
name: stepified-process-handlers
description: >-
  How-to for the MinimalStepifiedSystem pattern — delegate, context, steps,
  endpoint attribute, DI registration, shared steps for duplicated logic, and
  checkpointed steps for durable flows. Includes the stepified-vs-direct
  decision guide. Use when implementing a multi-step backend flow or refactoring
  endpoint logic into steps.
scope: project
---

# Stepified Process Handlers

Use this skill to implement a multi-step endpoint flow with
MinimalStepifiedSystem 1.2.2 (`MinimalStepifiedSystem.Core` + `.SourceGen`).

## When to Invoke

- A flow has several conditional branches, persistence or integration calls
- "add a step", "compose validation + persistence + event publishing"
- Extracting duplicated logic from multiple steps

## When NOT to Invoke

- Short endpoints unlikely to grow — inject the repository/service directly
  into the endpoint instead. AdminsManagement does this for `SearchAdminAccountsEndpoint`,
  `GetAdminMeEndpoint`, `AdminLogoutEndpoint`, `GetAdminInvitesEndpoint`,
  `ResolveAdminInviteEndpoint`, `RevokeAdminInviteEndpoint` — while its
  `CreateAdminInviteEndpoint`/`AcceptAdminInviteEndpoint` ARE stepified
- **When unsure whether to stepify, ask the developer** (interactive question
  flow) — this is an explicit judgment call in this repo

## Background

The endpoint declares a delegate property; the `[StepifiedProcess]` source
generator turns it into an invocable pipeline. Steps are `AddScoped` DI
services implementing `IStep<TDelegate, TContext, IMaybe<T>>` that either
continue via `next(context, token)` or short-circuit with `IMaybe<T>`.
`T` is always a domain DTO (from `snowcoreBlog.Universal/PublicApi`) — never
`ApiResponse`/`HttpStatusCode` (that op-result pattern was removed 2026-11-11).

## Step 1 — Delegate (`Delegates/`)

```csharp
using MaybeResults;
using snowcoreBlog.Backend.ReadersManagement.Context;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;

namespace snowcoreBlog.Backend.ReadersManagement.Delegates;

public delegate Task<IMaybe<RequestReaderAccountCreationResultDto>> RequestCreateReaderAccountDelegate(
    RequestCreateReaderAccountContext context, CancellationToken token = default);
```

## Step 2 — Context (`Context/`, inherits `BaseGenericContext`)

```csharp
using MinimalStepifiedSystem.Base;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;

namespace snowcoreBlog.Backend.ReadersManagement.Context;

public class RequestCreateReaderAccountContext(RequestCreateReaderAccountDto request) : BaseGenericContext
{
    public RequestCreateReaderAccountDto CreateRequest { get; } = request;
    // add mutable properties here as steps produce intermediate data
}
```

## Step 3 — Steps (`Steps/<Feature>[/<Op>]/`)

Middle step: validate then continue, or short-circuit with a typed `[None]` error:

```csharp
public class ValidateReaderAccountEmailDomainStep(IRequestClient<CheckEmailDomain> requestClient)
    : IStep<RequestCreateReaderAccountDelegate, RequestCreateReaderAccountContext, IMaybe<RequestReaderAccountCreationResultDto>>
{
    public async Task<IMaybe<RequestReaderAccountCreationResultDto>> InvokeAsync(
        RequestCreateReaderAccountContext context,
        RequestCreateReaderAccountDelegate next,
        CancellationToken token = default)
    {
        context.ReportStepProgress(ReaderAccountStepProgressCodes.ValidatingEmailDomain); // optional SSE

        var result = await requestClient.GetResponse<DataResult<EmailDomainChecked>>(
            context.CreateRequest.ToCheckEmailDomain(), token);

        return result.Message.IsSuccess
            ? await next(context, token)
            : CreateUserForReaderAccountError<RequestReaderAccountCreationResultDto>.Create(
                EmailConstants.EmailDomainIsNotValid, result.Message.Errors);
    }
}
```

Terminal step: produce the DTO with `Maybe.Create(...)`:

```csharp
return Maybe.Create<RequestReaderAccountCreationResultDto>(new(responseObj.Id));
```

## Step 4 — Endpoint attribute

```csharp
[StepifiedProcess(Steps = [
    typeof(ValidateReaderAccountEmailDomainStep),
    typeof(ValidateReaderAccountNotExistsStep),
    typeof(ValidateReaderAccountTempRecordNotExistsStep),
    typeof(ValidateReaderAccountNickNameWasNotTakenStep),
    typeof(CreateReaderAccountTempUserStep),
])]
protected RequestCreateReaderAccountDelegate RequestCreateReaderAccount { get; }
```

Then in `HandleAsync`: build the context, invoke the delegate property, send
with the `ToApiResponse`/`ToStatusCode` envelope (see `fastendpoints-endpoints` skill).

## Step 5 — Register every step as scoped

In `Extensions/Startup/CoreServicesConfigurationExtensions.cs` → `AddCoreServicesConfiguration()`:

```csharp
services.AddScoped<ValidateReaderAccountEmailDomainStep>();
services.AddScoped<CreateReaderAccountTempUserStep>();
```

When two services in one project have same-named steps, qualify the namespace
(`services.AddScoped<Steps.ReaderAccount.Request.ValidateReaderAccountNotExistsStep>();`).

## Shared steps for duplicated logic

When several steps repeat the same logic, extract it — do not copy. Shared
helpers live in `Steps/<Feature>/Shared/`, are static classes named with the
**`...SharedStep` suffix**, and expose one method taking all required
dependencies. Real example:
`Steps/ReaderAccount/Shared/ValidateReaderAccountNotExistsSharedStep.cs` —
`CheckAsync(IRequestClient<ValidateUserExists> requestClient, ValidateUserExists request, CancellationToken token)`
returning `IMaybe<bool>`; the Request and Confirm flows each wrap that bool
into their own DTO result.

## Checkpointed steps (durable flows)

Endpoints tagged `ApiTagConstants.Persist` get idempotency + replay via the
shared processors; mark resumeable steps with `[CheckpointedStep]` (AspectInjector
aspect → `StepCheckpointCoordinator` snapshots the context, which must implement
`IStepCheckpointState` and be registered in the service's `*CheckpointJsonContext`).
Real anchors: `Endpoints/ReaderAccounts/ConfirmCreateByEmailEndpoint.cs` (tags),
`Context/ConfirmCreateReaderAccountContext.cs` (snapshot state),
`Steps/ReaderAccount/Confirm/CreateReaderAccountUserStep.cs` (`[CheckpointedStep]`),
`ReadersCheckpointJsonContext` registered as `IJsonTypeInfoResolver` in `Program.cs`.

## Reference implementations

- Full flow: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Endpoints/ReaderAccounts/RequestCreateByEmailEndpoint.cs` + `Delegates/`, `Context/`, `Steps/ReaderAccount/Request/`
- Request/response step into IAM: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Steps/Auth/ConfirmLogin/ValidateAdminExistsInIamStep.cs`
- Step registration: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/CoreServicesConfigurationExtensions.cs`
