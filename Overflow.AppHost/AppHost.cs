using Overflow.Common;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
    .WithEnvironment("VIRTUAL_HOST", "id.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "8080");

var postgres = builder
    .AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgWeb();

var typesenseApiKey = builder.Configuration[ConfigurationKeys.TypesenseApiKey]
                      ?? throw new InvalidOperationException("Could not get typesense api key");

var typesense = builder.AddContainer("typesense", "typesense/typesense", "29.0")
    .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithEnvironment("TYPESENSE_OPTIONS__API_KEY", typesenseApiKey)
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

#pragma warning disable ASPIRECERTIFICATES001
var redis = builder
    .AddRedis("redis")
    .WithRedisInsight()
    .WithoutHttpsCertificate()
#pragma warning restore ASPIRECERTIFICATES001
    .WithDataVolume("redis-data");

var questionService = builder.AddProject<Projects.Overflow_QuestionService>("question-svc")
    .WithReference(keycloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WithReference(redis, "question-redis")
    .WaitFor(keycloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq)
    .WaitFor(redis);

var searchService = builder.AddProject<Projects.Overflow_SearchService>("search-svc")
    .WithEnvironment("TypesenseOptions__ConnectionUrl", typesenseReference)
    .WithEnvironment("TypesenseOptions__ApiKey", typesenseApiKey)
    .WithEnvironment("TypesenseOptions__CollectionName", "local_questions")
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
    .WithReference(redis, "stat-redis")
    .WaitFor(statDb)
    .WaitFor(rabbitmq)
    .WaitFor(redis);

var voteService = builder.AddProject<Projects.Overflow_VoteService>("vote-svc")
    .WithReference(voteDb)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(voteDb)
    .WaitFor(rabbitmq);

var estimationService = builder.AddProject<Projects.Overflow_EstimationService>("estimation-svc")
    .WithReference(keycloak)
    .WithReference(estimationDb)
    .WithReference(redis, "estimation-redis")
    .WithReference(profileService)
    .WaitFor(keycloak)
    .WaitFor(estimationDb)
    .WaitFor(redis);

var notificationService = builder.AddProject<Projects.Overflow_NotificationService>("notification-svc")
    .WithReference(keycloak)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq);

var yarp = builder
    .AddYarp("gateway")
    .WithHostPort(8001)
    .WithContainerRuntimeArgs("--add-host", "host.docker.internal:host-gateway")
    .WithConfiguration(yarpBuilder =>
    {
        yarpBuilder.AddRoute("/questions/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/tags/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/search/{**catch-all}", searchService);
        yarpBuilder.AddRoute("/profiles/{**catch-all}", profileService);
        yarpBuilder.AddRoute("/stats/{**catch-all}", statService);
        yarpBuilder.AddRoute("/votes/{**catch-all}", voteService);
        yarpBuilder.AddRoute("/estimation/{**catch-all}", estimationService);
        yarpBuilder.AddRoute("/notifications/{**catch-all}", notificationService);
    });

var ollama = builder.AddContainer("ollama", "ollama/ollama", "latest")
    .WithVolume("ollama-data", "/root/.ollama")
    .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "ollama");

var ollamaReference = ollama.GetEndpoint("ollama");

var dataSeeder = builder.AddProject<Projects.Overflow_DataSeederService>("data-seeder-svc")
    .WithReference(keycloak)
    .WithEnvironment("SeederOptions__QuestionServiceUrl", yarp.GetEndpoint("http"))
    .WithEnvironment("SeederOptions__ProfileServiceUrl", yarp.GetEndpoint("http"))
    .WithEnvironment("SeederOptions__VoteServiceUrl", yarp.GetEndpoint("http"))
    .WithEnvironment("SeederOptions__LlmApiUrl", ollamaReference)
    .WaitFor(keycloak)
    .WaitFor(questionService)
    .WaitFor(profileService)
    .WaitFor(voteService)
    .WaitFor(ollama);

var webapp = builder.AddNpmApp("webapp", "../webapp", "dev")
    .WithReference(keycloak)
    .WithHttpEndpoint(env: "PORT", port: 3000, targetPort: 4000);

builder.Build().Run();