# Configuration Guide - KoboldLair Provider Setup

## Overview

KoboldLair.Server uses a layered configuration approach that works seamlessly in both development and production environments.

## Configuration Strategy

### Base Configuration (appsettings.json)

The base `appsettings.json` contains:
- **Complete provider definitions** with all metadata
- **All providers disabled by default** (IsEnabled: false)
- **No sensitive data** (API keys stored in environment variables or removed)

### Environment-Specific Overrides

Environment-specific files only override what's needed:

**appsettings.Development.json**
- Enables specific providers for development
- Can override logging levels
- Uses minimal configuration (only Name and IsEnabled fields)

**appsettings.Production.json**
- Enables production-ready providers
- Sets appropriate logging levels
- Configures which agents use which providers

## How .NET Configuration Merging Works

.NET merges configurations in this order:
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (environment-specific)
3. Environment variables
4. Command-line arguments

**Important:** Arrays merge by index position, so environment files only specify changes.

## Provider Configuration Structure

### Full Provider Definition (appsettings.json)

```json
{
  "KoboldLairProviders": {
    "Providers": [
      {
        "Name": "githubcopilot",
        "DisplayName": "GitHub Copilot",
        "Type": "githubcopilot",
        "DefaultModel": "gpt-4o",
        "CompatibleAgents": ["dragon", "wyvern", "kobold"],
        "IsEnabled": false,
        "RequiresApiKey": true,
        "Description": "GitHub Copilot API",
        "Configuration": {
          "clientId": "your-client-id",
          "baseUrl": "https://api.githubcopilot.com/chat/completions"
        }
      }
    ],
    "AgentProviders": {
      "DragonProvider": "claudehaiku",
      "WyvernProvider": "claudehaiku",
      "KoboldProvider": "claudehaiku"
    }
  }
}
```

### Environment Override (appsettings.Development.json)

```json
{
  "KoboldLairProviders": {
    "Providers": [
      { "Name": "githubcopilot", "IsEnabled": true },
      { "Name": "claudehaiku", "IsEnabled": true }
    ]
  }
}
```

## API Key Management

### Best Practice: Use Environment Variables

API keys should be stored in environment variables, never in configuration files:

```bash
# Development (.env or shell)
export GITHUB_COPILOT_TOKEN="your-token-here"
export ANTHROPIC_API_KEY="your-key-here"

# Production (set in hosting environment)
GITHUB_COPILOT_TOKEN=your-token-here
ANTHROPIC_API_KEY=your-key-here
```

### Supported Environment Variables

| Provider Type | Environment Variable |
|--------------|---------------------|
| githubcopilot | GITHUB_COPILOT_TOKEN |
| claude | ANTHROPIC_API_KEY |
| openai | OPENAI_API_KEY |
| gemini | GOOGLE_API_KEY |
| azureopenai | AZURE_OPENAI_API_KEY |
| llamacpp | LLAMACPP_API_KEY |

### Fallback to Configuration

If an environment variable is not set, the service will check the Configuration dictionary in the provider definition. This is useful for development but not recommended for production.

## Agent Assignment

Each agent type can use a different provider:

```json
{
  "AgentProviders": {
    "DragonProvider": "githubcopilot",
    "WyvernProvider": "claudehaiku",
    "KoboldProvider": "githubcopilot-gpt-5-mini"
  }
}
```

## Validation and Logging

### Startup Logs

When the server starts, it logs the complete configuration:

```
========================================
Provider Configuration Loaded
========================================
Total Providers: 5
  - githubcopilot (GitHub Copilot): Enabled=True, Model=gpt-4o, Agents=dragon, wyvern, kobold
  - claudehaiku (Claude Haiku Direct): Enabled=True, Model=claude-3-haiku-20240307, Agents=dragon, wyvern, kobold
Agent Assignments:
  - Dragon: claudehaiku
  - Wyvern: claudehaiku
  - Kobold: claudehaiku
========================================
```

### Provider Validation

The service validates each provider on startup:
- Checks if provider is enabled
- Verifies API key availability (environment variable or config)
- Logs warnings for missing API keys

## Example Configurations

### Development Setup

Focus on using free or local providers:

```json
// appsettings.Development.json
{
  "KoboldLairProviders": {
    "Providers": [
      { "Name": "norainhosellamacpp", "IsEnabled": true },
      { "Name": "claudehaiku", "IsEnabled": true }
    ],
    "AgentProviders": {
      "DragonProvider": "claudehaiku",
      "WyvernProvider": "norainhosellamacpp",
      "KoboldProvider": "norainhosellamacpp"
    }
  }
}
```

### Production Setup

Use reliable cloud providers:

```json
// appsettings.Production.json
{
  "KoboldLairProviders": {
    "Providers": [
      { "Name": "githubcopilot", "IsEnabled": true },
      { "Name": "claudehaiku", "IsEnabled": true }
    ],
    "AgentProviders": {
      "DragonProvider": "githubcopilot",
      "WyvernProvider": "githubcopilot",
      "KoboldProvider": "githubcopilot"
    }
  }
}
```

## Troubleshooting

### Problem: Providers not loading

**Check:**
1. Verify environment variable is set correctly
2. Check startup logs for provider list
3. Ensure provider is enabled in environment-specific config
4. Validate provider name matches exactly (case-sensitive)

### Problem: API key errors

**Solutions:**
1. Set the correct environment variable
2. Check the environment variable name matches the provider type
3. For development, temporarily add apiKey to Configuration (not recommended for production)

### Problem: Wrong provider being used

**Check:**
1. Review the AgentProviders section in logs
2. Verify environment-specific overrides are applied
3. Ensure ASPNETCORE_ENVIRONMENT is set correctly

## Best Practices

1. **Keep base config complete** - All provider metadata in appsettings.json
2. **Disable by default** - Enable only what's needed per environment
3. **Never commit secrets** - Use environment variables for API keys
4. **Use minimal overrides** - Environment files should be small and focused
5. **Check startup logs** - Verify configuration loaded correctly
6. **Test provider validation** - Ensure ValidateProvider returns expected results

## Testing Configuration

```bash
# Check which providers are loaded
curl http://localhost:5000/

# View logs on startup
dotnet run --environment Development

# Set environment for testing
export ASPNETCORE_ENVIRONMENT=Production
dotnet run
```
