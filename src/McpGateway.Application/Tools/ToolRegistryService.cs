using System.Text.Json;
using McpGateway.Domain;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Tools;

/// <summary>
/// Commands and queries for the tool registry. All domain exceptions are
/// converted to <see cref="OperationResult{T}"/> here so the API layer only
/// maps outcomes to HTTP statuses.
/// </summary>
public sealed class ToolRegistryService(
    IToolRegistryRepository repository,
    TimeProvider timeProvider,
    ILogger<ToolRegistryService> logger)
{
    public async Task<OperationResult<ToolDetailResponse>> RegisterToolAsync(
        RegisterToolRequest request, CancellationToken cancellationToken)
    {
        ToolDefinition tool;
        try
        {
            var name = ToolName.Create(request.Name);
            if (await repository.GetByNameAsync(name, cancellationToken) is not null)
            {
                return OperationResult<ToolDetailResponse>.Conflict($"Tool '{name}' is already registered.");
            }

            tool = ToolDefinition.Register(
                name,
                ToSpec(request.Version, request.Description, request.RiskLevel, request.ApprovalRequired,
                    request.RequiredScopes, request.TimeoutSeconds, request.InputSchema, request.OutputSchema),
                timeProvider.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return OperationResult<ToolDetailResponse>.Invalid(ex.Message);
        }

        await repository.AddAsync(tool, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered tool {ToolName} at version {ToolVersion}",
            tool.Name.Value, tool.LatestVersion.Number.ToString());
        return OperationResult<ToolDetailResponse>.Success(ToDetail(tool));
    }

    public async Task<OperationResult<ToolDetailResponse>> RegisterVersionAsync(
        string name, RegisterVersionRequest request, CancellationToken cancellationToken)
    {
        var (tool, failure) = await FindToolAsync<ToolDetailResponse>(name, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            tool!.AddVersion(
                ToSpec(request.Version, request.Description, request.RiskLevel, request.ApprovalRequired,
                    request.RequiredScopes, request.TimeoutSeconds, request.InputSchema, request.OutputSchema),
                timeProvider.GetUtcNow());
        }
        catch (DomainConflictException ex)
        {
            return OperationResult<ToolDetailResponse>.Conflict(ex.Message);
        }
        catch (DomainException ex)
        {
            return OperationResult<ToolDetailResponse>.Invalid(ex.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered tool {ToolName} version {ToolVersion}",
            tool!.Name.Value, tool.LatestVersion.Number.ToString());
        return OperationResult<ToolDetailResponse>.Success(ToDetail(tool));
    }

    public async Task<OperationResult<bool>> SetEnabledAsync(
        string name, bool enabled, CancellationToken cancellationToken)
    {
        var (tool, failure) = await FindToolAsync<bool>(name, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (enabled)
        {
            tool!.Enable();
        }
        else
        {
            tool!.Disable();
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Tool {ToolName} {ToolState}", tool.Name.Value, enabled ? "enabled" : "disabled");
        return OperationResult<bool>.Success(enabled);
    }

    public async Task<OperationResult<bool>> DeprecateVersionAsync(
        string name, string version, CancellationToken cancellationToken)
    {
        var (tool, failure) = await FindToolAsync<bool>(name, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        ToolVersionNumber number;
        try
        {
            number = ToolVersionNumber.Create(version);
        }
        catch (DomainException ex)
        {
            return OperationResult<bool>.Invalid(ex.Message);
        }

        if (tool!.Versions.All(v => v.Number != number))
        {
            return OperationResult<bool>.NotFound($"Tool '{tool.Name}' has no version {number}.");
        }

        tool.DeprecateVersion(number);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deprecated tool {ToolName} version {ToolVersion}", tool.Name.Value, number.ToString());
        return OperationResult<bool>.Success(true);
    }

    public async Task<OperationResult<ToolDetailResponse>> GetToolAsync(
        string name, CancellationToken cancellationToken)
    {
        var (tool, failure) = await FindToolAsync<ToolDetailResponse>(name, cancellationToken);
        return failure ?? OperationResult<ToolDetailResponse>.Success(ToDetail(tool!));
    }

    public async Task<OperationResult<IReadOnlyList<ToolSummaryResponse>>> ListToolsAsync(
        ToolDiscoveryFilter filter, CancellationToken cancellationToken)
    {
        var tools = await repository.ListAsync(cancellationToken);

        IReadOnlyList<ToolSummaryResponse> summaries = tools
            .Where(t => filter.IncludeDisabled || t.Enabled)
            .Where(t => filter.RiskLevel is null || t.LatestVersion.RiskLevel == filter.RiskLevel)
            .Where(t => filter.NameContains is null
                || t.Name.Value.Contains(filter.NameContains, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name.Value)
            .Select(ToSummary)
            .ToList();

        return OperationResult<IReadOnlyList<ToolSummaryResponse>>.Success(summaries);
    }

    private async Task<(ToolDefinition? Tool, OperationResult<T>? Failure)> FindToolAsync<T>(
        string name, CancellationToken cancellationToken)
    {
        ToolName toolName;
        try
        {
            toolName = ToolName.Create(name);
        }
        catch (DomainException ex)
        {
            return (null, OperationResult<T>.Invalid(ex.Message));
        }

        var tool = await repository.GetByNameAsync(toolName, cancellationToken);
        return tool is null
            ? (null, OperationResult<T>.NotFound($"Tool '{toolName}' is not registered."))
            : (tool, null);
    }

    private static ToolVersionSpec ToSpec(
        string version, string description, RiskLevel riskLevel, bool approvalRequired,
        IReadOnlyList<string> scopes, int timeoutSeconds, JsonElement inputSchema, JsonElement outputSchema) =>
        new(
            ToolVersionNumber.Create(version),
            description,
            riskLevel,
            approvalRequired,
            scopes,
            timeoutSeconds,
            inputSchema.GetRawText(),
            outputSchema.GetRawText());

    private static ToolDetailResponse ToDetail(ToolDefinition tool) =>
        new(
            tool.Name.Value,
            tool.Enabled,
            tool.CreatedAt,
            tool.Versions.Select(v => new ToolVersionResponse(
                v.Number.ToString(),
                v.Description,
                v.RiskLevel,
                v.ApprovalRequired,
                v.RequiredScopes,
                v.TimeoutSeconds,
                JsonSerializer.Deserialize<JsonElement>(v.InputSchemaJson),
                JsonSerializer.Deserialize<JsonElement>(v.OutputSchemaJson),
                v.Status,
                v.RegisteredAt)).ToList());

    private static ToolSummaryResponse ToSummary(ToolDefinition tool) =>
        new(
            tool.Name.Value,
            tool.Enabled,
            tool.LatestVersion.Number.ToString(),
            tool.LatestVersion.Description,
            tool.LatestVersion.RiskLevel,
            tool.LatestVersion.ApprovalRequired,
            tool.CreatedAt);
}
