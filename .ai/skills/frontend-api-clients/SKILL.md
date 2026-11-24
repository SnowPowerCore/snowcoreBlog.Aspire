---
name: frontend-api-clients
description: >-
  How-to for frontend consumption of backend APIs — Refitter-generated Apizr
  interfaces (IArticlesApi etc.), .refitter configs and responseTypeOverride,
  the ×3 PublicApi mirroring, IApizrManager injection + ExecuteAsync + ToData,
  named SSE clients and the trailing-slash gotcha, Host vs Client handler
  registration. Use when connecting a Blazor page (public frontend or admin
  portal) to a backend endpoint.
scope: project
---

# Frontend API Clients (Refitter + Apizr)

Use this skill when a frontend page needs to call a backend endpoint, or when a
backend endpoint's contract changes.

## When to Invoke

- "call the articles API from a page", "add the new endpoint to the frontend"
- Backend route/DTO changed and clients must follow
- Wiring an SSE streaming endpoint into a page

## When NOT to Invoke

- Implementing the backend endpoint itself (see `fastendpoints-endpoints`)
- Fusion realtime compute clients (separate `AddFusionClient` wiring per module)

## Background

Client interfaces (`IArticlesApi`, `IReaderAccountManagementApi`, ...) are
**Refitter-generated** from the running service's OpenAPI JSON and executed via
**Apizr** managers. The generated sources + `.refitter` configs live in
`snowcoreBlog.Universal/PublicApi/Api/` — and that whole PublicApi tree is
**duplicated ×3** (Backend, Frontend, Console). The frontend compiles only
against its own copy.

## Step 1 — Update the contract in ALL THREE mirrors

Hand-add the method matching the future regeneration (do not leave mirrors to
diverge), and add the matching `responseTypeOverride` entry:

```csharp
// IArticlesApi.cs — in Backend, Frontend AND Console copies
[Headers("Accept: application/json, application/problem+json")]
[Get("/{slug}/v1")]
Task<IApiResponse<ApiResponse>> GetArticleBySlug(string slug, [RequestOptions] IApizrRequestOptions options);
```

```jsonc
// .refitter-articles (same folder, ×3)
{
  "openApiPath": "https://localhost/api/articles/openapi/v1.json",
  "responseTypeOverride": {
    "GetArticleBySlug": "IApiResponse<ApiResponse>"
  }
}
```

Envelope endpoints always map to `IApiResponse<ApiResponse>`. Endpoints that
stream (`text/event-stream`) are **not Refit-expressible** — remove them from
the interface + overrides and use the SSE client instead (Step 4).

## Step 2 — Register/inject the Apizr manager

Managers are registered Host-side (`Frontend/Host/Extensions/ApizrConfigurationExtensions.cs`
→ `AddServerSideApizrManagers`, with `HttpContextCookiesPropagationHandler`) and
Client-side (`Frontend.ClientShared/Extensions/ApizrConfigurationExtensions.cs`
→ `AddClientSideApizrManagers`, with `IncludeCookiesHandler`). Pages just inject:

```razor
@inject IApizrManager<IArticlesApi> ArticlesApi
```

## Step 3 — Execute + unwrap with `ToData<T>`

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    await base.OnAfterRenderAsync(firstRender);
    if (firstRender)
    {
        await LoadArticlesAsync();
        await InvokeAsync(StateHasChanged);
    }
}

private async Task LoadArticlesAsync()
{
    using var tokenSource = new CancellationTokenSource();
    using var response = await ArticlesApi.ExecuteAsync(
        (opt, api) => api.GetArticlesCached(opt), o => o.WithCancellation(tokenSource.Token));

    var data = response.ToData<List<ArticleDto>>(out var errors);
    if (data is not default(List<ArticleDto>))
        _articles = data;
}
```

`ToData<T>` (in `snowcoreBlog.Universal/PublicApi/Extensions/ApizrResponseExtensions.cs`,
each mirror) returns `default` and fills `errors` on failure — surface `errors`
in the page (e.g. `FluentMessageBar Intent="MessageIntent.Error"`).

Mutating endpoints carry an antiforgery header — the generated interface declares
it explicitly (`[Header("RequestVerificationToken")] string requestVerificationToken`);
the token comes from the service's `GetAntiforgeryTokenEndpoint`, and named
antiforgery clients / `AddAntiforgeryRetryHandler()` exist for exactly this flow.

## Step 4 — SSE endpoints use the named streaming clients

```csharp
// Frontend.Infrastructure/Protocol/Sse/SseClientExtensions.cs
public static async IAsyncEnumerable<SseFrame> PostServerSentEventsAsync(
    this HttpClient httpClient, string? requestUri, object? requestBody,
    Action<HttpRequestMessage>? configureRequest = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default);

// StepProgressStreamReader.cs
public static async Task<StepProgressFinalEvent?> ReadAsync(
    IAsyncEnumerable<SseFrame> frames, Action<string> onStepProgress,
    CancellationToken cancellationToken = default);
```

Registered as named clients (e.g. `"ReadersManagementSseClient"`, 3-minute
timeout) on BOTH Host and Client sides. **Base address must end with a
trailing slash** (`"https://localhost/api/readers/"`) — `HttpClient` otherwise
replaces the last segment (`/api/readers` + `create/x/v1` → `/api/create/x/v1`
→ 404). Apizr managers join paths themselves and are unaffected.

## Red Flags

- Editing generated `IArticlesApi` in one mirror only.
- New endpoint missing from `responseTypeOverride` → next Refitter regen diverges.
- A page constructing its own `HttpClient` where a manager exists.
- SSE client base address without the trailing slash.
- Reading `response.Result.Data` manually instead of `ToData<T>` (loses error extraction).

## Reference implementations

- Page consuming Apizr: `snowcoreBlog.Frontend/Modules/Articles/Pages/ArticlesHomePage.razor`
- Admin portal equivalent (server-rendered): `snowcoreBlog.AdminFrontend/Components/Pages/Readers.razor`
- Generated interface + config: `snowcoreBlog.Frontend/snowcoreBlog.Universal/PublicApi/Api/IArticlesApi.cs`, `Api/.refitter-articles`
- SSE plumbing: `snowcoreBlog.Frontend/snowcoreBlog.Frontend.Infrastructure/Protocol/Sse/`, `Frontend/Host/Extensions/ApizrConfigurationExtensions.cs`
