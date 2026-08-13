using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Tools;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class ToolApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _client;

    public ToolApiTests(PostgresFixture postgres)
    {
        _factory = new GatewayApiFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => GatewayApiFactory.AuthenticateAsync(_client);

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private static object RegisterBody(string name, string version = "1.0", string riskLevel = "ReadOnly") => new
    {
        name,
        version,
        description = "Returns current queue depth and consumer lag.",
        riskLevel,
        approvalRequired = false,
        requiredScopes = new[] { "queue.read" },
        timeoutSeconds = 10,
        inputSchema = new { type = "object" },
        outputSchema = new { type = "object" },
    };

    private static object VersionBody(string version) => new
    {
        version,
        description = "Adds message age percentiles.",
        riskLevel = "ReadOnly",
        approvalRequired = false,
        requiredScopes = new[] { "queue.read" },
        timeoutSeconds = 15,
        inputSchema = new { type = "object" },
        outputSchema = new { type = "object" },
    };

    [Fact]
    public async Task Register_returns_201_with_location_and_detail()
    {
        var response = await _client.PostAsJsonAsync("/api/tools", RegisterBody("api_register_tool"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/tools/api_register_tool", response.Headers.Location!.ToString());
        var detail = await response.Content.ReadFromJsonAsync<ToolDetailResponse>(Json);
        Assert.Equal("1.0.0", Assert.Single(detail!.Versions).Version);
    }

    [Fact]
    public async Task Register_duplicate_returns_409()
    {
        await _client.PostAsJsonAsync("/api/tools", RegisterBody("api_duplicate_tool"));

        var response = await _client.PostAsJsonAsync("/api/tools", RegisterBody("api_duplicate_tool"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("Not_Snake_Case")]
    [InlineData("ab")]
    public async Task Register_invalid_name_returns_400(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tools", RegisterBody(name));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_without_schemas_returns_400()
    {
        var body = new
        {
            name = "api_missing_schema_tool",
            version = "1.0",
            description = "Tool registered without schemas.",
            riskLevel = "ReadOnly",
            approvalRequired = false,
            requiredScopes = new[] { "queue.read" },
            timeoutSeconds = 10,
        };

        var response = await _client.PostAsJsonAsync("/api/tools", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Version_lifecycle_register_deprecate_disable_and_discover()
    {
        var name = "api_lifecycle_tool";
        await _client.PostAsJsonAsync("/api/tools", RegisterBody(name));

        // Higher version registers; duplicate of it conflicts.
        var addVersion = await _client.PostAsJsonAsync($"/api/tools/{name}/versions", VersionBody("1.1"));
        var duplicate = await _client.PostAsJsonAsync($"/api/tools/{name}/versions", VersionBody("1.1"));
        Assert.Equal(HttpStatusCode.Created, addVersion.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // Deprecate the original version.
        var deprecate = await _client.PostAsync($"/api/tools/{name}/versions/1.0/deprecate", null);
        Assert.Equal(HttpStatusCode.NoContent, deprecate.StatusCode);

        var detail = await _client.GetFromJsonAsync<ToolDetailResponse>($"/api/tools/{name}", Json);
        Assert.Equal(
            ["Deprecated", "Active"],
            detail!.Versions.OrderBy(v => v.Version).Select(v => v.Status.ToString()).ToArray());

        // Disable: gone from default discovery, still visible with includeDisabled.
        var disable = await _client.PostAsync($"/api/tools/{name}/disable", null);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        var visible = await _client.GetFromJsonAsync<List<ToolSummaryResponse>>("/api/tools", Json);
        var all = await _client.GetFromJsonAsync<List<ToolSummaryResponse>>("/api/tools?includeDisabled=true", Json);
        Assert.DoesNotContain(visible!, t => t.Name == name);
        Assert.Contains(all!, t => t.Name == name && !t.Enabled);
    }

    [Fact]
    public async Task Discovery_filters_by_risk_level()
    {
        await _client.PostAsJsonAsync("/api/tools", RegisterBody("api_readonly_tool", riskLevel: "ReadOnly"));
        await _client.PostAsJsonAsync("/api/tools", RegisterBody("api_privileged_tool", riskLevel: "Privileged"));

        var raw = await _client.GetAsync("/api/tools?riskLevel=Privileged&nameContains=api_");
        Assert.True(raw.IsSuccessStatusCode, await raw.Content.ReadAsStringAsync());
        var privileged = await raw.Content.ReadFromJsonAsync<List<ToolSummaryResponse>>(Json);

        Assert.Equal(["api_privileged_tool"], privileged!.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task Unknown_tool_returns_404()
    {
        var get = await _client.GetAsync("/api/tools/api_absent_tool");
        var enable = await _client.PostAsync("/api/tools/api_absent_tool/enable", null);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, enable.StatusCode);
    }
}
