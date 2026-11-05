using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the backend-adminsmanagement endpoints consumed by the
/// admin portal (server-side only). Replaces the former raw named-HttpClient calls.
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IAdminsApi
{
    [Post("/login/link/v1")]
    Task<IApiResponse<ApiResponse>> RequestLoginLink([Body] RequestAdminLoginLinkDto request, [RequestOptions] IApizrRequestOptions options);

    [Post("/login/confirm/v1")]
    Task<IApiResponse<ApiResponse>> ConfirmLogin([Body] ConfirmAdminLoginDto request, [RequestOptions] IApizrRequestOptions options);

    [Get("/me/v1")]
    Task<IApiResponse<ApiResponse>> Me([RequestOptions] IApizrRequestOptions options);

    [Post("/logout/v1")]
    Task<IApiResponse<ApiResponse>> Logout([RequestOptions] IApizrRequestOptions options);

    [Get("/admins/v1")]
    Task<IApiResponse<ApiResponse>> SearchAdmins([Query] int page, [Query] int pageSize, [Query] string? search, [RequestOptions] IApizrRequestOptions options);

    [Delete("/admins/{id}/v1")]
    Task<IApiResponse<ApiResponse>> DeleteAdmin(Guid id, [RequestOptions] IApizrRequestOptions options);

    [Post("/invites/v1")]
    Task<IApiResponse<ApiResponse>> CreateInvite([Body] CreateAdminInviteDto request, [RequestOptions] IApizrRequestOptions options);

    [Get("/invites/v1")]
    Task<IApiResponse<ApiResponse>> GetInvites([RequestOptions] IApizrRequestOptions options);

    [Delete("/invites/{id}/v1")]
    Task<IApiResponse<ApiResponse>> RevokeInvite(Guid id, [RequestOptions] IApizrRequestOptions options);

    [Get("/invites/resolve/v1")]
    Task<IApiResponse<ApiResponse>> ResolveInvite([Query] string token, [RequestOptions] IApizrRequestOptions options);

    [Post("/invites/accept/v1")]
    Task<IApiResponse<ApiResponse>> AcceptInvite([Body] AcceptAdminInviteDto request, [RequestOptions] IApizrRequestOptions options);
}