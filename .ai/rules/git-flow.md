---
description: Change flow — branching policy and cross-repo propagation guidelines for snowcoreBlog.
globs: ["**"]
---

# Change flow

## Branching

All work happens on `main` — in this repo and in every submodule, all the
time. Do NOT create feature/topic branches unless the developer explicitly
asks for one for a specific change.

## Propagation guidelines (multi-repo)

1. **Universal / PublicApi mirroring** — `snowcoreBlog.Universal` and
   `snowcoreBlog.Universal/PublicApi` are duplicated per consumer repo
   (Backend, Frontend, Console). Any shared contract/DTO change must update
   **all three copies** in the same changeset — mirror first, then consumers.
   The frontend compiles against its own copy; a missing mirror breaks
   unrelated builds.
2. **Submodule pointer bumps** — after a submodule change lands, bump the
   submodule SHA in the parent repo, referencing the submodule commit.
3. **Gateway coupling** — endpoint/path/auth changes must update, together:
   backend `SecurityConstants`/claims, YARP route config (anonymous
   catch-alls vs protected routes), and the root `Program.cs`
   `.WithAuthPolicies` **literals** — the AppHost does not compile-reference
   service assemblies, so literals must be kept in sync by hand.
4. **Serialization registration** — new Marten entities/DTOs require
   `CoreSerializationContext` registration in the same changeset (the Marten
   serializer fails at runtime otherwise).
5. **Frontend wiring** — new backend endpoints need their PublicApi DTO,
   HttpClient/Apizr registration and (when interactive) Fusion service
   registration mirrored into the frontend copies.
