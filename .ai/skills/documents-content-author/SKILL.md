---
name: documents-content-author
description: >-
  How-to for the Documents service content pipeline — multipart upload steps,
  content-addressable SHA-256 storage with dedup, the IDocumentContentStore
  seam, download streaming, and the FastEndpoints multipart gotchas. Use when
  adding upload/download features or modeling file storage.
scope: project
---

# Documents Content Author

Use this skill when extending the Documents service or modeling file
storage/upload flows anywhere in the backend.

## When to Invoke

- "add an upload endpoint", "store files", "download X"
- New domain document services built on the Documents base

## When NOT to Invoke

- Stripe/payment flows (separate service — see `stripe-payments-author`)
- Ordinary JSON endpoints (multipart has its own FastEndpoints rules here)

## Background

`Shared/Services/Documents` stores content content-addressably: files land as
`{Root}/ab/cd/{sha256-hex}` via atomic temp+rename writes (idempotent — same
bytes never write twice). The storage seam is `IDocumentContentStore`
(`Interfaces/Storage/`) with `FileSystemDocumentContentStore`
(`Services/Storage/`) as the implementation — swap the DI registration for blob
storage and the steps never see paths. Options: `Documents:Storage:RootPath`,
`Documents:UploadLimits:{MaxSizeBytes, AllowedContentTypes}`,
`Documents:PublicBaseUrl` (download URL building). Entities are centralized in
`Shared/Documents.Core/Entities/Documents/` (`DocumentEntity`, `DocumentStatus`) with the
`DocumentUploaded` event contract in `Shared/Documents.Core/Contracts/`.

## Rule 1 — Upload pipeline (stepified, dedup-aware)

`UploadDocumentEndpoint` — `Post("upload")`, multipart,
`[StepifiedProcess(Steps = [Validate..., ComputeChecksum, ResolveDuplicate,
PersistContent, CreateEntity, PublishUploaded, BuildResult])]`. Context pattern:
request part immutable, step outputs mutable (`Checksum`, `ContentKey`,
`IsDuplicate`, `Document`) so steps stay reorderable. Dedup: `ResolveDuplicateDocumentStep`
sets `IsDuplicate` when the checksum exists → content write AND the
`DocumentUploaded` publish are skipped, the existing record is returned
(idempotent uploads). Documents are born `Processed` (sync pipeline); async
domain pipelines would set `Processing`/`Rejected` in their own steps.

## Rule 2 — FastEndpoints multipart gotchas (verified)

- `EndpointWithoutRequest` has NO `Request` property (CS0103) — parse multipart
  via `HttpContext.Request.HasFormContentType` /
  `HttpContext.Request.ReadFormAsync(ct)`.
- `AllowFileUploads()` exists for enabling multipart.
- Download: `Send.StreamAsync(stream, fileName, fileLength, contentType,
  cancellation: ct)` — positional order is fileName → fileLength →
  contentType, and the stream is disposed BY `SendStreamAsync` (do not
  `await using` it yourself).

## Rule 3 — Delete keeps blobs

Deleting a document removes the RECORD, not the content-addressed blob
(shared content may be referenced by other records) — do not "fix" this by
deleting files without introducing reference counting.

## Rule 4 — Downloads are served by route

`GET download/{id}/v1` streams the stored content back (frontend links to
`/api/documents/download/{id}/v1` — the admin portal uses this exact URL).
Access is anonymous today (same TODO-restrict stance as other new services).

## Red Flags

- Writing content before dedup resolution (defeats content-addressing).
- Direct `File.*` calls instead of `IDocumentContentStore`.
- Skipping the size/content-type option checks (`Documents:UploadLimits`).
- Disposing the download stream manually.
- A second upload pipeline that bypasses the checksum/dedup steps.

## Reference implementations

- Store seam: `snowcoreBlog.Backend/Shared/Services/Documents/Interfaces/Storage/IDocumentContentStore.cs`, `Services/Storage/FileSystemDocumentContentStore.cs`
- Upload pipeline: `.../Documents/Steps/` + `Endpoints/Documents/UploadDocumentEndpoint.cs`
- Entities/event: `snowcoreBlog.Backend/Shared/Documents.Core/Entities/Documents/`, `Shared/Documents.Core/Contracts/DocumentUploaded.cs`
- Options: `.../Documents/Options/`
