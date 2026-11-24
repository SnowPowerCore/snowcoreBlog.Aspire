---
name: fusion-realtime-author
description: >-
  How-to for ActualLab.Fusion realtime compute services in snowcoreBlog —
  IComputeService contracts with [ComputeMethod], Invalidation.Begin in Notify
  methods, backend AddFusionConfiguration, per-module frontend AddFusionClient
  (websocket peer + registration ORDER), and ComputedStateComponent UI
  consumers. Use when adding or changing realtime values like nickname
  availability.
scope: project
---

# Fusion Realtime Author

Use this skill when a value must update live across clients (availability
checks, live counters) or when touching the Fusion wiring.

## When to Invoke

- "nickname should update without refresh", "realtime availability"
- Adding a compute service, a claim-provider style invalidation, or a UI
  component that re-renders on compute changes

## When NOT to Invoke

- One-shot request/response (plain Apizr call)
- SSE step progress of a running flow (see `sse-step-progress`)

## Background

Fusion contracts live in the ×3-mirrored PublicApi
(`PublicApi/Services/INickNameAvailabilityService.cs`); implementations in the
owning service (`ReadersManagement/Services/NickNameAvailabilityService.cs`).
Frontends become replicas over a websocket per gateway route (`/fusion/ws`).
ActualLab.Fusion is 15.x (packages in root `Directory.Packages.props`).

## Step 1 — Contract (PublicApi, mirrored ×3)

```csharp
using ActualLab.Fusion;

public interface INickNameAvailabilityService : IComputeService
{
    [ComputeMethod]
    Task<NickNameAvailability> GetAsync(string nickName, CancellationToken cancellationToken = default);

    Task NotifyReservedAsync(string nickName, CancellationToken cancellationToken = default);
}
```

## Step 2 — Backend implementation + invalidation

```csharp
public class NickNameAvailabilityService(...) : INickNameAvailabilityService
{
    [ComputeMethod]
    public virtual async Task<NickNameAvailability> GetAsync(string nickName, CancellationToken cancellationToken = default)
    { ... }

    public Task NotifyReservedAsync(string nickName, CancellationToken cancellationToken = default)
    {
        using (Invalidation.Begin())
        {
            _ = GetAsync(candidate, cancellationToken);   // registers the invalidation key; does NOT run
        }
        return Task.CompletedTask;
    }
}
```

`[ComputeMethod]` methods must be `virtual`; invalidation is declarative —
calling `GetAsync` inside `Invalidation.Begin()` only registers the key.
Register the service with `builder.Services.AddFusionConfiguration()`
(ReadersManagement `Extensions/Startup/FusionConfigurationExtensions.cs`).

## Step 3 — Frontend client (per module, ORDER matters)

`Modules/<Area>/Extensions/FusionConfigurationExtensions.cs` → `AddFusionClient`:

```csharp
var fusion = services.AddFusion();
fusion.AddBlazor();                                    // registers scoped CircuitHub — required by ComputedStateComponent
fusion.AddClient<INickNameAvailabilityService>();
fusion.Rpc.AddWebSocketClient($"{gatewayUrl}{FusionWebSocketSuffix}");   // wss://localhost/api/readers + /fusion/ws
services.AddScoped<IUpdateDelayer>(c => new UpdateDelayer(c.UIActionTracker(), 0.25));
```

**Registration order is load-bearing**: `AddReadersManagement` must run BEFORE
`AddAuthorsManagement` in `ClientShared/Extensions/ServiceCollectionExtensions.cs`
— the authors module `Replace()`es the shared Fusion singletons
(`RpcWebSocketClientOptions` and `RpcOutboundCallOptions`); reversed order
breaks the authors RPC peer ("Endpoint not found"). A THIRD Fusion module must
respect the same chain and re-check what it replaces.

## Step 4 — UI consumer

```razor
@inherits ComputedStateComponent<NickNameAvailability>
@inject INickNameAvailabilityService GetNickNameAvailability
```

`ComputedStateComponent<T>` (ActualLab.Fusion.Blazor) re-renders when the
compute invalidates — this is why `fusion.AddBlazor()` is mandatory. See
`Modules/ReadersManagement/Components/NickName/NickNameAvailabilityIndicator.razor`.
Note the repo-wide ConfigureAwait.Fody weaving: any manual `StateHasChanged`
after `await` needs `await InvokeAsync(StateHasChanged)`.

## Red Flags

- A second module calling `AddWebSocketClient` without checking what the
  previous module replaced.
- `[ComputeMethod]` without `virtual`, or invalidation that CALLS the method
  outside `Invalidation.Begin()` (that EXECUTES it).
- Fusion contract added to one PublicApi mirror only.
- `AddBlazor()` forgotten on a frontend that uses `ComputedStateComponent`
  (CircuitHub resolution failure).
- Hardcoding the websocket URL instead of the module's gateway-url constant.

## Reference implementations

- Contract + impl + invalidation: `snowcoreBlog.Frontend/snowcoreBlog.Universal/PublicApi/Services/INickNameAvailabilityService.cs`, `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Services/NickNameAvailabilityService.cs`
- Client wiring: `snowcoreBlog.Frontend/Modules/ReadersManagement/Extensions/FusionConfigurationExtensions.cs` (and the AuthorsManagement twin)
- Order coupling: `snowcoreBlog.Frontend/snowcoreBlog.Frontend.ClientShared/Extensions/ServiceCollectionExtensions.cs`
- UI: `snowcoreBlog.Frontend/Modules/ReadersManagement/Components/NickName/NickNameAvailabilityIndicator.razor`
