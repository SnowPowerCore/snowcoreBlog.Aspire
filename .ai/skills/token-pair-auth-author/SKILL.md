---
name: token-pair-auth-author
description: >-
  How-to for adding or changing a reader login method end-to-end: verify the
  credential against IAM via a request contract, issue the token pair through
  ReaderAccountTokenPairIssuer (gateway-minted JWT + cookies + Redis record),
  expose the endpoint with its anonymous-surface gates, wire the frontend
  BitzArt sign-in payload. Use when adding a login surface or touching
  refresh/revoke/claims.
scope: project
---

# Token-Pair Auth Author

Use this skill to add a new way for a reader to sign in, or to change how
tokens are issued/refreshed/revoked in ReadersManagement.

## When to Invoke

- "add passkey login", "add a new login method", "issue tokens after X"
- Changes to `tokens/refresh` / `tokens/revoke` / claims / cookies
- "the user should get authorAccount after becoming an author" (claim refresh)

## When NOT to Invoke

- Admin login/invites/bootstrap — see the `admin-auth-author` skill
  (different issuer, different service)
- IAM-internal credential stores (key material, Fido2/TOTP wiring) — only the
  request contracts concern this skill

## Background

ReadersManagement owns sessions; IAM owns credentials. The JWT itself is
minted by the gateway: `ReaderAccountTokenPairIssuer.TryIssueAsync` sends
`GetUserTokenPairWithPayload` over MassTransit (1-min timeout), merges base
config claims (`Security:ReaderAccountTokenRequirements:Claims`, e.g.
`readerAccount: "true"`) with fresh claim-provider claims, sets BOTH cookies
(`.DotNet.Application.User.SystemKey` / `.SystemUpdateKey`) and persists the
rotating Redis record (`reader-refresh:{token}` + `reader-user-refresh:{userId:N}`).

## Step 1 — Verify the credential in IAM (new contract + consumer)

Add the contract to `Shared/IAM.Core/Contracts/`, a consumer under
`IAM/Features/` responding `DataResult<T>` (success = `Errors == null`), then a
step in ReadersManagement calling it. Existing shape to copy — passkey
(`Steps/Assertion/AttemptLoginByAssertionStep.cs`):

```csharp
var redisValue = await db.StringGetAsync($"{context.LoginByAssertion.Email}{Fido2AssertionOptions}");
var assertionOptionsForUser = Encoding.UTF8.GetString(redisValue != RedisValue.Null ? redisValue! : []);
if (string.IsNullOrWhiteSpace(assertionOptionsForUser))
    return AssertionError<LoginByAssertionResultDto>.Create(AssertionConstants.NoAssertionOptionsInSession);

var result = await requestClient.GetResponse<DataResult<UserLoginResult>>(
    context.LoginByAssertion.ToLoginUser(assertionOptionsForUser), token);

if (result.Message.IsSuccess) {
    await publishEndpoint.Publish<ReaderAccountUserLoggedIn>(new(result.Message.Value!.Id, ...), token);
    context.SetDataWith(ReaderAccountUserConstants.CurrentUserId, result.Message.Value.Id);
    return await next(context, token);
}
```

Transient challenge state (assertion options) goes to Redis with a 5-min TTL
key `"{email}Fido2AssertionOptions"`. Failure path: return a typed `[None]`
error (`XError<TDto>.Create(...)`) — never throw.

## Step 2 — Issue the token pair (terminal step)

Follow `Steps/Assertion/GetTokenForReaderAccountStep.cs` (or
`Steps/OneTimeCode/IssueTokenForOneTimeCodeLoginStep.cs`): pull
`CurrentUserId` + email from the context, then

```csharp
var (success, errors) = await tokenPairIssuer.TryIssueAsync(userId, email, token);
```

The issuer does everything else: gateway mint request, claim merge, cookie
append, Redis record + pointer. Do NOT build `SecurityTokenDescriptor`s for
readers, and do not touch the refresh Redis keys here — rotation happens only
in the refresh pipeline.

## Step 3 — Endpoint + gates

Stepified endpoint, anonymous-surface gates, envelope response
(`RequestCreateByEmailEndpoint` is the reference):

```csharp
Post("login/<method>");
Version(1);
SerializerContext(CoreSerializationContext.Default);
Validator<LoginByXDtoValidator>();
AllowAnonymous();
EnableAntiforgery();
Tags(EndpointTagConstants.RequireCaptchaVerification);
Description(b => b.WithTags(ApiTagConstants.ReaderAccountManagement, ApiTagConstants.Captcha)
    .Accepts<LoginByXDto>(MediaTypeNames.Application.Json)
    .ProducesProblemFE((int)HttpStatusCode.BadRequest));
```

Also add the anonymous YARP route for the new prefix in the AppHost
`appsettings.Development.json` (`login/external`, `login/totp` exist today) —
otherwise the gateway 401s the call before it reaches the service.

## Step 4 — String-body endpoints need the binder

If the endpoint accepts a raw string (token refresh), wire
`RequestBinder(new RefreshTokenRequestBinder())` — FastEndpoints' default
string deserializer 400s empty bodies ("does not contain any JSON tokens")
before the cookie fallback can run. See
`Endpoints/Tokens/RefreshTokenRequestBinder.cs`.

## Step 5 — Frontend sign-in

`LoginByAssertionDto` is a UNION payload (nullable `AuthenticatorAssertion` +
optional `OneTimeCode`): BitzArt's `SignInAsync` is single-payload-typed, so new
login factors ride the same DTO — add a nullable member, branch in the Host
`GlobalReaderAccountAuthenticationService.SignInAsync`, the WASM `UserService`
and the Client-side `BlazorHostBackedClientUserService<TSignInPayload>`, and
relax `LoginByAssertionValidator` with `.When(x => x.OneTimeCode is null)`-style
conditions. Navigation note: emails go in the QUERY string, not the route —
dots in emails break Blazor last-segment routing (existing pages read it via
`[SupplyParameterFromQuery]` / `QueryValue`, e.g. `CompleteRegistrationPage`).

Refresh/revoke need no frontend work beyond `IReaderAccountTokensApi`
(`RefreshReaderJwtPair`, `RevokeReaderTokens` — mirrored ×3 in PublicApi);
rotation is self-healing for claims (fresh provider merge on every refresh).

## Red Flags

- Minting reader JWTs in-module (gateway owns it) or writing refresh Redis
  keys outside `RotateReaderTokenPairStep`.
- Missing antiforgery/captcha tag or anonymous YARP route on a new login route.
- Using another service's `AntiforgeryState` for this endpoint's call.
- Email in a Blazor route segment.

## Reference implementations

- Full flow (passkey): `snowcoreBlog.Backend/BusinessServices/ReadersManagement/Steps/Assertion/` + `Endpoints/ReaderAccounts/LoginByAssertionEndpoint.cs`
- Issuer: `.../ReadersManagement/Services/ReaderAccountTokenPairIssuer.cs`
- Refresh pipeline: `.../ReadersManagement/Endpoints/Tokens/RefreshReaderAccountTokenEndpoint.cs` + `Steps/Tokens/`
- IAM side: `.../Shared/Services/IAM/Features/` (assertion/TOTP consumers), contracts `.../Shared/IAM.Core/Contracts/`
