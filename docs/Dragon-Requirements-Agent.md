# 🐉 Dragon - Requirements Gathering Agent

## Overview

Dragon is a specialized AI agent designed for interactive requirements gathering. It conducts conversations with users to understand project goals, technical requirements, and specifications, then produces comprehensive documentation that triggers the KoboldLair automated workflow (Wyvern → Drake → Kobold).

**Dragon is the ONLY interactive interface in KoboldLair.** All other agents (Wyvern, Drake, Kobold Planner, Kobold) work automatically in the background.

## Architecture

```
User (via WebSocket)
    ↓
DragonService (/dragon endpoint)
    ↓
DragonAgent (Council Leader)
    ↓ Uses tools:
┌─────────────────────────────────────┐
│ list_projects          → Show all projects
│ delegate_to_council    → Route to council members
└─────────────────────────────────────┘
    ↓ Delegates to Dragon Council:
┌─────────────────────────────────────┐
│ Sage     → Specifications, features, delete features, approval
│ Seeker   → Import existing codebases
│ Sentinel → Git status/init, diffs, commits, merging
│ Warden   → Config, task details, progress, workspace, retry, delete
└─────────────────────────────────────┘
    ↓ Creates
{ProjectsPath}/{project-name}/specification.md (Status: Prototype)
    ↓ User confirms
Sage calls approve_specification
    ↓ Status changes to "New"
ProjectService triggers processing
    ↓ Automatic Background Processing
WyvernProcessingService (every 60s) detects "New" specs
    ↓
Assigns Wyvern → Analyzes → Creates tasks
    ↓
DrakeExecutionService (every 30s) detects analyzed projects
    ↓
Creates Drakes → Summons Kobold Planners → Creates implementation plans
    ↓
Kobolds execute plans step-by-step → generate code
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
- **Custom System Prompt**: Dragon Council leader persona
- **Dragon Council**: Delegates to specialized sub-agents (Sage, Seeker, Sentinel, Warden)
- **Interactive Mode**: Conducts back-and-forth conversations

**Key Features:**
- Routes tasks to appropriate council members
- Coordinates specification creation via Sage
- Imports existing projects via Seeker
- Manages git operations via Sentinel
- Handles configuration via Warden

**Usage:**
```csharp
var provider = new OpenAiProvider(apiKey, "gpt-4o");
var dragon = new DragonAgent(
    provider,
    options,
    getProjects: () => projectService.GetAllProjects(),
    delegateToCouncil: async (member, task) => await councilService.DelegateAsync(member, task)
);

// Start conversation
var response = await dragon.StartSessionAsync("I want to build a web app");

// Continue conversation
var nextResponse = await dragon.ContinueSessionAsync("It should have user login");
```

### 2. DragonService (`Services/DragonService.cs`)

WebSocket service for real-time Dragon conversations with multi-session support.

**Features:**
- **Multi-session management** - Multiple concurrent sessions per WebSocket connection
- **Session persistence** - Sessions survive disconnects and can be resumed
- **Message history** - Up to 100 messages stored per session for replay on reconnect
- **Conversation persistence** - History saved to `dragon-history.json` per project (server-side)
- **Project context loading** - When user loads a project, previous conversation history is loaded and summarized
- **Context isolation** - Switching projects resets Dragon's LLM context to prevent cross-project contamination
- **No client localStorage** - All session data is server-side; client relies on server replay
- **Automatic cleanup** - Expired sessions cleaned up after 10 minutes of inactivity
- Bi-directional messaging
- Typing indicators
- Automatic specification detection
- **Project registration** - Automatically registers projects when specs are created
- Error handling and reconnection support

**Session Architecture:**
```csharp
// Session tracking
ConcurrentDictionary<string, DragonSession> _sessions;
ConcurrentDictionary<string, WebSocket> _sessionWebSockets;

// Session record
public class DragonSession {
    public string SessionId { get; init; }
    public DragonAgent? Agent { get; set; }
    public DateTime LastActivity { get; set; }
    public List<SessionMessage> MessageHistory { get; }
    public string? LastMessageId { get; set; }
    public string? CurrentProjectFolder { get; set; }
    public bool PendingContextReset { get; set; }
}

// Message tracking for replay
public record SessionMessage(string MessageId, string Type, object Data, DateTime Timestamp);
```

**Session Lifecycle:**
1. **New Session**: Created on first connection, assigned unique SessionId
2. **Active**: Messages tracked, LastActivity updated on each interaction
3. **Disconnected**: Session preserved in memory for potential reconnection
4. **Resumed**: On reconnect with sessionId, agent context restored, message history replayed
5. **Expired**: Cleaned up after 10 minutes of inactivity by cleanup timer

**Project Switch Flow:**
1. User asks to "work on project X"
2. Dragon delegates to Sage → `manage_specification` loads the project
3. `OnProjectLoaded` callback fires:
   - Sets `CurrentProjectFolder` on the session
   - If switching from another project: sets `PendingContextReset` flag, clears old message history
   - Loads `dragon-history.json` from the project folder
   - Returns a brief conversation summary (message counts + last 3 user topics)
4. Summary is appended to tool response → Dragon presents it to the user
5. After response is sent: Dragon's LLM conversation history is cleared, only the switch exchange is kept
6. Next user message → Dragon has clean, project-specific context

**Conversation Persistence:**
- History saved to `{project-folder}/dragon-history.json` after each message (fire-and-forget)
- Up to 100 messages persisted per project
- Thread-safe via `_historyLock`
- Loaded automatically when user loads a project specification

**WebSocket Protocol:**

**From Client:**
```json
{
  "message": "I want to build a REST API",
  "sessionId": "optional-session-id",
  "type": "message",
  "provider": "optional-provider-override"
}
```

**To Client:**
```json
{
  "type": "dragon_message",
  "messageId": "unique-message-id",
  "sessionId": "abc123",
  "message": "Great! Tell me more about...",
  "timestamp": "2026-01-26T18:42:25.779Z"
}
```

**Message Types:**
- `dragon_message` - Dragon's response
- `dragon_typing` - Typing indicator (Dragon is thinking)
- `specification_created` - Specification file was created
- `session_resumed` - Session reconnection with history replay info
- `dragon_reloaded` - Agent reloaded with new provider
- `error` - Error occurred (includes errorType: `llm_connection`, `llm_timeout`, `llm_response`, `llm_error`, `general`)

**Session Resume Protocol:**
1. Client reconnects with `sessionId` in query string or message
2. Server sends `session_resumed` with `messageCount` and `lastMessageId`
3. Server replays message history with `isReplay: true` flag
4. Client filters replayed messages via `receivedMessageIds` set to avoid duplicates
5. If session not found: server creates new session, client can replay in-memory messages

### 3. Dragon Tools (`Agents/Tools/`)

Dragon uses the Dragon Council pattern - it has two direct tools and delegates specialized work to council members.

#### Dragon's Direct Tools

| Tool | Purpose |
|------|---------|
| `list_projects` | List all registered projects with status |
| `delegate_to_council` | Route tasks to council members |

#### Dragon Council Tools

Each council member has specialized tools:

**Sage** (Specifications & Features):
| Tool | Purpose |
|------|---------|
| `manage_specification` | Create/edit specification content |
| `manage_features` | Add/update/remove project features |
| `delete_feature` | Delete Draft features from specifications |
| `process_features` | Promote features and trigger analysis |
| `view_specification_history` | View specification version history |
| `approve_specification` | Approve specs (Prototype → New) |

**Seeker** (Project Import):
| Tool | Purpose |
|------|---------|
| `add_existing_project` | Scan and import existing codebases |

**Sentinel** (Git Operations):
| Tool | Purpose |
|------|---------|
| `git_status` | View branch status, merge readiness, init repos |
| `git_diff` | View branch diffs, commit logs, change summaries |
| `git_commit` | Stage and commit changes in project repos |
| `git_merge` | Merge feature branches to main |

**Warden** (Configuration & Monitoring):
| Tool | Purpose |
|------|---------|
| `manage_external_paths` | Add/remove allowed external paths |
| `agent_configuration` | Configure agent providers and models |
| `select_agent` | Select agent type for tasks |
| `retry_analysis` | Retry failed Wyvern analysis (list/retry/status) |
| `agent_status` | View running agents (Drakes, Kobolds) per project |
| `retry_failed_task` | Retry failed tasks by resetting to Unassigned |
| `set_task_priority` | Override task priority to control execution order |
| `view_task_details` | View detailed task info, errors, plan step progress |
| `project_progress` | View completion %, task breakdowns, success rates |
| `view_workspace` | Browse generated output files in workspace |
| `delete_project` | Permanently remove cancelled projects |
| `pause_project` | Temporarily halt project execution |
| `resume_project` | Resume paused or suspended project |
| `suspend_project` | Long-term hold for projects |
| `cancel_project` | Permanently cancel project |

#### Tool Details

##### agent_status

**Purpose**: View running agents (Drakes, Kobolds) per project with real-time status information.

**Actions**:
- `list` - Show all projects with running agents
- `project` - Show details for a specific project
- `summary` - Global overview of all agent activity

**Use Cases**:
- Check which Kobolds are currently working
- Identify stuck or long-running tasks
- Monitor overall system activity
- Debug execution issues

**Example**:
```json
{
  "action": "list"
}
```

**Response**:
```
Projects with Running Agents:

📦 my-web-app (Status: Active)
  🦅 Drakes: 2
  👹 Kobolds: 4 (3 working, 1 assigned)

📦 api-service (Status: Active)
  🦅 Drakes: 1
  👹 Kobolds: 2 (2 working)

Total: 2 projects, 3 Drakes, 6 Kobolds
```

##### retry_failed_task

**Purpose**: Retry failed tasks by resetting their status to Unassigned so Drake can reassign them.

**Actions**:
- `list` - List all failed tasks across all projects
- `retry` - Retry a specific task by task ID
- `retry_all` - Retry all failed tasks for a specific project

**Use Cases**:
- Recover from transient network errors
- Retry after fixing configuration issues
- Resume work after provider API downtime
- Clear failed state after manual intervention

**Examples**:
```json
{
  "action": "list"
}
```

```json
{
  "action": "retry",
  "task_id": "task-abc-123"
}
```

```json
{
  "action": "retry_all",
  "project_id": "proj-guid-here"
}
```

**Response**:
```
Failed Tasks:

📦 my-web-app
  ❌ Create User API endpoint (Failed: Network error after 3 retries)
  ❌ Add authentication middleware (Failed: Provider timeout)

📦 api-service
  ❌ Database migrations (Failed: Task execution timeout)

Total: 3 failed tasks

Use retry_failed_task with action 'retry' and the task_id to retry.
```

#### Two-Stage Specification Workflow

1. Dragon delegates to Sage to create **Prototype** specification
2. User reviews and confirms
3. Sage uses `approve_specification` tool
4. Status changes to **New** → Wyvern processes

This prevents accidental task generation from incomplete specifications.

### 4. Frontend (`wwwroot/dragon.html`, `dragon-view.js`, `dragon.css`)

Interactive chat interface for Dragon conversations.

**Features:**
- Real-time WebSocket communication
- Message formatting (markdown-like)
- Typing indicators and streaming display
- Multi-tab session support
- Session recovery modal (for server restarts)
- Provider selection and agent reload
- Conversation download (text export)
- No localStorage dependency - server is source of truth for conversation history

**UI Components:**
- Chat messages (Dragon 🐉 and User 👤)
- Input textarea with Send button
- Session tabs with connection status indicators
- Provider selection dropdown
- Clear Context / Reload Agent / Download buttons

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
Delegates to Sage (via delegate_to_council)
  ↓
Sage calls manage_specification tool
  ↓
Creates: ./projects/web-app-with-auth/specification.md
  ↓
Returns confirmation to user
  ↓
Frontend shows notification
```

### 4. Automatic Processing by Wyvern

```
Once specification is approved (Prototype → New status):
  ↓
WyvernProcessingService detects new spec (runs every 60s)
  ↓
Wyvern reads specification
  ↓
Creates task files ({area}-tasks.md in project folder)
  ↓
Drake supervisors assign Kobolds
  ↓
Work gets done automatically!
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

### Projects Path

Default: `./projects` (configurable via `appsettings.json` under `KoboldLair.ProjectsPath`)

Each project gets its own folder with all related files:
```
./projects/{project-name}/
    specification.md              # Project specification
    specification.features.json   # Feature list
    dragon-history.json           # Dragon conversation history (auto-persisted)
    {area}-tasks.md               # Task files
    analysis.md                   # Wyvern analysis
    workspace/                    # Generated code
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
- Delegates to Sage to create specification
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
1. Dragon creates specification → git init (automatic)
     ↓
2. User approves spec → Wyrm pre-analyzes
     ↓
3. Wyvern breaks into tasks → creates feature branches (automatic)
     ↓
4. Drake summons Kobolds → creates git worktrees per feature branch (automatic)
     ↓
5. Kobolds execute work in isolated worktrees
     ↓
6. Drake commits completed tasks → cleans up worktrees (automatic)
     ↓
7. Notification: "Feature branch ready for merge!" (automatic, persisted)
     ↓
8. User asks Dragon → Sentinel reviews diff → merges to main (user-initiated)
```

### Git Workflow

Git is fully automated except for the final merge step:

| Stage | Trigger | Git Operation | Actor |
|-------|---------|---------------|-------|
| Init | Project folder created | `git init -b main` | ProjectService |
| Branch | Feature assigned to Wyvern | `git branch feature/{id}-{name}` | Wyvern |
| Worktree | Task assigned to Kobold | `git worktree add .worktrees/{branch}` | Drake |
| Execute | Kobold starts work | Works in `worktree/workspace/` | Kobold |
| Commit | Task completed (Done) | `git add -A && git commit` | Drake |
| Cleanup | After commit | `git worktree remove` | Drake |
| Notify | All feature tasks done | Persist to `notifications.json` | ProjectNotificationService |
| Merge | User requests via Dragon | `git merge --no-ff` | Sentinel (git_merge tool) |

**Parallel Safety**: Multiple Drakes working on different features each get their own git worktree — an isolated filesystem copy that shares the `.git` object store. No branch checkout conflicts.

**External Projects**: For imported codebases (`IsExistingProject=true`), all git operations target the external `SourcePath` rather than the KoboldLair metadata folder.

**Notifications**: Persisted to disk so they survive server restarts. Pushed to Dragon client in real-time or delivered on reconnect.

### Example

```bash
# Step 1: Gather requirements with Dragon
# Visit http://localhost:{client-port}/dragon.html
# Discuss project with Dragon
# Dragon creates: ./projects/my-api/specification.md (Status: Prototype)

# Step 2: Approve specification
# Review spec with Dragon, then Dragon calls approve_specification
# Status changes to "New"

# Step 3: Automatic processing by Wyvern (background service)
# WyvernProcessingService detects "New" status
# Wyvern analyzes and creates:
#   - ./projects/my-api/backend-tasks.md (C# agent)
#   - ./projects/my-api/frontend-tasks.md (React agent)
#   - ./projects/my-api/testing-tasks.md (C# agent)

# Step 4: Automatic execution via Drakes and Kobolds
# DrakeMonitoringService assigns Kobolds to tasks
# Kobolds execute tasks → code output to ./projects/my-api/workspace/
# Project gets built automatically!
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
        Func<List<ProjectInfo>>? getProjects = null,
        Func<string, string, Task<string>>? delegateToCouncil = null
    )

    // Methods
    public async Task<string> StartSessionAsync(string? initialMessage = null)
    public async Task<string> ContinueSessionAsync(string userMessage)

    // Tools (created internally)
    // - ListProjectsTool: Lists all registered projects
    // - DelegateToCouncilTool: Routes tasks to council members (Sage, Seeker, Sentinel, Warden)
}
```

#### Dragon Council Pattern

DragonAgent coordinates with specialized sub-agents via the `delegateToCouncil` callback:
- **Sage**: Specifications, features, project approval
- **Seeker**: Scan/import existing codebases
- **Sentinel**: Git operations, branches, merging
- **Warden**: Agent configuration, limits, external paths

### DragonService

```csharp
public class DragonService : IDisposable
{
    // Constructor
    public DragonService(
        ILogger<DragonService> logger,
        ProviderConfigurationService providerConfigService,
        ProjectService? projectService = null
    )

    // Methods
    public async Task HandleWebSocketAsync(WebSocket webSocket, string? existingSessionId = null)
    public DragonStatistics GetStatistics()
    public void Dispose()

    // Session Configuration
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(10);
    private readonly int _maxMessageHistory = 100;
}

public class DragonSession
{
    public string SessionId { get; init; }
    public DragonAgent? Agent { get; set; }
    public DateTime LastActivity { get; set; }
    public List<SessionMessage> MessageHistory { get; }
    public string? LastMessageId { get; set; }
    public string? CurrentProjectFolder { get; set; }
    public bool PendingContextReset { get; set; }

    // Persistence
    public Task SaveHistoryToFileAsync(string projectFolder, ILogger? logger = null)
    public static Task<List<SessionMessage>?> LoadHistoryFromFileAsync(string projectFolder, ILogger? logger = null)
}

public record SessionMessage(string MessageId, string Type, object Data, DateTime Timestamp);

public class DragonStatistics
{
    public int ActiveSessions { get; set; }
    public int TotalSpecifications { get; set; }
}
```

### SpecificationManagementTool (Sage)

```csharp
public class SpecificationManagementTool : Tool
{
    public override string Name => "manage_specification";
    public override string Description { get; }
    public override object? InputSchema { get; }

    public override string Execute(
        string workingDirectory,
        Dictionary<string, object> input
    )
}
```

### DelegateToCouncilTool (Dragon)

```csharp
public class DelegateToCouncilTool : Tool
{
    public override string Name => "delegate_to_council";
    public override string Description { get; }
    public override object? InputSchema { get; }

    // Delegates to: Sage, Seeker, Sentinel, Warden
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
- Verify `{ProjectsPath}` directory exists and has write permissions
- Check DragonAgent logs
- Ensure ProjectService is properly configured
- Verify `KoboldLair.ProjectsPath` setting in appsettings.json

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
  │   ├── index.html         ← Main entry point
  │   ├── dragon-view.js     ← Dragon chat UI (no localStorage)
  │   ├── websocket.js       ← WebSocket client with reconnection
  │   ├── server-manager.js  ← Server connection config (localStorage)
  │   └── css/styles.css     ← Styling
  └── Program.cs             ← Static files + proxy

DraCode.KoboldLair/
  ├── Agents/
  │   ├── DragonAgent.cs     ← Dragon coordinator agent
  │   ├── SubAgents/
  │   │   └── SageAgent.cs   ← Specifications (passes onProjectLoaded)
  │   └── Tools/
  │       └── SpecificationManagementTool.cs ← Triggers project load callback
  └── ...

DraCode.KoboldLair.Server/
  ├── Services/
  │   └── DragonService.cs   ← WebSocket service, session management,
  │                              conversation persistence, project switching
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
