// Put extensions in the Aspire.Hosting namespace to ease discovery as referencing
// the .NET Aspire hosting package automatically adds this namespace.
namespace Aspire.Hosting;

public static class LocalSESResourceBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="LocalSESResource"/> to the given
    /// <paramref name="builder"/> instance.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <returns>
    /// An <see cref="IResourceBuilder{LocalSESResource}"/> instance that
    /// represents the added ntfy resource.
    /// </returns>
    public static IResourceBuilder<LocalSESResource> AddLocalSES(
        this IDistributedApplicationBuilder builder,
        string name)
    {
        // The AddResource method is a core API within .NET Aspire and is
        // used by resource developers to wrap a custom resource in an
        // IResourceBuilder<T> instance. Extension methods to customize
        // the resource (if any exist) target the builder interface.
        var resource = new LocalSESResource(name);

        return builder.AddResource(resource)
                      .WithImage(LocalSESContainerImageTags.Image)
                      .WithImageRegistry(LocalSESContainerImageTags.Registry)
                      .WithImageTag(LocalSESContainerImageTags.Tag)
                      .WithHttpEndpoint(
                          targetPort: 8282,
                          port: 8282,
                          name: LocalSESResource.HttpEndpointName);
    }
}

// This class just contains constant strings that can be updated periodically
// when new versions of the underlying container are released.
internal static class LocalSESContainerImageTags
{
    internal const string Registry = "docker.io";

    internal const string Image = "dansyuqri/local-ses";

    internal const string Tag = "latest";
}