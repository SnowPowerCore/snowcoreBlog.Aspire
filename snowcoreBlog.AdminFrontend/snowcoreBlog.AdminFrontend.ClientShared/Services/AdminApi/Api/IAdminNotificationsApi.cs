using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the ServiceNotifications CRUD endpoints consumed by
/// the admin portal (server-side only). All verbs target /v1.
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IAdminNotificationsApi
{
    [Get("/v1")]
    Task<IApiResponse<ApiResponse>> GetAll([RequestOptions] IApizrRequestOptions options);

    [Post("/v1")]
    Task<IApiResponse<ApiResponse>> Create([Body] CreateNotificationDto request, [RequestOptions] IApizrRequestOptions options);

    [Put("/v1")]
    Task<IApiResponse<ApiResponse>> Update([Body] UpdateNotificationDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/v1")]
    Task<IApiResponse<ApiResponse>> Delete([Body] DeleteNotificationDto request, [RequestOptions] IApizrRequestOptions options);
}