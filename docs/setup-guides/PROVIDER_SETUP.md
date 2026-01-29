# LLM Provider Setup Guide

This guide covers setup for all supported LLM providers in DraCode.

---

## Quick Reference

| Provider | Type | Env Variable | Default Model |
|----------|------|--------------|---------------|
| OpenAI | `openai` | `OPENAI_API_KEY` | `gpt-4o` |
| Claude | `claude` | `ANTHROPIC_API_KEY` | `claude-3-5-sonnet-latest` |
| Gemini | `gemini` | `GOOGLE_API_KEY` or `GEMINI_API_KEY` | `gemini-2.0-flash-exp` |
| Azure OpenAI | `azureopenai` | `AZURE_OPENAI_API_KEY` + `AZURE_OPENAI_ENDPOINT` | Deployment name |
| Ollama | `ollama` | None (local) | `llama3.2` |
| llama.cpp | `llamacpp` | None (local) | Custom |
| GitHub Copilot | `githubcopilot` | `GITHUB_COPILOT_TOKEN` | `gpt-4o` |

---

## OpenAI

### Setup
1. Get API key from https://platform.openai.com/api-keys
2. Set environment variable:
   ```bash
   export OPENAI_API_KEY="sk-..."
   ```

### Configuration
```json
{
  "openai": {
    "type": "openai",
    "apiKey": "${OPENAI_API_KEY}",
    "model": "gpt-4o",
    "baseUrl": "https://api.openai.com/v1/chat/completions"
  }
}
```

### Available Models
- `gpt-4o` - Latest multimodal (recommended)
- `gpt-4-turbo` - Fast and capable
- `gpt-4` - Original GPT-4
- `gpt-3.5-turbo` - Cost-effective

---

## Claude (Anthropic)

### Setup
1. Get API key from https://console.anthropic.com/
2. Set environment variable:
   ```bash
   export ANTHROPIC_API_KEY="sk-ant-..."
   ```

### Configuration
```json
{
  "claude": {
    "type": "claude",
    "apiKey": "${ANTHROPIC_API_KEY}",
    "model": "claude-3-5-sonnet-latest",
    "baseUrl": "https://api.anthropic.com/v1/messages"
  }
}
```

### Available Models
- `claude-3-5-sonnet-latest` - Most intelligent (recommended)
- `claude-3-5-haiku-latest` - Fast and efficient
- `claude-3-opus-latest` - Highest capability

### Resources
- Console: https://console.anthropic.com/
- Docs: https://docs.anthropic.com/
- Status: https://status.anthropic.com/

---

## Google Gemini

### Setup
1. Get API key from https://makersuite.google.com/app/apikey
2. Set environment variable:
   ```bash
   export GEMINI_API_KEY="AIza..."
   ```

### Configuration
```json
{
  "gemini": {
    "type": "gemini",
    "apiKey": "${GEMINI_API_KEY}",
    "model": "gemini-2.0-flash-exp",
    "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
  }
}
```

### Available Models
- `gemini-2.0-flash-exp` - Latest (free during preview)
- `gemini-1.5-pro-latest` - Complex reasoning, 2M context
- `gemini-1.5-flash-latest` - Fast and efficient, 1M context
- `gemini-1.5-flash-8b-latest` - Cost-optimized

### Free Tier Limits
- 15 requests per minute
- 1 million tokens per minute
- 1,500 requests per day

### Resources
- AI Studio: https://makersuite.google.com/
- Docs: https://ai.google.dev/docs
- Pricing: https://ai.google.dev/pricing

---

## Azure OpenAI

### Setup
1. Request access at https://aka.ms/oai/access (requires approval)
2. Create Azure OpenAI resource in Azure Portal
3. Deploy a model in Azure OpenAI Studio
4. Set environment variables:
   ```bash
   export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
   export AZURE_OPENAI_API_KEY="your-api-key"
   ```

### Configuration
```json
{
  "azureopenai": {
    "type": "azureopenai",
    "endpoint": "${AZURE_OPENAI_ENDPOINT}",
    "apiKey": "${AZURE_OPENAI_API_KEY}",
    "deployment": "your-deployment-name"
  }
}
```

**Important:** Use your **deployment name**, not the model name!

### Available Models
Same as OpenAI: `gpt-4o`, `gpt-4-turbo`, `gpt-4`, `gpt-35-turbo`

### Resources
- Portal: https://portal.azure.com/
- Studio: https://oai.azure.com/
- Docs: https://learn.microsoft.com/azure/ai-services/openai/

---

## Ollama (Local)

### Setup
1. Install Ollama:
   - **Windows**: https://ollama.com/download/windows
   - **macOS**: `brew install ollama`
   - **Linux**: `curl -fsSL https://ollama.com/install.sh | sh`

2. Download a model:
   ```bash
   ollama pull llama3.2
   ```

3. Start Ollama (usually auto-starts)

### Configuration
```json
{
  "ollama": {
    "type": "ollama",
    "model": "llama3.2",
    "baseUrl": "http://localhost:11434"
  }
}
```

### Recommended Models
| Model | Size | RAM | Best For |
|-------|------|-----|----------|
| `llama3.2` | 3B | 4 GB | Fast iteration |
| `codellama` | 7B | 8 GB | Code tasks |
| `mistral` | 7B | 8 GB | General use |
| `deepseek-coder` | 6.7B | 8 GB | Code-specialized |

### Commands
```bash
ollama list          # List downloaded models
ollama pull <model>  # Download model
ollama run <model>   # Interactive chat
ollama serve         # Start server
```

### Benefits
- 100% private - no data sent externally
- Free - no API costs
- Works offline
- Fast local inference

### Resources
- Website: https://ollama.com/
- Models: https://ollama.com/library
- GitHub: https://github.com/ollama/ollama

---

## GitHub Copilot

### Setup
1. Have active GitHub Copilot subscription
2. Generate a token from GitHub settings
3. Set environment variable:
   ```bash
   export GITHUB_COPILOT_TOKEN="your-token"
   ```

### Configuration
```json
{
  "githubcopilot": {
    "type": "githubcopilot",
    "clientId": "your-client-id",
    "baseUrl": "https://api.githubcopilot.com/chat/completions"
  }
}
```

---

## Configuration Best Practices

### Security
1. **Never commit API keys** to version control
2. **Use environment variables** for all secrets
3. **Add `appsettings.local.json`** to `.gitignore`
4. **Rotate keys** periodically

### Setting Environment Variables

**Windows (PowerShell):**
```powershell
$env:OPENAI_API_KEY = "sk-..."
# Add to $PROFILE for persistence
```

**Windows (Command Prompt):**
```cmd
set OPENAI_API_KEY=sk-...
# Use setx for persistence
```

**Linux/macOS:**
```bash
export OPENAI_API_KEY="sk-..."
# Add to ~/.bashrc or ~/.zshrc for persistence
```

### Configuration Structure

Base config (`appsettings.json`) - all providers disabled:
```json
{
  "Agent": {
    "Provider": "openai",
    "Providers": {
      "openai": { "type": "openai", "IsEnabled": false },
      "claude": { "type": "claude", "IsEnabled": false }
    }
  }
}
```

Environment config (`appsettings.Development.json`) - enable what you need:
```json
{
  "Agent": {
    "Providers": {
      "openai": { "IsEnabled": true },
      "claude": { "IsEnabled": true }
    }
  }
}
```

---

## Running DraCode with Providers

```bash
# Specify provider on command line
dotnet run -- --provider=openai --task="Create hello world"
dotnet run -- --provider=claude --task="Create hello world"
dotnet run -- --provider=gemini --task="Create hello world"
dotnet run -- --provider=ollama --task="Create hello world"

# Use default from config
dotnet run
```

---

## Troubleshooting

### "Invalid API Key" / "Unauthorized"
- Verify environment variable is set: `echo $OPENAI_API_KEY`
- Check key format (OpenAI: `sk-...`, Claude: `sk-ant-...`)
- Restart terminal after setting variables

### "Rate Limit Exceeded"
- Wait and retry
- Upgrade account tier
- Use a different model

### "Connection Refused" (Ollama)
- Check Ollama is running: `curl http://localhost:11434/api/tags`
- Start Ollama: `ollama serve`

### "Provider Not Loading"
- Check `ASPNETCORE_ENVIRONMENT` matches config file
- Verify provider is enabled (`IsEnabled: true`)
- Check startup logs

---

## Model Recommendations

| Use Case | Recommended Provider/Model |
|----------|---------------------------|
| Best quality | Claude `claude-3-5-sonnet-latest` or OpenAI `gpt-4o` |
| Fast iteration | Gemini `gemini-2.0-flash-exp` or Ollama `llama3.2` |
| Cost-effective | Gemini free tier or Ollama (free) |
| Privacy-focused | Ollama (local) |
| Enterprise | Azure OpenAI |
| Large context | Gemini 1.5 Pro (2M tokens) |
