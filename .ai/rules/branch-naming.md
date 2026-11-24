---
description: Branch naming for every snowcoreBlog repo — allowed namespaces, release branch shape, personal dev handles.
globs: ["**"]
---

# Branch naming

Format: `<namespace>/<short-topic>` — kebab-case, lowercase, ASCII, no spaces.

## Allowed namespaces

| Namespace | Use for |
|---|---|
| `feature/` | new functionality (e.g. `feature/passkey-login`) |
| `bugfix/` | non-urgent defect fixes |
| `hotfix/` | urgent fixes cut from `main` or an active release branch |
| `refactor/` | behavior-preserving restructuring |
| `docs/` | documentation-only changes |
| `chore/` | build, CI, dependency bumps, tooling, submodule pointer bumps |
| `agents/` | branches created by AI agents (e.g. `agents/url-shortener-microservice-setup`) |
| `dev/<handle>/` | personal work-in-progress; `<handle>` is your short personal id (e.g. `dev/snowcore/experiment-yarp`) |
| `release/vX.Y[.Z]` | release branches — see shape below |

Anything outside these namespaces needs agreement in the MR that introduces it.

## Release branch shape

- Cut `release/vX.Y[.Z]` from `main` when a version freezes.
- Only cherry-picked `bugfix/` or `hotfix/` commits land on a release branch —
  never merge `main` into it and never start feature work there.
- The final version-bump/tag commit merges back to `main` after release.

## Multi-repo notes

- Branch **in the submodule repo that owns the change**. The parent (Aspire)
  repo only gets a pointer-bump branch (e.g. `chore/bump-backend`) after the
  submodule MR is merged.
- One topic per branch; delete branches after they merge.
