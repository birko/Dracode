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
| llama.cpp | `llamacpp` | `LLAMACPP_BASE_URL` (local) | Custom |
| GitHub Copilot | `githubcopilot` | `GITHUB_COPILOT_TOKEN` | `gpt-4o` |
| Z.AI | `zai` | `ZHIPU_API_KEY` | `glm-4.5-flash` |
| vLLM | `vllm` | `VLLM_BASE_URL` (local) | Custom |
| SGLang | `sglang` | `SGLANG_BASE_URL` (local) | Custom |

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

## Z.AI (Zhipu AI)

Z.AI (formerly Zhipu AI) offers GLM models with OpenAI-compatible API.

### Setup
1. Get API key from https://open.bigmodel.cn/ (China) or https://z.ai/ (International)
2. Set environment variable:
   ```bash
   export ZHIPU_API_KEY="your-api-key"
   ```

### Configuration
```json
{
  "zai": {
    "type": "zai",
    "apiKey": "${ZHIPU_API_KEY}",
    "model": "glm-4.5-flash",
    "baseUrl": "https://api.z.ai/api/paas/v4"
  }
}
```

### Available Models
- `glm-4.5-flash` - Fast and cost-effective (recommended)
- `glm-4.6-flash` - Improved capabilities
- `glm-4.7` - Latest model with Deep Thinking support

### Endpoints
- **International**: `https://api.z.ai/api/paas/v4`
- **China Mainland**: `https://open.bigmodel.cn/api/paas/v4`

### Features
- Deep Thinking mode for complex reasoning (supported on GLM-4.7)
- Tool calling support
- Long context windows

---

## vLLM (Local Inference)

vLLM is a high-throughput inference server for LLMs.

### Setup
1. Install vLLM:
   ```bash
   pip install vllm
   ```

2. Start the server:
   ```bash
   python -m vllm.entrypoints.openai.api_server \
     --model meta-llama/Llama-2-7b-chat-hf \
     --port 8000
   ```

3. Set environment variable (optional):
   ```bash
   export VLLM_BASE_URL="http://localhost:8000"
   ```

### Configuration
```json
{
  "vllm": {
    "type": "vllm",
    "model": "meta-llama/Llama-2-7b-chat-hf",
    "baseUrl": "http://localhost:8000/v1"
  }
}
```

### Benefits
- High throughput with continuous batching
- OpenAI-compatible API
- Support for many open-source models
- GPU memory optimization

### Resources
- GitHub: https://github.com/vllm-project/vllm
- Docs: https://docs.vllm.ai/

---

## SGLang (Local Inference)

SGLang is a structured generation language runtime for LLMs.

### Setup
1. Install SGLang:
   ```bash
   pip install "sglang[all]"
   ```

2. Start the server:
   ```bash
   python -m sglang.launch_server --model-path meta-llama/Llama-2-7b-chat-hf --port 30000
   ```

3. Set environment variable (optional):
   ```bash
   export SGLANG_BASE_URL="http://localhost:30000"
   ```

### Configuration
```json
{
  "sglang": {
    "type": "sglang",
    "model": "meta-llama/Llama-2-7b-chat-hf",
    "baseUrl": "http://localhost:30000/v1"
  }
}
```

### Benefits
- Structured generation with RadixAttention
- OpenAI-compatible API
- Efficient KV cache management
- Multi-modal support

### Resources
- GitHub: https://github.com/sgl-project/sglang
- Docs: https://sgl-project.github.io/

---

## llama.cpp (Local Inference)

llama.cpp runs GGUF models locally with minimal dependencies.

### Setup
1. Download llama.cpp from https://github.com/ggerganov/llama.cpp
2. Build or download prebuilt server
3. Download a GGUF model
4. Start the server:
   ```bash
   ./llama-server -m model.gguf --port 8080
   ```

### Configuration
```json
{
  "llamacpp": {
    "type": "llamacpp",
    "model": "custom",
    "baseUrl": "http://localhost:8080/v1"
  }
}
```

### Benefits
- Lightweight and fast
- CPU and GPU support
- Quantized models for lower memory
- No Python dependencies

### Resources
- GitHub: https://github.com/ggerganov/llama.cpp
- Models: https://huggingface.co/models?search=gguf

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
| Privacy-focused | Ollama, vLLM, SGLang, or llama.cpp (all local) |
| Enterprise | Azure OpenAI |
| Large context | Gemini 1.5 Pro (2M tokens) |
| High throughput | vLLM (local with GPU) |
| Structured output | SGLang (local) |
| China region | Z.AI `glm-4.5-flash` |
| Complex reasoning | Z.AI `glm-4.7` with Deep Thinking |
