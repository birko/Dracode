# Project Configuration API Documentation

## Overview
The Project Configuration API allows you to manage project-specific settings including resource limits, agent providers, timeouts, and security settings.

## Configuration Structure

### New Sectioned Format (v2.4.2+)

```json
{
  "projects": [
    {
      "project": {
        "id": "uuid-here",
        "name": "My Project"
      },
      "agents": {
        "wyrm": {
          "enabled": true,
          "provider": "openai",
          "model": "gpt-4",
          "maxParallel": 1,
          "timeout": 300
        },
        "wyvern": {
          "enabled": true,
          "provider": "openai",
          "model": "gpt-4",
          "maxParallel": 1,
          "timeout": 600
        },
        "drake": {
          "enabled": true,
          "provider": "claude",
          "model": null,
          "maxParallel": 1,
          "timeout": 0
        },
        "koboldPlanner": {
          "enabled": true,
          "provider": null,
          "model": null,
          "maxParallel": 1,
          "timeout": 300
        },
        "kobold": {
          "enabled": true,
          "provider": "openai",
          "model": "gpt-4",
          "maxParallel": 4,
          "timeout": 1800
        }
      },
      "security": {
        "allowedExternalPaths": ["/shared/libs", "/common/templates"],
        "sandboxMode": "workspace"
      },
      "metadata": {
        "lastUpdated": "2026-02-04T20:00:00Z",
        "createdAt": "2026-02-04T10:00:00Z"
      }
    }
  ]
}
```

---

## Configuration Sections

### Project Identity

| Field | Type | Description |
|-------|------|-------------|
| `project.id` | string | Unique project identifier (UUID) |
| `project.name` | string? | Display name for project |

### Agent Configuration

Each agent type (wyrm, wyvern, drake, koboldPlanner, kobold) has these fields:

| Field | Type | Description |
|-------|------|-------------|
| `enabled` | bool | Whether agent is active (default: false) |
| `provider` | string? | LLM provider override (null = use global) |
| `model` | string? | Model override (null = use provider default) |
| `maxParallel` | int | Max concurrent instances (default: 1) |
| `timeout` | int | Timeout in seconds (0 = no timeout) |

### Security Configuration

| Field | Type | Description |
|-------|------|-------------|
| `security.allowedExternalPaths` | string[] | Paths outside workspace that agents can access |
| `security.sandboxMode` | string | Security mode: "workspace", "relaxed", or "strict" |

**Sandbox Modes:**
- `workspace` - Only project workspace accessible (default)
- `relaxed` - Workspace + allowed external paths
- `strict` - Minimal access, explicit allowlist only

### Metadata

| Field | Type | Description |
|-------|------|-------------|
| `metadata.lastUpdated` | DateTime? | Last modification timestamp (auto-set) |
| `metadata.createdAt` | DateTime? | Creation timestamp |

---

## Agent Types

| Agent | Purpose | Typical Timeout |
|-------|---------|-----------------|
| `wyrm` | Specification analysis | 300s (5 min) |
| `wyvern` | Project analysis, task breakdown | 600s (10 min) |
| `drake` | Task supervision | 0 (no timeout) |
| `koboldPlanner` | Implementation planning | 300s (5 min) |
| `kobold` | Task execution | 1800s (30 min) |

---

## API Endpoints

### 1. Get All Project Configurations
**GET** `/api/project-configs`

Returns all project configurations along with default values.

**Response:**
```json
{
  "defaults": {
    "maxParallelKobolds": 1,
    "maxParallelDrakes": 1,
    "maxParallelWyrms": 1,
    "maxParallelWyverns": 1
  },
  "projects": [/* array of project configs */]
}
```

---

### 2. Get Specific Project Configuration
**GET** `/api/project-configs/{projectId}`

Returns configuration for a specific project.

---

### 3. Update Project Configuration
**PUT** `/api/project-configs/{projectId}`

Creates or fully updates a project configuration.

---

### 4. Delete Project Configuration
**DELETE** `/api/project-configs/{projectId}`

Deletes a project configuration.

---

### 5. Get Agent Configuration
**GET** `/api/project-configs/{projectId}/agents/{agentType}`

Gets settings for a specific agent type.

**Agent Types:** `wyrm`, `wyvern`, `drake`, `kobold-planner`, `kobold`

**Response:**
```json
{
  "provider": "openai",
  "model": "gpt-4",
  "enabled": true,
  "maxParallel": 4,
  "timeout": 1800
}
```

---

### 6. Update Agent Configuration
**PUT** `/api/project-configs/{projectId}/agents/{agentType}`

Updates settings for a specific agent type.

**Request Body:**
```json
{
  "provider": "claude",
  "model": "claude-3-opus",
  "enabled": true,
  "maxParallel": 2,
  "timeout": 900
}
```

---

## JavaScript Client Library

### Installation
Include the client library in your HTML:
```html
<script src="/js/project-config-client.js"></script>
```

### Usage Examples

#### Initialize Client
```javascript
const client = new ProjectConfigClient();
```

#### Get All Configurations
```javascript
const data = await client.getAllConfigs();
console.log('Defaults:', data.defaults);
console.log('Projects:', data.projects);
```

#### Get Specific Project Config
```javascript
const config = await client.getConfig('my-project');
console.log('Kobold Max Parallel:', config.agents.kobold.maxParallel);
```

#### Update Agent Settings
```javascript
await client.setAgentProvider('my-project', 'kobold', 'claude', 'claude-3-sonnet');
```

#### Set Agent Timeout
```javascript
// Set 30-minute timeout for Kobolds
await client.setAgentTimeout('my-project', 'kobold', 1800);
```

#### Toggle Agent
```javascript
// Enable Wyrm
await client.toggleAgent('my-project', 'wyrm', true);

// Disable Drake
await client.toggleAgent('my-project', 'drake', false);
```

#### Get Agent Configuration
```javascript
const koboldConfig = await client.getAgentConfig('my-project', 'kobold');
console.log('Provider:', koboldConfig.provider);
console.log('Max Parallel:', koboldConfig.maxParallel);
console.log('Timeout:', koboldConfig.timeout);
```

---

## Error Handling

All endpoints return appropriate HTTP status codes:
- **200 OK** - Request succeeded
- **400 Bad Request** - Invalid request data
- **404 Not Found** - Resource not found
- **500 Internal Server Error** - Server error

Error responses include a message:
```json
{
  "error": "Configuration not found for project: my-project"
}
```

---

## Migration from Legacy Format

The legacy flat format is no longer supported as of v2.4.2. If you have old configuration files, convert them to the new sectioned format:

**Legacy (pre-2.4.2):**
```json
{
  "projectId": "...",
  "maxParallelKobolds": 1,
  "wyrmProvider": "openai",
  "wyrmEnabled": true
}
```

**New (2.4.2+):**
```json
{
  "project": { "id": "..." },
  "agents": {
    "wyrm": { "enabled": true, "provider": "openai", "maxParallel": 1 },
    "kobold": { "maxParallel": 1 }
  }
}
```
