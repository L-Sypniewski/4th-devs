# Investigation: Service Hosting

**Status**: Pending
**Priority**: High
**Category**: Aspire

---

## Microsoft Documentation

### Primary References
- [.NET Aspire Overview](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire AppHost](https://learn.microsoft.com/dotnet/aspire/fundamentals/app-host-overview)
- [Aspire ServiceDefaults](https://learn.microsoft.com/dotnet/aspire/fundamentals/service-defaults)
- [Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard)

### Related Types
- `DistributedApplication` - AppHost builder
- `IResourceBuilder<T>` - Resource configuration
- `ProjectResource` - Project resource type
- `ContainerResource` - Container resource type

---

## Investigation Questions

### 1. Aspire Solution Structure
- How to structure an Aspire solution (AppHost, ServiceDefaults)?
- What is the relationship between AppHost and ServiceDefaults projects?
- How to organize multiple agent services in an Aspire solution?

### 2. Service Hosting
- How to host the agent service as an Aspire project?
- How to configure project references and dependencies?
- How to set up service-to-service communication?

### 3. Health Checks and Service Discovery
- How to configure health checks for agent services?
- How to implement service discovery between components?
- How to expose health endpoints for monitoring?

### 4. Aspire Dashboard Integration
- How to integrate with Aspire dashboard for observability?
- How to view logs, traces, and metrics in the dashboard?
- How to configure resource visualization?

---

## Code Patterns

### AppHost Configuration (Expected .NET)
```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Add agent service as a project
var agentService = builder.AddProject<Projects.AgentService>("agent-service")
    .WithExternalHttpEndpoints();

// Add dependencies (e.g., Redis, PostgreSQL)
var cache = builder.AddRedis("cache");
var db = builder.AddPostgres("db").AddDatabase("agentdb");

// Configure service with dependencies
agentService
    .WithReference(cache)
    .WithReference(db);

builder.Build().Run();
```

### ServiceDefaults Setup (Expected .NET)
```csharp
// ServiceDefaults/Extensions.cs
public static IHostApplicationBuilder AddServiceDefaults(
    this IHostApplicationBuilder builder)
{
    // Add OpenTelemetry
    builder.AddOpenTelemetry();

    // Add default health checks
    builder.AddBasicHealthChecks();

    // Add service discovery
    builder.AddServiceDiscovery();

    // Configure HttpClient defaults
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddServiceDiscovery();
    });

    return builder;
}
```

### Health Check Configuration (Expected .NET)
```csharp
// AgentService/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add custom health checks
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agent-health");

// Map health endpoints
var app = builder.Build();
app.MapHealthChecks("/health");
app.MapDefaultEndpoints();

app.Run();
```

### Service Discovery Usage (Expected .NET)
```csharp
// Using service discovery for HTTP clients
builder.Services.AddHttpClient<IAgentClient, AgentClient>(
    client => client.BaseAddress = new Uri("https+http://agent-service"));
```

---

## Aspire Project Structure

```
solution/
├── AppHost/                    # Orchestration project
│   ├── Program.cs
│   ├── appsettings.json
│   └── AppHost.csproj
├── ServiceDefaults/            # Shared service configuration
│   ├── Extensions.cs
│   └── ServiceDefaults.csproj
├── AgentService/               # Agent service project
│   ├── Program.cs
│   ├── AgentService.csproj
│   └── ...
└── Directory.Packages.props    # Central package management
```

---

## Expected Deliverables

1. **AppHost Project**
   - Project configuration with proper references
   - Resource definitions for agent services
   - Dependency configuration (databases, caches)

2. **ServiceDefaults Project**
   - OpenTelemetry configuration
   - Health check setup
   - Service discovery configuration
   - Common middleware pipeline

3. **Agent Service Integration**
   - Service registration with Aspire
   - Health endpoint exposure
   - Dashboard-compatible telemetry

---

## Notes

- Aspire provides built-in container orchestration for local development
- ServiceDefaults should contain shared configuration for all services
- Consider using Aspire's integration packages for external services
- Dashboard provides real-time observability without additional configuration
