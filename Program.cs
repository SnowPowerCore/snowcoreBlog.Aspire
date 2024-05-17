var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithImage("ghcr.io/microsoft/garnet")
    .WithImageTag("latest");

var db = builder.AddPostgres("db");

builder.AddProject<Projects.snowcoreBlog_Backend_AdminsManagement>("backend-adminsmanagement")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Backend_UsersManagement>("backend-usersmanagement")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend-articles")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Frontend_Host>("frontend-apphost")
    .WithReference(cache);

builder.AddProject<Projects.snowcoreBlog_Console_App>("console-appdefault");

await builder.Build().RunAsync();