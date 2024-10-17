using snowcoreBlog.Aspire.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithImageRegistry("ghcr.io")
    .WithImage("microsoft/garnet")
    .WithImageTag("latest")
    .WithHealthCheck();

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithImage("masstransit/rabbitmq")
    .WithHealthCheck();

var dbSnowCoreBlogEntities = builder.AddPostgres("db-snowcore-blog-entities")
    .WithHealthCheck();
var dbIamEntities = builder.AddPostgres("db-iam-entities")
    .WithHealthCheck();

builder.AddProject<Projects.snowcoreBlog_Backend_IAM>("backend-iam")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithReference(dbIamEntities)
    .WaitFor(cache)
    .WaitFor(dbIamEntities)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.snowcoreBlog_Backend_AuthorsManagement>("backend-authorsmanagement")
    .WithReference(cache)
    .WithReference(dbSnowCoreBlogEntities)
    .WaitFor(cache)
    .WaitFor(dbSnowCoreBlogEntities)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.snowcoreBlog_Backend_ReadersManagement>("backend-readersmanagement")
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithReference(dbSnowCoreBlogEntities)
    .WaitFor(cache)
    .WaitFor(dbSnowCoreBlogEntities)
    .WaitFor(rabbitmq);

builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend-articles")
    .WithReference(cache)
    .WaitFor(cache)
    .WaitFor(rabbitmq);

// builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
//     .WithReference(cache);

// builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

await builder.Build().RunAsync();