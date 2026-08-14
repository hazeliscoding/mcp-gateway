using McpGateway.Domain.Auditing;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpGateway.Infrastructure.Persistence;

/// <summary>
/// Maps the append-only AuditEntry: value objects convert to strings and enums
/// persist as strings, mirroring the other configurations. Indexed on the columns
/// the trail is queried by.
/// </summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at");
        builder.Property(a => a.TraceId).HasColumnName("trace_id").HasMaxLength(64);
        builder.Property(a => a.EventType).HasColumnName("event_type").HasConversion<string>().HasMaxLength(32);

        builder.Property(a => a.ActorClientId)
            .HasConversion(id => id.Value, value => ClientId.Create(value))
            .HasColumnName("actor_client_id")
            .HasMaxLength(64);
        builder.Property(a => a.ActorType).HasColumnName("actor_type").HasConversion<string>().HasMaxLength(16);

        builder.Property(a => a.ToolName)
            .HasConversion(name => name!.Value, value => ToolName.Create(value))
            .HasColumnName("tool_name")
            .HasMaxLength(64);

        builder.Property(a => a.Version)
            .HasConversion(number => number!.ToString(), value => ToolVersionNumber.Create(value))
            .HasColumnName("version")
            .HasMaxLength(32);

        builder.Property(a => a.Result).HasColumnName("result").HasMaxLength(32);
        builder.Property(a => a.Detail).HasColumnName("detail").HasMaxLength(500);
        builder.Property(a => a.RequestHash).HasColumnName("request_hash").HasMaxLength(64);
        builder.Property(a => a.ApprovalId).HasColumnName("approval_id");

        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.ToolName);
        builder.HasIndex(a => a.ActorClientId);
    }
}
