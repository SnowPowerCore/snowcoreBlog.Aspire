using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the Documents admin endpoints consumed by the admin
/// portal (server-side only). Mirrors the former URLs: list under
/// /admin/documents/v1, delete directly under /{id}/v1.
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IAdminDocumentsApi
{
    [Get("/admin/documents/v1")]
    Task<IApiResponse<ApiResponse>> SearchDocuments([Query] int page, [Query] int pageSize, [Query] string? search, [Query] string? category, [Query] int? status, [RequestOptions] IApizrRequestOptions options);

    [Delete("/{id}/v1")]
    Task<IApiResponse> DeleteDocument(Guid id, [RequestOptions] IApizrRequestOptions options);
}