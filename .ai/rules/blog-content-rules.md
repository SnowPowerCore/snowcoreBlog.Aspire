---
description: Blog content domain — article + snapshot versioning model, seeding conventions, frontend-only markdown rendering, ServiceNotifications FastEndpoints pattern
globs: ["snowcoreBlog.Backend/**/*.cs", "snowcoreBlog.Frontend/**/*.razor"]
alwaysApply: false
---

# Blog Content Rules

Use this rule when changing article persistence, publication, seeding, content
rendering, or the site-notification system.

## Service Context

- **Articles** (`BusinessServices/Articles`): entity + snapshot versioning over
  Marten; content is markdown, rendered by the frontend only.
- **ServiceNotifications** (`Shared/Services/ServiceNotifications`): site-bar
  notifications (Info/Warning/Maintenance/Outage), consumed by the frontend
  `NotificationBar`. Standard FastEndpoints stack like every other backend
  service; only the two cached/fan-out reads stay stepified.
- Content entities live in per-service Core projects: `Shared/Articles.Core/Entities/Article/`
  (`ArticleEntity`, `ArticleSnapshotEntity`, `ArticleSeedData`) and
  `Shared/ServiceNotifications.Core/Entities/Notification/NotificationEntity.cs`.

## Domain Rules

1. **Article content lives ONLY in snapshots**: `ArticleEntity` carries
   metadata (`Title`, `Slug`, `AuthorUserIds`, `Tags`, `Category`,
   `CoverImageUrl`, `PublishedAt?`, `LatestSnapshotId`) and
   `ArticleSnapshotEntity` carries `ArticleId`, `Markdown`, `Excerpt?`,
   `MetaDescription?`, `ModifiedAt`. "Latest" = `LatestSnapshotId` pointer at
   creation AND, everywhere else, highest `ModifiedAt` per `ArticleId`
   (group-by in `GetLatestArticlesWithSnapshotsAsync`; per-article
   `OrderByDescending(ModifiedAt)` in `GetLatestSnapshotAsync`).
   Why: version history is the retrieval model; mutating snapshot content or
   recomputing the pointer ad hoc breaks both lookup paths.

2. **Creation writes snapshot-then-article atomically**:
   `IArticleRepository.InsertArticleWithSnapshotAsync(entity, snapshot, ct)`
   inserts the snapshot first, then the article, one `SaveChangesAsync`;
   older versions go through `InsertSnapshotAsync(snapshot)`. In the creation
   step the snapshot is born with `ArticleId = Guid.Empty` and re-pointed via
   `snapshot with { ArticleId = articleId }` (`Steps/Articles/Create/GenerateSlugStep.cs`),
   after `SlugExistsAsync` guard.
   Why: Marten has no FK-enforced ordering here — the two-insert one-commit
   pattern is what keeps article+content consistent.

3. **No article-edit flow exists yet; publication is a metadata flip**: the
   only mutation endpoint is `SetArticlePublicationEndpoint`
   (`PUT admin/articles/{id:guid}/publication`, NOT stepified — direct
   `IDocumentSession` injection) doing `article with { PublishedAt = req.Publish
   ? (req.PublishedAt?.UtcDateTime ?? DateTime.UtcNow) : null }` then
   `Store` + `SaveChangesAsync`. The admin routes are anonymous catch-alls today
   (`// TODO: restrict to admin users` in code).
   Why: when adding real editing, APPEND a new snapshot (new `ModifiedAt`) —
   never mutate existing ones; and do not "fix" the anonymous route without
   coordinating the admin policy.

4. **Markdown is stored raw and rendered frontend-only**: zero Markdig in the
   backend. Rendering happens in
   `snowcoreBlog.Frontend/Modules/Articles/Pages/ArticleDetailsPage.razor` and
   `Components/Fluent/MarkdownEditor.razor` via
   `Markdown.ToHtml(data.Markdown, MarkdownPipeline)` with pipeline
   `UseAdvancedExtensions().UseSoftlineBreakAsHardlineBreak().DisableHtml()`
   (`DisableHtml` is the XSS guard). `Excerpt`/`MetaDescription` null-fallback
   chain: MetaDescription → Excerpt → derive on client.
   Why: server-side rendering would duplicate the sanitizer story; the pipeline
   choice is a security decision, not taste.

5. **Seeding is skip-if-empty and must never crash startup**:
   `Articles/Services/ArticlesApplicationLaunchService.cs` returns when
   `Query<ArticleEntity>().AnyAsync()` is true, seeds via
   `InsertArticleWithSnapshotAsync` + `InsertSnapshotAsync` (older versions),
   re-pointing `article with { LatestSnapshotId = latest.Id }`, whole body in
   try/catch. Seed content lives in `Shared/Articles.Core/Entities/Article/ArticleSeedData.cs`
   with fixed GUIDs and raw string literals — never nest `"""` inside a `"""`
   literal (CS8997), e.g. for fenced code blocks in markdown samples.
   Why: `ApplicationLaunchService` runs on every boot; a throwing seeder or
   duplicate seed run breaks startup/idempotency.

6. **ServiceNotifications CRUD is direct FastEndpoints, reads are stepified**:
   the notification CRUD endpoints (`Create/Update/Delete/GetAll/GetById`) are
   plain `Endpoint<T, ApiResponse?>` handlers injecting `INotificationRepository`
   directly — no steps/delegates/contexts. Only the two reads stay stepified
   (`GetActiveNotificationsCachedStep` — Redis cache-aside;
   `GetActiveNotificationsForUserStep` — MassTransit fan-out to provider queues
   + per-user Redis cache). Envelope comes from the global FE config
   (`ResponseSerializer` + `Errors.ResponseBuilder` in
   `Extensions/Startup/FastEndpointsConfigurationExtensions.cs`), validation via
   `Validator<XxxNotificationValidator>()`, antiforgery via `EnableAntiforgery()`
   on the mutating endpoints. Update/Delete not-found returns
   `NotificationNotFoundError<T>` → 400 envelope (NOT 404); GetById sends a
   manual 404 envelope.
   Why: re-adding steps/delegates/contexts for single-repository-call CRUD
   recreates single-element pipelines with no benefit.

7. **Notification activation is a PUT, not a toggle**: there is no
   activate/deactivate endpoint — `UpdateNotificationDto.IsActive` flows through
   `PUT /notifications/v1`. The frontend bar reads `GET /notifications/active/v1`
   (`ResponseCache(300)`) via `INotificationsApi.GetActiveNotifications` into
   `NotificationBarState` (TimeWarp State: `ActiveNotifications`,
   `DismissedNotificationIds`, computed `VisibleNotifications`). The service
   publishes no events and has no consumers.
   Why: agents habitually invent a toggle endpoint/events that don't exist.

## Red Flags

- Storing markdown on `ArticleEntity` or mutating an existing snapshot.
- Adding Markdig (or any rendering) to the backend.
- A seeder that runs unconditionally or throws out of `InitAsync`.
- Stepifying notification CRUD (steps/delegates/contexts for a single
  repository call) or reviving FastSharp-style modules/envelope helpers.
- A new "activate" endpoint duplicating `IsActive` on the update DTO.
- Content DTOs added anywhere but the ×3-mirrored PublicApi.

## Evidence

- Entities + seed: `snowcoreBlog.Backend/Shared/Articles.Core/Entities/Article/` (`ArticleEntity.cs`, `ArticleSnapshotEntity.cs`, `ArticleSeedData.cs`)
- Repository: `.../BusinessServices/Articles/Interfaces/Repositories/Marten/IArticleRepository.cs` (+ impl)
- Slug/creation step: `.../BusinessServices/Articles/Steps/Articles/Create/GenerateSlugStep.cs`
- Publication: `.../BusinessServices/Articles/Endpoints/Articles/Admin/SetArticlePublicationEndpoint.cs`
- Seeder: `.../BusinessServices/Articles/Services/ArticlesApplicationLaunchService.cs`
- FastEndpoints endpoints: `.../Shared/Services/ServiceNotifications/Endpoints/Notifications/`
  (`CreateNotificationEndpoint.cs` direct CRUD, `GetActiveNotificationsEndpoint.cs`
  stepified read); FE config: `.../Extensions/Startup/FastEndpointsConfigurationExtensions.cs`
- Frontend render: `snowcoreBlog.Frontend/Modules/Articles/Pages/ArticleDetailsPage.razor`; bar state: `snowcoreBlog.Frontend/Modules/ServiceNotifications/Features/NotificationBar/NotificationBarState.cs`
