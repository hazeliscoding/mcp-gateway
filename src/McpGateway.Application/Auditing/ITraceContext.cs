namespace McpGateway.Application.Auditing;

/// <summary>Supplies the correlation id (trace id) of the current operation.</summary>
public interface ITraceContext
{
    string CurrentTraceId { get; }
}
