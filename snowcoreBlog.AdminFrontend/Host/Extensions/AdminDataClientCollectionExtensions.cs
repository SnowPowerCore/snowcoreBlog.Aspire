using Apizr;
using Microsoft.Extensions.DependencyInjection;
using snowcoreBlog.AdminFrontend.ClientShared.Services.AdminApi.Api;
using snowcoreBlog.AdminFrontend.Host.Handlers;

namespace snowcoreBlog.AdminFrontend.Host.Extensions;

public static class AdminDataClientCollectionExtensions
{
    /// <summary>
    /// Host-side Apizr managers for the admin APIs. The handler forwards the
    /// browser's auth cookie and relays Set-Cookie responses; the primary
    /// handler must not keep its own cookie jar.
    /// </summary>
    public static IServiceCollection AddAdminDataClients(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddTransient<AdminCookiesPropagationHandler>();

        RegisterManager<IAdminReadersApi>(services, "https://localhost/api/readers");
        RegisterManager<IAdminAuthorsApi>(services, "https://localhost/api/authors");
        RegisterManager<IAdminArticlesApi>(services, "https://localhost/api/articles");
        RegisterManager<IAdminDocumentsApi>(services, "https://localhost/api/documents");
        RegisterManager<IAdminNotificationsApi>(services, "https://localhost/api/notifications");
        RegisterManager<IApiAccessRestrictionsAdminApi>(services, "https://localhost/api/access");
        RegisterManager<IAdminsApi>(services, "https://localhost/api/admins");

        return services;
    }

    private static void RegisterManager<TApi>(IServiceCollection services, string baseAddress)
        where TApi : class
    {
        services.AddApizr(
            registry => registry.AddManagerFor<TApi>(),
            options => options
                .WithBaseAddress(baseAddress)
                .WithHttpMessageHandler<AdminCookiesPropagationHandler>()
                .ConfigureHttpClientBuilder(static builder => builder
                    .ConfigurePrimaryHttpMessageHandler(static () => new HttpClientHandler { UseCookies = false })));
    }
}
