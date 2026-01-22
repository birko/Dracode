# Claude (Anthropic) Setup Guide

This guide explains how to set up Claude AI by Anthropic in DraCode.

## Prerequisites

- Anthropic API account
- Valid API key with sufficient credits

## Getting Your API Key

### 1. Create an Anthropic Account

1. Go to https://console.anthropic.com/
2. Sign up or log in with your account
3. Navigate to **API Keys** section
4. Click **"Create Key"**
5. Give your key a name (e.g., "DraCode")
6. Copy the API key (starts with `sk-ant-`)

⚠️ **Important**: Save your API key immediately - you won't be able to see it again!

### 2. Check Your Credits

- Navigate to **"Settings"** → **"Billing"** in the Anthropic Console
- Ensure you have sufficient credits or set up a payment method
- Claude API uses pay-as-you-go pricing

## Configuration

You have two options to configure your Claude API key:

### Option A: Environment Variable (Recommended)

Set an environment variable:

**Windows (PowerShell):**
```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set ANTHROPIC_API_KEY=sk-ant-your-api-key-here
```

**Linux/Mac:**
```bash
export ANTHROPIC_API_KEY="sk-ant-your-api-key-here"
```

To make it permanent, add to your shell profile:
```bash
# Linux/Mac - Add to ~/.bashrc or ~/.zshrc
echo 'export ANTHROPIC_API_KEY="sk-ant-your-api-key-here"' >> ~/.bashrc
source ~/.bashrc
```

### Option B: Direct Configuration

Edit `appsettings.local.json` and replace `${ANTHROPIC_API_KEY}` with your actual API key:

```json
{
  "Agent": {
    "Provider": "claude",
    "Providers": {
      "claude": {
        "type": "claude",
        "apiKey": "sk-ant-your-api-key-here",
        "model": "claude-3-5-sonnet-latest",
        "baseUrl": "https://api.anthropic.com/v1/messages"
      }
    }
  }
}
```

⚠️ **Security Warning**: If using Option B, ensure `appsettings.local.json` is in `.gitignore` to avoid committing your API key!

## Running DraCode with Claude

### Single Task
```bash
dotnet run -- --provider=claude --task="Create a hello world program"
```

### Multiple Tasks
```bash
dotnet run -- --provider=claude --task="Create main.py,Add tests,Write README"
```

### Interactive Mode
```bash
dotnet run -- --provider=claude
# Follow the prompts
```

### Set as Default Provider
Edit `appsettings.local.json`:
```json
{
  "Agent": {
    "Provider": "claude",
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

Claude offers several models with different capabilities and pricing:

| Model | Name | Description | Best For |
|-------|------|-------------|----------|
| **claude-3-5-sonnet-latest** | Claude 3.5 Sonnet | Most intelligent model | Complex coding, analysis |
| **claude-3-5-sonnet-20241022** | Claude 3.5 Sonnet (Oct) | Specific snapshot | Version pinning |
| **claude-3-5-haiku-latest** | Claude 3.5 Haiku | Fast and efficient | Simple tasks, cost savings |
| **claude-3-opus-latest** | Claude 3 Opus | Highest capability | Most demanding tasks |
| **claude-3-sonnet-20240229** | Claude 3 Sonnet | Previous generation | Legacy compatibility |

### Changing Models

Update your configuration:
```json
"claude": {
  "apiKey": "${ANTHROPIC_API_KEY}",
  "model": "claude-3-5-haiku-latest",
  "baseUrl": "https://api.anthropic.com/v1/messages"
}
```

### Model Recommendations

- **Development**: Use `claude-3-5-sonnet-latest` for best results
- **Production**: Use date-specific versions (e.g., `claude-3-5-sonnet-20241022`) for consistency
- **Cost Optimization**: Use `claude-3-5-haiku-latest` for simple tasks
- **Maximum Quality**: Use `claude-3-opus-latest` for critical work

## Rate Limits

Claude API has rate limits based on your account tier:

### Free Tier
- Limited requests per minute
- May experience queuing during high demand

### Paid Tier (Default)
- Higher rate limits
- Better reliability
- Usage-based pricing

### Enterprise
- Custom rate limits
- Dedicated support
- Volume discounts

**Handling Rate Limits:**
DraCode will display rate limit errors. If you encounter them:
1. Wait a few moments before retrying
2. Reduce task complexity
3. Upgrade your account tier
4. Use `--quiet` mode to reduce output and speed up execution

## Pricing

Claude uses token-based pricing. Approximate costs:

| Model | Input (per 1M tokens) | Output (per 1M tokens) |
|-------|----------------------|------------------------|
| Claude 3.5 Sonnet | $3.00 | $15.00 |
| Claude 3.5 Haiku | $0.80 | $4.00 |
| Claude 3 Opus | $15.00 | $75.00 |

**Cost Estimation:**
- Simple tasks: ~$0.01 - $0.05
- Complex code generation: ~$0.10 - $0.50
- Large refactoring: ~$1.00+

**Monitor Usage:**
- Check your usage at https://console.anthropic.com/settings/billing
- Set up billing alerts to avoid surprises

## Troubleshooting

### "Authentication Error" or "Invalid API Key"

**Causes:**
- Incorrect API key format
- API key has been revoked
- Environment variable not set correctly

**Solutions:**
```bash
# Verify environment variable
echo $ANTHROPIC_API_KEY  # Linux/Mac
echo %ANTHROPIC_API_KEY%  # Windows CMD
$env:ANTHROPIC_API_KEY   # Windows PowerShell

# Should output: sk-ant-...
# If empty, set it again
```

### "Rate Limit Exceeded"

**Causes:**
- Too many requests in short time
- Account tier limits reached

**Solutions:**
- Wait 60 seconds and retry
- Use verbose mode (`--verbose`) to see rate limit details
- Upgrade your account tier at https://console.anthropic.com/settings/billing

### "Insufficient Credits"

**Causes:**
- No payment method on file
- Credits depleted

**Solutions:**
- Add payment method at https://console.anthropic.com/settings/billing
- Purchase additional credits

### "overloaded_error"

**Causes:**
- Claude API is experiencing high load
- Temporary service issue

**Solutions:**
- Wait a few seconds and retry
- Use a different model (e.g., Haiku instead of Sonnet)
- Check status at https://status.anthropic.com/

### Connection Errors

**Causes:**
- Network connectivity issues
- Firewall blocking API access
- Proxy configuration

**Solutions:**
```bash
# Test connectivity
curl https://api.anthropic.com/v1/messages \
  -H "x-api-key: $ANTHROPIC_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{"model":"claude-3-5-sonnet-latest","max_tokens":10,"messages":[{"role":"user","content":"Hi"}]}'

# Should return a JSON response, not an error
```

### "Invalid Request" Errors

**Causes:**
- Malformed input
- Message format issues
- Tool definition problems

**Solutions:**
- Use `--verbose` to see full error details
- Check that your DraCode version is up to date
- Report issue on GitHub with error details

## Best Practices

### Security
1. **Never commit API keys** to version control
2. **Use environment variables** for API keys
3. **Add `appsettings.local.json`** to `.gitignore`
4. **Rotate keys periodically** for security
5. **Use separate keys** for development and production

### Cost Optimization
1. **Use Haiku for simple tasks** - much cheaper
2. **Enable `--quiet` mode** - reduces token usage
3. **Be specific in prompts** - reduces iterations
4. **Set working directory** to limit file operations
5. **Monitor usage regularly** in Anthropic Console

### Performance
1. **Use latest models** - they're faster and better
2. **Batch related tasks** - use multi-task execution
3. **Provide context** - helps Claude work more efficiently
4. **Use verbose mode for debugging** - understand what's happening

### Reliability
1. **Pin model versions** in production
2. **Handle rate limits gracefully** - add retries if needed
3. **Monitor API status** - https://status.anthropic.com/
4. **Set up alerts** for usage and billing

## Configuration Examples

### Development Setup
```json
{
  "Agent": {
    "Provider": "claude",
    "Verbose": true,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "claude": {
        "type": "claude",
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-5-sonnet-latest",
        "baseUrl": "https://api.anthropic.com/v1/messages"
      }
    }
  }
}
```

### Production Setup
```json
{
  "Agent": {
    "Provider": "claude",
    "Verbose": false,
    "WorkingDirectory": "/app/workspace",
    "Tasks": [],
    "Providers": {
      "claude": {
        "type": "claude",
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-5-sonnet-20241022",
        "baseUrl": "https://api.anthropic.com/v1/messages"
      }
    }
  }
}
```

### Cost-Optimized Setup
```json
{
  "Agent": {
    "Provider": "claude",
    "Verbose": false,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "claude": {
        "type": "claude",
        "apiKey": "${ANTHROPIC_API_KEY}",
        "model": "claude-3-5-haiku-latest",
        "baseUrl": "https://api.anthropic.com/v1/messages"
      }
    }
  }
}
```

## Additional Resources

- **Anthropic Console**: https://console.anthropic.com/
- **API Documentation**: https://docs.anthropic.com/
- **Model Comparison**: https://docs.anthropic.com/en/docs/models-overview
- **Pricing Details**: https://www.anthropic.com/pricing
- **API Status**: https://status.anthropic.com/
- **Support**: support@anthropic.com

## Support

For DraCode-specific issues:
- Open an issue on GitHub
- Check existing documentation

For Anthropic API issues:
- Contact Anthropic support at support@anthropic.com
- Check API status at https://status.anthropic.com/
- Review documentation at https://docs.anthropic.com/
