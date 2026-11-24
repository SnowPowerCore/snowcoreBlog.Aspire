---
name: admin-auth-author
description: >-
  How-to for the admin authentication domain — passwordless magic-link login,
  single-use SHA-256 tokens, invite lifecycle with bootstrap admin, in-module
  admin JWT minting (issuer "admin", AdminScheme), and the portal-side
  Login/InviteAccept pages. Use when touching AdminsManagement auth flows or
  the admin portal sign-in.
scope: project
---

# Admin Auth Author

Use this skill when changing admin login, invites, bootstrap seeding, admin
JWT issuance, or the admin portal sign-in pages.

## When to Invoke

- "admin can't log in", "invite flow broken", "add admin auth feature"
- Changes to `AdminsManagement/Services/AdminTokenPairIssuer.cs`, token
  entities, `Security:Signing:Admin` config, gateway `AdminScheme`

## When NOT to Invoke

- Reader token flows (gateway-minted — see the `token-pair-auth-author` skill)
- Portal page CRUD patterns beyond Login/InviteAccept (see the
  `admin-portal-page-author` skill)

## Background

Admin auth is passwordless: an active admin requests a single-use magic link
(32 random bytes hex; only the SHA-256 hash is stored, unique-indexed), clicks
`{AdminFrontendDomain}/login?token=...`, the portal confirms and receives an
admin JWT cookie. Unlike reader JWTs (gateway-minted), ADMIN tokens are minted
IN-MODULE by `AdminTokenPairIssuer` and validated by the gateway's `AdminScheme`
(issuer `"admin"`, admin signing key from `Security:Signing:Admin:SigningKey`).
The gateway policy literal enforced by the root AppHost:
`("regularAdmin", policy => policy.RequireAuthenticatedUser()
 .AddAuthenticationSchemes("AdminScheme").RequireClaim("adminAccount", allowedValues: "true"))`.

## Step 1 — Magic-link request (login/link)

`RequestAdminLoginLinkEndpoint` — `Post("login/link")`, `AllowAnonymous()`,
3-step pipeline: `ResolveActiveAdminByEmailStep` (only sends when an ACTIVE
admin exists — no user enumeration) → `CreateAdminLoginTokenStep`
(`AdminTokenUtilities.NewToken()` = 32 random bytes hex, stored SHA-256 via
`Sha256Hex`, as `AdminLoginTokenEntity { Email, TokenHash, ExpiresAtUtc }`,
`IsUsable` computed) → `SendAdminLoginLinkEmailStep` builds
`{AdminFrontendDomain}/login?token={RawToken}` and publishes the generic email.

## Step 2 — Confirm (login/confirm)

`ConfirmAdminLoginEndpoint` — `Post("login/confirm")`, `AllowAnonymous()`,
5-step pipeline: `ResolveAdminLoginTokenStep` (hash lookup) →
`ResolveAdminForLoginStep` → `ConsumeAdminLoginTokenStep` (single-use via
`UsedAtUtc`) → `ValidateAdminExistsInIamStep` (MassTransit
`ValidateAdminExists` — the identity must still exist in IAM) →
`IssueAdminSessionStep`.

## Step 3 — JWT minting (in-module)

`AdminsManagement/Services/AdminTokenPairIssuer.cs`:

```csharp
var descriptor = new SecurityTokenDescriptor
{
    Issuer = AdminSecurityConstants.AdminIssuer,   // "admin"
    Expires = expiresAt,                           // AccessTokenValidityDurationInMinutes (dev: 480)
    SigningCredentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.ASCII.GetBytes(signingKey.Value.Value)),
        SecurityAlgorithms.HmacSha256Signature),
    Subject = new ClaimsIdentity(
    [
        new Claim(AdminClaimConstants.AdminUserIdClaimKey, admin.Id.ToString()),  // "AdminUserId"
        new Claim(AdminClaimConstants.AdminEmailClaimKey, admin.Email),           // "AdminEmail"
        new Claim(AdminClaimConstants.AdminAccountClaimKey, AdminClaimConstants.AdminAccountClaimValue), // adminAccount=true
    ]),
};
var token = new JsonWebTokenHandler().CreateToken(descriptor);
// cookie: AuthCookieConstants.AdminAccessTokenCookieName = ".DotNet.Application.Admin.SystemKey"
//         HttpOnly, Secure, SameSite=Lax
```

Claims live in `AdminsManagement/Constants/AdminClaimConstants.cs`; issuer in
`AdminSecurityConstants.cs`; key binding in
`CoreServicesConfigurationExtensions.cs` (throws when `Security:Signing:Admin:SigningKey`
is missing). Logout (`AdminLogoutEndpoint`, direct-injection) just clears the cookie.

## Step 4 — Invites

`POST invites` — `CreateAdminInviteEndpoint`, stepified:
no-admin-with-email check → no-open-invite check → entity → email. 
`AdminInviteEntity { Email, NickName, TokenHash, InvitedByAdminId (NOT null),
ExpiresAtUtc, AcceptedAtUtc, IsUsable }` with a UNIQUE index on `TokenHash`
(`admins_uq_admin_invite_token_hash_idx` in `MartenConfigurationExtensions.cs`).
`POST invites/accept` — `AcceptAdminInviteEndpoint`, 5 steps: resolve by hash →
`CreateAdminIdentityStep` FIRST (MassTransit `CreateAdmin` with
`FromAdmin = invite.InvitedByAdminId` — IAM verifies the INVITER exists and
returns the `IamUserId`) → create the `AdminEntity` consuming that `IamUserId`
→ consume invite → `IssueAdminSessionStep` (issue + set cookie).
`GET invites/resolve` is anonymous + token-gated (used by the portal's
InviteAccept page).

## Step 5 — Bootstrap admin

`AdminsApplicationLaunchService` (singleton `IApplicationLaunchService`): seeds
ONLY when `Query<AdminEntity>().AnyAsync()` is false; creates the IAM identity
first with the bootstrap marker:

```csharp
new CreateAdmin { FromAdmin = Guid.Empty,   // IAM consumer skips the inviter check; idempotent on email
    Email = options.Value.BootstrapAdminEmail, NickName = options.Value.BootstrapAdminNickName }
```

then inserts the Marten `AdminEntity` with `InvitedByAdminId = null` (the
null-inviter = bootstrap marker) and `IamUserId` from IAM's response. Config
keys (`appsettings.Development.json`): `AdminsManagement:BootstrapAdminEmail`
(`admin@snowcore.blog`), `BootstrapAdminNickName` (`root`),
`AdminFrontendDomain` (`https://localhost/admin`),
`Integrations:Email:SenderAccount:*`, `LoginLinkValidityDurationInMinutes: 15`,
`InviteValidityDurationInDays: 7`.

## Step 6 — Portal side

`Components/Pages/Login.razor` (`@inject IApizrManager<IAdminsApi>`):
`?token=` present → confirm mode (`ConfirmAsync(token)` → signed-in); else the
email form calls `api.RequestLoginLink(...)` and shows the "single-use sign-in
link is on its way" bar. `InviteAccept.razor` handles `/invite?token=`.
Cookie round-trips work because every portal manager carries
`AdminCookiesPropagationHandler` (browser Cookie → gateway request; gateway
Set-Cookie → browser response) — see the `admin-portal-page-author` skill.

## Red Flags

- Storing raw invite/login tokens (only `Sha256Hex` hashes are persisted).
- Reader-style gateway minting for admin JWTs (AdminsManagement mints itself).
- Changing `AdminScheme`/`adminAccount` literals in one place only — gateway
  config, `AdminClaimConstants`, and the root `Program.cs` policy must agree.
- Making invites re-usable (`AcceptedAtUtc` must gate `IsUsable`).
- Forgetting the IAM existence check on confirm (deleted identities must not
  mint sessions).

## Reference implementations

- Link/confirm endpoints: `snowcoreBlog.Backend/BusinessServices/AdminsManagement/Endpoints/Auth/`
- Issuer + token utils: `.../AdminsManagement/Services/AdminTokenPairIssuer.cs`, `AdminTokenUtilities.cs`
- Entities + index: `.../Shared/AdminsManagement.Core/Entities/Admin/`, `.../AdminsManagement/Extensions/Startup/MartenConfigurationExtensions.cs`
- Bootstrap: `.../AdminsManagement/Services/AdminsApplicationLaunchService.cs`; IAM consumer `.../Shared/Services/IAM/Features/Admin/InviteAndCreateAdminConsumer.cs`
- Portal: `snowcoreBlog.AdminFrontend/Components/Pages/Login.razor`, `InviteAccept.razor`
