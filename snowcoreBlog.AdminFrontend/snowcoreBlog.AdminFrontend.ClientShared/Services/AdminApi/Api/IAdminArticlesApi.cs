using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the Articles admin endpoints consumed by the admin
/// portal (server-side only).
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IAdminArticlesApi
{
    [Get("/admin/articles/v1")]
    Task<IApiResponse<ApiResponse>> SearchArticles([Query] int page, [Query] int pageSize, [Query] string? search, [Query] string? tag, [RequestOptions] IApizrRequestOptions options);

    [Get("/admin/articles/{id}/v1")]
    Task<IApiResponse<ApiResponse>> GetArticle(Guid id, [RequestOptions] IApizrRequestOptions options);

    [Put("/admin/articles/{id}/v1/publication")]
    Task<IApiResponse<ApiResponse>> SetPublication(Guid id, [Body] SetArticlePublicationDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/admin/articles/{id}/v1")]
    Task<IApiResponse<ApiResponse>> DeleteArticle(Guid id, [RequestOptions] IApizrRequestOptions options);
}