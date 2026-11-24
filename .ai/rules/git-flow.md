---
description: Change flow — merge vs cherry-pick policy and cross-repo propagation guidelines.
globs: ["**"]
---

# Git flow

## main and MRs

- `main` is protected in every repo (root + submodules). Land changes through
  MRs from a namespace branch (see `branch-naming.md`). No direct pushes,
  except a declared hotfix escape.
- One MR = one topic = one repo. Cross-repo changes are separate, linked MRs.

## Merge vs cherry-pick

- **Merge** `feature/`, `bugfix/`, `refactor/`, `docs/`, `chore/` branches into
  `main` (squash or merge commit — keep `main` bisectable).
- **Cherry-pick** only to carry a specific fix from `main` onto an active
  `release/vX.Y` branch. Keep the original message with a
  `(cherry picked from commit ...)` trailer.
- Never merge `main` into a release branch; never merge a release branch back
  into `main` except the final version-bump/tag commit.

## Propagation guidelines (multi-repo)

1. **Universal / PublicApi mirroring** — `snowcoreBlog.Universal` and
   `snowcoreBlog.Universal/PublicApi` are duplicated per consumer repo
   (Backend, Frontend, Console). Any shared contract/DTO change must update
   **all three copies** in the same changeset — mirror first, then consumers.
   The frontend compiles against its own copy; a missing mirror breaks
   unrelated builds.
2. **Submodule pointer bumps** — after a submodule MR merges, open a parent
   repo MR that bumps the submodule SHA (`chore/bump-<repo>`), referencing the
   submodule MR.
3. **Gateway coupling** — endpoint/path/auth changes must update, together:
   backend `SecurityConstants`/claims, YARP route config (anonymous
   catch-alls vs protected routes), and the root `Program.cs`
   `.WithAuthPolicies` **literals** — the AppHost does not compile-reference
   service assemblies, so literals must be kept in sync by hand.
4. **Serialization registration** — new Marten entities/DTOs require
   `CoreSerializationContext` registration in the same MR (the Marten
   serializer fails at runtime otherwise).
5. **Frontend wiring** — new backend endpoints need their PublicApi DTO,
   HttpClient/Apizr registration and (when interactive) Fusion service
   registration mirrored into the frontend copies.
