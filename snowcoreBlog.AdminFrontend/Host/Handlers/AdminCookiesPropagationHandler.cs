using System.Net.Http.Headers;

namespace snowcoreBlog.AdminFrontend.Host.Handlers;

/// <summary>
/// Bridges the browser session to server-side HttpClients:
/// <list type="bullet">
/// <item>Request direction: copies the browser's Cookie header onto the outgoing
/// gateway request (admin auth cookie reaches backend-adminsmanagement).</item>
/// <item>Response direction: copies Set-Cookie headers from the gateway response
/// onto the browser response (login/invite-confirm cookies land in the browser).</item>
/// </list>
/// The primary handler MUST run with UseCookies=false so no container cookie jar
/// hijacks the flow (same reasoning as the public frontend host).
/// </summary>
public class AdminCookiesPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext is not default(HttpContext))
        {
            var cookieHeader = httpContext.Request.Headers.Cookie.ToString();
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookieHeader);
            }
        }

        return base.SendAsync(request, cancellationToken).ContinueWith(static (task, state) =>
        {
            var response = task.Result;
            if (response.Headers.TryGetValues(HeaderNames.SetCookie, out var setCookies))
            {
                var context = (HttpContext)state!;
                foreach (var setCookie in setCookies)
                {
                    context.Response.Headers.Append(HeaderNames.SetCookie, setCookie);
                }
            }
            return response;
        }, httpContext, cancellationToken);
    }

    private static class HeaderNames
    {
        public const string Cookie = "Cookie";
        public const string SetCookie = "Set-Cookie";
    }
}
