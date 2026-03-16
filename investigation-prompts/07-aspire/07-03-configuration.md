# Investigation: Configuration Management

**Status**: Pending
**Priority**: High
**Category**: Aspire

---

## Microsoft Documentation

### Primary References
- [Configuration in .NET](https://learn.microsoft.com/dotnet/core/extensions/configuration)
- [Options Pattern](https://learn.microsoft.com/dotnet/core/extensions/options)
- [Azure Key Vault Configuration Provider](https://learn.microsoft.com/azure/key-vault/general/tutorial-net-virtual-machine)
- [Aspire Configuration](https://learn.microsoft.com/dotnet/aspire/fundamentals/configuration)

### Related Types
- `IConfiguration` - Configuration root
- `IOptions<T>` - Options accessor
- `IOptionsMonitor<T>` - Updatable options
- `IOptionsSnapshot<T>` - Scoped options
- `ConfigureOptions<T>` - Options configuration

---

## Investigation Questions

### 1. Provider Settings Configuration
- How to configure provider settings (API keys, endpoints)?
- What is the best structure for provider configuration classes?
- How to support multiple providers simultaneously?

### 2. .NET Configuration Sources
- How to use .NET configuration (appsettings.json, environment variables)?
- What is the configuration source priority order?
- How to configure per-environment settings?

### 3. Azure Key Vault Integration
- How to integrate with Azure Key Vault for secrets?
- How to use Aspire's Azure integrations for Key Vault?
- How to handle local development without Key Vault?

### 4. Options Pattern Implementation
- How to implement options pattern for strongly-typed configuration?
- How to add configuration validation?
- How to handle configuration reload?

---

## Code Patterns

### Options Classes (Expected .NET)
```csharp
// Configuration/AgentOptions.cs
public class AgentOptions
{
    public const string SectionName = "Agent";

    public string DefaultProvider { get; set; } = "openai";
    public int MaxConversationLength { get; set; } = 100;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

// Configuration/ProviderOptions.cs
public class ProviderOptions
{
    public OpenAIOptions OpenAI { get; set; } = new();
    public AnthropicOptions Anthropic { get; set; } = new();
    public AzureOpenAIOptions AzureOpenAI { get; set; } = new();
}

public class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";
    public string? Organization { get; set; }
}

public class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-sonnet-20240229";
    public int MaxTokens { get; set; } = 4096;
}

public class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Deployment { get; set; } = string.Empty;
}
```

### Options Registration (Expected .NET)
```csharp
// Extensions/OptionsExtensions.cs
public static class OptionsExtensions
{
    public static IServiceCollection AddAgentOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options from configuration
        services.Configure<AgentOptions>(
            configuration.GetSection(AgentOptions.SectionName));

        services.Configure<ProviderOptions>(
            configuration.GetSection("Providers"));

        // Add validation
        services.AddOptions<OpenAIOptions>()
            .Bind(configuration.GetSection("Providers:OpenAI"))
            .Validate(options => !string.IsNullOrEmpty(options.ApiKey))
            .ValidateOnStart();

        return services;
    }
}
```

### Configuration Validation (Expected .NET)
```csharp
// Configuration/ValidatedOptions.cs
public class OpenAIOptions : IValidatableObject
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4";

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            yield return new ValidationResult(
                "ApiKey is required",
                new[] { nameof(ApiKey) });
        }

        if (!ValidModels.Contains(Model))
        {
            yield return new ValidationResult(
                $"Invalid model: {Model}",
                new[] { nameof(Model) });
        }
    }
}

// Registration with validation
services.AddOptions<OpenAIOptions>()
    .Bind(configuration.GetSection("Providers:OpenAI"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

### Azure Key Vault Integration (Expected .NET)
```csharp
// Program.cs - Aspire integration
var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault via Aspire
if (!builder.Environment.IsDevelopment())
{
    builder.AddAzureKeyVault("kv-agent-secrets");
}

// Configuration setup
builder.Services.AddAgentOptions(builder.Configuration);
```

### Using Options in Services (Expected .NET)
```csharp
// Services/AgentService.cs
public class AgentService : IAgentService
{
    private readonly AgentOptions _agentOptions;
    private readonly ProviderOptions _providerOptions;
    private readonly IChatClient _chatClient;

    public AgentService(
        IOptions<AgentOptions> agentOptions,
        IOptions<ProviderOptions> providerOptions,
        IChatClient chatClient)
    {
        _agentOptions = agentOptions.Value;
        _providerOptions = providerOptions.Value;
        _chatClient = chatClient;
    }

    public async Task<AgentResult> ExecuteAsync(AgentRequest request)
    {
        // Use strongly-typed configuration
        if (request.Messages.Count > _agentOptions.MaxConversationLength)
        {
            throw new InvalidOperationException("Conversation too long");
        }

        // Access provider settings
        var model = _providerOptions.OpenAI.Model;
        // ...
    }
}
```

### appsettings.json Structure (Expected)
```json
{
  "Agent": {
    "DefaultProvider": "openai",
    "MaxConversationLength": 100,
    "RequestTimeout": "00:05:00"
  },
  "Providers": {
    "OpenAI": {
      "Model": "gpt-4",
      "Organization": null
    },
    "Anthropic": {
      "Model": "claude-3-sonnet-20240229",
      "MaxTokens": 4096
    },
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "Deployment": "gpt-4"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Environment Variables Pattern
```bash
# Provider configuration via environment variables
Providers__OpenAI__ApiKey=sk-xxx
Providers__Anthropic__ApiKey=sk-ant-xxx
Providers__AzureOpenAI__ApiKey=xxx

# Agent configuration
Agent__DefaultProvider=openai
Agent__MaxConversationLength=200
```

---

## Configuration Source Priority

| Priority | Source | Use Case |
|----------|--------|----------|
| 1 (Highest) | Command-line args | Override for debugging |
| 2 | Environment variables | Container/Cloud deployment |
| 3 | Azure Key Vault | Production secrets |
| 4 | User secrets | Local development |
| 5 | appsettings.{Environment}.json | Environment-specific |
| 6 | appsettings.json | Default configuration |

---

## Expected Deliverables

1. **Options Classes**
   - `AgentOptions` for agent configuration
   - `ProviderOptions` for AI provider settings
   - Per-provider options classes

2. **Options Registration Extensions**
   - `AddAgentOptions()` extension method
   - Configuration validation setup
   - Key Vault integration

3. **Configuration Files**
   - appsettings.json template
   - appsettings.Development.json example
   - Environment variable documentation

---

## Notes

- Never store API keys in appsettings.json - use Key Vault or environment variables
- Use `IOptionsSnapshot<T>` for configuration that may change during runtime
- Consider using `IOptionsMonitor<T>` for change detection in long-running services
- Aspire provides built-in integration for Azure Key Vault and other Azure services
- Use placeholder values in appsettings.json and document required configuration
