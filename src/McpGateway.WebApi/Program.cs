using System.Text;
using System.Text.Json.Serialization;
using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;
using McpGateway.Infrastructure;
using McpGateway.Infrastructure.Persistence;
using McpGateway.Infrastructure.Security;
using McpGateway.WebApi.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpGatewayInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = authOptions.Issuer,
        ValidAudience = authOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Development keeps the framework's developer exception page; elsewhere errors surface as ProblemDetails.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseAuthentication();
app.UseAuthorization();

// Apply pending migrations and seed the bootstrap admin identity so a fresh
// `docker compose up` is immediately usable. The bootstrap secret comes from
// configuration and is meant for local development only.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<McpGatewayDbContext>();
    dbContext.Database.Migrate();

    var bootstrapClientId = app.Configuration["Auth:Bootstrap:ClientId"];
    var bootstrapSecret = app.Configuration["Auth:Bootstrap:ClientSecret"];
    if (!string.IsNullOrWhiteSpace(bootstrapClientId)
        && !string.IsNullOrWhiteSpace(bootstrapSecret)
        && !dbContext.Identities.Any())
    {
        var hasher = scope.ServiceProvider.GetRequiredService<ISecretHasher>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        dbContext.Identities.Add(GatewayIdentity.Register(
            ClientId.Create(bootstrapClientId), IdentityType.Service, "Bootstrap Administrator",
            hasher.Hash(bootstrapSecret), ["gateway.admin"], timeProvider.GetUtcNow()));
        dbContext.SaveChanges();
        app.Logger.LogWarning(
            "Seeded bootstrap admin identity {ClientId}; rotate its secret before any non-local use", bootstrapClientId);
    }
}

app.MapOAuthEndpoints();
app.MapIdentityEndpoints();
app.MapToolEndpoints();
app.MapAuthorizationEndpoints();

app.Run();

/// <summary>Exposes the implicit Program class to WebApplicationFactory-based tests.</summary>
public partial class Program;
