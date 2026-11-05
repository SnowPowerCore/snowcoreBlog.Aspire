using Apizr.Configuring.Request;
using Refit;
using snowcoreBlog.PublicApi.BusinessObjects.Dto;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;

/// <summary>
/// Typed Refit contract for the ApiAccessRestrictions rule/template CRUD endpoints
/// consumed by the admin portal (server-side only). These endpoints return the raw
/// entity JSON (no ApiResponse envelope), so the methods return typed payloads
/// directly. DELETE endpoints return no body.
/// </summary>
[Headers("Accept: application/json, application/problem+json")]
public interface IApiAccessRestrictionsAdminApi
{
    // ---- Rules ----

    [Get("/rules/v1")]
    Task<IApiResponse<List<ApiAccessRuleView>>> GetRules([RequestOptions] IApizrRequestOptions options);

    [Post("/rules/v1")]
    Task<IApiResponse<ApiAccessRuleView>> CreateRule([Body] CreateApiAccessRuleDto request, [RequestOptions] IApizrRequestOptions options);

    [Put("/rules/{id}/v1")]
    Task<IApiResponse<ApiAccessRuleView>> UpdateRule(Guid id, [Body] UpdateApiAccessRuleDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/rules/{id}/v1")]
    Task<IApiResponse> DeleteRule(Guid id, [RequestOptions] IApizrRequestOptions options);

    // ---- Response templates ----

    [Get("/templates/v1")]
    Task<IApiResponse<List<ApiAccessResponseTemplateView>>> GetTemplates([RequestOptions] IApizrRequestOptions options);

    [Post("/templates/v1")]
    Task<IApiResponse<ApiAccessResponseTemplateView>> CreateTemplate([Body] CreateApiAccessResponseTemplateDto request, [RequestOptions] IApizrRequestOptions options);

    [Put("/templates/{id}/v1")]
    Task<IApiResponse<ApiAccessResponseTemplateView>> UpdateTemplate(Guid id, [Body] UpdateApiAccessResponseTemplateDto request, [RequestOptions] IApizrRequestOptions options);

    [Delete("/templates/{id}/v1")]
    Task<IApiResponse> DeleteTemplate(Guid id, [RequestOptions] IApizrRequestOptions options);
}