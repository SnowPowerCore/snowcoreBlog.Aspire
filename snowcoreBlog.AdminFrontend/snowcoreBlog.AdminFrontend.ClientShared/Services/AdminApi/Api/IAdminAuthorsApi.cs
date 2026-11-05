using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the AuthorsManagement admin endpoints consumed by the
/// admin portal (server-side only).
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IAdminAuthorsApi
{
    [Get("/admin/accounts/v1")]
    Task<IApiResponse<ApiResponse>> SearchAccounts([Query] int page, [Query] int pageSize, [Query] string? search, [RequestOptions] IApizrRequestOptions options);

    [Delete("/admin/accounts/{id}/v1")]
    Task<IApiResponse<ApiResponse>> DeleteAccount(Guid id, [RequestOptions] IApizrRequestOptions options);
}