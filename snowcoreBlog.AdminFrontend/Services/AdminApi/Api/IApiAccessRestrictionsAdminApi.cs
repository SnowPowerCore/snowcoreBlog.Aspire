using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;
using snowcoreBlog.PublicApi.Utilities.Api;

namespace snowcoreBlog.AdminFrontend.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the ApiAccessRestrictions rule/template CRUD endpoints
/// consumed by the admin portal (server-side only). Since 2026-11-11 these endpoints
/// return the standard ApiResponse envelope (Data = rule/template DTO, errors → HTTP 400
/// envelope), so the methods unwrap via <c>ToData&lt;T&gt;</c> like every other admin API.
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IApiAccessRestrictionsAdminApi
{
    // ---- Rules ----

    [Get("/rules/v1")]
    Task<IApiResponse<ApiResponse>> GetRules([RequestOptions] IApizrRequestOptions options);

    [Post("/rules/v1")]
    Task<IApiResponse<ApiResponse>> CreateRule([Body] CreateApiAccessRuleDto request, [RequestOptions] IApizrRequestOptions options);

    [Put("/rules/{id}/v1")]
    Task<IApiResponse<ApiResponse>> UpdateRule(Guid id, [Body] UpdateApiAccessRuleDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/rules/{id}/v1")]
    Task<IApiResponse<ApiResponse>> DeleteRule(Guid id, [RequestOptions] IApizrRequestOptions options);

    // ---- Response templates ----

    [Get("/templates/v1")]
    Task<IApiResponse<ApiResponse>> GetTemplates([RequestOptions] IApizrRequestOptions options);

    [Post("/templates/v1")]
    Task<IApiResponse<ApiResponse>> CreateTemplate([Body] CreateApiAccessResponseTemplateDto request, [RequestOptions] IApizrRequestOptions options);

    [Put("/templates/{id}/v1")]
    Task<IApiResponse<ApiResponse>> UpdateTemplate(Guid id, [Body] UpdateApiAccessResponseTemplateDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/templates/{id}/v1")]
    Task<IApiResponse<ApiResponse>> DeleteTemplate(Guid id, [RequestOptions] IApizrRequestOptions options);
}