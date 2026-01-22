# Multi-Provider Support - DraCode.Web Client

The DraCode.Web client now supports connecting to multiple LLM providers simultaneously with separate output for each.

## 🎯 Key Features

### 1. Multiple Provider Connections
- Connect to multiple providers at the same time
- Each provider runs as an independent agent
- Separate task inputs and outputs for each agent

### 2. Provider Selection Grid
- Visual grid display of all configured providers
- Shows configuration status (configured/not configured)
- Click any provider card to connect instantly
- Uses server-side configuration automatically

### 3. Tabbed Interface
- Each connected provider gets its own tab
- Switch between providers easily
- Close individual agents without affecting others
- Empty state when no agents are connected

### 4. Hidden Manual Configuration
- Manual provider config is hidden by default
- Click "➕ Add Manual Provider" button to reveal
- Configure custom providers with API keys
- Useful for testing or non-configured providers

## 🚀 How to Use

### Connect to Server
1. Start DraCode.WebSocket server
2. Open http://localhost:5001
3. Click "Connect to Server"
4. Provider list loads automatically

### Connect to Configured Provider
1. Browse the provider grid
2. Click on any provider card (e.g., "openai")
3. A new tab opens for that provider
4. Start sending tasks!

### Connect to Manual Provider
1. Click "➕ Add Manual Provider" button
2. Fill in provider details:
   - Provider type (dropdown)
   - API Key (required)
   - Model (optional)
   - Working Directory (optional)
3. Click "Connect Manual Provider"
4. New tab opens for manual provider

### Work with Multiple Agents
1. Connect to multiple providers (e.g., openai, claude, gemini)
2. Each gets its own tab
3. Switch between tabs to work with different agents
4. Each agent has:
   - Independent task input
   - Separate activity log
   - Reset button
   - Close button (×)

## 📋 UI Components

### Server Connection Section
- WebSocket URL input
- Connect/Disconnect buttons
- List Providers button
- Connection status indicator

### Provider Grid
- Visual cards for each provider
- Shows: name, model, configuration status
- Green border = already connected
- Click to connect

### Agent Tabs
- Tab per connected agent
- Active tab highlighted
- Close button (×) on each tab
- Switches content area

### Agent Content Area
- **Task Section**: Input and send tasks
- **Activity Log**: Real-time agent responses
- **Actions**: Reset agent, clear log

### Manual Configuration Panel (Hidden)
- Toggle with button click
- Provider type selector
- API key input (password field)
- Optional model and directory
- Connect and Cancel buttons

## 💡 Benefits

1. **Compare Providers**: Test same task across multiple LLMs
2. **Parallel Work**: Different tasks for different agents simultaneously
3. **Easy Testing**: Quickly switch between providers
4. **Clean Interface**: Only see what you're using
5. **No Redundancy**: Reuse server configuration when available

## 🔧 Technical Details

### Message Structure
```json
{
  "command": "connect",
  "agentId": "agent-openai-1234567890",
  "config": {
    "provider": "openai"
  }
}
```

### Agent Identification
- Each agent has unique ID: `agent-{provider}-{timestamp}`
- AgentId included in all commands
- Server routes responses to correct agent

### State Management
```javascript
agents = {
  "agent-openai-123": {
    provider: "openai",
    name: "openai",
    tabElement: HTMLElement,
    contentElement: HTMLElement
  },
  "agent-claude-456": { ... }
}
```

## 📱 Responsive Design

- Grid adapts to screen size
- Tabs scroll horizontally on mobile
- Mobile-friendly touch targets
- Responsive typography

## 🎨 Visual Indicators

- **🟢 Green**: Connected status
- **🔵 Blue**: Default/info states
- **🟡 Yellow**: Connecting/processing
- **🔴 Red**: Errors and disconnected
- **⚫ Gray**: Inactive elements

## 🔮 Future Enhancements

Potential features for future versions:
- Drag-and-drop tab reordering
- Save/load workspace configurations
- Export conversation histories
- Side-by-side comparison view
- Agent performance metrics
- Shared task broadcasting (send to all agents)

## 📝 Example Workflow

```
1. Open web client
2. Connect to server
3. See 6 available providers (openai, claude, gemini, etc.)
4. Click "openai" → new tab opens
5. Click "claude" → another tab opens
6. In openai tab: "Explain quantum computing"
7. Switch to claude tab: "Write a poem about AI"
8. Compare responses in their respective logs
9. Close claude tab when done
10. Continue working with openai
```

## ⚠️ Notes

- Server supports multiple agents per WebSocket connection
- Each agent maintains independent state
- Manual providers don't persist (reset on disconnect)
- Configured providers use server-side API keys
- Closing tab disconnects and disposes agent

