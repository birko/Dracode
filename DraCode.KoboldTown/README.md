# DraCode.KoboldTown

A WebSocket-based orchestrator service with a clean vanilla JavaScript, HTML, and CSS frontend for managing AI agent tasks.

## Features

- **WebSocket Server** - Real-time bidirectional communication
- **AI Orchestrator** - Intelligent task routing to specialized agents
- **Task Tracking** - Monitor task status through lifecycle
- **Modular JavaScript** - Clean ES6 modules architecture
- **Modern UI** - Responsive design with dark theme
- **Markdown Reports** - Download task status reports

## Architecture

```
┌─────────────────┐
│   Frontend      │
│  (HTML/CSS/JS)  │
└────────┬────────┘
         │ WebSocket
         ↓
┌─────────────────┐
│  WebSocket      │
│  Endpoint (/ws) │
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│  Orchestrator   │
│  Service        │
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│  Task Tracker   │
│  (Status Mgmt)  │
└─────────────────┘
```

## Project Structure

```
DraCode.KoboldTown/
├── Orchestrator/
│   ├── TaskRecord.cs       - Task data model
│   └── TaskTracker.cs      - Task management & markdown generation
├── Services/
│   └── OrchestratorService.cs - WebSocket handler & orchestrator integration
├── wwwroot/
│   ├── index.html          - Main UI
│   ├── css/
│   │   └── styles.css      - Styling (dark theme, responsive)
│   └── js/
│       ├── main.js         - Application entry point
│       ├── websocket.js    - WebSocket communication module
│       ├── taskManager.js  - Task state management
│       └── ui.js           - UI controller module
├── Program.cs              - Application startup
└── appsettings.json        - Configuration

## Frontend Modules

### websocket.js
- WebSocket connection management
- Automatic reconnection with exponential backoff
- Message routing by type
- Connection status tracking

### taskManager.js
- Task state management
- Filtering by status
- Update notifications
- In-memory task storage

### ui.js
- DOM manipulation
- Event handling
- Task rendering
- Log management
- Status updates

### main.js
- Application initialization
- Module coordination
- WebSocket message handling
- Markdown download

## WebSocket API

### Client → Server Messages

#### Submit Task
```json
{
  "action": "submit_task",
  "task": "Create a React login component"
}
```

#### Get All Tasks
```json
{
  "action": "get_tasks"
}
```

#### Get Single Task
```json
{
  "action": "get_task",
  "taskId": "abc-123"
}
```

#### Get Markdown Report
```json
{
  "action": "get_markdown"
}
```

### Server → Client Messages

#### Task Created
```json
{
  "type": "task_created",
  "taskId": "abc-123",
  "task": "Create React component",
  "status": "unassigned"
}
```

#### Status Update
```json
{
  "type": "status_update",
  "taskId": "abc-123",
  "status": "working",
  "assignedAgent": "react",
  "errorMessage": null
}
```

#### Agent Message
```json
{
  "type": "agent_message",
  "taskId": "abc-123",
  "messageType": "info",
  "content": "Processing task..."
}
```

#### Tasks List
```json
{
  "type": "tasks_list",
  "tasks": [...]
}
```

#### Markdown Report
```json
{
  "type": "markdown_report",
  "markdown": "# Report\n..."
}
```

#### Error
```json
{
  "type": "error",
  "error": "Error message"
}
```

## Task Lifecycle

1. **⚪ unassigned** - Task submitted, awaiting orchestrator
2. **🔵 notinitialized** - Agent selected, not yet started
3. **🟡 working** - Agent actively processing task
4. **🟢 done** - Task completed successfully

## Configuration

Edit `appsettings.json`:

```json
{
  "Orchestrator": {
    "Provider": "openai",
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4o"
  }
}
```

Supported providers: `openai`, `azureopenai`, `claude`, `gemini`, `ollama`, `githubcopilot`

## Running

### Standalone
```bash
dotnet run --project DraCode.KoboldTown
```

### With Aspire AppHost
```bash
dotnet run --project DraCode.AppHost
```

Then navigate to the URL shown (typically `http://localhost:5xxx`)

## Development

### Adding New WebSocket Actions

1. Add handler in `OrchestratorService.ProcessMessageAsync()`
2. Implement action method
3. Add client-side handler in `main.js`

### Modifying UI

- **Styling**: Edit `wwwroot/css/styles.css`
- **Layout**: Edit `wwwroot/index.html`
- **Behavior**: Edit modules in `wwwroot/js/`

### Adding Features

The modular architecture allows easy extension:
- New task filters
- Additional status types
- Custom visualizations
- Export formats

## Dependencies

- **DraCode.Agent** - AI agent framework
- **DraCode.ServiceDefaults** - Aspire service defaults
- **ASP.NET Core** - Web framework
- **System.Net.WebSockets** - WebSocket support

## Browser Support

Modern browsers with:
- WebSocket support
- ES6 modules
- CSS Grid & Flexbox
- Fetch API

## License

See main repository LICENSE file.
