using snowcoreBlog.AdminFrontend.Components;
using snowcoreBlog.AdminFrontend.Extensions;
using snowcoreBlog.AdminFrontend.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseDefaultServiceProvider(static (c, opts) =>
{
    opts.ValidateScopes = true;
    opts.ValidateOnBuild = true;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAdminClient(builder.Configuration);
// Server-side admin data access: gateway HttpClients (cookie propagation) + scoped services.
builder.Services.AddAdminDataClients();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.MapStaticAssets();
app.UseMiddleware<ApplyBasePathMiddleware>();
app.UseMiddleware<RequireAdminSignInMiddleware>();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
