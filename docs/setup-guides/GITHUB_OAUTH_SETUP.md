# GitHub Copilot OAuth Setup Guide

This guide explains how to set up GitHub Copilot authentication using OAuth Device Flow in DraCode.

## Prerequisites

- Active GitHub Copilot subscription
- GitHub account with access to create OAuth Apps

## Setup Instructions

### 1. Create a GitHub OAuth App

1. Go to https://github.com/settings/developers
2. Click **"New OAuth App"**
3. Fill in the application details:
   - **Application name**: `DraCode` (or your preferred name)
   - **Homepage URL**: `http://localhost` (or your app URL)
   - **Authorization callback URL**: `http://localhost` (not strictly needed for device flow)
   - **Description**: Optional
4. Click **"Register application"**
5. Copy the **Client ID** (you'll need this)
6. Note: Client Secret is not required for device flow

### 2. Configure DraCode

You have two options to configure the GitHub Client ID:

#### Option A: Environment Variable (Recommended)
Set an environment variable:

**Windows (PowerShell):**
```powershell
$env:GITHUB_CLIENT_ID = "your_client_id_here"
```

**Windows (Command Prompt):**
```cmd
set GITHUB_CLIENT_ID=your_client_id_here
```

**Linux/Mac:**
```bash
export GITHUB_CLIENT_ID=your_client_id_here
```

#### Option B: Direct Configuration
Edit `appsettings.local.json` and replace `${GITHUB_CLIENT_ID}` with your actual Client ID:

```json
{
  "Agent": {
    "Provider": "githubcopilot",
    "Providers": {
      "githubcopilot": {
        "clientId": "Ov23li1YourActualClientIDHere",
        "model": "gpt-4o",
        "baseUrl": "https://api.githubcopilot.com/chat/completions"
      }
    }
  }
}
```

### 3. Run DraCode with GitHub Copilot

```bash
# Using command line
dotnet run --provider=githubcopilot --task="Your task here"

# Or set as default in appsettings.local.json
# Change "Provider": "githubcopilot"
```

### 4. First-Time Authentication

On first run, you'll see:

```
Initiating GitHub OAuth Device Flow...

Please visit: https://github.com/login/device
And enter code: XXXX-XXXX

Waiting for authorization...
```

1. Open the URL in your browser
2. Enter the displayed code
3. Authorize the application
4. Return to the terminal - authentication will complete automatically

✓ Authentication successful!

## Token Management

### Token Storage
- Tokens are stored in: `~/.dracode/github_token.json`
- Tokens are automatically refreshed when expired
- Tokens typically last 8 hours

### Re-authentication
If you need to re-authenticate (e.g., token expired or corrupted):

```bash
# Delete the token file
rm ~/.dracode/github_token.json

# Or on Windows
del %USERPROFILE%\.dracode\github_token.json

# Run DraCode again - it will prompt for authentication
```

### Logout
To manually remove stored tokens:

```bash
# Delete token file
rm ~/.dracode/github_token.json  # Linux/Mac
del %USERPROFILE%\.dracode\github_token.json  # Windows
```

## Troubleshooting

### "Failed to initiate device flow"
- Check that your Client ID is correct
- Ensure you have internet connectivity
- Verify the OAuth app still exists on GitHub

### "Timeout waiting for authorization"
- You have 10 minutes to authorize
- Make sure you completed the authorization in the browser
- Try authenticating again

### "Unauthorized" or 401 errors
- Token may be expired
- Delete the token file and re-authenticate
- Ensure your GitHub Copilot subscription is active

### "Personal Access Tokens are not supported"
- You're using the old configuration with `apiKey` instead of `clientId`
- Update your `appsettings.local.json` to use the OAuth configuration shown above

## Configuration Options

### Available Models
The GitHub Copilot API supports various models:
- `gpt-4o` (default, recommended)
- `gpt-4o-mini`
- `gpt-4-turbo`
- `gpt-3.5-turbo`

Change the model in your configuration:
```json
"githubcopilot": {
  "clientId": "${GITHUB_CLIENT_ID}",
  "model": "gpt-4o-mini",
  "baseUrl": "https://api.githubcopilot.com/chat/completions"
}
```

## Security Notes

⚠️ **Important Security Considerations:**

1. **Never commit your Client ID to public repositories** if it's a sensitive application
2. **Token files contain access tokens** - keep `~/.dracode/` directory secure
3. **Do not share your token file** with others
4. **Tokens auto-refresh** but you should still protect them as they provide API access

## Advanced: Custom Token Storage

If you need custom token storage (e.g., encrypted), you can modify:
- `DraCode.Agent/Auth/TokenStorage.cs`

The default implementation stores tokens as plain JSON files in the user's home directory.
