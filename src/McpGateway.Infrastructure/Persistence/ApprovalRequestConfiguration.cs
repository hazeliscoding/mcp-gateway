using McpGateway.Domain.Approvals;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpGateway.Infrastructure.Persistence;

/// <summary>
/// Maps the ApprovalRequest aggregate: the Guid id is the key, value objects
/// convert to strings, and enums persist as strings, mirroring the other
/// aggregate configurations.
/// </summary>
public sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("approval_requests");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ToolName)
            .HasConversion(name => name.Value, value => ToolName.Create(value))
            .HasColumnName("tool_name")
            .HasMaxLength(64);

        builder.Property(a => a.Version)
            .HasConversion(number => number.ToString(), value => ToolVersionNumber.Create(value))
            .HasColumnName("version")
            .HasMaxLength(32);

        builder.Property(a => a.RequesterClientId)
            .HasConversion(id => id.Value, value => ClientId.Create(value))
            .HasColumnName("requester_client_id")
            .HasMaxLength(64);

        builder.Property(a => a.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Action).HasColumnName("action").HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Environment).HasColumnName("environment").HasMaxLength(64);
        builder.Property(a => a.Resource).HasColumnName("resource").HasMaxLength(256);
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.RequestedAt).HasColumnName("requested_at");
        builder.Property(a => a.DecidedAt).HasColumnName("decided_at");

        builder.Property(a => a.DecidedBy)
            .HasConversion(id => id!.Value, value => ClientId.Create(value))
            .HasColumnName("decided_by")
            .HasMaxLength(64);

        builder.Property(a => a.DecisionNote).HasColumnName("decision_note").HasMaxLength(500);

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => new { a.RequesterClientId, a.ToolName, a.Version, a.Status });
    }
}
