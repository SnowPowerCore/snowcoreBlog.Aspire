using snowcoreBlog.AdminFrontend.ClientShared.Extensions;
using snowcoreBlog.AdminFrontend.Host.Components;
using snowcoreBlog.AdminFrontend.Host.Extensions;
using snowcoreBlog.AdminFrontend.Host.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(static (c, opts) =>
{
    opts.ValidateScopes = true;
    opts.ValidateOnBuild = true;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddAdminClient(builder.Configuration);
// Server-side admin data access: gateway HttpClients (cookie propagation) + scoped services.
builder.Services.AddAdminDataClients();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseMiddleware<ApplyBasePathMiddleware>();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(snowcoreBlog.AdminFrontend.Client._Imports).Assembly,
        typeof(snowcoreBlog.AdminFrontend.ClientShared._Imports).Assembly);

await app.RunAsync();
