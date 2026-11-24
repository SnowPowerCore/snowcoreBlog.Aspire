---
name: stripe-payments-author
description: >-
  How-to for the Stripe payments service — Checkout session pipeline
  (session-created-before-entity, client_reference_id linkage), the raw-body
  webhook with HMAC verification and exactly-once event ledger, status
  transition guards, namespace shadowing, and the no-trim constraint. Use when
  adding payment flows, webhook event types, or debugging Stripe status.
scope: project
---

# Stripe Payments Author

Use this skill when extending `snowcoreBlog.Backend.Stripe` (donations/goods
via Stripe Checkout) or consuming its events.

## When to Invoke

- "add a payment kind", "handle another Stripe event", "webhook misses updates"
- Debugging payment status stuck in Pending/Processing

## When NOT to Invoke

- Generic stepified-endpoint mechanics (see `stepified-process-handlers`)
- Refund flows as new endpoints — refunds are currently a STATUS FLIP via the
  webhook (`charge.refunded`), not a separate API surface

## Background

Shared service at `Shared/Services/Stripe`, own DB `db-stripe-entities`, one-time
payments only (no subscriptions). Two write paths exist: Checkout creation
(creates the session + record) and the webhook (ONLY writer of status
transitions — guards Pending/Processing → terminal).

## Rule 1 — Namespace shadowing

The service namespace `snowcoreBlog.Backend.Stripe` SHADOWS the `Stripe` SDK
namespace: qualified refs like `Stripe.Checkout.Session` bind to your own
types (CS0234). Use `global::Stripe.Checkout.Session` for qualified references
and keep the SDK usings (`using Stripe;` / `using Stripe.Checkout;`) for
unqualified ones (`SessionCreateOptions`, `SessionService`, `EventTypes`).
Stripe.net is 43.13.0 with the renamed `Stripe.Checkout.*` classes.

## Rule 2 — Checkout creation: session first, entity second, ids cross-linked

The stepified pipeline is Validate → CreateSession → Persist → BuildResult:
the Stripe session is created BEFORE the local record exists; the local record
id rides in `client_reference_id` + metadata (`payment_record_id`,
`payment_record_kind`), so the webhook can find the row without a session-id
lookup race. Persist happens BEFORE the URL is returned to the client.

## Rule 3 — Webhook: raw body, HMAC, exactly-once ledger, STJ parsing

```csharp
// NO model binding — the HMAC is computed over the EXACT request bytes.
var evt = EventUtility.ConstructEvent(rawBody, stripeSignatureHeader, webhookSecret);
```

- EventId ledger (`StripeWebhookEventEntity`, unique index) → duplicates return
  200 without reprocessing (exactly-once).
- Parse `data.object` via System.Text.Json into local DTOs — NOT Stripe.net
  typed event models (decouples from SDK churn).
- Entity update + ledger row in ONE scoped `IDocumentSession.SaveChanges`.
- Domain events (`DonationPaid`/`PaymentPaid`/... in
  `Shared/Stripe.Core/Contracts/Stripe/`) publish AFTER commit.
- Response contract: unknown event types / entity-not-found ⇒ 200 (Stripe
  disables endpoints on non-2xx); invalid signature ⇒ 400; missing
  `WebhookSecret` ⇒ 500.

## Rule 4 — Status transitions are guarded per event family

Paid/intent events (`payment_intent.*`, `charge.*`) require the record to be
Pending or Processing; session expired/failed events require Pending (→
Canceled); refunds require Succeeded → Refunded. Nothing can regress a terminal
status. Handled events: `checkout.session.completed`,
`checkout.session.async_payment_succeeded`, `checkout.session.expired`,
`checkout.session.async_payment_failed`, `payment_intent.succeeded`,
`payment_intent.payment_failed`, `charge.succeeded` (captures `receipt_url`),
`charge.refunded`.

## Rule 5 — Client + config shape

`IStripeClient` is a singleton that lazily throws on a missing `Stripe:SecretKey`
— the host starts fine without keys (ping/openapi work; checkout endpoints
demand `Stripe:SecretKey`, the webhook demands `Stripe:WebhookSecret` —
blank ⇒ 500). Options (`StripeOptions`): currency allowlist,
donation/goods min/max minor units, session expiry, success URL template with
`{CHECKOUT_SESSION_ID}` (cancel URL is a plain redirect). Events/entities in
`Shared/Stripe.Core/Contracts/Stripe/` + `Shared/Stripe.Core/Entities/Stripe/`. DTO mirroring:
every Stripe DTO (donation AND goods) is mirrored to ALL THREE PublicApi copies
(Backend, Frontend, Console) alongside their `Validation/Dto` validators — keep
any Stripe DTO change in all three.

## Red Flags

- Model-binding the webhook body (breaks HMAC verification).
- Writing status outside the webhook pipeline.
- New Stripe SDK reference without `global::` / correct usings.
- Enabling `PublishTrimmed` (Stripe.net is reflection-heavy).
- An event handler that saves the entity without the ledger row (replay risk)
  or publishes before `SaveChanges`.

## Reference implementations

- Webhook endpoint + ledger: `snowcoreBlog.Backend/Shared/Services/Stripe/Endpoints/**` + `Features/`
- Checkout pipeline: `.../Stripe/Steps/` (Validate → CreateSession → Persist → BuildResult)
- Contracts/events/entities: `snowcoreBlog.Backend/Shared/Stripe.Core/Contracts/Stripe/`, `Shared/Stripe.Core/Entities/Stripe/`
- Config: `.../Stripe/Options/`, `appsettings*.json` (`Stripe` section)
