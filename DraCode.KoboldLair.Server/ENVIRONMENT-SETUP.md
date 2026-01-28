# Environment Setup Guide

## Setting API Keys

To use KoboldLair.Server, you need to set up API keys as environment variables. Never commit API keys to source control.

### Windows (PowerShell)

Create or edit your PowerShell profile:

```powershell
notepad $PROFILE
```

Add your API keys:

```powershell
# GitHub Copilot
$env:GITHUB_COPILOT_TOKEN = "your-github-copilot-token"

# Anthropic Claude
$env:ANTHROPIC_API_KEY = "your-anthropic-api-key"

# OpenAI
$env:OPENAI_API_KEY = "your-openai-api-key"

# Set environment for development
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Save and reload:

```powershell
. $PROFILE
```

### Windows (Command Prompt)

Set environment variables for current session:

```cmd
set GITHUB_COPILOT_TOKEN=your-github-copilot-token
set ANTHROPIC_API_KEY=your-anthropic-api-key
set ASPNETCORE_ENVIRONMENT=Development
```

Or set permanently (requires admin):

```cmd
setx GITHUB_COPILOT_TOKEN "your-github-copilot-token"
setx ANTHROPIC_API_KEY "your-anthropic-api-key"
setx ASPNETCORE_ENVIRONMENT "Development"
```

### Linux/macOS (Bash)

Edit your `~/.bashrc` or `~/.zshrc`:

```bash
# GitHub Copilot
export GITHUB_COPILOT_TOKEN="your-github-copilot-token"

# Anthropic Claude  
export ANTHROPIC_API_KEY="your-anthropic-api-key"

# OpenAI
export OPENAI_API_KEY="your-openai-api-key"

# Set environment for development
export ASPNETCORE_ENVIRONMENT="Development"
```

Reload:

```bash
source ~/.bashrc  # or source ~/.zshrc
```

### Using .env File (Development)

Create a `.env` file in the project root (add to .gitignore):

```env
GITHUB_COPILOT_TOKEN=your-github-copilot-token
ANTHROPIC_API_KEY=your-anthropic-api-key
OPENAI_API_KEY=your-openai-api-key
ASPNETCORE_ENVIRONMENT=Development
```

Note: You'll need a package like `DotNetEnv` to load .env files, or use `dotnet user-secrets` instead.

### Using User Secrets (Recommended for Development)

User secrets are stored outside your project directory and never committed:

```bash
cd DraCode.KoboldLair.Server
dotnet user-secrets init
dotnet user-secrets set "GITHUB_COPILOT_TOKEN" "your-token"
dotnet user-secrets set "ANTHROPIC_API_KEY" "your-key"
```

## Production Deployment

### Azure App Service

Configure in the Azure Portal:
1. Go to your App Service
2. Settings → Configuration → Application settings
3. Add new settings:
   - `GITHUB_COPILOT_TOKEN`
   - `ANTHROPIC_API_KEY`
   - `ASPNETCORE_ENVIRONMENT` = `Production`

### Docker

Pass as environment variables:

```bash
docker run -e GITHUB_COPILOT_TOKEN="your-token" \
           -e ANTHROPIC_API_KEY="your-key" \
           -e ASPNETCORE_ENVIRONMENT="Production" \
           koboldlair-server
```

Or use docker-compose.yml:

```yaml
version: '3.8'
services:
  koboldlair:
    image: koboldlair-server
    environment:
      - GITHUB_COPILOT_TOKEN=${GITHUB_COPILOT_TOKEN}
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - ASPNETCORE_ENVIRONMENT=Production
    env_file:
      - .env.production
```

### Kubernetes

Create a secret:

```bash
kubectl create secret generic koboldlair-secrets \
  --from-literal=GITHUB_COPILOT_TOKEN='your-token' \
  --from-literal=ANTHROPIC_API_KEY='your-key'
```

Reference in deployment:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: koboldlair-server
spec:
  template:
    spec:
      containers:
      - name: server
        image: koboldlair-server
        env:
        - name: GITHUB_COPILOT_TOKEN
          valueFrom:
            secretKeyRef:
              name: koboldlair-secrets
              key: GITHUB_COPILOT_TOKEN
        - name: ANTHROPIC_API_KEY
          valueFrom:
            secretKeyRef:
              name: koboldlair-secrets
              key: ANTHROPIC_API_KEY
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
```

## Verifying Configuration

Run the server and check the logs:

```bash
dotnet run --project DraCode.KoboldLair.Server
```

Look for:

```
========================================
Provider Configuration Loaded
========================================
Total Providers: 5
  - githubcopilot (GitHub Copilot): Enabled=True, Model=gpt-4o, Agents=dragon, wyvern, kobold
  ...
========================================
```

## Troubleshooting

### API Key Not Found

If you see warnings about missing API keys:

1. **Check environment variable is set:**
   ```powershell
   # Windows
   echo $env:GITHUB_COPILOT_TOKEN
   
   # Linux/macOS
   echo $GITHUB_COPILOT_TOKEN
   ```

2. **Restart your terminal** after setting environment variables

3. **Check variable name** matches exactly (case-sensitive on Linux/macOS)

### Provider Not Loading

1. **Check appsettings file** for your environment
2. **Verify provider is enabled** (`IsEnabled: true`)
3. **Review startup logs** for configuration details

### Wrong Environment

Verify the environment setting:

```powershell
# Check current environment
echo $env:ASPNETCORE_ENVIRONMENT

# Run with specific environment
dotnet run --environment Development
dotnet run --environment Production
```

## Security Best Practices

1. **Never commit API keys** to version control
2. **Add .env to .gitignore**
3. **Use secrets management** in production (Azure Key Vault, AWS Secrets Manager, etc.)
4. **Rotate keys regularly**
5. **Use different keys** for development and production
6. **Restrict key permissions** to minimum required
