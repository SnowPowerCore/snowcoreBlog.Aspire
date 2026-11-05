using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using snowcoreBlog.AdminFrontend.ClientShared.Extensions;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
{
    ValidateScopes = true,
    ValidateOnBuild = true
}));

builder.Services.AddAdminClient(builder.Configuration);

await builder.Build().RunAsync();
