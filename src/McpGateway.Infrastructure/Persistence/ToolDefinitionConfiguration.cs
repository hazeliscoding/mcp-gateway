using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpGateway.Infrastructure.Persistence;

/// <summary>
/// Maps the ToolDefinition aggregate: value objects convert to strings,
/// versions are an owned collection, and schemas persist as jsonb so they stay
/// queryable in Postgres.
/// </summary>
public sealed class ToolDefinitionConfiguration : IEntityTypeConfiguration<ToolDefinition>
{
    public void Configure(EntityTypeBuilder<ToolDefinition> builder)
    {
        builder.ToTable("tools");

        builder.Property(t => t.Name)
            .HasConversion(name => name.Value, value => ToolName.Create(value))
            .HasColumnName("name")
            .HasMaxLength(64);
        builder.HasKey(t => t.Name);

        builder.Property(t => t.Enabled).HasColumnName("enabled");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.OwnsMany(t => t.Versions, version =>
        {
            version.ToTable("tool_versions");

            version.WithOwner().HasForeignKey("ToolName");
            version.Property<ToolName>("ToolName")
                .HasConversion(name => name.Value, value => ToolName.Create(value))
                .HasColumnName("tool_name")
                .HasMaxLength(64);

            version.Property(v => v.Number)
                .HasConversion(number => number.ToString(), value => ToolVersionNumber.Create(value))
                .HasColumnName("version")
                .HasMaxLength(32);
            version.HasKey("ToolName", "Number");

            version.Property(v => v.Description).HasColumnName("description").HasMaxLength(500);
            version.Property(v => v.RiskLevel).HasColumnName("risk_level").HasConversion<string>().HasMaxLength(16);
            version.Property(v => v.ApprovalRequired).HasColumnName("approval_required");
            version.Property(v => v.TimeoutSeconds).HasColumnName("timeout_seconds");
            version.Property(v => v.InputSchemaJson).HasColumnName("input_schema").HasColumnType("jsonb");
            version.Property(v => v.OutputSchemaJson).HasColumnName("output_schema").HasColumnType("jsonb");
            version.Property(v => v.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
            version.Property(v => v.RegisteredAt).HasColumnName("registered_at");

            // The public IReadOnlyList is a projection; the backing field is what persists.
            version.Ignore(v => v.RequiredScopes);
            version.Property<List<string>>("_requiredScopes").HasColumnName("required_scopes");
        });

        builder.Navigation(t => t.Versions)
            .HasField("_versions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
