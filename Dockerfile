FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore with only the project files so dependency layers cache across code changes.
COPY nuget.config Directory.Build.props ./
COPY src/McpGateway.Domain/McpGateway.Domain.csproj src/McpGateway.Domain/
COPY src/McpGateway.Application/McpGateway.Application.csproj src/McpGateway.Application/
COPY src/McpGateway.Infrastructure/McpGateway.Infrastructure.csproj src/McpGateway.Infrastructure/
COPY src/McpGateway.WebApi/McpGateway.WebApi.csproj src/McpGateway.WebApi/
RUN dotnet restore src/McpGateway.WebApi

COPY src/ src/
RUN dotnet publish src/McpGateway.WebApi -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "McpGateway.WebApi.dll"]
