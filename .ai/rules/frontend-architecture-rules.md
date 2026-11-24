---
description: Frontend architecture — Core→Infrastructure→ClientShared→Client→Host layering, feature modules, data-load pattern, Apizr calls, TimeWarp.State, Fusion clients, AdminFrontend mirror, ×3 contract mirroring
globs: ["snowcoreBlog.Frontend/**/*.cs", "snowcoreBlog.Frontend/**/*.razor", "snowcoreBlog.AdminFrontend/**/*.cs", "snowcoreBlog.AdminFrontend/**/*.razor"]
alwaysApply: false
---

# Frontend Architecture Rules

Use this rule when adding pages/features to the public Blazor frontend or the
admin portal, wiring HTTP/Fusion clients, managing UI state, or deciding where
new frontend code belongs.

## Service Context

- **Public frontend** (`snowcoreBlog.Frontend`, submodule) layering, dependency direction: `Core` (state/models) → `Infrastructure` (HTTP/SSE/auth plumbing) → `ClientShared` (layout, shared services, registration entry `AddClient()`) → `Client` (WASM entry) + `Host` (ASP.NET host). **Feature work goes through `Modules/<Area>/`** (`Articles`, `AuthorsManagement`, `ReadersManagement`, `ServiceNotifications`, `SharedComponents`), each with `Pages/ Features/ Components/ Extensions/`.
- **Admin portal** (`snowcoreBlog.AdminFrontend`, lives in the root repo) is a **single server-only Blazor Web App project** (`Components/Pages/`, `InteractiveServer` pinned via `App.razor`, no WASM client/Host split), with `AddAdminClient()` (FluentUI) + `AddAdminDataClients()` (host-side Apizr managers) called from `Program.cs`.
- Global render mode is InteractiveAuto; backend calls go through YARP at `https://localhost/api/<service>`.

## Architecture Rules

1. **Put feature code in `Modules/<Area>/`, not in `ClientShared` or `Client`.** `ClientShared` only holds layout/shared services; `Client`/`Host` only entry/wiring.
   Why: modules are the isolation unit (own `_Imports.razor`, own DI extension, own tests); shared-layer bloat slows WASM load and couples features.

2. **Load data with the first-render pattern** (avoids prerender double-fetch and works with ConfigureAwait.Fody, which resumes continuations off the Blazor dispatcher):
   ```razor
   @attribute [StreamRendering]

   protected override async Task OnAfterRenderAsync(bool firstRender)
   {
       await base.OnAfterRenderAsync(firstRender);
       if (firstRender)
       {
           await LoadArticlesAsync();
           await InvokeAsync(StateHasChanged);   // marshal back to the dispatcher
       }
   }
   ```
   Why: plain `StateHasChanged()` after an `await` can throw `InvalidOperationException` (wrong dispatcher) because the repo-wide Fody weaving strips the sync context.

3. **Call backend APIs only through Apizr managers**: inject `IApizrManager<IArticlesApi>` (etc.) and unwrap with the envelope helper:
   ```csharp
   using var response = await ArticlesApi.ExecuteAsync(
       (opt, api) => api.GetArticlesCached(opt), o => o.WithCancellation(tokenSource.Token));
   var data = response.ToData<List<ArticleDto>>(out var errors);
   ```
   `ToData` lives in the mirrored `snowcoreBlog.Universal/PublicApi/Extensions/ApizrResponseExtensions.cs` — never hand-roll `HttpClient` calls for existing APIs.
   Why: Apizr managers carry antiforgery/cookies/resilience handlers; raw clients silently miss them.

4. **UI state uses TimeWarp.State** — a `State<T>` with nested `ActionSet`/`Action`/`ActionHandler` partials per feature, registered through `AddTimeWarpState(options => options.Assemblies = [...])` in `ClientShared/Extensions/ServiceCollectionExtensions.cs`. Do not introduce another state framework, and do not store mutable cross-dispatch data in handler fields (handlers are transient) — model it as state properties.
   Why: handlers are resolved per dispatch; handler fields reset every call.

5. **Fusion clients are registered per module** (`Modules/<Area>/Extensions/FusionConfigurationExtensions.cs` → `AddFusionClient`): `fusion.AddBlazor()`, `fusion.AddClient<INickNameAvailabilityService>()`, `fusion.Rpc.AddWebSocketClient(gatewayUrl + "/fusion/ws")`. **Registration order matters** — `AddReadersManagement` before `AddAuthorsManagement` (authors replaces the shared `RpcWebSocketClientOptions` singleton; reversed order breaks the authors RPC peer).
   Why: authors `Replace()`es the shared Fusion singletons (`RpcWebSocketClientOptions` **and** `RpcOutboundCallOptions`) — a third Fusion peer module must keep the same order.

6. **Admin portal pages are server-rendered and injected with scoped admin API services** — see `snowcoreBlog.AdminFrontend/Components/Pages/Readers.razor` (`@inject IApizrManager<IAdminReadersApi>` + `ExecuteAsync` + `ToData` + `await InvokeAsync(StateHasChanged)`). Keep new admin pages in `Components/Pages/`.
   Why: `App.razor` pins `InteractiveServer`; WASM pages there would never activate.

7. **Any PublicApi contract change is mirrored ×3** (Backend, Frontend, Console copies under `snowcoreBlog.Universal/PublicApi`) in the same changeset — the frontend compiles only against its own copy. Regenerated Refitter interfaces and `.refitter` overrides must be updated in all three (see the `frontend-api-clients` skill).
   Why: a missing mirror breaks unrelated repo builds.

## Red Flags

- `new HttpClient()` or a feature-private named client where an Apizr manager exists.
- `StateHasChanged()` called bare after `await` (no `InvokeAsync`).
- Business pages added to `ClientShared` (public frontend) or anywhere outside `Components/Pages/` (admin portal).
- Editing `IArticlesApi`-style generated interfaces without updating the `.refitter` file and the other two mirrors.
- A new state framework (Redux devtools, custom stores, event aggregator) next to TimeWarp.State.
- WASM-rendered pages in `snowcoreBlog.AdminFrontend` (single server-only project — no WASM bundle exists).

## Evidence

- First-render load: `snowcoreBlog.Frontend/Modules/Articles/Pages/ArticlesHomePage.razor`
- Apizr call + ToData: same file + `snowcoreBlog.Frontend/snowcoreBlog.Universal/PublicApi/Extensions/ApizrResponseExtensions.cs`
- TimeWarp state: `snowcoreBlog.Frontend/Modules/ReadersManagement/Features/Register/RegisterState.cs` (+ `.SetCurrentInput.cs`)
- Fusion client: `snowcoreBlog.Frontend/Modules/ReadersManagement/Extensions/FusionConfigurationExtensions.cs`
- Admin page pattern: `snowcoreBlog.AdminFrontend/Components/Pages/Readers.razor`
