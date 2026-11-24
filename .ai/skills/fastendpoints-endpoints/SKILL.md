---
name: fastendpoints-endpoints
description: >-
  How-to for authoring FastEndpoints HTTP endpoints in snowcoreBlog backend
  services: Configure() conventions (route, Version, SerializerContext,
  validators, antiforgery/captcha/persist opt-ins), the ApiResponse envelope
  contract, response-sending patterns (stepified envelope, SSE stream, manual
  404), and the YARP route that must accompany every new endpoint. Use when
  asked to add, change, or debug any backend HTTP endpoint.
scope: project
---

# FastEndpoints Endpoints

Use this skill to add or modify an HTTP endpoint in a backend service
(`snowcoreBlog.Backend/**/Endpoints/**`), and to wire the matching YARP route.

## When to Invoke

- "add an endpoint", "expose an API for X", "return X from the articles service"
- Debugging envelope/status-code mismatches (400 vs 404, `StatusCode: -1` bodies)
- A new route must appear (or be protected) behind the YARP ingress

## When NOT to Invoke

- Worker services (IAM, Email, Push) — they expose no business endpoints
- Frontend consumption of the endpoint (see `frontend-api-clients` skill)
- Step/delegate/context internals (see `stepified-process-handlers` skill)

## Background: the ApiResponse envelope

Every endpoint responds with `ApiResponse` (in `snowcoreBlog.Universal/PublicApi/Utilities/Api`):

| Property | Success | Error |
|---|---|---|
| `StatusCode` (business) | `0` | `-1` |
| `DataCount` | `1`, or `ICollection.Count` | `0` |
| `Data` | DTO serialized to `JsonDocument` | `null` |
| `Errors` | empty | `"{Code}: {Description}"` per detail, or the message |

`IMaybe<T>` extensions produce it: `ToApiResponse(serializerOptions)` + `ToStatusCode()`
(`Some<T>` → HTTP 200, `INone<T>` → HTTP 400, anything else → 500). Source-gen
serialization is mandatory: `SerializerContext(CoreSerializationContext.Default)`.

**Validation failures also return the envelope:** `Validator<T>()` failures come
back as HTTP 400 whose BODY is the `ApiResponse` envelope — the global
`c.Errors.ResponseBuilder` (in `FastEndpointsConfigurationExtensions.cs`) reshapes
FastEndpoints problem details via `ErrorResponseUtilities.ApiResponseWithErrors`.
`.ProducesProblemFE(...)` is OpenAPI metadata only, not the actual body shape.

## Step 1 — Skeleton (Configure + HandleAsync)

```csharp
public class GetArticleBySlugEndpoint : EndpointWithoutRequest<ApiResponse?>
{
    public IOptions<JsonOptions> JsonOptions { get; set; }

    public override void Configure()
    {
        Get("{slug}");
        Version(1);                                        // real route becomes /{slug}/v1
        SerializerContext(CoreSerializationContext.Default);
        ResponseCache(1800);                               // GETs may cache at the edge/browser
        Description(b => b
            .WithTags(ApiTagConstants.Articles)
            .Produces<ApiResponseForOpenApi<ArticleDto>>((int)HttpStatusCode.OK, MediaTypeNames.Application.Json)
            .Produces<ApiResponse>((int)HttpStatusCode.NotFound)
            .ProducesProblemFE((int)HttpStatusCode.BadRequest));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // build IMaybe<ArticleDto> and send — see Step 2
    }
}
```

`ApiResponseForOpenApi<T>` keeps frontend NSwag codegen typed while the wire
body stays the `ApiResponse` envelope.

Opt-in markers, applied when applicable: `AllowAnonymous()`, `EnableAntiforgery()`,
`Validator<SomeValidator>()`, `Claims(AdminClaimConstants.AdminAccountClaimKey)`,
`Tags(EndpointTagConstants.RequireCaptchaVerification)` (Altcha pre-processor),
`Tags(ApiTagConstants.Persist)` (durable-request processors). The service's
`c.Endpoints.Configurator` (in `Extensions/Startup/FastEndpointsConfigurationExtensions.cs`)
attaches the processors for these tags automatically. `EnableAntiforgery()` imposes
a client contract: callers must send the antiforgery token from the service's
`GetAntiforgeryTokenEndpoint` (header documented by `AntiforgeryHeaderProcessor`).
GET endpoints may add response caching, e.g. `ResponseCache(1800)` (see
`GetArticleBySlugEndpoint`); typed OpenAPI wrappers (`ApiResponseForOpenApi<T>`)
keep frontend NSwag codegen typed.

## Step 2 — Send the response

Stepified flow (delegate already returns `IMaybe<TDto>`):

```csharp
var result = await RequestCreateReaderAccount(context, token);

await Send.ResponseAsync(
    result?.ToApiResponse(serializerOptions: JsonOptions.Value.SerializerOptions),
    result?.ToStatusCode() ?? (int)HttpStatusCode.InternalServerError,
    ct);
```

Direct (non-stepified) endpoint — build the `IMaybe` inline:

```csharp
var items = await AdminRepository.SearchAsync(search, page, pageSize, ct);
var result = Maybe.Create(items.Select(static a => a.ToDto()).ToList());

await Send.ResponseAsync(
    result?.ToApiResponse(serializerOptions: JsonOptions.Value.SerializerOptions),
    result?.ToStatusCode() ?? (int)HttpStatusCode.InternalServerError,
    ct);
```

**404 note:** `ToStatusCode()` never returns 404 (`INone` → 400). For
resource-missing semantics, send manually:

```csharp
await Send.ResponseAsync(
    ErrorResponseUtilities.ApiResponseWithErrors([$"Article with slug '{slug}' was not found."], (int)HttpStatusCode.NotFound),
    (int)HttpStatusCode.NotFound,
    ct);
```

FastEndpoints reference: `GetArticleBySlugEndpoint.cs` in the Articles service.
The same 404 pattern also exists in FastEndpoints form in
`Shared/Services/ServiceNotifications/Endpoints/Notifications/GetNotificationByIdEndpoint.cs`.

**SSE step-progress variant** (long flows): keep Configure as above but add
`.Produces((int)HttpStatusCode.OK, contentType: MediaTypeNames.Text.EventStream)`
and wrap the invocation:

```csharp
await HttpContext.SendStepProgressStreamAsync(
    async (reporter, token) =>
    {
        var context = new RequestCreateReaderAccountContext(req);
        context.AttachStepProgressReporter(reporter);
        var result = await RequestCreateReaderAccount(context, token);
        return (result?.ToApiResponse(serializerOptions: JsonOptions.Value.SerializerOptions),
            result?.ToStatusCode() ?? (int)HttpStatusCode.InternalServerError);
    },
    ct);
```

## Step 3 — Wire the YARP route

New endpoints under an existing service prefix need nothing (catch-all routes
like `/api/articles/{**catch-all}` already exist in the AppHost
`appsettings.Development.json`). For a NEW prefix or a non-default auth
requirement: add/adjust the cluster + route entry, choose `anonymous` vs a
policy explicitly, and if a new policy is needed add it to the root
`Program.cs` `.WithAuthPolicies(...)` **using string literals** ("AdminScheme",
"adminAccount", ...) kept in sync with backend `SecurityConstants` — the
AppHost does not compile-reference service assemblies.

## Reference implementations

- Stepified + SSE: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Endpoints/ReaderAccounts/RequestCreateByEmailEndpoint.cs`
- Direct + Claims: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Admins/SearchAdminAccountsEndpoint.cs`
- Endpoint configurator (captcha/persist processors): `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/FastEndpointsConfigurationExtensions.cs`
