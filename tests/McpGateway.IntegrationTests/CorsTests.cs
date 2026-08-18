using System.Net.Http.Headers;

namespace McpGateway.IntegrationTests;

/// <summary>Confirms the browser console origin is allowed through CORS preflight.</summary>
[Collection("postgres")]
public sealed class CorsTests : IAsyncLifetime
{
    private const string ConsoleOrigin = "http://localhost:4200";

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _client;

    public CorsTests(PostgresFixture postgres)
    {
        _factory = new GatewayApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Preflight_from_the_console_origin_is_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/tools");
        request.Headers.Add("Origin", ConsoleOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains(ConsoleOrigin, origins!);
    }
}
