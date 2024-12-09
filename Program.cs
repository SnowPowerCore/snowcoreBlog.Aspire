var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithImageRegistry("ghcr.io")
    .WithImage("microsoft/garnet")
    .WithImageTag("latest")
    .WithRedisInsight();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithImage("masstransit/rabbitmq")
    .WithHttpsEndpoint(targetPort: 61633);

var dbSnowCoreBlogEntities = builder.AddPostgres("db-snowcore-blog-entities")
    .WithPgAdmin()
    .WithHttpsEndpoint(targetPort: 61636);
var dbIamEntities = builder.AddPostgres("db-iam-entities")
    .WithPgAdmin()
    .WithHttpsEndpoint(targetPort: 61637);

builder.AddProject<Projects.snowcoreBlog_Backend_IAM>("backend-iam")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbIamEntities)
    .WaitFor(dbIamEntities);

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
    .WithReference(dbSnowCoreBlogEntities)
    .WaitFor(dbSnowCoreBlogEntities);

var backendReadersManagementProject = builder.AddProject<Projects.snowcoreBlog_Backend_ReadersManagement>("backend-readersmanagement")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WithReference(dbSnowCoreBlogEntities)
    .WaitFor(dbSnowCoreBlogEntities);

var backendArticlesProject = builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend-articles")
    .WithReference(cache)
    .WaitFor(cache)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

builder.AddYarp("ingress")
    .WithReference(backendAuthorsManagementProject)
    .WithReference(backendReadersManagementProject)
    .WithReference(backendArticlesProject)
    .LoadFromConfiguration("ReverseProxy")
    .WithHttpsEndpoint(targetPort: 443);

// builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
//     .WithReference(cache);

// builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

await builder.Build().RunAsync();