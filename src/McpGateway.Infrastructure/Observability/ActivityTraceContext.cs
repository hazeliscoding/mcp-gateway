using System.Diagnostics;
using McpGateway.Application.Auditing;

namespace McpGateway.Infrastructure.Observability;

/// <summary>
/// Reads the current trace id from the ambient <see cref="Activity"/> that ASP.NET
/// Core starts per request. Falls back to a fresh id when no activity is running
/// (e.g. background work), so an audit entry always has a correlation id.
/// </summary>
public sealed class ActivityTraceContext : ITraceContext
{
    public string CurrentTraceId =>
        Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
}
