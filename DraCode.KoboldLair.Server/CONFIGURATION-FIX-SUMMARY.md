# Provider Configuration Fix - Summary

## What Was Changed

Fixed the KoboldLair.Server provider configuration to work correctly in both development and production environments.

## Problem

The previous configuration had providers duplicated between `appsettings.json` and `appsettings.Development.json`, causing unexpected merging behavior due to how .NET handles array configuration merging. API keys were also hardcoded in configuration files, which is a security risk.

## Solution

### 1. Configuration Structure

**appsettings.json (Base)**
- Contains complete provider definitions with all metadata
- All providers are **disabled by default** (`IsEnabled: false`)
- No sensitive API keys stored (removed from configuration)
- Serves as the source of truth for provider metadata

**appsettings.Development.json (Development Overrides)**
- Only contains minimal overrides (`Name` and `IsEnabled`)
- Enables all development providers
- Increased logging verbosity

**appsettings.Production.json (Production Overrides)**
- Only contains minimal overrides (`Name` and `IsEnabled`)
- Enables only production-ready providers
- Reduced logging for performance
- Sets production agent provider assignments

### 2. API Key Management

**Environment Variables (Recommended)**
- API keys now loaded from environment variables
- Falls back to configuration only if environment variable not set
- Added validation warnings for missing API keys

**Supported Environment Variables:**
- `GITHUB_COPILOT_TOKEN` - GitHub Copilot API
- `ANTHROPIC_API_KEY` - Anthropic Claude API
- `OPENAI_API_KEY` - OpenAI API
- `GOOGLE_API_KEY` - Google Gemini API
- `AZURE_OPENAI_API_KEY` - Azure OpenAI
- `LLAMACPP_API_KEY` - Llama.cpp (if needed)

### 3. Service Improvements

**ProviderConfigurationService.cs**
- Improved API key lookup logic (checks environment first, then config)
- Better logging for debugging
- Uses provider `Type` field instead of `Name` for environment variable mapping
- Enhanced validation with better error messages

## Files Changed

1. **DraCode.KoboldLair.Server/appsettings.json**
   - Added complete provider definitions
   - Set all providers to `IsEnabled: false`
   - Removed hardcoded API keys

2. **DraCode.KoboldLair.Server/appsettings.Development.json**
   - Simplified to minimal overrides
   - Enables all development providers
   - Enhanced logging for debugging

3. **DraCode.KoboldLair.Server/appsettings.Production.json** (NEW)
   - Created production-specific configuration
   - Enables production providers
   - Optimized logging levels

4. **DraCode.KoboldLair.Server/Services/ProviderConfigurationService.cs**
   - Improved API key resolution
   - Better validation logic
   - Enhanced logging

5. **DraCode.KoboldLair.Server/CONFIGURATION-NOTES.md**
   - Complete rewrite with new approach
   - Added troubleshooting guide
   - Documented best practices

6. **DraCode.KoboldLair.Server/ENVIRONMENT-SETUP.md** (NEW)
   - Step-by-step environment setup guide
   - Platform-specific instructions (Windows/Linux/macOS)
   - Production deployment examples
   - Security best practices

## How to Use

### Development Setup

1. Set environment variables:
   ```powershell
   # Windows PowerShell
   $env:GITHUB_COPILOT_TOKEN = "your-token"
   $env:ANTHROPIC_API_KEY = "your-key"
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   ```

2. Run the server:
   ```bash
   dotnet run --project DraCode.KoboldLair.Server
   ```

3. Check startup logs for loaded providers

### Production Deployment

1. Set environment variables in your hosting platform
2. Ensure `ASPNETCORE_ENVIRONMENT=Production`
3. Verify only production providers are enabled

## Benefits

✅ **Security**: No API keys in source control  
✅ **Clarity**: Clear separation between base config and overrides  
✅ **Flexibility**: Easy to switch providers per environment  
✅ **Maintainability**: Single source of truth for provider metadata  
✅ **Debugging**: Enhanced logging shows exactly what's loaded  
✅ **Best Practices**: Follows .NET configuration patterns  

## Verification

After starting the server, you should see:

```
========================================
Provider Configuration Loaded
========================================
Total Providers: 5
  - githubcopilot (GitHub Copilot): Enabled=True, Model=gpt-4o
  - claudehaiku (Claude Haiku Direct): Enabled=True, Model=claude-3-haiku-20240307
  ...
Agent Assignments:
  - Dragon: claudehaiku
  - Wyvern: claudehaiku
  - Kobold: claudehaiku
========================================
```

## Next Steps

1. Remove any API keys from version control history (if previously committed)
2. Update deployment pipelines to set environment variables
3. Test in both development and production environments
4. Document any custom providers in CONFIGURATION-NOTES.md
