using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace snowcoreBlog.AdminFrontend.Extensions;

/// <summary>
/// Shared service registration for the admin portal (single server-only
/// project). Admin module registrations (readers, authors, articles,
/// documents, api access restrictions) will be added here as features land.
/// </summary>
public static class AdminClientServiceCollectionExtensions
{
    public static IServiceCollection AddAdminClient(this IServiceCollection serviceCollection, IConfiguration configuration)
    {
        serviceCollection.AddFluentUIComponents();
        return serviceCollection;
    }
}
