# DraCode Web Client - User Guide

## Overview

DraCode Web Client is a **Single Page Application (SPA)** for connecting to multiple LLM providers simultaneously via WebSocket. Built with **Birko.Web.Shell** and **Birko.Web.Components** for a modern, responsive interface.

---

## 🚀 Getting Started

### 1. Start the Backend Services

```bash
# Start WebSocket API server (port 5000)
dotnet run --project DraCode.WebSocket

# Start Web Client (port 5001)
dotnet run --project DraCode.Web
```

### 2. Open the Web Client

Navigate to: **https://localhost:5001**

---

## 📱 Interface Overview

The application has **3 main tabs**:

### 1. **🔌 Providers Tab**
Display and connect to available LLM providers.

#### Features:
- **Provider Cards**: Show provider name, type, model, and configuration status
- **Status Badge**: ✅ Configured (green) or ❌ Not Configured (gray)
- **Connect Agent Button**: Create a new agent instance for the provider
- **🔄 Refresh Button**: Reload the provider list from the server

#### How to Use:
1. Make sure you're connected (green badge in header)
2. Click **"Refresh"** to load available providers
3. Find a configured provider (green badge)
4. Click **"Connect Agent"** to create an agent
5. Switch to the **Agents** tab to interact with your agent

---

### 2. **🤖 Agents Tab**
Manage and interact with active agent instances.

#### Features:
- **Agent Cards**: Each active agent shows:
  - Agent ID and provider name
  - Agent log showing real-time activity
  - Task input field
  - **💬 Chat** button: Focus the input field
  - **🔄 Reset** button: Reset the agent state
- **Real-time Logging**: See agent responses and errors in the log
- **Enter Key**: Press Enter in the input field to send tasks

#### How to Use:
1. Connect to a provider from the **Providers** tab first
2. Switch to the **Agents** tab
3. Type your task in the input field
4. Click **"Send"** or press **Enter**
5. View the agent's response in the log

#### Example Tasks:
```
"Write a function that sorts an array in ascending order"
"Explain the difference between React and Vue"
"Create a REST API endpoint for user authentication"
```

---

### 3. **⚙️ Connection Tab**
Configure WebSocket connection settings.

#### Features:
- **Server URL Input**: WebSocket server address (default: `ws://localhost:5000/ws`)
- **🔌 Connect Button**: Establish connection to the server
- **🔌 Disconnect Button**: Close the connection

#### Connection Status:
- **○ Disconnected** (gray): Not connected to server
- **● Connected** (green): Successfully connected

---

## 🔧 Quick Reference

### Connection Workflow
```
1. Open https://localhost:5001
2. Go to ⚙️ Connection tab
3. Click "🔌 Connect"
4. Go to 🔌 Providers tab
5. Click "🔄 Refresh"
6. Click "Connect Agent" on a provider
7. Go to 🤖 Agents tab
8. Send tasks to your agent
```

### Keyboard Shortcuts
- **Enter** in agent input: Send task
- Click tabs to navigate between sections

---

## 🎨 UI Components Used

- **Custom Tabs**: Tab navigation system
- **`b-card`**: Content cards for providers and agents
- **`b-button`**: Action buttons with variants (primary/secondary)
- **`b-input`**: Text input fields
- **`b-badge`**: Status indicators and labels

---

## 🐛 Troubleshooting

### "No providers found"
- Make sure you're connected to the server
- Click **"🔄 Refresh"** to reload the provider list
- Check that backend services are running

### "Cannot send task"
- Verify you have an active agent in the **Agents** tab
- Check that the WebSocket connection is active (green badge)
- Try refreshing the page

### "Connection failed"
- Verify the WebSocket server is running on port 5000
- Check the server URL in the **Connection** tab
- Look for console errors (F12 → Console)

---

## 📊 Technical Details

### Architecture
- **Frontend**: TypeScript with esbuild bundler
- **UI Framework**: Birko.Web.Shell + Birko.Web.Components
- **Communication**: WebSocket (ws://localhost:5000/ws)
- **State Management**: Birko.Web.Core Store<T>

### Message Format
```json
{
  "Command": "task|list|connect|reset",
  "AgentId": "provider-name",
  "Data": "task content or null"
}
```

### Features
- **TypeScript**: Fully typed codebase
- **ES Modules**: Modern JavaScript module system
- **Component-Based**: Custom web components with Shadow DOM
- **Responsive Design**: Mobile-friendly layout
- **Real-time Updates**: WebSocket push notifications

---

## 📝 Development

### Build Commands
```bash
cd DraCode.Web
npm run build          # Build for production
npm run build:dev      # Build for development
npm run watch          # Watch mode with auto-rebuild
npm run type-check     # TypeScript type checking
```

### File Structure
```
DraCode.Web/
├── src/
│   ├── main.ts          # Entry point
│   ├── app-shell.ts     # Main SPA component
│   └── types.ts         # TypeScript types
├── wwwroot/
│   ├── index.html       # HTML container
│   ├── css/             # Birko CSS files (copied during build)
│   │   ├── reset.css
│   │   └── tokens.css
│   └── js/              # Bundled JavaScript
├── build.js             # esbuild configuration
└── package.json
```

---

## 🔐 Security Notes

- API keys are configured on the backend server
- Never expose API keys in the frontend
- Use HTTPS in production environments
- Configure allowed WebSocket origins on the server

---

## 🌐 Compatibility

Chrome 90+, Firefox 88+, Safari 14+, Edge 90+

---

**Built with ❤️ using Birko.Web.Shell, Birko.Web.Components, and TypeScript 5.7**
