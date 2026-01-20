# Google Gemini Setup Guide

This guide explains how to set up Google Gemini AI in DraCode.

## Prerequisites

- Google Cloud account or Google AI Studio account
- Valid API key

## Getting Your API Key

### Option 1: Google AI Studio (Recommended for Developers)

1. Go to https://makersuite.google.com/app/apikey
2. Sign in with your Google account
3. Click **"Create API Key"**
4. Select an existing Google Cloud project or create a new one
5. Click **"Create API key in existing project"** or **"Create API key in new project"**
6. Copy the API key (starts with `AIza`)

⚠️ **Important**: Save your API key immediately!

### Option 2: Google Cloud Console (For Production)

1. Go to https://console.cloud.google.com/
2. Create a new project or select existing one
3. Enable the **"Generative Language API"**:
   - Search for "Generative Language API" in the API Library
   - Click **"Enable"**
4. Go to **"Credentials"** page
5. Click **"Create Credentials"** → **"API Key"**
6. Copy the API key
7. (Optional) Restrict the API key to specific APIs and domains

## Configuration

You have two options to configure your Gemini API key:

### Option A: Environment Variable (Recommended)

Set an environment variable:

**Windows (PowerShell):**
```powershell
$env:GEMINI_API_KEY = "AIza-your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set GEMINI_API_KEY=AIza-your-api-key-here
```

**Linux/Mac:**
```bash
export GEMINI_API_KEY="AIza-your-api-key-here"
```

To make it permanent, add to your shell profile:
```bash
# Linux/Mac - Add to ~/.bashrc or ~/.zshrc
echo 'export GEMINI_API_KEY="AIza-your-api-key-here"' >> ~/.bashrc
source ~/.bashrc
```

### Option B: Direct Configuration

Edit `appsettings.local.json` and replace `${GEMINI_API_KEY}` with your actual API key:

```json
{
  "Agent": {
    "Provider": "gemini",
    "Providers": {
      "gemini": {
        "type": "gemini",
        "apiKey": "AIza-your-api-key-here",
        "model": "gemini-2.0-flash-exp",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      }
    }
  }
}
```

⚠️ **Security Warning**: If using Option B, ensure `appsettings.local.json` is in `.gitignore` to avoid committing your API key!

## Running DraCode with Gemini

### Single Task
```bash
dotnet run -- --provider=gemini --task="Create a hello world program"
```

### Multiple Tasks
```bash
dotnet run -- --provider=gemini --task="Create main.go,Add tests,Write README"
```

### Interactive Mode
```bash
dotnet run -- --provider=gemini
# Follow the prompts
```

### Set as Default Provider
Edit `appsettings.local.json`:
```json
{
  "Agent": {
    "Provider": "gemini",
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

Google Gemini offers several models:

| Model | Description | Best For | Status |
|-------|-------------|----------|--------|
| **gemini-2.0-flash-exp** | Gemini 2.0 Flash (Experimental) | Latest features, fast | Experimental |
| **gemini-1.5-pro-latest** | Gemini 1.5 Pro | Complex reasoning | Stable |
| **gemini-1.5-flash-latest** | Gemini 1.5 Flash | Fast responses | Stable |
| **gemini-1.5-flash-8b-latest** | Gemini 1.5 Flash 8B | Cost-optimized | Stable |
| **gemini-pro** | Gemini Pro | General purpose | Legacy |

### Model Capabilities

**Gemini 2.0 Flash (Experimental)**:
- Multimodal (text, images, audio, video)
- Fast inference
- Latest capabilities
- May change without notice

**Gemini 1.5 Pro**:
- 2M token context window (largest in industry)
- Advanced reasoning
- Multimodal capabilities
- Production-ready

**Gemini 1.5 Flash**:
- Fast and efficient
- 1M token context window
- Good for most coding tasks
- Balanced cost/performance

**Gemini 1.5 Flash 8B**:
- Smaller, faster model
- Lower cost
- Good for simple tasks
- Limited reasoning

### Changing Models

Update your configuration:
```json
"gemini": {
  "apiKey": "${GEMINI_API_KEY}",
  "model": "gemini-1.5-pro-latest",
  "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
}
```

### Model Recommendations

- **Development**: Use `gemini-2.0-flash-exp` for latest features
- **Production**: Use `gemini-1.5-pro-latest` for stability
- **Fast Iteration**: Use `gemini-1.5-flash-latest`
- **Cost Optimization**: Use `gemini-1.5-flash-8b-latest`
- **Large Context**: Use `gemini-1.5-pro-latest` (2M tokens)

## Rate Limits

Gemini API has rate limits based on your usage tier:

### Free Tier
- **15 requests per minute (RPM)**
- **1 million tokens per minute (TPM)**
- **1,500 requests per day (RPD)**
- Free quota: No cost up to limits

### Pay-as-you-go
- **1,000+ RPM** (model dependent)
- **4 million+ TPM**
- No daily limit
- Usage-based pricing

### Rate Limit Headers
Gemini returns rate limit information in response headers:
- `x-goog-ratelimit-requests-remaining`
- `x-goog-ratelimit-tokens-remaining`

**Handling Rate Limits:**
DraCode will display rate limit errors. If you encounter them:
1. Wait before retrying (60 seconds for free tier)
2. Upgrade to pay-as-you-go
3. Use smaller models (Flash 8B)
4. Reduce task complexity

## Pricing

Gemini uses token-based pricing:

### Free Tier (Google AI Studio)
- **Gemini 1.5 Flash**: 15 RPM, 1M TPM, 1,500 RPD - **FREE**
- **Gemini 1.5 Pro**: 2 RPM, 32K TPM, 50 RPD - **FREE**
- Perfect for development and testing

### Paid Tier (per 1M tokens)

**Gemini 2.0 Flash (Experimental)**:
- Input: **FREE** (during preview)
- Output: **FREE** (during preview)

**Gemini 1.5 Pro**:
- Input (≤128K): $1.25
- Input (>128K): $2.50
- Output: $5.00

**Gemini 1.5 Flash**:
- Input (≤128K): $0.075
- Input (>128K): $0.15
- Output: $0.30

**Gemini 1.5 Flash 8B**:
- Input (≤128K): $0.0375
- Input (>128K): $0.075
- Output: $0.15

**Cost Estimation:**
- Simple tasks: ~$0.001 - $0.01
- Complex code generation: ~$0.05 - $0.20
- Large refactoring: ~$0.50+

**Monitor Usage:**
- Check quota at https://console.cloud.google.com/apis/api/generativelanguage.googleapis.com/quotas
- Set up billing alerts in Google Cloud Console

## Troubleshooting

### "API key not valid" or "Invalid API Key"

**Causes:**
- Incorrect API key format
- API key has been deleted/disabled
- API not enabled in project

**Solutions:**
```bash
# Verify environment variable
echo $GEMINI_API_KEY  # Linux/Mac
echo %GEMINI_API_KEY%  # Windows CMD
$env:GEMINI_API_KEY   # Windows PowerShell

# Should output: AIza...
# If empty, set it again
```

**Enable the API:**
1. Go to https://console.cloud.google.com/
2. Select your project
3. Navigate to "APIs & Services" → "Library"
4. Search for "Generative Language API"
5. Click "Enable"

### "Rate Limit Exceeded" or 429 Errors

**Causes:**
- Too many requests (free tier: 15 RPM)
- Too many tokens (free tier: 1M TPM)

**Solutions:**
- Wait 60 seconds and retry
- Upgrade to pay-as-you-go at https://console.cloud.google.com/billing
- Use `--quiet` mode to reduce token usage
- Switch to Flash 8B model for lower rate limits

### "Resource has been exhausted" (Quota)

**Causes:**
- Daily quota exceeded (free tier: 1,500 RPD)
- Monthly quota exceeded

**Solutions:**
- Wait for quota reset (daily at midnight Pacific Time)
- Upgrade to paid tier
- Request quota increase at https://console.cloud.google.com/quotas

### "Permission Denied" or 403 Errors

**Causes:**
- API key doesn't have access to project
- Generative Language API not enabled
- API key restrictions

**Solutions:**
1. Check API is enabled in your project
2. Verify API key is from correct project
3. Check API key restrictions (IP, referrer, API)
4. Regenerate API key if needed

### Model Not Found

**Causes:**
- Model name typo
- Model not available in your region
- Experimental model removed

**Solutions:**
- Verify model name in config
- Try a stable model: `gemini-1.5-flash-latest`
- Check available models at https://ai.google.dev/models/gemini

### Connection Errors

**Causes:**
- Network connectivity issues
- Firewall blocking API access
- Proxy configuration

**Solutions:**
```bash
# Test connectivity
curl "https://generativelanguage.googleapis.com/v1beta/models?key=$GEMINI_API_KEY"

# Should return a JSON list of models
```

## Best Practices

### Security
1. **Never commit API keys** to version control
2. **Use environment variables** for API keys
3. **Add `appsettings.local.json`** to `.gitignore`
4. **Restrict API keys** to specific APIs and domains
5. **Rotate keys periodically** for security
6. **Use different keys** for dev/staging/production

### Cost Optimization
1. **Start with free tier** - generous limits for development
2. **Use Flash 8B for simple tasks** - cheapest option
3. **Enable `--quiet` mode** - reduces token usage
4. **Be specific in prompts** - reduces back-and-forth
5. **Use Flash for iteration** - then Pro for final pass
6. **Monitor usage** in Google Cloud Console

### Performance
1. **Use Flash models** - faster response times
2. **Batch related tasks** - use multi-task execution
3. **Provide clear context** - helps model work efficiently
4. **Use 2.0 Flash (exp)** - fastest model available
5. **Enable verbose mode for debugging** only when needed

### Reliability
1. **Use `-latest` models** for automatic updates
2. **Pin specific versions** in production (when available)
3. **Handle rate limits gracefully** - implement retries
4. **Monitor API status** - https://status.cloud.google.com/
5. **Set up alerts** for quota and billing

## Configuration Examples

### Development Setup (Free Tier)
```json
{
  "Agent": {
    "Provider": "gemini",
    "Verbose": true,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "gemini": {
        "type": "gemini",
        "apiKey": "${GEMINI_API_KEY}",
        "model": "gemini-2.0-flash-exp",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      }
    }
  }
}
```

### Production Setup
```json
{
  "Agent": {
    "Provider": "gemini",
    "Verbose": false,
    "WorkingDirectory": "/app/workspace",
    "Tasks": [],
    "Providers": {
      "gemini": {
        "type": "gemini",
        "apiKey": "${GEMINI_API_KEY}",
        "model": "gemini-1.5-pro-latest",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      }
    }
  }
}
```

### Cost-Optimized Setup
```json
{
  "Agent": {
    "Provider": "gemini",
    "Verbose": false,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "gemini": {
        "type": "gemini",
        "apiKey": "${GEMINI_API_KEY}",
        "model": "gemini-1.5-flash-8b-latest",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      }
    }
  }
}
```

### High-Context Setup (Large Codebases)
```json
{
  "Agent": {
    "Provider": "gemini",
    "Verbose": true,
    "WorkingDirectory": "./large-project",
    "Tasks": [],
    "Providers": {
      "gemini": {
        "type": "gemini",
        "apiKey": "${GEMINI_API_KEY}",
        "model": "gemini-1.5-pro-latest",
        "baseUrl": "https://generativelanguage.googleapis.com/v1beta/models/"
      }
    }
  }
}
```

## Additional Resources

- **Google AI Studio**: https://makersuite.google.com/
- **API Documentation**: https://ai.google.dev/docs
- **Model Information**: https://ai.google.dev/models/gemini
- **Pricing Details**: https://ai.google.dev/pricing
- **API Reference**: https://ai.google.dev/api
- **Quota Management**: https://console.cloud.google.com/quotas
- **API Status**: https://status.cloud.google.com/

## Support

For DraCode-specific issues:
- Open an issue on GitHub
- Check existing documentation

For Google Gemini API issues:
- Visit Google AI Forum: https://discuss.ai.google.dev/
- Check documentation: https://ai.google.dev/docs
- Report bugs: https://issuetracker.google.com/issues?q=componentid:1230749

## FAQ

**Q: What's the difference between Google AI Studio and Google Cloud?**
A: Google AI Studio is optimized for developers with generous free tier. Google Cloud is for production deployments with billing and enterprise features.

**Q: Can I use both free tier and paid tier?**
A: Yes, you can use free tier for development and paid tier for production by using different API keys.

**Q: Which model should I use?**
A: For most coding tasks, start with `gemini-2.0-flash-exp` (free during preview) or `gemini-1.5-flash-latest` (stable).

**Q: How do I upgrade to paid tier?**
A: Enable billing in Google Cloud Console at https://console.cloud.google.com/billing

**Q: Are API keys free?**
A: API keys are free to create. You only pay for API usage beyond free tier limits.

**Q: Can I use Gemini offline?**
A: No, Gemini is a cloud-based API and requires internet connectivity.

**Q: What's the context window size?**
A: Gemini 1.5 Pro: 2M tokens, Gemini 1.5 Flash: 1M tokens (largest in the industry!)
