namespace snowcoreBlog.AdminFrontend.ClientShared.Utilities;

public static class AdminApiErrors
{
    public static bool IsUnauthorizedOrForbidden(IReadOnlyList<string> errors)
        => errors.Any(static e => e.Contains("401") || e.Contains("403"));
}