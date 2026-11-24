---
name: masstransit-features-consumers
description: >-
  How-to for MassTransit messaging in snowcoreBlog backend — IConsumer
  implementations under Features/, per-service AddMassTransitConfiguration
  registration with ConfigureEndpoints and JSON context, event vs request
  contracts and where they live, IRequestClient request/response steps,
  publishing from steps and consumers, idempotency expectations. Use when
  adding or changing message-driven behavior between services.
scope: project
---

# MassTransit Consumers (Features/)

Use this skill to implement or register message consumers and cross-service
request/response flows over RabbitMQ.

## When to Invoke

- "react to X happening in another service", "publish an event", "send a command to IAM"
- Adding a consumer to a service (including worker services like Email)
- Debugging queue naming or serialization of messages

## When NOT to Invoke

- Scheduled background work — that is TickerQ (`ITickerFunction` + `MapTicker<T>`), not messaging
- In-process eventing inside a single service — use steps/direct calls

## Background

Transport is RabbitMQ (the Aspire `rabbitmq` resource, `masstransit/rabbitmq`
image). Consumers live under the owning service's `Features/` — flat where the
service is small (ReadersManagement), area-subfoldered where it grows
(IAM uses `Features/Admin/`); queues are auto-named by
`ConfigureEndpoints(context)`. Message JSON must flow through the same static
serialization context as everything else.

## Rule 1 — Consumers: `IConsumer<T>` under `Features/<Area>/`, thin and idempotent

```csharp
public class ReaderAccountTempUserCreatedConsumer(
    IOptions<SendGridSenderAccountOptions> options,
    IConfiguration configuration) : IConsumer<ReaderAccountTempUserCreated>
{
    public Task Consume(ConsumeContext<ReaderAccountTempUserCreated> context) =>
        context.Publish(
            GenericEmailExtensions.ToGeneric(
                options.Value,
                context.Message.UserEmail,
                EmailConstants.ReaderAccountTempUserCreatedSubject,
                $"Welcome {context.Message.UserFirstName}, your activation link is: {context.Message.VerificationUrl}"),
            context.CancellationToken);
}
```

Consumers may be redelivered — make consumption idempotent (upsert semantics,
natural keys) and delegate business work to repositories/services rather than
embedding it in `Consume`.

## Rule 2 — Register in `AddMassTransitConfiguration` with the JSON context

`Extensions/Startup/MassTransitConfigurationExtensions.cs` (per service):

```csharp
services.AddMassTransit(busConfigurator =>
{
    busConfigurator.AddConsumer<ReaderAccountTempUserCreatedConsumer>();
    busConfigurator.ConfigureHttpJsonOptions(static o => { /* AllowReadingFromString + SetJsonSerializationContext */ });
    busConfigurator.UsingRabbitMq((context, config) =>
    {
        config.ConfigureJsonSerializerOptions(static options => options.SetJsonSerializationContext());
        config.Host(rabbitMqConnectionString);
        config.ConfigureEndpoints(context);           // auto-named queues per consumer
    });
    configurator?.Invoke(busConfigurator);
});
```

Program.cs: `builder.Services.AddMassTransitConfiguration(builder.Configuration.GetConnectionString("rabbitmq"));`

## Rule 3 — Contracts have fixed homes

- **Events** (publish/subscribe): the owning service's Core project — `snowcoreBlog.Backend/Shared/<Service>.Core/Contracts/` (e.g. `ReadersManagement.Core/Contracts/ReaderAccountTempUserCreated.cs`: `public sealed record ReaderAccountTempUserCreated(string UserFirstName, string UserEmail, string VerificationUrl, string VerificationTokenUntilThatDate);`)
- **Request/response** (point-to-point): the owning integration's Core project — IAM contracts live in `snowcoreBlog.Backend/Shared/IAM.Core/Contracts/` (`CreateAdmin`, `ValidateAdminExists`, `CreateTempUser`, `LoginUser`, ...).

Responses wrap in `DataResult<T>` (`Value` + `Errors?`; `IsSuccess == Errors == null`).

## Rule 4 — Request/response via `IRequestClient<T>` (steps) and responder consumers

Client side (a step awaiting IAM):

```csharp
public sealed class ValidateAdminExistsInIamStep(IRequestClient<ValidateAdminExists> validateAdminClient)
    : IStep<ConfirmAdminLoginDelegate, ConfirmAdminLoginContext, IMaybe<AdminDto>>
{
    public async Task<IMaybe<AdminDto>> InvokeAsync(
        ConfirmAdminLoginContext context, ConfirmAdminLoginDelegate next, CancellationToken token = default)
    {
        var iamCheck = await validateAdminClient.GetResponse<DataResult<AdminExistsValidationResult>>(
            new ValidateAdminExists { Email = context.LoginToken!.Email }, token);

        return !iamCheck.Message.IsSuccess || iamCheck.Message.Value?.Exists is false
            ? AdminApplicationAdminNotExistsError<AdminDto>.Create("This admin identity no longer exists in IAM.")
            : await next(context, token);
    }
}
```

Responder side (IAM): `Features/Admin/ValidateAdminExistsConsumer : IConsumer<ValidateAdminExists>` — registered in IAM's own `AddMassTransitConfiguration`.

## Rule 5 — Publishing from steps uses `IPublishEndpoint`

Inject `IPublishEndpoint` into the step and `await publishEndpoint.Publish(new ReaderAccountTempUserCreated(...), token)`. Inside consumers, use `context.Publish(...)` so the message flows with the consume pipeline. There is **no outbox** — publishes are fire-and-forget; document replay consequences for durable flows.

## Reference implementations

- Thin consumer: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Features/ReaderAccountTempUserCreatedConsumer.cs`
- Responder consumer: `snowcoreBlog.Backend/Shared/Services/IAM/Features/Admin/ValidateAdminExistsConsumer.cs`
- Registration: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Extensions/Startup/MassTransitConfigurationExtensions.cs`
- Step as request client: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Steps/Auth/ConfirmLogin/ValidateAdminExistsInIamStep.cs`
- Event contract: `snowcoreBlog.Backend/Shared/ReadersManagement.Core/Contracts/ReaderAccountTempUserCreated.cs`
