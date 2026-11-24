---
name: admin-portal-page-author
description: >-
  How-to for admin portal pages (snowcoreBlog.AdminFrontend) — host-only Apizr
  managers with cookie propagation, server-rendered ClientShared/Pages pattern
  (search + paged table + delete confirm), backend Claims guard, and the
  Login/InviteAccept token pages. Use when adding or changing an admin portal
  page or its data wiring.
scope: project
---

# Admin Portal Page Author

Use this skill to add or change a page in the admin portal
(`snowcoreBlog.AdminFrontend`, lives in the ROOT repo, not a submodule).
Dedicated `/api/<svc>/admin/**` anonymous catch-all routes exist for readers,
authors and articles; documents, notifications, access and admins ride the
generic anonymous catch-alls — either way the BACKEND enforces `adminAccount`.

## When to Invoke

- "add an admin page for X", "extend the readers/articles/notifications admin table"
- Admin portal can't reach a backend endpoint (cookies/401 problems)

## When NOT to Invoke

- Public frontend pages — different render model (InteractiveAuto, first-render
  load); see `frontend-architecture-rules`
- The backend admin endpoints themselves — see `fastendpoints-endpoints`

## Background

The portal is pinned `InteractiveServer` (`App.razor`): pages NEVER run in
WASM, so all data access is host-side. Backend calls go through YARP
(`https://localhost/api/<service>`) carrying the admin cookie
(`.DotNet.Application.Admin.SystemKey`), minted by the AdminsManagement
magic-link flow. Endpoints must be reachable via the anonymous
`/api/<svc>/admin/**` catch-alls and enforce `adminAccount` themselves.

## Step 1 — Backend guard + contract

Guard the endpoint with `Claims(AdminClaimConstants.AdminAccountClaimKey)`
(see `SearchAdminAccountsEndpoint`) and put the DTO in PublicApi
(`AdminManagementDtos.cs`, mirrored ×3). List endpoints follow the
`search/page/pageSize` query convention:
`SearchAsync(search, page, pageSize, ct)` on the repository.

## Step 2 — Register the Apizr manager (NO trailing slash)

`Extensions/AdminDataClientCollectionExtensions.cs` → `AddAdminDataClients()`
(called from `Program.cs`; NOT `AddAdminClient`, which is the shared FluentUI
registration in `Extensions/AdminClientServiceCollectionExtensions.cs`):

```csharp
// per service: base https://localhost/api/readers | /authors | /articles
//             | /documents | /notifications | /access | /admins   — NO trailing slash
builder.AddApizr(registry => registry.AddManagerFor<IAdminReadersApi>(),
    options => options.WithBaseAddress("https://localhost/api/readers")
        .WithHttpMessageHandler<AdminCookiesPropagationHandler>() ...);
// primary handler: new HttpClientHandler { UseCookies = false }  (no container cookie jar)
```

`Handlers/AdminCookiesPropagationHandler.cs` copies the browser `Cookie`
header onto the outgoing request AND relays gateway `Set-Cookie` back to the
browser — this is why login/invite Set-Cookie flows work through the portal.
Public-frontend SSE clients use trailing slashes; the ADMIN managers do NOT.

## Step 3 — The page pattern (Components/Pages/)

Pages live in `snowcoreBlog.AdminFrontend/Components/Pages/` (server-rendered,
FluentUI). Copy the `Readers.razor` skeleton:

```razor
@page "/readers"
@inject IApizrManager<IAdminReadersApi> ReadersAdmin

@code {
    private bool _loading;
    private List<string> _errors = [];

    protected override void OnInitialized() => _ = LoadAsync(1);

    private async Task LoadAsync(int page)
    {
        _loading = true; _errors = []; StateHasChanged();
        using var response = await ReadersAdmin.ExecuteAsync((options, api) =>
            api.SearchAccounts(page, _pageSize, _search, options));
        var data = response.ToData<List<ReaderAccountAdminDto>>(out var errors);
        if (errors.Count > 0) _errors = errors;
        else if (data is not null) _accounts = data;
        _loading = false;
        await InvokeAsync(StateHasChanged);   // ConfigureAwait.Fody: marshal back to dispatcher
    }
}
```

Conventions across existing pages (Readers/Authors/Articles/Documents/
Notifications/AccessRestrictions): search box + paged table + delete-confirm
dialog; `FluentProgressRing` while `_loading`; errors via
`FluentMessageBar Intent="MessageIntent.Error"`; init-only DTO records are NOT
`@bind`-able — use local mutable form-model classes; `Encoding`/`System.Text`
are not implicit usings (fully qualify).

## Step 4 — Auth pages (token in the query)

`Login.razor` (`@inject IApizrManager<IAdminsApi>`): `OnInitialized` reads
`?token=` — present → confirm mode calls the login-confirm API and sets
`_signedIn`; absent → email form calling `api.RequestLoginLink(...)`
("single-use sign-in link is on its way" info bar). `InviteAccept.razor`
handles `/invite?token=` via `invites/resolve` + accept. Page access is gated
by `RequireAdminSignInMiddleware`: any document request without the
admin cookie redirects to `/login`; API-level 401/403 responses still surface
through the `AdminApiErrors.IsUnauthorizedOrForbidden` hint (e.g. cookie
expiring while a circuit stays open on one page).

## Red Flags

- Putting pages outside `Components/Pages/` or making them WASM-interactive —
  `App.razor` pins InteractiveServer and the project is server-only.
- Base address with a trailing slash on admin managers (public-frontend SSE
  clients need it; admin managers don't).
- Bypassing `AdminCookiesPropagationHandler` → login/logout/invite cookies stop
  flowing.
- A "reachable without auth" admin endpoint — backend must enforce
  `Claims(adminAccount)`; the YARP catch-alls are anonymous by design.
- Hand-rolling `HttpClient` instead of the registered manager.

## Reference implementations

- Table page: `snowcoreBlog.AdminFrontend/Components/Pages/Readers.razor`
- Manager wiring: `snowcoreBlog.AdminFrontend/Extensions/AdminDataClientCollectionExtensions.cs`
- Cookie handler: `snowcoreBlog.AdminFrontend/Handlers/AdminCookiesPropagationHandler.cs`
- Sign-in gate: `snowcoreBlog.AdminFrontend/Middleware/RequireAdminSignInMiddleware.cs` (mirrors `AuthCookieConstants` literals by hand)
- Token pages: `.../Components/Pages/Login.razor`, `.../Components/Pages/InviteAccept.razor`
- Backend guard example: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Admins/SearchAdminAccountsEndpoint.cs`
