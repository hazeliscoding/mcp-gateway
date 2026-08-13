using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

/// <summary>Builders for valid domain inputs that individual tests mutate to trigger violations.</summary>
internal static class TestSpecs
{
    public static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    public static ToolVersionSpec Valid(
        string version = "1.0",
        string description = "Redrives messages from a dead-letter queue back to its source queue.",
        RiskLevel riskLevel = RiskLevel.Privileged,
        bool approvalRequired = true,
        IReadOnlyList<string>? scopes = null,
        int timeoutSeconds = 30,
        string inputSchema = """{"type":"object","properties":{"queueUrl":{"type":"string"}}}""",
        string outputSchema = """{"type":"object","properties":{"redrivenCount":{"type":"integer"}}}""") =>
        new(
            ToolVersionNumber.Create(version),
            description,
            riskLevel,
            approvalRequired,
            scopes ?? ["queue.read", "queue.redrive"],
            timeoutSeconds,
            inputSchema,
            outputSchema);

    public static ToolDefinition RegisteredTool(string name = "redrive_dead_letter_queue", string version = "1.0") =>
        ToolDefinition.Register(ToolName.Create(name), Valid(version), Now);

    public static ToolDefinition RegisteredToolWith(ToolVersionSpec spec) =>
        ToolDefinition.Register(ToolName.Create("redrive_dead_letter_queue"), spec, Now);
}
