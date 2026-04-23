var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var db = postgres.AddDatabase("BlogContext", "miniblog");

builder.AddProject<Projects.Miniblog_Core>("miniblog")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
