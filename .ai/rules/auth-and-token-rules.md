---
description: Auth and token domain — reader/admin identity split, gateway-vs-module JWT minting, Redis refresh-token rotation with self-healing claims, per-service antiforgery, anonymous-surface gates, no-password-login rule
globs: ["snowcoreBlog.Backend/**/*.cs", "snowcoreBlog.Frontend/**/*.cs", "snowcoreBlog.AdminFrontend/**/*.cs"]
alwaysApply: false
---

# Auth and Token Rules

Use this rule when adding or changing any login flow, token issuance/refresh/
revoke, claims handling, or an anonymous surface that must be gated.

## Service Context

- **Three identities, two authorities**: readers/authors authenticate via
  `ReadersManagement` (reader JWTs); admins via `AdminsManagement` (admin JWTs).
  `IAM` (Shared/Services/IAM) is a **MassTransit-only identity store** over
  Marten (`ApplicationUserEntity`/`ApplicationAdminEntity` : `IdentityUser`,
  `MartenUserStore`) — it has **no business HTTP endpoints and no password
  login anywhere in the system**.
- **Cookies** (`Shared/Shared.Core/Constants/AuthCookieConstants.cs`):
  `.DotNet.Application.User.SystemKey` / `.SystemUpdateKey` (access/refresh),
  `.DotNet.Application.Admin.SystemKey` / `.Admin.SystemUpdateKey`, plus BitzArt
  `AccessToken`/`RefreshToken` aliases the frontend host also clears.
- Gateway (`AspireYarpGateway`) validates both JWT flavors: `AdminScheme`
  (issuer `"admin"`, admin signing key, token read from the admin cookie) and
  the user scheme; root `Program.cs` `.WithAuthPolicies` enforces literals
  (`"adminAccount": "true"`, `readerAccount == "true"`).

## Domain Rules

1. **No password login — the login methods are fixed**: passkey
   (Fido2NetLib, IAM `LoginUser` contract + `ValidateAndCreateAssertionConsumer`),
   authenticator TOTP (Otp.NET, IAM `TotpService.Verify` via
   `ValidateUserOneTimeCodeConsumer`; route `login/totp/verify`), admin
   magic-link (SHA-256 token, AdminsManagement), external OAuth
   (Google/Microsoft/Apple — registered ONLY when
   `Integrations:ExternalAuth:{Provider}` config is non-empty; **external login
   never auto-creates accounts**; Apple requires a public HTTPS domain).
   Why: adding a password path would bypass the whole delegated-identity design
   and every existing credential flow.

2. **JWT minting is split — do not cross the streams**: reader tokens are
   minted BY THE GATEWAY via MassTransit —
   `ReaderAccountTokenPairIssuer.TryIssueAsync(Guid userId, string email, ...)`
   sends `GetUserTokenPairWithPayload` (1-min timeout) to
   `AspireYarpGateway/Features/GetUserTokenPairWithPayloadConsumer`. Admin
   tokens are minted IN-MODULE by `AdminTokenPairIssuer` (own
   `SecurityTokenDescriptor`: `Issuer = "admin"`, admin signing key, claims
   `AdminUserId`/`AdminEmail`/`adminAccount=true`, cookie HttpOnly/Secure/Lax).
   Why: the gateway owns the reader signing keys/policy; AdminsManagement owns
   the admin ones — mixing them breaks scheme validation silently.

3. **Dynamic claims come only from claim providers, and refresh self-heals**:
   `readerAccount=true` is a static base claim (`Security:ReaderAccountTokenRequirements:Claims`
   in appsettings); `authorAccount` is added ONLY by the claim-provider flow —
   Redis key `ClaimProviderServices` lists provider queue names
   (populated by `AuthorsManagementApplicationLaunchService`), the issuer sends
   `RequestReaderClaims` to each (`queue:{provider}`; live consumer:
   `AuthorsManagement/Features/ReturnClaimsIfUserAuthorConsumer`) and merges
   results OVER base claims. `RotateReaderTokenPairStep` re-merges fresh
   provider claims on EVERY refresh before minting and persists the merged set
   in the new record — never replay only the stale `record.Claims`.
   `/check/me` returns claims parsed from the JWT only (no live lookup) — the
   frontend must call refresh (e.g. `RefreshJwtPairAsync(string.Empty)`, cookie
   fallback) after a claim-granting action like become-author.
   Why: this exact staleness bug shipped once; the re-merge is the fix.

4. **Refresh tokens live ONLY in Redis, two keys per user**:
   `reader-refresh:{token}` → `ReaderRefreshTokenRecord { UserId, Claims,
   ExpiresAt }` (source-gen serialized) and `reader-user-refresh:{userId:N}` →
   current-token pointer. Rotation deletes the old record + rewrites both keys.
   Failure semantics are fixed and typed: `RefreshTokenMissingError` (no
   cookie/body), `RefreshTokenInvalidError` (no record), `RefreshTokenExpiredError`
   (`ExpiresAt` past), `RefreshTokenRevokedError` (pointer mismatch = older of
   two rotations) — all surfaced as `INone` → HTTP 400 through `ToStatusCode()`
   (distinct error types/messages are the disambiguator; a literal 401 appears
   only in OpenAPI `Produces` metadata). Concurrency is guarded by
   `UseRefreshTokenLockStep` (`SET NX`, 5 s expiry, deleted in `finally`).
   Revoke = atomic `StringGetDeleteAsync` + pointer cleanup + delete all four
   cookies.
   Why: the Redis `cache` resource must keep `.WithDataVolume()` (host restarts
   otherwise wipe sessions → mass 401 on next rotation) and every step's error
   mapping depends on this exact key model.

5. **A `string`-body token endpoint needs a custom binder**: FastEndpoints'
   default string deserializer throws on an empty JSON body before the pipeline
   runs. `RefreshReaderAccountTokenEndpoint` wires
   `RequestBinder(new RefreshTokenRequestBinder())` (empty → `string.Empty`
   for cookie-only rotation; JSON-string → unwrapped; else passthrough).
   Why: proactive refresh sends an empty body — without the binder every such
   call 400s with "does not contain any JSON tokens".

6. **Anonymous surfaces are gated twice, per service**: every login/registration
   endpoint carries `AllowAnonymous()` + `EnableAntiforgery()` +
   `Tags(EndpointTagConstants.RequireCaptchaVerification)` (Altcha pre-processor)
   and needs an anonymous YARP route. Antiforgery is **per-service** — each
   service has its own `GetAntiforgeryTokenEndpoint` + cookie; tokens are NOT
   interchangeable, and the frontend must use the matching module's
   `AntiforgeryState` (using ReadersManagement's state for an Authors call
   fails with "Anti-forgery token is invalid!").
   Why: the gates key off those exact tags/config; cross-service token reuse
   is a real shipped bug.

7. **New credential-verification logic belongs in IAM, exposed via
   request/response contracts** (`Shared/IAM.Core/Contracts/`, e.g.
   `LoginUser { Email, AssertionOptionsJson, AuthenticatorAssertionRawResponse }`):
   add a `DataResult<T>`-responding consumer under `IAM/Features/`, then a
   ReadersManagement step calling `IRequestClient<T>.GetResponse<DataResult<T>>`.
   Transient session state (e.g. assertion options) goes to Redis with a 5-min
   TTL (`"{email}Fido2AssertionOptions"` pattern).
   Why: IAM owns credential material (public keys, TOTP secrets); Readers
   owns sessions/tokens — the contract boundary keeps key material out of the
   token service.

## Red Flags

- `SecurityTokenDescriptor` for a READER token outside the gateway consumer.
- A refresh-path change that skips `MergeExternalClaimsAsync` or persists only
  record claims.
- A new anonymous login endpoint missing antiforgery/captcha tags or its
  anonymous YARP route.
- Password fields/checks introduced anywhere (IAM included).
- Antiforgery state from one service's module used for another service's call.
- Redis record reads without the pointer check (revoked-but-unexpired tokens
  would resurrect).

## Evidence

- Issuer: `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Services/ReaderAccountTokenPairIssuer.cs`
- Refresh endpoint + binder: `.../ReadersManagement/Endpoints/Tokens/RefreshReaderAccountTokenEndpoint.cs`, `RefreshTokenRequestBinder.cs`
- Refresh steps: `.../ReadersManagement/Steps/Tokens/` (`ResolveRefreshTokenStep`, `UseRefreshTokenLockStep`, `ValidateRefreshTokenRecordStep`, `RotateReaderTokenPairStep`)
- Redis keys/record: `.../ReadersManagement/Constants/ReaderRefreshTokenConstants.cs`, `Shared/Infrastructure/ReaderRefreshTokenRecord.cs`
- Admin issuer: `.../AdminsManagement/Services/AdminTokenPairIssuer.cs`; claim constants `.../AdminsManagement/Constants/AdminClaimConstants.cs`, `AdminSecurityConstants.cs`
- Claim provider consumer: `.../AuthorsManagement/Features/ReturnClaimsIfUserAuthorConsumer.cs`
- Gateway admin scheme: `.../Shared/Services/AspireYarpGateway/Extensions/WebApplicationBuilderExtensions.cs`
- Cookie constants: `.../Shared/Shared.Core/Constants/AuthCookieConstants.cs`
