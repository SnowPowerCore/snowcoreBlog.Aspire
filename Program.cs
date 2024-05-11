var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithImage("ghcr.io/microsoft/garnet")
    .WithImageTag("latest");

var db = builder.AddPostgres("snowcoreBlog.Db");

builder.AddProject<Projects.snowcoreBlog_Backend_AdminsManagement>("backend_adminsmanagement")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Backend_UsersManagement>("backend_usersmanagement")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Backend_Articles>("backend_articles")
    .WithReference(cache)
    .WithReference(db);

builder.AddProject<Projects.snowcoreBlog_Console_App>("console_appdefault");

await builder.Build().RunAsync();