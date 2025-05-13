var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithRedisInsight();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithImage("masstransit/rabbitmq");

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume(isReadOnly: false)
    .WithPgWeb();

var dbSnowCoreBlogEntitiesDb = postgres.AddDatabase("db-snowcore-blog-entities");
var dbIamEntitiesDb = postgres.AddDatabase("db-iam-entities");

builder.AddProject<Projects.snowcoreBlog_Backend_IAM>("backend-iam")
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

var backendAuthorsManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_AuthorsManagement>("backend-authorsmanagement")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogEntitiesDb)
    .WaitFor(dbSnowCoreBlogEntitiesDb);

var backendReadersManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_ReadersManagement>("backend-readersmanagement")
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
    .WaitFor(rabbitmq);

// builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
//     .WaitFor(backendAuthorsManagementProject)
//     .WaitFor(backendReadersManagementProject)
//     .WaitFor(backendArticlesProject)
//     .WithReference(cache)
//     .WaitFor(cache);

// builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

builder.AddYarp("ingress")
    .WithReference(backendAuthorsManagementProject)
    .WithReference(backendReadersManagementProject)
    .WithReference(backendArticlesProject)
    .WithReference(rabbitmq)
    .LoadFromConfiguration("ReverseProxy")
    .WithAuthPolicies(
        ("regularReader", policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim("readerAccount", allowedValues: true.ToString().ToLower())))
    .WithHttpsEndpoint(targetPort: 443);

await builder.Build().RunAsync();