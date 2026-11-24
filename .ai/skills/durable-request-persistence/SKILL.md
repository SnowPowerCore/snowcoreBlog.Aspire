---
name: durable-request-persistence
description: >-
  How-to for the durable request persistence system — Tags(ApiTagConstants.Persist)
  opt-in with idempotency keys, step checkpoints ([CheckpointedStep] +
  IStepCheckpointState), lease-based takeover, outcome classification, and the
  TickerQ recovery loop. Use when a flow must survive crashes/replays or must
  be idempotent under client retries.
scope: project
---

# Durable Request Persistence

Use this skill when an endpoint must be idempotent under retries and resumable
after crashes (registration-style long flows).

## When to Invoke

- "this request must not run twice", "resume interrupted flows", "idempotency key"
- Adding steps to an existing Persist-tagged flow

## When NOT to Invoke

- Ordinary endpoints without durability needs (the processors are opt-in)
- MassTransit message dedup (no outbox exists — see Limits)

## Background

Opt-in is tag-driven: an endpoint declares `Tags(ApiTagConstants.Persist)` and
the shared `c.Endpoints.Configurator` (in each service's
`FastEndpointsConfigurationExtensions`) attaches
`PersistRequestPreProcessor` / `PersistRequestPostProcessor`
(`ep.AttachPersistRequestProcessors()` in
`Shared/Infrastructure/Processors/PersistRequestFastEndpointsExtensions.cs` —
same mechanism as the Altcha captcha processor). Storage documents register via
`options.AddPersistRequestDocuments()` (`PersistedRequestEntity` +
`RequestCheckpointEntity`, unique idempotency-key index).

## Flow (what each stage guarantees)

1. **Claim** (pre-processor): requires the `Idempotency-Key` header (400
   without it). Body is read, SHA-256 hashed, and the `PersistedRequestEntity`
   claim is committed in an INDEPENDENT Marten session before any business
   logic — a claim storage failure rethrows before the business pipeline runs,
   so unpersisted requests never execute (the XML doc mentions 503; the code
   actually rethrows — treat claim failure as a 5xx-class error). Response
   capture happens via stream swap (SSE endpoints skip capture).
2. **Duplicate short-circuit**: same key with a different body hash ⇒ 202
   Conflict; already Succeeded/Failed ⇒ 200 informative
   (`IdempotencyRequestStatusDto` — the stored business response is never
   returned); fresh Processing lease ⇒ 202 InProgress; stale lease / Persisted /
   due RetryNeeded ⇒ atomic takeover (`TryTakeOverAsync`: patch + increment in
   one `SaveChanges`, verified by re-read) and the request resumes from the
   highest contiguous step checkpoint.
3. **Step checkpoints**: steps marked `[CheckpointedStep]` (in-repo
   AspectInjector aspect) report entry/completion to `StepCheckpointCoordinator`.
   Contexts implement `IStepCheckpointState` (typed snapshots) and the service
   registers its `*CheckpointJsonContext` as `IJsonTypeInfoResolver` (AOT-safe).
   On resume the context hydrates in place and completed steps are skipped
   (the aspect calls `next` directly).
4. **Completion** (post-processor): outcome classified — 2xx Succeeded, 4xx
   Failed, 5xx/429 RetryNeeded with backoff — and the marker is durable BEFORE
   response bytes flush. First-attempt finishes purge to tombstone stubs;
   recovered requests keep full records for audit.
5. **Recovery**: `PersistRequestRecoveryJob` (TickerQ, cron seeded from
   `PersistRequestOptions.RecoveryPollInterval`) polls aged Persisted / due
   RetryNeeded / expired leases, takes over, and re-dispatches via HTTP
   loopback (`ReplayRequestDispatcher`) with a fresh antiforgery token and
   `IReplayAuthenticationProvider` re-auth (ReadersManagement: refresh-token
   rotation; rejection ⇒ Failed + manual).

## Wiring a new service (checklist)

1. `AddMarten(options => { ...; options.AddPersistRequestDocuments(); })`.
2. `builder.Services.AddPersistRequestProcessing(options => configuration.GetSection("PersistRequest").Bind(options))`
   plus the per-service `IJsonTypeInfoResolver` checkpoint context and
   `IReplayAuthenticationProvider` singletons (default passthrough provider is
   registered by the extension; the service-level one overrides it).
3. Merge `ep.AttachPersistRequestProcessors()` into the Configurator.
4. Add the `PersistHeaderProcessor` to the Swagger `OperationProcessors` (documents the header).
5. Tag endpoints `Tags(ApiTagConstants.Persist)`; mark checkpointable steps `[CheckpointedStep]`.

## Limits (documented, accepted)

- The single in-flight (crashed) step may still duplicate its external side
  effects on resume — checkpoints are BETWEEN-step, not mid-step.
- MassTransit publishes are not covered by an outbox; a replay may re-publish.
- Requires Postgres `synchronous_commit=on` (default) for the durability contract.

## Red Flags

- Marking a step `[CheckpointedStep]` whose context doesn't implement
  `IStepCheckpointState` or whose snapshot types aren't in the checkpoint JSON
  context (resume silently loses state).
- External side effects placed before the first checkpoint of the step that
  performs them (re-executes on resume).
- Forgetting the per-service replay-auth provider on authenticated flows
  (replays re-auth with the passthrough provider and fail).

## Reference implementations

- Processors + storage: `snowcoreBlog.Backend/Shared/Infrastructure/Processors/PersistRequestFastEndpointsExtensions.cs`, `Shared/Infrastructure/RequestPersistence/`
- Tagged flow: `.../ReadersManagement/Endpoints/ReaderAccounts/ConfirmCreateByEmailEndpoint.cs`; checkpoint state `.../Context/ConfirmCreateReaderAccountContext.cs`; `[CheckpointedStep]` in `Steps/ReaderAccount/Confirm/CreateReaderAccountUserStep.cs`
- Recovery: `Shared/Infrastructure/RequestPersistence/PersistRequestRecoveryJobs.cs`, `PersistRequestTickerQ.cs`
- Service wiring: `.../ReadersManagement/Program.cs` (`AddPersistRequestProcessing` + `ReadersCheckpointJsonContext` + `ReadersReplayAuthenticationProvider`)
