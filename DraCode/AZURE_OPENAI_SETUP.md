# Azure OpenAI Setup Guide

This guide explains how to set up Azure OpenAI Service in DraCode.

## Prerequisites

- Azure account with active subscription
- Azure OpenAI Service access (requires application approval)
- Azure OpenAI resource created

## Getting Started with Azure OpenAI

### 1. Request Access to Azure OpenAI Service

Azure OpenAI requires application approval:

1. Go to https://aka.ms/oai/access
2. Fill out the application form
3. Wait for approval (usually 1-2 business days)
4. Check your email for approval notification

**Note**: Azure OpenAI is available for enterprise customers. If you don't have access, consider using regular OpenAI API instead.

### 2. Create Azure OpenAI Resource

Once approved:

1. Go to https://portal.azure.com/
2. Click **"Create a resource"**
3. Search for **"Azure OpenAI"**
4. Click **"Create"**
5. Fill in the details:
   - **Subscription**: Select your subscription
   - **Resource group**: Create new or select existing
   - **Region**: Choose a region (e.g., East US, West Europe)
   - **Name**: Give your resource a unique name (e.g., `dracode-openai`)
   - **Pricing tier**: Select Standard S0
6. Click **"Review + create"** → **"Create"**
7. Wait for deployment to complete

### 3. Deploy a Model

After resource creation:

1. Go to your Azure OpenAI resource
2. Click **"Model deployments"** or **"Go to Azure OpenAI Studio"**
3. In Azure OpenAI Studio, click **"Deployments"**
4. Click **"Create new deployment"**
5. Select a model:
   - **gpt-4o** (recommended - latest, most capable)
   - **gpt-4-turbo** (fast and efficient)
   - **gpt-4** (classic, reliable)
   - **gpt-35-turbo** (cost-effective)
6. Give it a deployment name (e.g., `gpt-4o-deployment`)
7. Click **"Create"**

**Important**: Remember your deployment name - you'll need it for configuration!

### 4. Get Your Credentials

You need two things:

#### Endpoint URL
1. In Azure Portal, go to your Azure OpenAI resource
2. Click **"Keys and Endpoint"** in the left menu
3. Copy the **"Endpoint"** (e.g., `https://your-resource.openai.azure.com/`)

#### API Key
1. On the same page, copy **"KEY 1"** or **"KEY 2"**
2. Either key works - keep one as backup

## Configuration

You have two options to configure Azure OpenAI:

### Option A: Environment Variables (Recommended)

Set environment variables:

**Windows (PowerShell):**
```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "your-api-key-here"
```

**Windows (Command Prompt):**
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
set AZURE_OPENAI_API_KEY=your-api-key-here
```

**Linux/Mac:**
```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-api-key-here"
```

To make it permanent, add to your shell profile:
```bash
# Linux/Mac - Add to ~/.bashrc or ~/.zshrc
echo 'export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"' >> ~/.bashrc
echo 'export AZURE_OPENAI_API_KEY="your-api-key-here"' >> ~/.bashrc
source ~/.bashrc
```

### Option B: Direct Configuration

Edit `appsettings.local.json`:

```json
{
  "Agent": {
    "Provider": "azureopenai",
    "Providers": {
      "azureopenai": {
        "type": "azureopenai",
        "endpoint": "https://your-resource.openai.azure.com/",
        "apiKey": "your-api-key-here",
        "deployment": "gpt-4o-deployment"
      }
    }
  }
}
```

⚠️ **Security Warning**: If using Option B, ensure `appsettings.local.json` is in `.gitignore` to avoid committing your API key!

**Important Fields:**
- `endpoint`: Your Azure OpenAI endpoint URL (with trailing slash)
- `apiKey`: Your API key from Azure Portal
- `deployment`: The deployment name you created (NOT the model name!)

## Running DraCode with Azure OpenAI

### Single Task
```bash
dotnet run -- --provider=azureopenai --task="Create a hello world program"
```

### Multiple Tasks
```bash
dotnet run -- --provider=azureopenai --task="Create main.cs,Add tests,Write README"
```

### Interactive Mode
```bash
dotnet run -- --provider=azureopenai
# Follow the prompts
```

### Set as Default Provider
Edit `appsettings.local.json`:
```json
{
  "Agent": {
    "Provider": "azureopenai",
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

Azure OpenAI offers the same models as OpenAI, but you deploy them yourself:

| Model | Description | Best For | Context Window |
|-------|-------------|----------|----------------|
| **gpt-4o** | Latest multimodal model | Complex coding, analysis | 128K tokens |
| **gpt-4-turbo** | Fast GPT-4 | Quick iterations | 128K tokens |
| **gpt-4** | Original GPT-4 | Reliable performance | 8K / 32K tokens |
| **gpt-35-turbo** | Cost-effective | Simple tasks | 16K tokens |
| **gpt-35-turbo-16k** | Extended context | Larger codebases | 16K tokens |

### Model Selection Recommendations

- **Development**: `gpt-4o` - best capabilities
- **Production**: `gpt-4-turbo` - balance of speed and quality
- **Cost Optimization**: `gpt-35-turbo` - most economical
- **Large Context**: `gpt-4-turbo` or `gpt-4o` - 128K tokens

### Deployment Names vs Model Names

**Important Distinction:**
- **Model Name**: The actual model (e.g., `gpt-4o`)
- **Deployment Name**: Your custom name (e.g., `my-gpt4o-deployment`)

In DraCode configuration, use the **deployment name**, not the model name!

```json
{
  "deployment": "my-gpt4o-deployment"  // ✅ Your deployment name
  // NOT "model": "gpt-4o"              // ❌ Wrong
}
```

## Rate Limits and Quotas

Azure OpenAI uses Token Per Minute (TPM) quotas:

### Default Quotas by Model
- **gpt-4o**: 30K TPM
- **gpt-4-turbo**: 80K TPM
- **gpt-4**: 10K TPM
- **gpt-35-turbo**: 240K TPM

### Quota Management
1. View quotas: Azure Portal → Your Resource → **"Quotas"**
2. Request increase: Click **"Request quota increase"**
3. Adjust per-deployment: Distribute quota across deployments

**Handling Rate Limits:**
- Azure returns 429 errors when quota exceeded
- Wait before retrying
- Increase quota in Azure Portal
- Distribute load across multiple deployments

## Pricing

Azure OpenAI uses pay-as-you-go pricing per 1,000 tokens:

### Standard Pricing (per 1K tokens)

| Model | Input | Output |
|-------|-------|--------|
| **gpt-4o** | $0.0025 | $0.01 |
| **gpt-4-turbo** | $0.01 | $0.03 |
| **gpt-4** (8K) | $0.03 | $0.06 |
| **gpt-4** (32K) | $0.06 | $0.12 |
| **gpt-35-turbo** | $0.0005 | $0.0015 |

**Note**: Prices may vary by region. Check Azure Portal for exact pricing.

### Cost Estimation
- Simple tasks: ~$0.01 - $0.05
- Complex code generation: ~$0.10 - $0.50
- Large refactoring: ~$1.00+

### Cost Monitoring
- View costs: Azure Portal → Cost Management + Billing
- Set budget alerts
- Use Azure Cost Management tools

## Troubleshooting

### "Resource not found" or 404 Errors

**Causes:**
- Incorrect endpoint URL
- Wrong deployment name
- Resource not fully provisioned

**Solutions:**
```bash
# Verify endpoint format
echo $AZURE_OPENAI_ENDPOINT
# Should be: https://your-resource.openai.azure.com/
# With trailing slash!

# Verify deployment exists
# Check Azure OpenAI Studio → Deployments
```

### "Unauthorized" or 401 Errors

**Causes:**
- Invalid API key
- API key from wrong resource
- Key regenerated but not updated

**Solutions:**
1. Go to Azure Portal → Your Resource → Keys and Endpoint
2. Copy a fresh API key
3. Update your configuration
4. Restart DraCode

### "Rate limit exceeded" or 429 Errors

**Causes:**
- TPM quota exceeded
- Too many concurrent requests

**Solutions:**
1. Check quota: Azure Portal → Your Resource → Quotas
2. Request quota increase
3. Wait before retrying
4. Use lower-tier model (e.g., gpt-35-turbo)

### "Deployment not found"

**Causes:**
- Deployment name typo in configuration
- Deployment was deleted
- Using model name instead of deployment name

**Solutions:**
```json
// ❌ Wrong - using model name
"deployment": "gpt-4o"

// ✅ Correct - using deployment name
"deployment": "my-gpt4o-deployment"
```

Check your deployment name in Azure OpenAI Studio → Deployments.

### "Content filtering" Errors

**Causes:**
- Input or output triggered Azure content filters
- Content policy violation

**Solutions:**
- Review content filter settings in Azure OpenAI Studio
- Adjust severity levels if needed (admin access required)
- Modify input to be less potentially harmful

### "Invalid Request" Errors

**Causes:**
- Malformed request
- Unsupported parameters for Azure OpenAI

**Solutions:**
- Use `--verbose` to see full error details
- Check API version compatibility
- Verify deployment supports requested features

## Best Practices

### Security
1. **Use Managed Identity** in production (Azure VMs, App Service, Functions)
2. **Rotate API keys** regularly (Azure Portal → Keys)
3. **Use Key Vault** for storing secrets in production
4. **Implement RBAC** for team access control
5. **Monitor access logs** in Azure Monitor
6. **Never commit keys** to version control
7. **Use separate resources** for dev/staging/production

### Cost Optimization
1. **Use gpt-35-turbo** for simple tasks - 50x cheaper than gpt-4
2. **Enable verbose mode only for debugging** - saves tokens
3. **Monitor costs daily** in Azure Cost Management
4. **Set budget alerts** to avoid surprises
5. **Use quotas** to prevent runaway costs
6. **Cache common responses** if applicable
7. **Optimize prompts** to reduce iterations

### Performance
1. **Deploy in nearby region** - reduces latency
2. **Use gpt-4-turbo** for faster responses
3. **Batch related tasks** - use multi-task execution
4. **Provision adequate quota** for your workload
5. **Monitor throttling** in Azure metrics

### Reliability
1. **Use multiple deployments** for high availability
2. **Implement retry logic** for transient errors
3. **Monitor deployment health** in Azure Portal
4. **Set up alerts** for failures and throttling
5. **Have fallback deployments** in different regions
6. **Keep deployments updated** with latest models

### Compliance
1. **Use private endpoints** for network isolation
2. **Enable diagnostic logging** for audit trails
3. **Configure data residency** in appropriate region
4. **Review Microsoft's** data processing terms
5. **Implement content filtering** as needed

## Configuration Examples

### Development Setup
```json
{
  "Agent": {
    "Provider": "azureopenai",
    "Verbose": true,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "azureopenai": {
        "type": "azureopenai",
        "endpoint": "${AZURE_OPENAI_ENDPOINT}",
        "apiKey": "${AZURE_OPENAI_API_KEY}",
        "deployment": "gpt-4o-dev"
      }
    }
  }
}
```

### Production Setup
```json
{
  "Agent": {
    "Provider": "azureopenai",
    "Verbose": false,
    "WorkingDirectory": "/app/workspace",
    "Tasks": [],
    "Providers": {
      "azureopenai": {
        "type": "azureopenai",
        "endpoint": "${AZURE_OPENAI_ENDPOINT}",
        "apiKey": "${AZURE_OPENAI_API_KEY}",
        "deployment": "gpt-4-turbo-prod"
      }
    }
  }
}
```

### Cost-Optimized Setup
```json
{
  "Agent": {
    "Provider": "azureopenai",
    "Verbose": false,
    "WorkingDirectory": "./workspace",
    "Tasks": [],
    "Providers": {
      "azureopenai": {
        "type": "azureopenai",
        "endpoint": "${AZURE_OPENAI_ENDPOINT}",
        "apiKey": "${AZURE_OPENAI_API_KEY}",
        "deployment": "gpt-35-turbo"
      }
    }
  }
}
```

### Multi-Region Failover Setup
```json
{
  "Agent": {
    "Providers": {
      "azureopenai-primary": {
        "type": "azureopenai",
        "endpoint": "https://resource-eastus.openai.azure.com/",
        "apiKey": "${AZURE_OPENAI_API_KEY_PRIMARY}",
        "deployment": "gpt-4o"
      },
      "azureopenai-secondary": {
        "type": "azureopenai",
        "endpoint": "https://resource-westeurope.openai.azure.com/",
        "apiKey": "${AZURE_OPENAI_API_KEY_SECONDARY}",
        "deployment": "gpt-4o"
      }
    }
  }
}
```

## Advanced Features

### Using Managed Identity (Production)

Instead of API keys, use Azure Managed Identity:

1. Enable Managed Identity on your Azure resource (VM, App Service, etc.)
2. Grant the identity "Cognitive Services OpenAI User" role
3. Update DraCode to use DefaultAzureCredential (requires code modification)

**Benefits:**
- No API keys to manage
- Automatic credential rotation
- Better security posture

### Content Filtering

Azure OpenAI includes content filtering:

1. Go to Azure OpenAI Studio
2. Navigate to Content filters
3. Create custom filter policies
4. Adjust severity levels (Low, Medium, High)
5. Apply to deployments

### Private Endpoints

For network isolation:

1. Azure Portal → Your Resource → Networking
2. Click "Private endpoint connections"
3. Create private endpoint in your VNet
4. Update DNS settings
5. Access Azure OpenAI privately

## Additional Resources

- **Azure OpenAI Portal**: https://portal.azure.com/
- **Azure OpenAI Studio**: https://oai.azure.com/
- **Documentation**: https://learn.microsoft.com/azure/ai-services/openai/
- **Pricing Calculator**: https://azure.microsoft.com/pricing/calculator/
- **Quota Management**: https://learn.microsoft.com/azure/ai-services/openai/quotas-limits
- **API Reference**: https://learn.microsoft.com/azure/ai-services/openai/reference
- **Service Status**: https://status.azure.com/
- **Support**: Azure Portal → Support + troubleshooting

## Support

For DraCode-specific issues:
- Open an issue on GitHub
- Check existing documentation

For Azure OpenAI issues:
- Azure Portal → Support + troubleshooting → New support request
- Azure OpenAI documentation
- Microsoft Learn forums

## FAQ

**Q: What's the difference between OpenAI and Azure OpenAI?**
A: Azure OpenAI is the same models but hosted on Azure with enterprise features: private networking, SLA, compliance, integration with Azure services.

**Q: Can I use the same code for both OpenAI and Azure OpenAI?**
A: Yes, DraCode abstracts the differences. Just change the provider configuration.

**Q: How long does approval take?**
A: Usually 1-2 business days for the application review.

**Q: Can I migrate from OpenAI to Azure OpenAI?**
A: Yes, it's just a configuration change. No code changes needed in DraCode.

**Q: Which regions support Azure OpenAI?**
A: Check https://learn.microsoft.com/azure/ai-services/openai/concepts/models for current availability.

**Q: Can I use fine-tuned models?**
A: Yes, deploy your fine-tuned model and use its deployment name in configuration.

**Q: What's the SLA?**
A: 99.9% uptime SLA for Standard tier. Check Azure SLA documentation for details.

**Q: How do I control costs?**
A: Use quotas, budget alerts, cost management tools, and choose appropriate models for tasks.
