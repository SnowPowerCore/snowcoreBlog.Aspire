// Put resource-related types in the Aspire.Hosting namespace to ease discovery
// as referencing the .NET Aspire hosting package automatically adds this namespace.
namespace Aspire.Hosting;

// This class just contains constant strings that can be updated periodically
// when new versions of the underlying container are released.
internal static class LocalSESContainerImageTags
{
    internal const string Registry = "docker.io";

    internal const string Image = "dansyuqri/local-ses";

    internal const string Tag = "latest";
}
