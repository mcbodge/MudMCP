var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.MudDocs>("muddocs");

builder.Build().Run();
