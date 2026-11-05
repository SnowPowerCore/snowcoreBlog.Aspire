var builder = DistributedApplication.CreateBuilder(args);

// WithDataVolume persists Redis snapshots across host restarts. Without it every
// restart wipes the refresh-token records (reader-refresh:*) and all active
// sessions die with 401 "Invalid refresh token" on the next proactive rotation.
var cache = builder.AddRedis("cache")
    .WithDataVolume()
    .WithRedisInsight();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithImage("masstransit/rabbitmq");

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume(isReadOnly: false)
    .WithPgWeb();

var dbSnowCoreBlogEntitiesDb = postgres.AddDatabase("db-snowcore-blog-entities");
var dbSnowCoreBlogArticleEntitiesDb = postgres.AddDatabase("db-snowcore-blog-article-entities");
var dbIamEntitiesDb = postgres.AddDatabase("db-iam-entities");
var dbIpRestrictionsEntitiesDb = postgres.AddDatabase("db-ip-restrictions-entities");
var dbDocumentsEntitiesDb = postgres.AddDatabase("db-documents-entities");
var dbAdminsEntitiesDb = postgres.AddDatabase("db-admins-entities");

var backendIamProject = builder.AddProject<Projects.snowcoreBlog_Backend_IAM>("backend-iam")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbIamEntitiesDb)
    .WaitFor(dbIamEntitiesDb);

builder.AddProject<Projects.snowcoreBlog_Backend_Email>("backend-email")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

// builder.AddProject<Projects.snowcoreBlog_Backend_Push>("backend-push")
//     .WithReference(cache)
//     .WaitFor(cache)
//     .WithReference(rabbitmq)
//     .WaitFor(rabbitmq);

var backendAuthorsManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_AuthorsManagement>("backend-authorsmanagement")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogEntitiesDb)
    .WaitFor(dbSnowCoreBlogEntitiesDb);

var backendReadersManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_ReadersManagement>("backend-readersmanagement")
    .WithReference(backendAuthorsManagementProject)
    .WaitFor(backendAuthorsManagementProject)
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogEntitiesDb)
    .WaitFor(dbSnowCoreBlogEntitiesDb);

var backendArticlesProject = builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend-articles")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogArticleEntitiesDb)
    .WaitFor(dbSnowCoreBlogArticleEntitiesDb);

// var backendUrlShortenerProject = builder.AddProject<Projects.snowcoreBlog_Backend_UrlShortener>("backend-urlshortener")
//     .WithReference(cache)
//     .WaitFor(cache)
//     .WithReference(rabbitmq)
//     .WaitFor(rabbitmq)
//     .WithReference(dbSnowCoreBlogEntitiesDb)
//     .WaitFor(dbSnowCoreBlogEntitiesDb);

var backendServiceNotificationsProject = builder.AddProject<Projects.snowcoreBlog_Backend_ServiceNotifications>("backend-servicenotifications")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogEntitiesDb)
    .WaitFor(dbSnowCoreBlogEntitiesDb);

var backendApiAccessRestrictionsProject = builder.AddProject<Projects.snowcoreBlog_Backend_ApiAccessRestrictions>("backend-apiaccessrestrictions")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbIpRestrictionsEntitiesDb)
    .WaitFor(dbIpRestrictionsEntitiesDb);

var backendDocumentsProject = builder.AddProject<Projects.snowcoreBlog_Backend_Documents>("backend-documents")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbDocumentsEntitiesDb)
    .WaitFor(dbDocumentsEntitiesDb);

// Admin accounts, invitations and magic-link authentication (admin portal IAM).
var backendAdminsManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_AdminsManagement>("backend-adminsmanagement")
    .WaitFor(backendIamProject) // bootstrap CreateAdmin request targets the IAM queue
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbAdminsEntitiesDb)
    .WaitFor(dbAdminsEntitiesDb);

var frontendAppHostProject = builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
    .WaitFor(backendAuthorsManagementProject)
    .WaitFor(backendReadersManagementProject)
    .WaitFor(backendArticlesProject)
    .WithReference(cache)
    .WaitFor(cache);

// Admin portal (Blazor, FluentUI) operated by administrators over the same
// backend services as the public frontend. Served by the ingress under /admin.
var adminFrontendAppHostProject = builder.AddProject<Projects.snowcoreBlog_AdminFrontend_Host>("admin-frontend-apphost")
    .WaitFor(backendAdminsManagementProject)
    .WaitFor(backendAuthorsManagementProject)
    .WaitFor(backendReadersManagementProject)
    .WaitFor(backendArticlesProject)
    .WaitFor(backendServiceNotificationsProject)
    .WaitFor(backendApiAccessRestrictionsProject)
    .WaitFor(backendDocumentsProject)
    .WithReference(cache)
    .WaitFor(cache);

// builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

// builder.Addntfy("ntfy", 4010);
builder.AddLocalSES("local-ses");

builder.AddYarp("ingress")
    .WithReference(backendAuthorsManagementProject)
    .WaitFor(backendAuthorsManagementProject)
    .WithReference(backendReadersManagementProject)
    .WaitFor(backendReadersManagementProject)
    .WithReference(backendArticlesProject)
    .WaitFor(backendArticlesProject)
    .WithReference(backendServiceNotificationsProject)
    .WaitFor(backendServiceNotificationsProject)
    .WithReference(backendApiAccessRestrictionsProject)
    .WaitFor(backendApiAccessRestrictionsProject)
    .WithReference(backendDocumentsProject)
    .WaitFor(backendDocumentsProject)
    .WithReference(backendAdminsManagementProject)
    .WaitFor(backendAdminsManagementProject)
    .WithReference(frontendAppHostProject)
    .WaitFor(frontendAppHostProject)
    .WithReference(adminFrontendAppHostProject)
    .WaitFor(adminFrontendAppHostProject)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(cache)
    .WaitFor(cache)
    .LoadFromConfiguration("ReverseProxy")
    .WithAuthPolicies(
        ("regularReader", policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("readerAccount", allowedValues: true.ToString().ToLower())),
        // Admin policy: validated against the gateway AdminScheme JWTs (issuer
        // "admin", Security:Signing:Admin:SigningKey). Literals kept in sync with
        // SecurityConstants.AdminSchemeName + AdminClaimConstants in the backend.
        ("regularAdmin", policy => policy
            .RequireAuthenticatedUser()
            .AddAuthenticationSchemes("AdminScheme")
            .RequireClaim("adminAccount", allowedValues: "true")))
    .WithHttpsEndpoint(targetPort: 443);

await builder.Build().RunAsync();