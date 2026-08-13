using McpGateway.Domain.Identities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace McpGateway.Infrastructure.Persistence;

public sealed class GatewayIdentityConfiguration : IEntityTypeConfiguration<GatewayIdentity>
{
    public void Configure(EntityTypeBuilder<GatewayIdentity> builder)
    {
        builder.ToTable("identities");

        builder.Property(i => i.ClientId)
            .HasConversion(id => id.Value, value => ClientId.Create(value))
            .HasColumnName("client_id")
            .HasMaxLength(64);
        builder.HasKey(i => i.ClientId);

        builder.Property(i => i.Type).HasColumnName("identity_type").HasConversion<string>().HasMaxLength(16);
        builder.Property(i => i.DisplayName).HasColumnName("display_name").HasMaxLength(100);
        builder.Property(i => i.SecretHash).HasColumnName("secret_hash").HasMaxLength(256);
        builder.Property(i => i.Enabled).HasColumnName("enabled");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");

        // The public IReadOnlyList is a projection; the backing field is what persists.
        builder.Ignore(i => i.GrantedScopes);
        builder.Property<List<string>>("_grantedScopes").HasColumnName("granted_scopes");
    }
}
