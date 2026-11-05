using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace snowcoreBlog.AdminFrontend.ClientShared.Extensions;

/// <summary>
/// Shared service registration for the admin portal, invoked from BOTH the
/// Blazor.Auto host (server) and the WebAssembly client entry — mirrors
/// snowcoreBlog.Frontend.ClientShared.Extensions.ServiceCollectionExtensions.AddClient.
/// Admin module registrations (readers, authors, articles, documents,
/// api access restrictions) will be added here as features land.
/// </summary>
public static class AdminClientServiceCollectionExtensions
{
    public static IServiceCollection AddAdminClient(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddFluentUIComponents();
        return serviceCollection;
    }
}
