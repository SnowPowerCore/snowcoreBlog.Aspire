---
name: sse-step-progress
description: >-
  How-to for the SSE step-progress streaming pattern end-to-end — backend
  SendStepProgressStreamAsync + AttachStepProgressReporter/ReportStepProgress
  in steps, the text/event-stream endpoint metadata, PublicApi StepProgress
  contracts, and the frontend SseClient/StepProgressStreamReader + named-client
  trailing-slash rules. Use when adding live progress to a multi-step flow or
  consuming a streaming endpoint from the frontend.
scope: project
---

# SSE Step Progress

Use this skill when a multi-step flow should stream live progress to the UI,
or when a page needs to consume a `text/event-stream` endpoint.

## When to Invoke

- "show progress while X runs", "stream step status", "SSE endpoint"
- A stepified flow's steps should report what they are doing

## When NOT to Invoke

- Plain request/response endpoints (normal Apizr call — see `frontend-api-clients`)
- Realtime compute values (Fusion — see `fusion-realtime-author`)

## Background

Infrastructure lives in `snowcoreBlog.Backend/Shared/Infrastructure/StepProgress/`:
a channel-backed `StepProgressReporter` broadcaster, context extensions
(`AttachStepProgressReporter` / `ReportStepProgress` — the reporter is OPTIONAL,
steps no-op when absent), and `SendStepProgressStreamAsync` which runs the
process concurrently, streams `StepProgressEvent` frames (`step-progress`), and
terminates with ONE `StepProgressFinalEvent` (`final-result`) carrying the
`ApiResponse` + status code. Contracts live in PublicApi
`Utilities/Sse/` (mirrored ×3) with `[JsonSerializable]` entries in
`CoreSerializationContext`. No manual flush calls — FastEndpoints flushes.

## Step 1 — Endpoint (backend)

Keep the normal stepified endpoint, add event-stream metadata, and wrap the
invocation (`RequestCreateByEmailEndpoint` is the reference):

```csharp
Description(b => b.WithTags(ApiTagConstants.ReaderAccountManagement, ApiTagConstants.Captcha)
    .Produces((int)HttpStatusCode.OK, contentType: MediaTypeNames.Text.EventStream)
    .ProducesProblemFE((int)HttpStatusCode.BadRequest));

public override async Task HandleAsync(RequestCreateReaderAccountDto req, CancellationToken ct)
{
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
}
```

Endpoint tags (antiforgery/captcha) stay as for any anonymous surface.

## Step 2 — Steps report at their start

```csharp
context.ReportStepProgress(ReaderAccountStepProgressCodes.ValidatingEmailDomain);
```

Step codes are constants in PublicApi `Constants/` (mirrored ×3) so the
frontend can localize them.

## Step 3 — Refit is out (remove, don't convert)

`text/event-stream` is not Refit-expressible: REMOVE the method from the
`I<X>Api` interface and its `responseTypeOverride` entry in all THREE PublicApi
mirrors (done for the registration flows), and consume via the SSE client
instead.

## Step 4 — Frontend consumption

Plumbing: `Frontend.Infrastructure/Protocol/Sse/` —

```csharp
public static async IAsyncEnumerable<SseFrame> PostServerSentEventsAsync(
    this HttpClient httpClient, string? requestUri, object? requestBody,
    Action<HttpRequestMessage>? configureRequest = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default);

public static async Task<StepProgressFinalEvent?> ReadAsync(
    IAsyncEnumerable<SseFrame> frames, Action<string> onStepProgress,
    CancellationToken cancellationToken = default);
```

Named clients (e.g. `"ReadersManagementSseClient"`, 3-minute timeout) are
registered on BOTH sides — Host (`ApizrConfigurationExtensions` +
`HttpContextCookiesPropagationHandler`, `UseCookies = false`) and Client
(`IncludeCookiesHandler`). **Base address MUST end with a trailing slash**
(`"https://localhost/api/readers/"`): `HttpClient` otherwise replaces the last
path segment and the POST 404s. (Apizr managers join paths themselves — the
slash rule is SSE-client-specific.)

Page pattern (registration pages): content swap while processing —

```razor
@if (_isProcessing) { <FluentProgressRing /> + _statusMessage } else { original form }
```

Drive it with `PostServerSentEventsAsync` + `ReadAsync(frames, stepCode => {
    _statusMessage = RegistrationProgressMessages.ForStep(stepCode);
    StateHasChanged();
}, ct)` inside try/catch/finally — an exception or a missing final event
restores the form with a generic error. `RegistrationProgressMessages`
(backed by `TranslationResources`) is the step-code → localized-text mapper.

## Red Flags

- Reading the antiforgery state with a `using` (TimeWarp state dispose cancels
  its token forever — capture the token into a local first).
- Forgetting the trailing slash on the named SSE client base address.
- Keeping the endpoint in the Refit interface.
- Reporting step progress from a step that runs on OTHER flows sharing the
  context type without attaching a reporter (safe — no-op — but don't assume
  the reporter exists).
- Manually flushing the response stream.

## Reference implementations

- Backend helper: `snowcoreBlog.Backend/Shared/Infrastructure/StepProgress/StepProgressSseExtensions.cs`, `StepProgressContextExtensions.cs`
- Endpoints: `.../ReadersManagement/Endpoints/ReaderAccounts/RequestCreateByEmailEndpoint.cs`, `ConfirmCreateByEmailEndpoint.cs`, `ConfirmCreateByAuthenticatorEndpoint.cs`
- Frontend: `snowcoreBlog.Frontend/snowcoreBlog.Frontend.Infrastructure/Protocol/Sse/`, `snowcoreBlog.Frontend/Modules/ReadersManagement/Pages/` registration pages
