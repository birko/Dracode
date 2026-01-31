# 🐉 Dragon - Requirements Gathering Agent

## Overview

Dragon is a specialized AI agent designed for interactive requirements gathering. It conducts conversations with users to understand project goals, technical requirements, and specifications, then produces comprehensive documentation that triggers the KoboldLair automated workflow (Wyvern → Drake → Kobold).

**Dragon is the ONLY interactive interface in KoboldLair.** All other agents (Wyvern, Drake, Kobold) work automatically in the background.

## Architecture

```
User (via WebSocket)
    ↓
DragonService (/dragon endpoint)
    ↓
DragonAgent (Interactive conversation)
    ↓ Uses tools:
┌─────────────────────────────────────┐
│ write_specification    → Creates spec (Prototype status)
│ add_existing_project   → Imports existing code
│ approve_specification  → Changes status (Prototype → New)
│ manage_features        → Add/edit features
│ list_projects          → Show all projects
└─────────────────────────────────────┘
    ↓ Creates
./specifications/{project-name}.md (Status: Prototype)
    ↓ User confirms
Dragon calls approve_specification
    ↓ Status changes to "New"
ProjectService triggers processing
    ↓ Automatic Background Processing
WyvernProcessingService (every 60s) detects "New" specs
    ↓
Assigns Wyvern → Analyzes → Creates tasks
    ↓
DrakeMonitoringService assigns Kobolds
    ↓
Kobolds generate code
```

### Project Status Flow

```
Prototype → New → Analyzing → Active → Completed
    ↑           ↑
    │           └── Wyvern picks up
    └── Dragon creates spec (awaiting user approval)
```

## Components

### 1. DragonAgent (`Agents/DragonAgent.cs`)

Specialized agent that inherits from `Agent` with:
- **Custom System Prompt**: Senior requirements analyst persona
- **SpecificationWriterTool**: Saves specifications to markdown
- **Interactive Mode**: Conducts back-and-forth conversations

**Key Features:**
- Asks targeted questions about purpose, scope, requirements
- Uncovers technical details, architecture, integrations
- Confirms understanding before writing specification
- Creates structured markdown documents

**Usage:**
```csharp
var provider = new OpenAiProvider(apiKey, "gpt-4o");
var dragon = new DragonAgent(provider, options, specificationsPath: "./specifications");

// Start conversation
var response = await dragon.StartSessionAsync("I want to build a web app");

// Continue conversation
var nextResponse = await dragon.ContinueSessionAsync("It should have user login");
```

### 2. DragonService (`Services/DragonService.cs`)

WebSocket service for real-time Dragon conversations.

**Features:**
- Session management (one Dragon per WebSocket connection)
- Bi-directional messaging
- Typing indicators
- Automatic specification detection
- **Project registration** - Automatically registers projects when specs are created
- Error handling and reconnection support

**WebSocket Protocol:**

**From Client:**
```json
{
  "message": "I want to build a REST API",
  "sessionId": "optional-session-id"
}
```

**To Client:**
```json
{
  "type": "dragon_message",
  "sessionId": "abc123",
  "message": "Great! Tell me more about...",
  "timestamp": "2026-01-26T18:42:25.779Z"
}
```

**Message Types:**
- `dragon_message` - Dragon's response
- `dragon_typing` - Typing indicator (Dragon is thinking)
- `specification_created` - Specification file was created
- `error` - Error occurred

### 3. Dragon Tools (`Agents/Tools/`)

Dragon uses several specialized tools for project management:

#### SpecificationWriterTool
Writes specifications to markdown files.

```json
{
  "name": "write_specification",
  "description": "Writes a project or task specification to markdown...",
  "parameters": {
    "filename": "project-name.md",
    "content": "# Project Specification\n..."
  }
}
```

#### AddExistingProjectTool (NEW)
Scans and imports existing projects from disk.

```json
{
  "name": "add_existing_project",
  "description": "Scans a directory, analyzes its structure, and registers it as a project",
  "parameters": {
    "path": "/path/to/existing/project",
    "project_name": "optional-name"
  }
}
```

**Features:**
- Auto-detect 50+ technologies (C#, TypeScript, Python, etc.)
- Analyze project structure and dependencies
- Generate initial specification from existing code
- Support for solution files (.sln, .slnx), package managers, configs

#### ProjectApprovalTool (NEW)
Approves specifications to trigger Wyvern processing.

```json
{
  "name": "approve_specification",
  "description": "Approves a project specification (Prototype → New status)",
  "parameters": {
    "project_name": "my-project",
    "confirmation": "yes"
  }
}
```

**Two-Stage Specification Workflow:**
1. Dragon creates **Prototype** specification
2. User reviews and confirms
3. Dragon uses `approve_specification` tool
4. Status changes to **New** → Wyvern processes

This prevents accidental task generation from incomplete specifications.

**All Tools:**
| Tool | Purpose |
|------|---------|
| `write_specification` | Create/update specification files |
| `add_existing_project` | Import existing projects from disk |
| `approve_specification` | Approve specs (Prototype → New) |
| `manage_specification` | Edit specification content |
| `manage_features` | Add/update/remove project features |
| `list_projects` | List all registered projects |

### 4. Frontend (`wwwroot/dragon.html`, `dragon.js`, `dragon.css`)

Interactive chat interface for Dragon conversations.

**Features:**
- Real-time WebSocket communication
- Message formatting (markdown-like)
- Typing indicators
- Specification tracking sidebar
- Responsive design

**UI Components:**
- Chat messages (Dragon 🐉 and User 👤)
- Input textarea with Send button
- Connection status indicator
- Tips and guidelines panel
- Created specifications list

## Workflow

### 1. User Opens Dragon Interface

```
User navigates to /dragon.html
  → WebSocket connects to /dragon
  → DragonService creates new session
  → Dragon sends welcome message
```

### 2. Interactive Conversation

```
User: "I want to build a web app with authentication"
  ↓
Dragon: "Great! Let me understand:
         - Who are the target users?
         - What authentication methods? (email/password, OAuth, etc.)
         - What happens after login?"
  ↓
User: "B2C users, email/password, redirect to dashboard"
  ↓
Dragon: "Understood. Tell me about the dashboard:
         - What data is displayed?
         - Any CRUD operations?
         - Real-time updates needed?"
  ↓
... conversation continues ...
```

### 3. Specification Creation

```
Dragon has enough information
  ↓
Calls write_specification tool
  ↓
Creates: ./specifications/web-app-with-auth.md
  ↓
Returns confirmation to user
  ↓
Frontend shows notification
```

### 4. Use Specification with Wyvern

```
User provides specification to Wyvern:
  "Please break down ./specifications/web-app-with-auth.md into tasks"
  ↓
Wyvern reads specification
  ↓
Creates tasks (API endpoints, UI components, tests, etc.)
  ↓
Drake supervisors assign Kobolds
  ↓
Work gets done!
```

## Specification Format

Dragon creates comprehensive markdown specifications:

```markdown
# Project Name: Web App with Authentication

## Overview
Brief description of the project and its purpose.

## Purpose & Goals
Why this project exists and what problems it solves.

## Scope

### In Scope
- Feature A
- Feature B
- Feature C

### Out of Scope
- Feature X (future consideration)
- Feature Y (not needed)

## Functional Requirements

### User Authentication
- Users can register with email/password
- Email verification required
- Password reset flow via email
- Session management with JWT tokens

### Dashboard
- Display user profile
- Show recent activity
- CRUD operations for user data

## Non-Functional Requirements
- **Performance**: Page load < 2 seconds
- **Security**: HTTPS only, password hashing with bcrypt
- **Scalability**: Support 10K concurrent users
- **Availability**: 99.9% uptime

## Technical Architecture

### Stack
- Frontend: React 18 + TypeScript
- Backend: ASP.NET Core 8 Web API
- Database: PostgreSQL 15
- Authentication: JWT tokens
- Hosting: Azure App Service

### Architecture
```
Client (React) → API Gateway → Auth Service
                             → User Service
                             → Database
```

## User Stories

1. **As a user**, I want to register an account so I can access the platform
   - Acceptance: Email/password form, validation, confirmation email

2. **As a user**, I want to log in so I can see my dashboard
   - Acceptance: Login form, JWT issued, redirect to dashboard

## Success Criteria
- [ ] Users can register and verify email
- [ ] Users can log in and access dashboard
- [ ] Session persists across page refreshes
- [ ] All security requirements met

## Notes & Considerations
- Consider OAuth integration in future (Google, GitHub)
- Mobile app not in initial scope
- Analytics to be added in phase 2
```

## Configuration

### Program.cs Registration

```csharp
// Register DragonService
builder.Services.AddSingleton<DragonService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DragonService>>();
    return new DragonService(
        logger,
        defaultProvider: "openai",
        defaultConfig: new Dictionary<string, string>
        {
            ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            ["model"] = "gpt-4o"
        }
    );
});

// WebSocket endpoint
app.Map("/dragon", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var dragonService = context.RequestServices.GetRequiredService<DragonService>();
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await dragonService.HandleWebSocketAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});
```

### Environment Variables

```bash
# Required for Dragon
export OPENAI_API_KEY="sk-..."

# Or use other providers
export ANTHROPIC_API_KEY="sk-ant-..."
export AZURE_OPENAI_API_KEY="..."
export AZURE_OPENAI_ENDPOINT="https://..."
```

### Specifications Path

Default: `./specifications`

Custom path in agent factory:
```csharp
var dragon = new DragonAgent(provider, options, specificationsPath: "/custom/path");
```

## Dragon's Conversation Style

### Characteristics
- **Friendly & Conversational**: Warm, encouraging tone
- **Methodical**: Asks questions in logical order
- **Thorough**: Digs deep into requirements
- **Clarifying**: Confirms understanding
- **Efficient**: Doesn't overwhelm with too many questions at once

### Question Strategy

**Initial Questions:**
- What are you building?
- Why do you need it?
- Who will use it?

**Deep Dive:**
- What specific features are required?
- Any technical preferences or constraints?
- Performance or scalability requirements?
- Integration with existing systems?

**Confirmation:**
- "Let me summarize what I've learned..."
- "Is this accurate?"
- "Anything I'm missing?"

**Specification:**
- "I have enough information to create the specification!"
- Calls write_specification tool
- Provides confirmation and next steps

## Tips for Users

### Be Specific
❌ "I need a website"
✅ "I need an e-commerce website for selling handmade crafts, targeting individual consumers"

### Mention Technical Preferences
"I prefer React for frontend" or "Must use PostgreSQL"

### Define Success Criteria
"Users should be able to checkout in under 2 minutes"

### Clarify Out of Scope
"Payment integration will come later, not in initial version"

### Provide Context
"This replaces our current Excel-based system"

## Integration with KoboldLair

### Flow

```
1. Dragon creates specification
     ↓
2. User provides spec to Wyvern
     ↓
3. Wyvern breaks into tasks
     ↓
4. Drake summons Kobolds for tasks
     ↓
5. Kobolds execute work
     ↓
6. Project gets built!
```

### Example

```bash
# Step 1: Gather requirements with Dragon
# Visit http://localhost:5000/dragon.html
# Discuss project with Dragon
# Dragon creates: ./specifications/my-api.md

# Step 2: Use with Wyvern
# Visit http://localhost:5000/index.html
# Input: "Please implement the project described in ./specifications/my-api.md"
# Wyvern breaks into tasks:
#   - Create API project structure (C# agent)
#   - Implement endpoints (C# agent)
#   - Write tests (C# agent)
#   - Create API documentation (Diagramming agent)

# Step 3: Execute via Drakes and Kobolds
# Drakes monitor and manage Kobolds
# Kobolds execute tasks
# Project gets built!
```

## API Reference

### DragonAgent

```csharp
public class DragonAgent : Agent
{
    // Constructor
    public DragonAgent(
        ILlmProvider provider,
        AgentOptions? options = null,
        string specificationsPath = "./specifications"
    )

    // Methods
    public async Task<string> StartSessionAsync(string? initialMessage = null)
    public async Task<string> ContinueSessionAsync(string userMessage)
    
    // Properties
    public string SpecificationsPath { get; }
}
```

### DragonService

```csharp
public class DragonService
{
    // Constructor
    public DragonService(
        ILogger<DragonService> logger,
        string defaultProvider = "openai",
        Dictionary<string, string>? defaultConfig = null
    )

    // Methods
    public async Task HandleWebSocketAsync(WebSocket webSocket)
    public DragonStatistics GetStatistics()
}

public class DragonStatistics
{
    public int ActiveSessions { get; set; }
    public int TotalSpecifications { get; set; }
}
```

### SpecificationWriterTool

```csharp
public class SpecificationWriterTool : Tool
{
    public override string Name => "write_specification";
    public override string Description { get; }
    public override object? InputSchema { get; }
    
    public override string Execute(
        string workingDirectory,
        Dictionary<string, object> input
    )
}
```

## Troubleshooting

### Dragon not responding
- Check WebSocket connection status
- Verify OPENAI_API_KEY is set
- Check browser console for errors

### Specifications not being created
- Verify `./specifications` directory permissions
- Check DragonAgent logs
- Ensure tool is properly registered

### Frontend not connecting
- Ensure `/dragon` endpoint is mapped in Program.cs
- Verify WebSocket is enabled: `app.UseWebSockets()`
- Check firewall/proxy settings

## Dragon UI: Project Selection

### Overview
The Dragon page requires users to select a project and configure the AI provider before starting a chat session. This ensures that all specifications created are properly associated with a project.

### User Workflow
1. **Open Dragon Page** → Shows project setup screen
2. **Select/Create Project** → Choose existing or enter new name
3. **Select Provider** → Choose AI provider (e.g., OpenAI GPT-4)
4. **Start Chat** → Button enables when both selections made
5. **Chat with Dragon** → Discuss requirements
6. **Change Project** → Click button in header to switch

### Features
✅ Project selection (existing or new)  
✅ Provider selection (Dragon-compatible only)  
✅ Dynamic WebSocket URL (no hardcoded ports)  
✅ API proxy (no CORS issues)  
✅ Service discovery (automatic URL resolution)  
✅ Change project without refresh  
✅ Shows current project/provider in header  
✅ Validation before chat start

### UI Components

**Project Setup Panel:**
- Project selection dropdown (loads from `/api/projects`)
- "+ Create New Project" option with name input
- Provider selection dropdown (Dragon-compatible providers only)
- "Start Chat" button (enabled when both selected)

**Chat Interface:**
- Header showing current project name and provider
- "Change Project" button to return to setup
- Standard chat interface (messages, input, send button)

### Technical Details

**API Endpoints:**
- `GET /api/projects` - List all projects
- `GET /api/providers` - List AI providers with config status
- `WebSocket /dragon` - Dragon chat connection

**Architecture:**
```
Browser → Client (/api/*) → Server (service discovery) → Response
Browser → WebSocket (direct) → Server (/dragon)
```

**Service Discovery:**
- Client proxies API requests to avoid CORS
- Aspire resolves server URLs automatically
- WebSocket uses dynamic URL from server config

## Quick Reference Card

### Debug Checklist
Open Browser Console (F12) and verify:

✅ **Success indicators:**
```
Configuration loaded: { serverUrl: "ws://localhost:..." }
Connecting to WebSocket: ws://localhost:.../dragon
WebSocket connected
```

❌ **Failure indicators:**
```
Failed to load projects: TypeError: Failed to fetch
WebSocket connection failed
Connection error
```

### Common Issues

| Issue | Check | Fix |
|-------|-------|-----|
| Projects not loading | Server running? | Start Server in Aspire Dashboard |
| Connection error | Config loaded? | Check `/api/config` endpoint |
| Wrong WebSocket URL | Using localhost:5000? | Ensure config loads dynamically |
| CORS errors | Direct to Server? | Should go through Client proxy |

### Testing Commands
```bash
# Test configuration endpoint
curl http://localhost:{client-port}/api/config

# Test projects endpoint  
curl http://localhost:{client-port}/api/projects

# Test providers endpoint
curl http://localhost:{client-port}/api/providers

# Test WebSocket (in browser console)
fetch('/api/config')
  .then(r => r.json())
  .then(c => new WebSocket(c.serverUrl + '/dragon'))
```

### File Locations
```
DraCode.KoboldLair.Client/
  ├── wwwroot/
  │   ├── dragon.html        ← UI with project selection
  │   ├── css/dragon.css     ← Styling
  │   └── js/
  │       ├── config.js      ← Static config (fallback)
  │       └── dragon.js      ← Main logic with project setup
  └── Program.cs             ← Proxy + /api/config

DraCode.KoboldLair.Server/
  ├── Agents/
  │   ├── AgentFactory.cs    ← Creates Dragon, Wyrm, Drake agents
  │   └── DragonAgent.cs     ← Dragon agent implementation
  ├── Services/
  │   └── DragonService.cs   ← WebSocket service for Dragon
  └── Program.cs             ← API endpoints + WebSocket /dragon
```

## Future Enhancements

- [ ] Upload existing docs for context
- [ ] Template-based specifications
- [ ] Multi-language support
- [ ] Voice input integration
- [ ] Specification versioning
- [ ] Collaborative editing
- [ ] Integration with project management tools
- [ ] Auto-populate from similar projects

## Examples

See `Examples/DragonExample.cs` (to be created) for programmatic usage examples.

## Related Documentation

- [Wyvern Project Analyzer](Wyvern-Project-Analyzer.md) - Breaking specifications into tasks
- [Drake Monitoring System](Drake-Monitoring-System.md) - Managing task execution
- [Kobold System](Kobold-System.md) - Worker agents
- [KoboldLair Server README](../DraCode.KoboldLair.Server/README.md) - Complete system overview

## Summary

Dragon 🐉 is your friendly requirements analyst that:
- ✅ Conducts interactive conversations
- ✅ Asks the right questions
- ✅ Understands your project needs
- ✅ Creates comprehensive specifications
- ✅ Integrates seamlessly with KoboldLair

Start your project right with Dragon! 🐉
