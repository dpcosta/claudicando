var builder = DistributedApplication.CreateBuilder(args);

// 1. RECURSOS DE INFRAESTRUTURA
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent);

var catalogDb = sqlServer.AddDatabase("catalogdb");
var ordersDb = sqlServer.AddDatabase("ordersdb");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent);

var rabbitMq = builder.AddRabbitMQ("rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithManagementPlugin();

// 2. MICROSERVIÇOS
var catalogApi = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb)
    .WithReference(redis)
    .WaitFor(catalogDb)
    .WaitFor(redis);

var ordersApi = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WithReference(rabbitMq)
    .WithReference(catalogApi)
    .WaitFor(ordersDb)
    .WaitFor(rabbitMq)
    .WaitFor(catalogApi);

builder.Build().Run();
