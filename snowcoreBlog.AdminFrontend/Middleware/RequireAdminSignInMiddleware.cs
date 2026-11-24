namespace snowcoreBlog.AdminFrontend.Middleware;

// Redirects unauthenticated browser navigation to the sign-in page. Cookie
// names mirror AuthCookieConstants.AdminAccessTokenCookieName /
// AdminRefreshTokenCookieName (snowcoreBlog.Backend/Shared/Core/Constants) —
// the host does not compile-reference the AdminsManagement service assembly,
// so the literals are kept in sync by hand (same pattern as the gateway
// auth-policy literals in the root Program.cs).
public sealed class RequireAdminSignInMiddleware(RequestDelegate next)
{
    private const string AdminAccessTokenCookieName = ".DotNet.Application.Admin.SystemKey";
    private const string AdminRefreshTokenCookieName = ".DotNet.Application.Admin.SystemUpdateKey";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAdminCookiePresent(context) && !IsExempt(context.Request.Path))
        {
            context.Response.Redirect($"{context.Request.PathBase}/login");
            return;
        }

        await next(context);
    }

    private static bool IsAdminCookiePresent(HttpContext context)
        => context.Request.Cookies.ContainsKey(AdminAccessTokenCookieName)
           || context.Request.Cookies.ContainsKey(AdminRefreshTokenCookieName);

    // Entry pages, diagnostics and framework/static assets must always render —
    // otherwise the login page itself would lose its stylesheets and scripts.
    private static bool IsExempt(PathString path)
    {
        if (path.StartsWithSegments("/login")
            || path.StartsWithSegments("/invite")
            || path.StartsWithSegments("/error")
            || path.StartsWithSegments("/antiforgery"))
        {
            return true;
        }

        var value = path.Value ?? string.Empty;
        return value.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/_content", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
               // Static assets carry a file extension in their last segment.
               || value[(value.LastIndexOf('/') + 1)..].Contains('.');
    }
}
