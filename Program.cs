var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithImageRegistry("ghcr.io")
    .WithImage("microsoft/garnet")
    .WithImageTag("latest");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithImage("masstransit/rabbitmq");

var dbSnowCoreBlogEntities = builder.AddPostgres("db-snowcore-blog-entities");
var dbIamEntities = builder.AddPostgres("db-iam-entities");

builder.AddProject<Projects.snowcoreBlog_Backend_IAM>("backend-iam")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithReference(dbIamEntities);

builder.AddProject<Projects.snowcoreBlog_Backend_AuthorsManagement>("backend-authorsmanagement")
    .WithReference(cache)
    .WithReference(dbSnowCoreBlogEntities);

builder.AddProject<Projects.snowcoreBlog_Backend_ReadersManagement>("backend-readersmanagement")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithReference(dbSnowCoreBlogEntities);

builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend-articles")
    .WithReference(cache);

// builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
//     .WithReference(cache);

// builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

await builder.Build().RunAsync();