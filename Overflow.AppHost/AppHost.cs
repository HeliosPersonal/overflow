var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data")
    // .WithRealmImport("../infra/realms")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
    .WithEnvironment("VIRTUAL_HOST", "id.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "8080");

var postgres = builder
    .AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgWeb();

var typesenseApiKey = builder.Configuration["TypesenseOptions:ApiKey"]
                      ?? throw new InvalidOperationException("Could not get typesense api key");

var typesense = builder.AddContainer("typesense", "typesense/typesense", "29.0")
    .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithEnvironment("TYPESENSEOPTIONS__APIKEY", typesenseApiKey)
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typesenseReference = typesense.GetEndpoint("typesense");

var questionDb = postgres.AddDatabase("questionDb");
var profileDb = postgres.AddDatabase("profileDb");
var statDb = postgres.AddDatabase("statDb");
var voteDb = postgres.AddDatabase("voteDb");
var estimationDb = postgres.AddDatabase("estimationDb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin(port: 15672);

var questionService = builder.AddProject<Projects.Overflow_QuestionService>("question-svc")
    .WithReference(keycloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq);

var searchService = builder.AddProject<Projects.Overflow_SearchService>("search-svc")
    .WithEnvironment("TYPESENSEOPTIONS__ConnectionUrl", typesenseReference)
    .WithEnvironment("TYPESENSEOPTIONS__APIKEY", typesenseApiKey)
    .WithReference(typesenseReference)
    .WithReference(rabbitmq)
    .WaitFor(typesense)
    .WaitFor(rabbitmq);

var profileService = builder.AddProject<Projects.Overflow_ProfileService>("profile-svc")
    .WithReference(keycloak)
    .WithReference(profileDb)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(profileDb)
    .WaitFor(rabbitmq);

var statService = builder.AddProject<Projects.Overflow_StatsService>("stat-svc")
    .WithReference(statDb)
    .WithReference(rabbitmq)
    .WaitFor(statDb)
    .WaitFor(rabbitmq);

var voteService = builder.AddProject<Projects.Overflow_VoteService>("vote-svc")
    .WithReference(voteDb)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(voteDb)
    .WaitFor(rabbitmq);

var dataSeederService = builder.AddProject<Projects.Overflow_DataSeederService>("data-seeder-svc")
    .WaitFor(questionService);

var estimationService = builder.AddProject<Projects.Overflow_EstimationService>("estimation-svc")
    .WithReference(keycloak)
    .WithReference(estimationDb)
    .WaitFor(keycloak)
    .WaitFor(estimationDb);

var yarp = builder
    .AddYarp("gateway")
    .WithConfiguration(yarpBuilder =>
    {
        yarpBuilder.AddRoute("/questions/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/test/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/tags/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/search/{**catch-all}", searchService);
        yarpBuilder.AddRoute("/profiles/{**catch-all}", profileService);
        yarpBuilder.AddRoute("/stats/{**catch-all}", statService);
        yarpBuilder.AddRoute("/votes/{**catch-all}", voteService);
        yarpBuilder.AddRoute("/estimation/{**catch-all}", estimationService);
    })
    .WithEnvironment("ASPNETCORE_URLS", "http://*:8001")
    .WithEndpoint(port: 8001, targetPort: 8001, scheme: "http", name: "gateway", isExternal: true)
    .WithEnvironment("VIRTUAL_HOST", "api.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "8001");

var webapp = builder.AddNpmApp("webapp", "../webapp", "dev")
    .WithReference(keycloak)
    .WithHttpEndpoint(env: "PORT", port: 3000, targetPort: 4000);

builder.Build().Run();