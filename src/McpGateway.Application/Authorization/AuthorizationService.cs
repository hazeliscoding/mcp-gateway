using McpGateway.Application.Tools;
using McpGateway.Domain;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Authorization;

/// <summary>
/// Orchestrates an authorization decision: loads the target tool, resolves the
/// requested version, and hands the assembled attributes to the deterministic
/// <see cref="AuthorizationPolicy"/>. It maps decisions to responses but makes
/// none of the permit/deny rules itself.
/// </summary>
public sealed class AuthorizationService(
    IToolRegistryRepository repository,
    ILogger<AuthorizationService> logger)
{
    private const string DefaultEnvironment = "production";

    public async Task<OperationResult<AuthorizationDecisionResponse>> AuthorizeToolAsync(
        string toolName,
        AuthorizeToolRequest request,
        CallerPrincipal caller,
        CancellationToken cancellationToken)
    {
        ToolName name;
        ToolVersionNumber? requestedVersion;
        try
        {
            name = ToolName.Create(toolName);
            requestedVersion = string.IsNullOrWhiteSpace(request.Version)
                ? null
                : ToolVersionNumber.Create(request.Version);
        }
        catch (DomainException ex)
        {
            return OperationResult<AuthorizationDecisionResponse>.Invalid(ex.Message);
        }

        var tool = await repository.GetByNameAsync(name, cancellationToken);
        if (tool is null)
        {
            // The tool genuinely does not exist — distinct from "exists but denied".
            return OperationResult<AuthorizationDecisionResponse>.NotFound($"Tool '{name}' is not registered.");
        }

        var version = ResolveVersion(tool, requestedVersion);

        var decision = AuthorizationPolicy.Evaluate(new AuthorizationRequest(
            caller.ClientId,
            caller.Type,
            caller.GrantedScopes,
            tool.Name,
            tool.Enabled,
            version,
            request.Action,
            request.Environment?.Trim() is { Length: > 0 } env ? env : DefaultEnvironment,
            request.Resource));

        var reportedVersion = version?.Number.ToString() ?? requestedVersion?.ToString();
        logger.LogInformation(
            "Authorization {Outcome} for {IdentityType} {ClientId} on tool {ToolName} version {Version} action {Action}: {ReasonCodes}",
            decision.Permit ? "permitted" : "denied",
            caller.Type, caller.ClientId.Value, tool.Name.Value, reportedVersion ?? "unresolved",
            request.Action, string.Join(",", decision.Reasons.Select(r => r.Code)));

        return OperationResult<AuthorizationDecisionResponse>.Success(new AuthorizationDecisionResponse(
            decision.Permit,
            tool.Name.Value,
            reportedVersion,
            request.Action,
            decision.Reasons.Select(r => new ReasonResponse(r.Code, r.Message)).ToList()));
    }

    /// <summary>
    /// Picks the version the decision concerns: the exact requested version if
    /// given, otherwise the highest active (non-deprecated) version. Returns
    /// <see langword="null"/> when nothing matches, which the policy denies.
    /// </summary>
    private static ToolVersion? ResolveVersion(ToolDefinition tool, ToolVersionNumber? requestedVersion) =>
        requestedVersion is null
            ? tool.Versions.LastOrDefault(v => v.Status == ToolVersionStatus.Active)
            : tool.Versions.FirstOrDefault(v => v.Number == requestedVersion);
}
