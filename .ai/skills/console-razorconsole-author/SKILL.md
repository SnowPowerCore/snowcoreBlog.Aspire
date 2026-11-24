---
name: console-razorconsole-author
description: >-
  How-to for the snowcoreBlog Console TUI (RazorConsole port) — UseRazorConsole
  host shape, Apizr managers with the shared ConsoleAuthCookies cookie jar,
  KnownScreenRoutes navigation, the InvokeAsync dispatcher rule, LocalStorage
  and rebuild gotchas. Use when adding console screens/features or debugging
  the terminal app.
scope: project
---

# Console (RazorConsole) Author

Use this skill when working in `snowcoreBlog.Console/App` — the terminal client
built on RazorConsole.Core 0.7.0.

## When to Invoke

- "add a console screen", "console renders wrong", "console can't log in"
- Any change under the Console submodule's `App/` project

## When NOT to Invoke

- The public Blazor frontend (different host, different render loop)
- Backend endpoints consumed by the console (see `fastendpoints-endpoints`)

## Background

The console is a Blazor-flavored terminal app: `Host.CreateApplicationBuilder`
+ `host.UseRazorConsole<App>()` with `EnableTerminalResizing` +
`EnableMouseEvents`. Pages are `.razor` files using RazorConsole components
(`Panel`, `Markup`, `Rows`, `Columns`, `TextButton`, `Figlet`, `Newline`) under
`Pages/` (Home, ArticleDetails, CreateArticle, Login, Register,
RegistrationConfirmation, CompleteRegistration, BecomeAuthor, About) with
`Layout/MainLayout.razor` (header nav + notification bar + body + footer).
Navigation is a route stack: `KnownScreenRoutes` maps screen names to routes
(`/` default), `ConsoleNavigationService` raises `NavigationRequested`, and
`Components/App.razor` bridges it to `NavigationManager` via `InvokeAsync`
(thread-safe from steps). Localization via kli.Localize
(`Resources/Resource_en.json`).

## Rules

1. **The dispatcher rule is absolute**: ConfigureAwait.Fody strips the sync
   context, so EVERY `StateHasChanged` after an `await` must be
   `await InvokeAsync(StateHasChanged)` — violating it throws
   `InvalidOperationException` and kills the render loop mid-flight (stuck
   spinner). Services catch their own exceptions so pages never break rendering.

2. **Backend access = Apizr managers over the gateway with a shared cookie
   jar**: each manager is configured
   `.WithBaseAddress($"{gatewayBaseUrl}/api/<svc>")` +
   `.ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<ConsoleAuthCookies>().CreateHandler())`
   — one `ConsoleAuthCookies` cookie container shares the auth session across
   all managers. A named SSE client (`SseClientNames.ReadersManagement`) covers
   registration streaming, `Altcha.CreateSolver()` covers captcha.

3. **MediatR is required**: Apizr's generated `Configure*ApizrManagers` adds
   `WithMediation`, so `ApizrMediator` needs `MediatR.IMediator` — with
   `ValidateOnBuild` the host crashes at startup without the MediatR
   registration. Keep it.

4. **LocalStorage reads must guard**: `ILocalStorageService.Get<T>` (Hanssens)
   THROWS `ArgumentNullException` on a missing key — always `HasKey(key)`
   first (the dismissed-notifications store relies on this).

5. **Rebuild discipline**: the console session runs `dotnet run --no-build` —
   stop the running app (or kill `snowcoreBlog.Console.App`) before rebuilding,
   or MSB3027 file locks abort the build.

6. **Gateway-offline resilience**: Apizr wraps connection failures in
   exceptions with unhelpful messages ("Object reference not set") — unwrap one
   `InnerException` level (the `Describe(ex)` helper pattern) and let services
   catch everything; offline panels + Retry buttons are the established UX.

7. **Two checkouts exist**: the workspace-nested submodule
   (`snowcoreBlog.Console/App`) is the RazorConsole port the AppHost references;
   a sibling clone may hold the OLD plain-console app. Always confirm which
   checkout you are editing (the AppHost line for the console is commented out
   in root `Program.cs` — the app runs standalone).

## Red Flags

- Bare `StateHasChanged()` after `await`.
- A new Apizr manager without the shared cookie-jar handler (auth silently
  broken for that manager).
- Unhandled exceptions escaping page event handlers (render loop death).
- Reading LocalStorage without `HasKey`.
- Editing the sibling old-console clone by mistake.

## Reference implementations

- Host + managers: `snowcoreBlog.Console/App/Program.cs`
- Navigation bridge: `snowcoreBlog.Console/App/Components/App.razor`
- Page + components: `snowcoreBlog.Console/App/Pages/AboutPage.razor`, `Layout/MainLayout.razor`
- Launch steps: `Steps/` (prefetch antiforgery + auth state + notifications)
