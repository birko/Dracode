# Ollama Local Models Setup Guide

This guide explains how to set up Ollama to run local AI models with DraCode.

## What is Ollama?

Ollama allows you to run large language models locally on your machine. Benefits include:
- **100% Private** - No data sent to external APIs
- **No API Costs** - Free to use
- **Works Offline** - No internet required after download
- **Fast** - Low latency, local inference
- **Customizable** - Fine-tune and modify models

Perfect for:
- Privacy-sensitive projects
- Offline development
- Learning and experimentation
- Cost-free development
- Air-gapped environments

## Prerequisites

### System Requirements

**Minimum:**
- **RAM**: 8 GB (for 7B models)
- **Storage**: 4-10 GB per model
- **OS**: Windows 10/11, macOS, or Linux

**Recommended:**
- **RAM**: 16 GB+ (for larger models)
- **GPU**: NVIDIA GPU with 8+ GB VRAM (optional but faster)
- **Storage**: 50+ GB for multiple models
- **CPU**: Modern multi-core processor

**Model Size Guide:**
- 7B models: 4 GB RAM, 5 GB disk
- 13B models: 8 GB RAM, 8 GB disk
- 34B models: 16 GB RAM, 20 GB disk
- 70B models: 32+ GB RAM, 40 GB disk

## Installation

### Windows

**Option 1: Official Installer (Recommended)**
1. Download from https://ollama.com/download/windows
2. Run the installer (`OllamaSetup.exe`)
3. Follow installation wizard
4. Ollama will start automatically

**Option 2: Windows Package Manager**
```powershell
winget install Ollama.Ollama
```

### macOS

**Option 1: Official Installer**
1. Download from https://ollama.com/download/mac
2. Open the `.zip` file
3. Drag Ollama to Applications
4. Run Ollama from Applications

**Option 2: Homebrew**
```bash
brew install ollama
```

### Linux

**Install Script (Recommended):**
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

**Manual Installation:**
```bash
# Download binary
curl -L https://ollama.com/download/ollama-linux-amd64 -o /usr/local/bin/ollama
chmod +x /usr/local/bin/ollama

# Create service
sudo useradd -r -s /bin/false -m -d /usr/share/ollama ollama
sudo tee /etc/systemd/system/ollama.service > /dev/null <<EOF
[Unit]
Description=Ollama Service
After=network-online.target

[Service]
ExecStart=/usr/local/bin/ollama serve
User=ollama
Group=ollama
Restart=always
RestartSec=3

[Install]
WantedBy=default.target
EOF

# Enable and start
sudo systemctl daemon-reload
sudo systemctl enable ollama
sudo systemctl start ollama
```

### Docker

```bash
# Run Ollama in Docker
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

# Pull a model
docker exec -it ollama ollama pull llama3.2
```

## Verifying Installation

```bash
# Check if Ollama is running
ollama --version

# Test the API
curl http://localhost:11434/api/tags

# Should return JSON list of models (empty if none downloaded yet)
```

## Downloading Models

### Popular Models for Coding

| Model | Size | Best For | Command |
|-------|------|----------|---------|
| **Llama 3.2** (3B) | 2 GB | Fast, efficient coding | `ollama pull llama3.2` |
| **CodeLlama** (7B) | 4 GB | Code generation | `ollama pull codellama` |
| **Mistral** (7B) | 4 GB | Balanced performance | `ollama pull mistral` |
| **Llama 3.1** (8B) | 4.7 GB | Latest Llama | `ollama pull llama3.1` |
| **CodeLlama** (13B) | 7.4 GB | Better code quality | `ollama pull codellama:13b` |
| **Deepseek Coder** (6.7B) | 3.8 GB | Code-specialized | `ollama pull deepseek-coder` |
| **Phi-3** (3.8B) | 2.3 GB | Small but capable | `ollama pull phi3` |

### Download a Model

```bash
# Basic syntax
ollama pull <model-name>

# Examples
ollama pull llama3.2        # Latest Llama 3.2
ollama pull codellama:7b    # CodeLlama 7B
ollama pull mistral:latest  # Latest Mistral
ollama pull llama3.1:70b    # Llama 3.1 70B (requires 32+ GB RAM)

# List available models
ollama list

# Remove a model
ollama rm <model-name>
```

**Tip**: Start with `llama3.2` (2 GB) or `codellama` (4 GB) for coding tasks.

### Testing a Model

```bash
# Run interactive chat
ollama run llama3.2

# Ask a question
>>> Write a hello world program in Python

# Exit with /bye or Ctrl+D
```

## Configuration

Ollama runs locally on `http://localhost:11434` by default. No API key needed!

### Option A: Use Default Configuration

DraCode is pre-configured for Ollama. Just edit `appsettings.local.json`:

```json
{
  "Agent": {
    "Provider": "ollama",
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "llama3.2",
        "baseUrl": "http://localhost:11434"
      }
    }
  }
}
```

### Option B: Custom Port or Remote Ollama

If Ollama runs on different port or remote machine:

```json
{
  "Agent": {
    "Provider": "ollama",
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "codellama",
        "baseUrl": "http://192.168.1.100:11434"  // Remote Ollama
      }
    }
  }
}
```

### Model Selection

Change the `model` field to use different models:

```json
// Fast and efficient
"model": "llama3.2"

// Better code generation
"model": "codellama"

// Balanced
"model": "mistral"

// Specific version
"model": "llama3.1:8b"

// Latest
"model": "llama3.1:latest"
```

## Running DraCode with Ollama

### Prerequisites
1. Ollama installed and running
2. At least one model downloaded

### Single Task
```bash
dotnet run -- --provider=ollama --task="Create a hello world program"
```

### Multiple Tasks
```bash
dotnet run -- --provider=ollama --task="Create main.py,Add tests,Write README"
```

### Interactive Mode
```bash
dotnet run -- --provider=ollama
# Follow the prompts
```

### Set as Default Provider
Edit `appsettings.local.json`:
```json
{
  "Agent": {
    "Provider": "ollama",
    "Verbose": true,
    "WorkingDirectory": "./",
    "Tasks": []
  }
}
```

Then simply run:
```bash
dotnet run
```

## Available Models

### Code-Focused Models

**CodeLlama** - Meta's code-specialized model
- Variants: 7B, 13B, 34B, 70B
- Best for: Code generation, completion
- Pull: `ollama pull codellama`

**Deepseek Coder** - Code-specialized
- Variants: 1.3B, 6.7B, 33B
- Best for: Multiple programming languages
- Pull: `ollama pull deepseek-coder`

### General Purpose Models

**Llama 3.2** - Meta's latest compact model
- Size: 3B
- Best for: Fast, efficient general tasks
- Pull: `ollama pull llama3.2`

**Llama 3.1** - Meta's flagship
- Variants: 8B, 70B, 405B
- Best for: Complex reasoning
- Pull: `ollama pull llama3.1`

**Mistral** - Mistral AI's model
- Variants: 7B
- Best for: Balanced performance
- Pull: `ollama pull mistral`

**Phi-3** - Microsoft's small model
- Variants: 3.8B, 14B
- Best for: Efficient on smaller hardware
- Pull: `ollama pull phi3`

### Specialized Models

**Gemma** - Google's open model
- Variants: 2B, 7B
- Pull: `ollama pull gemma`

**Qwen** - Alibaba's model
- Variants: Various sizes
- Best for: Multilingual tasks
- Pull: `ollama pull qwen`

### Model Comparison

| Model | Size | RAM | Speed | Quality | Best For |
|-------|------|-----|-------|---------|----------|
| Llama 3.2 | 3B | 4 GB | ⚡⚡⚡ | ⭐⭐⭐ | Fast iteration |
| CodeLlama | 7B | 8 GB | ⚡⚡ | ⭐⭐⭐⭐ | Code tasks |
| Mistral | 7B | 8 GB | ⚡⚡ | ⭐⭐⭐⭐ | General use |
| Llama 3.1 8B | 8B | 8 GB | ⚡⚡ | ⭐⭐⭐⭐ | Balanced |
| CodeLlama 13B | 13B | 16 GB | ⚡ | ⭐⭐⭐⭐⭐ | Complex code |
| Llama 3.1 70B | 70B | 40+ GB | 💤 | ⭐⭐⭐⭐⭐ | Best quality |

## Performance Optimization

### GPU Acceleration

Ollama automatically uses GPU if available:

**Check GPU Support:**
```bash
# NVIDIA GPU
nvidia-smi

# macOS GPU (Metal)
system_profiler SPDisplaysDataType

# Should show GPU being used when running Ollama
```

**GPU Environment Variables:**
```bash
# Use specific GPU (multiple GPU systems)
export CUDA_VISIBLE_DEVICES=0

# Disable GPU (CPU only)
export OLLAMA_USE_GPU=false
```

### Model Quantization

Use quantized models for better performance:

```bash
# 4-bit quantization (smaller, faster)
ollama pull llama3.1:8b-instruct-q4_0

# 8-bit quantization (balanced)
ollama pull llama3.1:8b-instruct-q8_0

# Full precision (best quality, slowest)
ollama pull llama3.1:8b-instruct-fp16
```

**Quantization Levels:**
- `q4_0`, `q4_1`: 4-bit, smallest, fastest
- `q5_0`, `q5_1`: 5-bit, balanced
- `q8_0`: 8-bit, good quality
- `fp16`: Full precision, best quality

### Memory Management

```bash
# Keep model in memory
ollama run --keepalive 60m llama3.2

# Unload model immediately after use
ollama run --keepalive 0 llama3.2
```

### Concurrent Requests

```bash
# Set max concurrent requests (default: 512)
export OLLAMA_MAX_LOADED_MODELS=2
export OLLAMA_NUM_PARALLEL=4
```

## Troubleshooting

### "Connection refused" or "Cannot connect to Ollama"

**Causes:**
- Ollama service not running
- Wrong port or URL
- Firewall blocking connection

**Solutions:**
```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Start Ollama (if not running)
ollama serve

# Windows: Check system tray for Ollama icon
# macOS: Check menu bar for Ollama
# Linux: sudo systemctl status ollama
```

### "Model not found"

**Causes:**
- Model not downloaded
- Wrong model name
- Typo in configuration

**Solutions:**
```bash
# List downloaded models
ollama list

# Download the model
ollama pull llama3.2

# Check spelling in config
"model": "llama3.2"  // ✅ Correct
"model": "llama32"   // ❌ Wrong
```

### Slow Performance

**Causes:**
- Model too large for your RAM
- No GPU acceleration
- Other heavy processes running

**Solutions:**
1. Use smaller model: `llama3.2` instead of `llama3.1:70b`
2. Use quantized model: `codellama:7b-q4_0`
3. Close other applications
4. Enable GPU if available
5. Increase RAM allocation

### Out of Memory Errors

**Causes:**
- Model too large
- Not enough RAM
- Memory leak

**Solutions:**
```bash
# Use smaller model
ollama pull llama3.2  # 2 GB instead of llama3.1:70b (40 GB)

# Use 4-bit quantized version
ollama pull codellama:7b-q4_0

# Restart Ollama
ollama stop
ollama serve
```

### Model Takes Forever to Load

**Causes:**
- Large model loading into RAM
- Slow disk
- First-time use

**Solutions:**
- Wait patiently (70B models can take 1-2 minutes)
- Use SSD instead of HDD
- Keep model in memory: `ollama run --keepalive 60m <model>`

### Quality Issues / Poor Results

**Causes:**
- Model too small for task
- Too aggressive quantization
- Not a code-specialized model

**Solutions:**
1. Use larger model: `codellama:13b` instead of `codellama:7b`
2. Use less quantization: `q8_0` instead of `q4_0`
3. Use code-specific model: `codellama` instead of `llama3.2`
4. Provide more context in prompts

## Best Practices

### Model Selection
1. **Start small** - Try `llama3.2` or `codellama:7b` first
2. **Match to hardware** - Don't use 70B models on 8 GB RAM
3. **Code tasks** - Use `codellama` or `deepseek-coder`
4. **General tasks** - Use `llama3.1` or `mistral`
5. **Experiment** - Try different models to find best fit

### Performance
1. **Use GPU** - 5-10x faster than CPU
2. **Quantization** - Use `q4_0` or `q5_0` for speed
3. **Keep in memory** - Use `--keepalive` for frequent use
4. **Close other apps** - Free up RAM
5. **Use SSD** - Faster model loading

### Privacy
1. **All local** - No data leaves your machine
2. **No API keys** - No credentials to manage
3. **Offline capable** - Works without internet
4. **Full control** - You own the model

### Cost
1. **100% free** - No API charges
2. **One-time download** - Reuse unlimited times
3. **No metering** - Unlimited requests
4. **Development** - Perfect for experimentation

## Configuration Examples

### Development Setup (Fast Iteration)
```json
{
  "Agent": {
    "Provider": "ollama",
    "Verbose": true,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "llama3.2",
        "baseUrl": "http://localhost:11434"
      }
    }
  }
}
```

### Code Generation Setup
```json
{
  "Agent": {
    "Provider": "ollama",
    "Verbose": false,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "codellama:13b",
        "baseUrl": "http://localhost:11434"
      }
    }
  }
}
```

### High-Quality Setup (16+ GB RAM)
```json
{
  "Agent": {
    "Provider": "ollama",
    "Verbose": true,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "codellama:34b",
        "baseUrl": "http://localhost:11434"
      }
    }
  }
}
```

### Remote Ollama Setup
```json
{
  "Agent": {
    "Provider": "ollama",
    "Verbose": false,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "ollama": {
        "type": "ollama",
        "model": "llama3.1:70b",
        "baseUrl": "http://powerful-server:11434"
      }
    }
  }
}
```

## Advanced Usage

### Custom Models

Create custom models with Modelfile:

```bash
# Create Modelfile
cat > Modelfile <<EOF
FROM codellama
PARAMETER temperature 0.1
PARAMETER top_p 0.9
SYSTEM You are an expert Python developer. Always write clean, well-documented code.
EOF

# Create custom model
ollama create my-python-expert -f Modelfile

# Use in DraCode
# "model": "my-python-expert"
```

### API Direct Access

```bash
# Generate completion
curl http://localhost:11434/api/generate -d '{
  "model": "llama3.2",
  "prompt": "Write a hello world program",
  "stream": false
}'

# Chat completion
curl http://localhost:11434/api/chat -d '{
  "model": "llama3.2",
  "messages": [
    {"role": "user", "content": "Hello!"}
  ]
}'
```

### Environment Variables

```bash
# Change Ollama host
export OLLAMA_HOST=0.0.0.0:11434

# Model storage location
export OLLAMA_MODELS=/custom/path/models

# Keep models in memory
export OLLAMA_KEEP_ALIVE=60m

# Number of parallel requests
export OLLAMA_NUM_PARALLEL=4
```

## Additional Resources

- **Ollama Website**: https://ollama.com/
- **Model Library**: https://ollama.com/library
- **GitHub**: https://github.com/ollama/ollama
- **Documentation**: https://github.com/ollama/ollama/tree/main/docs
- **Discord Community**: https://discord.gg/ollama
- **Model Cards**: https://ollama.com/library/<model-name>

## Support

For DraCode-specific issues:
- Open an issue on GitHub
- Check existing documentation

For Ollama issues:
- GitHub Issues: https://github.com/ollama/ollama/issues
- Discord: https://discord.gg/ollama
- Documentation: https://github.com/ollama/ollama/tree/main/docs

## FAQ

**Q: Is Ollama really free?**
A: Yes! 100% free and open source. No API costs, no subscription.

**Q: Can I use it commercially?**
A: Yes, but check the license of the specific model you're using. Most are permissively licensed.

**Q: How much RAM do I need?**
A: Minimum 8 GB for 7B models. 16 GB recommended for better performance.

**Q: Do I need a GPU?**
A: No, but it's 5-10x faster with one. Works fine on CPU.

**Q: Can I use multiple models?**
A: Yes, download as many as you want. Switch by changing the `model` config.

**Q: Does it work offline?**
A: Yes! After downloading models, no internet needed.

**Q: Which model should I start with?**
A: For coding: `codellama` or `llama3.2`. For general: `llama3.1:8b`

**Q: Can I run it on a server?**
A: Yes! Set `OLLAMA_HOST=0.0.0.0` to allow remote connections.

**Q: How do I update Ollama?**
A: Download latest installer from ollama.com or use your package manager.

**Q: Can I fine-tune models?**
A: Yes, but requires additional tools. Check Ollama documentation for details.
