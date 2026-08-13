using System.Text.Json.Serialization;
using McpGateway.Infrastructure;
using McpGateway.Infrastructure.Persistence;
using McpGateway.WebApi.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpGatewayInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Development keeps the framework's developer exception page; elsewhere errors surface as ProblemDetails.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

// Apply pending migrations so `docker compose up` needs no manual step.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<McpGatewayDbContext>().Database.Migrate();
}

app.MapToolEndpoints();

app.Run();

/// <summary>Exposes the implicit Program class to WebApplicationFactory-based tests.</summary>
public partial class Program;
