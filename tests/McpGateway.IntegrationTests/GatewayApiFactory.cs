using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace McpGateway.IntegrationTests;

/// <summary>Boots the real API pipeline against the shared Testcontainers Postgres instance.</summary>
public sealed class GatewayApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-signing-key-at-least-32-bytes!";
    public const string BootstrapClientId = "bootstrap_admin";
    public const string BootstrapSecret = "bootstrap-integration-secret";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:McpGateway", connectionString);
        builder.UseSetting("Auth:SigningKey", SigningKey);
        builder.UseSetting("Auth:Bootstrap:ClientId", BootstrapClientId);
        builder.UseSetting("Auth:Bootstrap:ClientSecret", BootstrapSecret);
    }

    /// <summary>Requests a token via the real OAuth endpoint and applies it as the client's bearer credential.</summary>
    public static async Task<string> AuthenticateAsync(
        HttpClient client, string clientId = BootstrapClientId, string clientSecret = BootstrapSecret)
    {
        var response = await client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("access_token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return token;
    }
}
