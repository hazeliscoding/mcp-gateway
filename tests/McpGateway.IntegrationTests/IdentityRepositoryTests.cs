using McpGateway.Domain.Identities;
using McpGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class IdentityRepositoryTests(PostgresFixture postgres) : IAsyncLifetime
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

    [Fact]
    public async Task Identity_round_trips_and_mutations_persist()
    {
        var clientId = ClientId.Create("repo_test_agent");
        var identity = GatewayIdentity.Register(
            clientId, IdentityType.Agent, "Repository Test Agent", "stored-hash",
            ["queue.read", "deploy.read"], DateTimeOffset.UtcNow);

        await using (var write = CreateContext())
        {
            var repository = new IdentityRepository(write);
            await repository.AddAsync(identity, CancellationToken.None);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using (var mutate = CreateContext())
        {
            var repository = new IdentityRepository(mutate);
            var tracked = await repository.GetByClientIdAsync(clientId, CancellationToken.None);
            tracked!.RotateSecret("rotated-hash");
            tracked.Disable();
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var read = CreateContext();
        var loaded = await new IdentityRepository(read).GetByClientIdAsync(clientId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(IdentityType.Agent, loaded.Type);
        Assert.Equal("rotated-hash", loaded.SecretHash);
        Assert.False(loaded.Enabled);
        Assert.Equal(["queue.read", "deploy.read"], loaded.GrantedScopes);
        Assert.True(await new IdentityRepository(read).AnyAsync(CancellationToken.None));
    }
}
