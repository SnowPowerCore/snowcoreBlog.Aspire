// Put extensions in the Aspire.Hosting namespace to ease discovery as referencing
// the .NET Aspire hosting package automatically adds this namespace.
namespace Aspire.Hosting;

public static class ntfyResourceBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="ntfyResource"/> to the given
    /// <paramref name="builder"/> instance.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="httpPort">The HTTPS port.</param>
    /// <returns>
    /// An <see cref="IResourceBuilder{ntfyResource}"/> instance that
    /// represents the added ntfy resource.
    /// </returns>
    public static IResourceBuilder<ntfyResource> Addntfy(
        this IDistributedApplicationBuilder builder,
        string name,
        int? httpPort = null)
    {
        // The AddResource method is a core API within .NET Aspire and is
        // used by resource developers to wrap a custom resource in an
        // IResourceBuilder<T> instance. Extension methods to customize
        // the resource (if any exist) target the builder interface.
        var resource = new ntfyResource(name);

        return builder.AddResource(resource)
                      .WithImage(ntfyContainerImageTags.Image)
                      .WithImageRegistry(ntfyContainerImageTags.Registry)
                      .WithImageTag(ntfyContainerImageTags.Tag)
                      .WithArgs("serve")
                      .WithHttpEndpoint(
                          targetPort: 80,
                          port: httpPort,
                          name: ntfyResource.HttpEndpointName);
    }
}

// This class just contains constant strings that can be updated periodically
// when new versions of the underlying container are released.
internal static class ntfyContainerImageTags
{
    internal const string Registry = "docker.io";

    internal const string Image = "binwiederhier/ntfy";

    internal const string Tag = "latest";
}