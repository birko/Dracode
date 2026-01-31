# Project Configuration API Documentation

## Overview
The Project Configuration API allows you to manage project-specific settings including resource limits, agent providers, and agent enablement.

## Base URL
All endpoints are available through the client proxy:
```
http://localhost:<client-port>/api/project-configs
```

---

## Endpoints

### 1. Get All Project Configurations
**GET** `/api/project-configs`

Returns all project configurations along with default values.

**Response:**
```json
{
  "defaults": {
    "maxParallelKobolds": 1
  },
  "projects": [
    {
      "projectId": "my-project",
      "projectName": "My Project",
      "maxParallelKobolds": 3,
      "wyrmProvider": "openai",
      "wyrmModel": "gpt-4",
      "wyrmEnabled": true,
      "drakeProvider": "claude",
      "drakeModel": null,
      "drakeEnabled": false,
      "koboldProvider": "openai",
      "koboldModel": "gpt-4",
      "koboldEnabled": true,
      "lastUpdated": "2024-01-27T20:00:00Z"
    }
  ]
}
```

---

### 2. Get Default Configuration
**GET** `/api/project-configs/defaults`

Returns default configuration values.

**Response:**
```json
{
  "maxParallelKobolds": 1
}
```

---

### 3. Get Specific Project Configuration
**GET** `/api/project-configs/{projectId}`

Returns configuration for a specific project.

**Response:**
```json
{
  "projectId": "my-project",
  "projectName": "My Project",
  "maxParallelKobolds": 3,
  "wyrmProvider": "openai",
  "wyrmModel": "gpt-4",
  "wyrmEnabled": true,
  "drakeProvider": "claude",
  "drakeModel": null,
  "drakeEnabled": false,
  "koboldProvider": "openai",
  "koboldModel": "gpt-4",
  "koboldEnabled": true,
  "lastUpdated": "2024-01-27T20:00:00Z"
}
```

---

### 4. Create/Update Project Configuration (Full)
**PUT** `/api/project-configs/{projectId}`

Creates or fully updates a project configuration.

**Request Body:**
```json
{
  "projectName": "My Project",
  "maxParallelKobolds": 3,
  "wyrmProvider": "openai",
  "wyrmModel": "gpt-4",
  "wyrmEnabled": true,
  "drakeProvider": "claude",
  "drakeModel": null,
  "drakeEnabled": false,
  "koboldProvider": "openai",
  "koboldModel": "gpt-4",
  "koboldEnabled": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Project configuration updated",
  "config": { /* updated config */ }
}
```

---

### 5. Partially Update Project Configuration
**PATCH** `/api/project-configs/{projectId}`

Partially updates a project configuration (only specified fields).

**Request Body:**
```json
{
  "maxParallelKobolds": 5,
  "wyrmEnabled": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "Project configuration updated",
  "config": { /* updated config */ }
}
```

---

### 6. Delete Project Configuration
**DELETE** `/api/project-configs/{projectId}`

Deletes a project configuration.

**Response:**
```json
{
  "success": true,
  "message": "Project configuration deleted"
}
```

---

### 7. Get Agent Configuration
**GET** `/api/project-configs/{projectId}/agents/{agentType}`

Gets settings for a specific agent type (wyrm, drake, kobold).

**Response:**
```json
{
  "provider": "openai",
  "model": "gpt-4",
  "enabled": true
}
```

---

### 8. Update Agent Configuration
**PUT** `/api/project-configs/{projectId}/agents/{agentType}`

Updates settings for a specific agent type.

**Request Body:**
```json
{
  "provider": "claude",
  "model": "claude-3-opus",
  "enabled": true
}
```

**Response:**
```json
{
  "success": true,
  "message": "wyrm configuration updated"
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
console.log('Max Kobolds:', config.maxParallelKobolds);
```

#### Update Configuration
```javascript
// Full update
await client.updateConfig('my-project', {
  projectName: 'Updated Project',
  maxParallelKobolds: 5,
  wyrmEnabled: true,
  wyrmProvider: 'openai',
  wyrmModel: 'gpt-4'
});

// Partial update
await client.patchConfig('my-project', {
  maxParallelKobolds: 3
});
```

#### Set Max Parallel Kobolds
```javascript
await client.setMaxParallelKobolds('my-project', 5);
```

#### Toggle Agent
```javascript
// Enable Wyrm
await client.toggleAgent('my-project', 'wyrm', true);

// Disable Drake
await client.toggleAgent('my-project', 'drake', false);
```

#### Set Agent Provider
```javascript
await client.setAgentProvider('my-project', 'kobold', 'claude', 'claude-3-sonnet');
```

#### Get Agent Configuration
```javascript
const wyrmConfig = await client.getAgentConfig('my-project', 'wyrm');
console.log('Wyrm Provider:', wyrmConfig.provider);
console.log('Wyrm Model:', wyrmConfig.model);
console.log('Wyrm Enabled:', wyrmConfig.enabled);
```

#### Delete Configuration
```javascript
await client.deleteConfig('my-project');
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

JavaScript client throws errors that can be caught:
```javascript
try {
  await client.getConfig('non-existent-project');
} catch (error) {
  console.error('Error:', error.message);
}
```

---

## Configuration Fields

| Field | Type | Description |
|-------|------|-------------|
| `projectId` | string | Unique project identifier (required) |
| `projectName` | string? | Display name for project (optional) |
| `maxParallelKobolds` | int | Max concurrent Kobold workers (default: 1) |
| `wyrmProvider` | string? | LLM provider for Wyrm agent |
| `wyrmModel` | string? | Model override for Wyrm |
| `wyrmEnabled` | bool | Whether Wyrm is enabled (default: false) |
| `drakeProvider` | string? | LLM provider for Drake supervisors |
| `drakeModel` | string? | Model override for Drake |
| `drakeEnabled` | bool | Whether Drake is enabled (default: false) |
| `koboldProvider` | string? | LLM provider for Kobold workers (global default) |
| `koboldModel` | string? | Model override for Kobolds (global default) |
| `koboldEnabled` | bool | Whether Kobolds are enabled (default: false) |
| `koboldAgentTypeSettings` | array | Per-agent-type provider settings (see below) |
| `lastUpdated` | DateTime? | Last update timestamp (auto-set) |

### Kobold Agent Type Settings

Configure different LLM providers for different Kobold agent types:

```json
{
  "koboldAgentTypeSettings": [
    { "agentType": "csharp", "provider": "claude", "model": "claude-sonnet-4-20250514" },
    { "agentType": "python", "provider": "openai", "model": "gpt-4o" }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `agentType` | string | Agent type (e.g., "csharp", "python", "react") |
| `provider` | string? | Provider name (null = use global koboldProvider) |
| `model` | string? | Model override (null = use provider default) |

---

## Agent Types

- **`wyrm`** (or `wyvern`) - Project analyzer
- **`drake`** - Supervisor managing Kobold lifecycle
- **`kobold`** - Worker agent executing tasks
