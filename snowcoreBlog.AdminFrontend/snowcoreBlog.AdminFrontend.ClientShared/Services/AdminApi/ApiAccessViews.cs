using snowcoreBlog.PublicApi.BusinessObjects.Dto;

namespace snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi;

// ApiAccessRestrictions rule/template CRUD endpoints return the raw entity JSON
// (no ApiResponse envelope) — these view records mirror that wire shape. They are
// referenced by IApiAccessRestrictionsAdminApi and the AccessRestrictions page.

public sealed record ApiAccessRuleView
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool Enabled { get; init; }
    public int Priority { get; init; }
    public ApiAccessRestrictionActionDto Action { get; init; }
    public List<string> Methods { get; init; } = [];
    public List<string> PathPatterns { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public List<string> IpRanges { get; init; } = [];
    public List<string> RegionCodes { get; init; } = [];
    public Guid? ResponseTemplateId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record ApiAccessResponseTemplateView
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public int StatusCode { get; init; }
    public string ContentType { get; init; } = "application/json";
    public string? BodyJson { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
