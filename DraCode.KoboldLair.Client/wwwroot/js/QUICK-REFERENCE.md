# Project Configuration API - Quick Reference

## Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/project-configs` | List all configurations |
| GET | `/api/project-configs/defaults` | Get defaults |
| GET | `/api/project-configs/{projectId}` | Get specific config |
| PUT | `/api/project-configs/{projectId}` | Full update |
| PATCH | `/api/project-configs/{projectId}` | Partial update |
| DELETE | `/api/project-configs/{projectId}` | Delete config |
| GET | `/api/project-configs/{projectId}/agents/{agentType}` | Get agent config |
| PUT | `/api/project-configs/{projectId}/agents/{agentType}` | Update agent |

## JavaScript Client - Quick Start

```javascript
// Include in HTML
<script src="/js/project-config-client.js"></script>

// Initialize
const client = new ProjectConfigClient();

// Common Operations
await client.getAllConfigs();
await client.getConfig('project-id');
await client.setMaxParallelKobolds('project-id', 5);
await client.toggleAgent('project-id', 'wyrm', true);
await client.setAgentProvider('project-id', 'kobold', 'openai', 'gpt-4');
await client.patchConfig('project-id', { maxParallelKobolds: 3 });
```

## Agent Types
- `wyrm` (or `wyvern`) - Project analyzer
- `drake` - Supervisor
- `kobold` - Worker agent

## Common Provider Names
- `openai`
- `claude`
- `azureopenai`
- `gemini`
- `ollama`
- `githubcopilot`
