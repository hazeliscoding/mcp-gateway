using Testcontainers.PostgreSql;

namespace McpGateway.IntegrationTests;

/// <summary>
/// One PostgreSQL container shared by every test in the collection; each test
/// class gets isolation by using its own database schema state via unique tool
/// names rather than container-per-test (which would be far slower).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
