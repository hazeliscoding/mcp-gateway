using McpGateway.Domain.Tools;
using McpGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class ToolRegistryRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private McpGatewayDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<McpGatewayDbContext>()
            .UseNpgsql(postgres.ConnectionString)
            .Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static ToolVersionSpec Spec(string version) => new(
        ToolVersionNumber.Create(version),
        "Samples messages from a dead-letter queue for failure correlation.",
        RiskLevel.ReadOnly,
        ApprovalRequired: false,
        RequiredScopes: ["queue.read", "queue.sample"],
        TimeoutSeconds: 20,
        """{"type":"object","properties":{"queueUrl":{"type":"string"},"sampleSize":{"type":"integer"}}}""",
        """{"type":"object","properties":{"messages":{"type":"array"}}}""");

    [Fact]
    public async Task Aggregate_round_trips_through_postgres()
    {
        var name = ToolName.Create("get_dead_letter_queue_sample");
        var tool = ToolDefinition.Register(name, Spec("1.0"), DateTimeOffset.UtcNow);
        tool.AddVersion(Spec("1.1"), DateTimeOffset.UtcNow);

        await using (var write = CreateContext())
        {
            var repository = new ToolRegistryRepository(write);
            await repository.AddAsync(tool, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var read = CreateContext();
        var loaded = await new ToolRegistryRepository(read).GetByNameAsync(name, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.True(loaded.Enabled);
        Assert.Equal(2, loaded.Versions.Count);
        Assert.Equal(["1.0.0", "1.1.0"], loaded.Versions.Select(v => v.Number.ToString()).ToArray());
        Assert.Equal(["queue.read", "queue.sample"], loaded.Versions[0].RequiredScopes);
        Assert.Contains("queueUrl", loaded.Versions[0].InputSchemaJson);
    }

    [Fact]
    public async Task Mutations_on_tracked_aggregate_persist()
    {
        var name = ToolName.Create("restart_worker_service");
        var tool = ToolDefinition.Register(name, Spec("1.0"), DateTimeOffset.UtcNow);

        await using (var write = CreateContext())
        {
            var repository = new ToolRegistryRepository(write);
            await repository.AddAsync(tool, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var mutate = CreateContext())
        {
            var repository = new ToolRegistryRepository(mutate);
            var tracked = await repository.GetByNameAsync(name, CancellationToken.None);
            tracked!.DeprecateVersion(ToolVersionNumber.Create("1.0"));
            tracked.Disable();
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var read = CreateContext();
        var loaded = await new ToolRegistryRepository(read).GetByNameAsync(name, CancellationToken.None);

        Assert.False(loaded!.Enabled);
        Assert.Equal(ToolVersionStatus.Deprecated, loaded.Versions.Single().Status);
    }

    [Fact]
    public async Task Duplicate_tool_name_violates_primary_key()
    {
        var name = ToolName.Create("get_service_health");

        await using (var first = CreateContext())
        {
            var repository = new ToolRegistryRepository(first);
            await repository.AddAsync(ToolDefinition.Register(name, Spec("1.0"), DateTimeOffset.UtcNow), CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var second = CreateContext();
        var duplicate = new ToolRegistryRepository(second);
        await duplicate.AddAsync(ToolDefinition.Register(name, Spec("1.0"), DateTimeOffset.UtcNow), CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(CancellationToken.None));
    }
}
