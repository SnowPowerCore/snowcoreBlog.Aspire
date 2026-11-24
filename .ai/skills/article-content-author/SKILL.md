---
name: article-content-author
description: >-
  How-to for the article content domain — ArticleEntity + ArticleSnapshotEntity
  versioning, creation pipeline (slug, atomic insert), publication flip,
  seeding, DTO mirroring, and the frontend-only Markdig rendering pipeline.
  Use when adding article fields, content features, publication behavior, or
  new content types modeled after articles.
scope: project
---

# Article Content Author

Use this skill when extending articles (fields, publication, listing) or
modeling a new content type after the article pattern.

## When to Invoke

- "add a field to articles", "article editing", "draft/publish states"
- New public article endpoint or admin article feature
- Modeling versioned content (any `X + XSnapshot` domain type)

## When NOT to Invoke

- Site notifications — ServiceNotifications uses the standard FastEndpoints
  stack (see the `blog-content-rules` rule)
- Generic endpoint/step mechanics — see `fastendpoints-endpoints` /
  `stepified-process-handlers`

## Background

An article is TWO Marten documents: `ArticleEntity` (metadata: `Title`, `Slug`,
`AuthorUserIds`, `Tags`, `Category?`, `CoverImageUrl?`, `PublishedAt?`,
`LatestSnapshotId`) and `ArticleSnapshotEntity` (`ArticleId`, `Markdown`,
`Excerpt?`, `MetaDescription?`, `ModifiedAt`). Both are records with init-only
properties in `Shared/Articles.Core/Entities/Article/`. Content exists ONLY in
snapshots; `LatestSnapshotId` is set once at creation and "latest" is
additionally resolved as highest `ModifiedAt` per `ArticleId`
(`GetLatestSnapshotAsync`, `GetLatestArticlesWithSnapshotsAsync`).

## Step 1 — Shape changes: entity + snapshot + DTOs + serialization

1. Add init-only fields to `ArticleEntity` (metadata) or
   `ArticleSnapshotEntity` (content) in `Shared/Articles.Core/Entities/Article/`.
2. Extend the PublicApi DTO (`ArticleDto` / `ArticleAdminDto`) — **in all three
   mirrored copies** (`snowcoreBlog.Universal/PublicApi/BusinessObjects/Dto/`,
   Backend + Frontend + Console).
3. Add `[JsonSerializable(typeof(...))]` entries in
   `Shared/Infrastructure/Context/CoreSerializationContext.cs` (runtime
   failure otherwise).
4. Articles have NO Mapperly mapper — DTOs are hand-constructed inside the
   endpoints (`SearchArticlesEndpoint` does `Select(static e => new ArticleAdminDto
   ...)`, `SetArticlePublicationEndpoint` uses a manual initializer). Extend the
   existing hand-built construction; introduce a `[Mapper]` mapper only
   deliberately (Documents' `Extensions/DocumentMapper.cs` is the pattern to
   copy if you do).

## Step 2 — Creation flow (the only place articles are born)

`CreateArticleEndpoint` → stepified chain `ValidateAuthorAccountStep →
GenerateSlugStep → SaveArticleStep` (ABBREVIATED snippet; the insert currently
lives in `GenerateSlugStep` — its code comment says "The save currently happens
in this step (scaffold)" and `SaveArticleStep` is a pass-through):

```csharp
// GenerateSlugStep (abbreviated — see the file for the full slug-uniqueness loop)
var snapshot = new ArticleSnapshotEntity {
    Id = Guid.NewGuid(), ArticleId = Guid.Empty,        // born orphaned
    Markdown = request.Markdown, ModifiedAt = DateTimeOffset.UtcNow };
var entity = new ArticleEntity { Slug = slug, ..., LatestSnapshotId = snapshot.Id };
snapshot = snapshot with { ArticleId = entity.Id };   // real code: ArticleId = articleId (same value)
await articleRepository.InsertArticleWithSnapshotAsync(entity, snapshot, token);
```

`InsertArticleWithSnapshotAsync` inserts snapshot first, then article, ONE
`SaveChangesAsync`. New snapshot-only writes (version history) use
`InsertSnapshotAsync`.

## Step 3 — Publication flip (current mutation pattern)

`Endpoints/Articles/Admin/SetArticlePublicationEndpoint.cs` is the only
mutation endpoint and is deliberately NOT stepified — direct `IDocumentSession`
injection:

```csharp
var publishedAt = req.Publish ? (req.PublishedAt?.UtcDateTime ?? DateTime.UtcNow) : (DateTime?)null;
var updated = article with { PublishedAt = publishedAt };   // records: with-expression
DocumentSession.Store(updated);
await DocumentSession.SaveChangesAsync(ct);
var result = Maybe.Create(new ArticleAdminDto { Id = updated.Id, Title = updated.Title, /* ... manual initializer ... */ });
await Send.ResponseAsync(
    result.ToApiResponse(serializerOptions: JsonOptions.Value.SerializerOptions),
    result.ToStatusCode(), ct);
```

`SetArticlePublicationDto { Publish (bool), PublishedAt? }`. Route inventory
(all v1, `SerializerContext(CoreSerializationContext.Default)`): public
`GET cached`, `GET {slug}` (antiforgery + anonymous), `POST create` (antiforgery
+ `authorAccount` claims); admin `GET admin/articles` (search),
`GET admin/articles/{id:guid}`, `PUT admin/articles/{id:guid}/publication`,
`DELETE admin/articles/{id:guid}`. The admin routes are anonymous catch-alls
today with an in-code TODO ("restrict to admin users once IAM admin accounts
exist") — do not silently add an auth policy without coordinating the YARP
config and policy literals.

**Adding real editing:** APPEND a new `ArticleSnapshotEntity` (new `Id`, new
`ModifiedAt`) and leave `LatestSnapshotId` alone — retrieval resolves latest by
`ModifiedAt`. Never mutate existing snapshots; never "fix" the pointer
redundantly.

## Step 4 — Seeding

`Services/ArticlesApplicationLaunchService.cs`: skip when
`Query<ArticleEntity>().AnyAsync()`, insert newest via
`InsertArticleWithSnapshotAsync` + older via `InsertSnapshotAsync`, re-point
`article with { LatestSnapshotId = latest.Id }`, whole body try/catch (a
throwing seeder crashes app startup). Content lives in
`Shared/Articles.Core/Entities/Article/ArticleSeedData.cs` — fixed GUIDs, markdown in
raw string literals. Any run of 3+ double-quote characters inside the literal
ends it early (CS8997) — avoid `"""` sequences in markdown content; ``` backtick
code fences are fine and used throughout the seed data.

## Step 5 — Frontend rendering

Backend stores markdown raw and never renders it. The frontend renders in
`Modules/Articles/Pages/ArticleDetailsPage.razor` and
`Components/Fluent/MarkdownEditor.razor`:

```csharp
var html = Markdig.Markdown.ToHtml(data.Markdown, MarkdownPipeline);
// pipeline: UseAdvancedExtensions().UseSoftlineBreakAsHardlineBreak().DisableHtml()
```

`DisableHtml` is the XSS guard — keep it on every new render path.
`Excerpt`/`MetaDescription` fall back: MetaDescription → Excerpt → derive
client-side. Articles pages load in `OnAfterRenderAsync(firstRender)` +
`InvokeAsync(StateHasChanged)` per the frontend rules.

## Red Flags

- Markdown/`PublishedAt` mutations on existing snapshots.
- A second "latest" mechanism (recomputed pointers) diverging from the
  `ModifiedAt` group-by.
- DTO change in one PublicApi mirror only.
- Rendering markdown in the backend or dropping `DisableHtml`.
- `"""` inside a seed-data raw string.

## Reference implementations

- Entities + seed data: `snowcoreBlog.Backend/Shared/Articles.Core/Entities/Article/`
- Repository: `.../BusinessServices/Articles/Interfaces/Repositories/Marten/IArticleRepository.cs` + `Repositories/Marten/ArticleRepository.cs`
- Creation step: `.../BusinessServices/Articles/Steps/Articles/Create/GenerateSlugStep.cs`
- Publication: `.../BusinessServices/Articles/Endpoints/Articles/Admin/SetArticlePublicationEndpoint.cs`
- Seeder: `.../BusinessServices/Articles/Services/ArticlesApplicationLaunchService.cs`
- Render: `snowcoreBlog.Frontend/Modules/Articles/Pages/ArticleDetailsPage.razor`
