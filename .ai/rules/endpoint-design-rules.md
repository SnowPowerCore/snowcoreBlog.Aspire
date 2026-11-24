---
description: Endpoint design — stepified vs direct-injection decision, IMaybe of domain DTOs, ApiResponse envelope at endpoint level, typed [None] errors, PublicApi DTO placement, shared steps
globs: ["snowcoreBlog.Backend/**/Endpoints/**/*.cs", "snowcoreBlog.Backend/**/Steps/**/*.cs", "snowcoreBlog.Backend/**/Delegates/*.cs", "snowcoreBlog.Backend/**/Context/*.cs", "snowcoreBlog.Backend/**/ErrorResults/*.cs"]
alwaysApply: false
---

# Endpoint Design Rules

Use this rule when adding or changing an HTTP endpoint in any backend service,
deciding whether a flow should be stepified, writing steps/delegates/contexts,
or shaping error results.

## Service Context

- HTTP surface is FastEndpoints; multi-step flows use MinimalStepifiedSystem (`[StepifiedProcess]`).
- Operation results are `IMaybe<T>` from the `MaybeResults` namespace (NuGet `WTorricos.Results`).
- The HTTP envelope is `ApiResponse` (StatusCode 0 success / -1 error, `DataCount`, `Data` JsonDocument, `Errors`) produced by `ToApiResponse`/`ToStatusCode` extensions in `snowcoreBlog.Universal/PublicApi/Extensions/`.

## Design Rules

1. **Decide stepified vs direct per endpoint — and ask the developer when unsure.** If the endpoint is short and unlikely to grow, skip the pipeline and inject the repository/service directly into the endpoint (see `AdminsManagement` — `SearchAdminAccountsEndpoint`, `GetAdminMeEndpoint`, `AdminLogoutEndpoint`, `GetAdminInvitesEndpoint`, `ResolveAdminInviteEndpoint`, `RevokeAdminInviteEndpoint` all do this). If the flow has several conditional branches, persistence or integration steps, or is expected to grow, organize a `[StepifiedProcess]` (see `ReadersManagement`).
   Why: stepifying a two-line endpoint buries trivial logic behind five files; hand-rolling a growing flow in `HandleAsync` makes it untestable. This is a judgment call — use the interactive question flow when in doubt.

2. **Step delegates return `IMaybe<T>` where `T` is a domain DTO — never an HTTP-shaped object.** `T` must not embed `HttpResponse`, `HttpStatusCode`, or `ApiResponse`; HTTP concerns are applied only at the endpoint:
   ```csharp
   var result = await RequestCreateReaderAccount(context, token);

   await Send.ResponseAsync(
       result?.ToApiResponse(serializerOptions: JsonOptions.Value.SerializerOptions),
       result?.ToStatusCode() ?? (int)HttpStatusCode.InternalServerError,
       ct);
   ```
   Why: the op-result pattern (`OperationResult { ApiResponse Response; int HttpStatusCode }`) was removed repo-wide on 2026-11-11 exactly because it leaked transport into domain code. `ToStatusCode()` maps `Some<T>` → 200, `INone<T>` → 400, else → 500.

3. **Always use typed `[None]` error records; reuse existing ones before creating new.**
   ```csharp
   using MaybeResults;

   namespace snowcoreBlog.Backend.ReadersManagement;

   [None]
   public partial record CreateReaderAccountError;
   ```
   The source generator emits a GENERIC class — call sites are `CreateReaderAccountError<TDto>.Create(message)` (optionally with an error-details collection), returning `IMaybe<TDto>`. Never `XError.Create<T>(...)` (CS0308), never stringly-typed results, never exceptions for expected failures.
   Why: typed errors keep step signatures uniform (`IMaybe<TDto>` in, `IMaybe<TDto>` out) and message text lives at the call site where the failure is known.

4. **DTOs returned by steps/endpoints live in `snowcoreBlog.Universal/PublicApi` (`BusinessObjects/Dto`)** — mirrored ×3 (Backend/Frontend/Console). No exceptions as of 2026-11: AdminsManagement's former service-local DTOs were migrated into PublicApi (`AdminManagementDtos.cs`) — new endpoint DTOs always go to PublicApi.
   Why: the frontend compiles against its own PublicApi copy; service-local DTOs can never be deserialized by clients.

5. **Steps are the only place business logic lives**; endpoints build a context and send. Pass data between steps via mutable properties on the context (inherit `BaseGenericContext`). Keep each step single-responsibility.
   Why: steps are DI services (`AddScoped`) — individually resolvable and mockable; logic in `HandleAsync` is not.

6. **When the same logic appears in several steps, extract it instead of copy-pasting**: create `Steps/<Feature>/Shared/` with a static class whose single method takes all needed dependencies, and call it from the affected steps.
   Why: duplicated step bodies drift apart silently; a shared static entry point keeps one source of truth without inventing new DI coupling.

7. **`ToStatusCode()` has no 404 branch** — `INone` always maps to 400. When a lookup miss must be 404, send the response manually like `ServiceNotifications/Endpoints/Notifications/GetNotificationByIdEndpoint.cs` does (build the error envelope and `Send.ResponseAsync(..., (int)HttpStatusCode.NotFound, ct)`).
   Why: knowing this prevents "it returned 400 but should be 404" bugs and re-implementations of the mapping.

8. **Endpoint `Configure()` is uniform**: route + `Version(1)` + `SerializerContext(CoreSerializationContext.Default)` + `Description(...)` (`.WithTags(ApiTagConstants.<Service>, ...)`, `.Accepts<T>(MediaTypeNames.Application.Json)`, `.Produces...`/`.ProducesProblemFE(...)`). Opt-ins are explicit: `AllowAnonymous()`, `EnableAntiforgery()`, `Validator<T>()`, `Claims(...)`, and tags `EndpointTagConstants.RequireCaptchaVerification` (Altcha processor) or `ApiTagConstants.Persist` (durable-request processors) — both attached automatically by the service's `c.Endpoints.Configurator`.
   Why: the configurator and OpenAPI processors key off these exact markers; missing them silently skips antiforgery/captcha/idempotency.

## Red Flags

- `IMaybe<SomeOperationResult>` whose type carries `HttpStatusCode` or `ApiResponse` properties.
- Business logic (lookups, validation beyond shape, persistence) inside `HandleAsync` of a stepified endpoint.
- A new `string`-message error path, thrown exception, or untyped error instead of a `[None]` record.
- A DTO defined inside the service project that the frontend also needs (should be PublicApi, mirrored ×3).
- Copy-pasted step bodies that differ only by entity type (extract to `Steps/<Feature>/Shared/`).
- A second `Send.ObjectAsync`/`SendStringAsync` path where the `ApiResponse` envelope is expected.

## Evidence

- Stepified endpoint (gold): `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Endpoints/ReaderAccounts/RequestCreateByEmailEndpoint.cs`
- Direct-injection endpoint: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Admins/SearchAdminAccountsEndpoint.cs`
- Error record: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/ErrorResults/CreateReaderAccountError.cs`
- Envelope extensions: `snowcoreBlog.Backend/snowcoreBlog.Universal/PublicApi/Extensions/ApiResponseExtensions.cs`, `StatusCodeExtensions.cs`
- 404 pattern: `snowcoreBlog.Backend/Shared/Services/ServiceNotifications/Endpoints/Notifications/GetNotificationByIdEndpoint.cs`
