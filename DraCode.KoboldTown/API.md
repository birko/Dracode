# KoboldTown API Documentation

## REST API Endpoints

All REST endpoints return JSON responses.

### GET `/api/hierarchy`

Returns complete system hierarchy including statistics, projects, and agent relationships.

**Response:**
```json
{
  "statistics": {
    "dragonSessions": 1,
    "projects": 3,
    "wyrms": 2,
    "drakes": 4,
    "koboldsWorking": 8
  },
  "projects": [
    {
      "id": "proj-123",
      "name": "E-Commerce API",
      "status": "Analyzed",
      "wyrmId": "wyrm-1",
      "createdAt": "2026-01-26T19:00:00Z",
      "analyzedAt": "2026-01-26T19:02:00Z",
      "outputPath": "./workspace/e-commerce-api",
      "specificationPath": "./specifications/e-commerce-api.md",
      "taskFiles": {
        "backend": "backend-tasks.md"
      }
    }
  ],
  "hierarchy": {
    "dragon": {
      "name": "Dragon Requirements Agent",
      "icon": "🐉",
      "status": "active",
      "activeSessions": 1
    },
    "projects": [...]
  }
}
```

---

### GET `/api/projects`

Returns list of all projects with full details.

**Response:**
```json
[
  {
    "id": "proj-123",
    "name": "E-Commerce API",
    "status": "Analyzed",
    "wyrmId": "wyrm-1",
    "createdAt": "2026-01-26T19:00:00Z",
    "updatedAt": "2026-01-26T19:02:00Z",
    "analyzedAt": "2026-01-26T19:02:00Z",
    "outputPath": "./workspace/e-commerce-api",
    "specificationPath": "./specifications/e-commerce-api.md",
    "analysisOutputPath": "./workspace/e-commerce-api/analysis.md",
    "taskFiles": {
      "backend": "backend-tasks.md",
      "frontend": "frontend-tasks.md"
    },
    "errorMessage": null,
    "metadata": {}
  }
]
```

---

### GET `/api/stats`

Returns system-wide statistics.

**Response:**
```json
{
  "projects": {
    "totalProjects": 5,
    "newProjects": 1,
    "wyrmAssignedProjects": 1,
    "analyzedProjects": 2,
    "inProgressProjects": 1,
    "completedProjects": 0,
    "failedProjects": 0
  },
  "dragon": {
    "activeSessions": 1,
    "totalSpecifications": 5
  },
  "drakes": 4,
  "wyrms": 2,
  "koboldsWorking": 8
}
```

---

## WebSocket API

### Dragon Chat WebSocket

**Endpoint:** `ws://localhost:5000/dragon`

Dragon WebSocket provides bidirectional communication for requirements gathering.

#### Client → Server Messages

**User Message:**
```json
{
  "message": "I need a REST API for customer management"
}
```

#### Server → Client Messages

**Dragon Response:**
```json
{
  "type": "dragon_message",
  "sessionId": "session-123",
  "message": "Great! Let me help you with that. Can you tell me more about the customer data you need to manage?",
  "timestamp": "2026-01-26T19:00:00Z"
}
```

**Typing Indicator:**
```json
{
  "type": "dragon_typing",
  "sessionId": "session-123"
}
```

**Specification Created:**
```json
{
  "type": "specification_created",
  "sessionId": "session-123",
  "filename": "customer-management-api.md",
  "path": "./specifications/customer-management-api.md",
  "timestamp": "2026-01-26T19:05:00Z"
}
```

**Error:**
```json
{
  "type": "error",
  "sessionId": "session-123",
  "message": "Error message",
  "timestamp": "2026-01-26T19:00:00Z"
}
```

---

### Wyvern Task WebSocket (Legacy)

**Endpoint:** `ws://localhost:5000/ws`

⚠️ **Note:** Direct task submission is deprecated. Use Dragon for all project creation.

#### Client → Server Messages

**Submit Task:**
```json
{
  "action": "submit_task",
  "task": "Create a React component"
}
```

**Get Tasks:**
```json
{
  "action": "get_tasks"
}
```

**Get Markdown Report:**
```json
{
  "action": "get_markdown"
}
```

#### Server → Client Messages

**Task Created:**
```json
{
  "type": "task_created",
  "taskId": "task-123",
  "task": "Create React component",
  "status": "unassigned",
  "timestamp": "2026-01-26T19:00:00Z"
}
```

**Status Update:**
```json
{
  "type": "status_update",
  "taskId": "task-123",
  "status": "working",
  "assignedAgent": "react-agent",
  "timestamp": "2026-01-26T19:01:00Z"
}
```

**Agent Message:**
```json
{
  "type": "agent_message",
  "taskId": "task-123",
  "messageType": "info",
  "content": "Processing task...",
  "timestamp": "2026-01-26T19:01:00Z"
}
```

**Tasks List:**
```json
{
  "type": "tasks_list",
  "tasks": [...]
}
```

**Markdown Report:**
```json
{
  "type": "markdown_report",
  "markdown": "# Task Report\n..."
}
```

---

## Project Status Enum

```
New           - Project registered, no Wyrm assigned
WyrmAssigned  - Wyrm assigned, not yet analyzed
Analyzed      - Wyrm completed analysis, tasks created
InProgress    - Tasks being executed by Drakes/Kobolds
Completed     - All tasks completed successfully
Failed        - Error occurred during processing
```

---

## Task Status Enum (Legacy)

```
unassigned      - Task submitted, awaiting orchestrator
notinitialized  - Agent selected, not started
working         - Agent actively processing
done            - Task completed successfully
```

---

## Rate Limits

- REST endpoints: No rate limiting
- WebSocket: No rate limiting
- Background services: 60-second intervals (configurable)

---

## Error Responses

All errors follow this format:

```json
{
  "type": "error",
  "error": "Error description",
  "timestamp": "2026-01-26T19:00:00Z"
}
```

Common error codes:
- `404` - Resource not found
- `400` - Bad request format
- `500` - Internal server error

---

## Authentication

Currently, no authentication is required for local development.

For production deployment, consider:
- API key authentication for REST endpoints
- Token-based auth for WebSocket connections
- IP whitelisting
- Rate limiting

---

## Example Usage

### JavaScript/TypeScript

```javascript
// REST API
const response = await fetch('http://localhost:5000/api/hierarchy');
const data = await response.json();
console.log(data.statistics);

// Dragon WebSocket
const ws = new WebSocket('ws://localhost:5000/dragon');
ws.onopen = () => {
  ws.send(JSON.stringify({
    message: "I need a REST API"
  }));
};
ws.onmessage = (event) => {
  const data = JSON.parse(event.data);
  console.log(data);
};
```

### C#

```csharp
// REST API
var client = new HttpClient();
var response = await client.GetAsync("http://localhost:5000/api/hierarchy");
var json = await response.Content.ReadAsStringAsync();
var data = JsonSerializer.Deserialize<HierarchyResponse>(json);

// WebSocket
var ws = new ClientWebSocket();
await ws.ConnectAsync(new Uri("ws://localhost:5000/dragon"), CancellationToken.None);
var message = JsonSerializer.Serialize(new { message = "I need a REST API" });
await ws.SendAsync(
    Encoding.UTF8.GetBytes(message),
    WebSocketMessageType.Text,
    true,
    CancellationToken.None
);
```

---

## Monitoring

Use the built-in UI pages:
- `/` - Status Monitor
- `/dragon.html` - Dragon Chat
- `/hierarchy.html` - Hierarchy Visualization

All provide real-time monitoring of the system state.
