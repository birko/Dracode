# Multiple Connections to Same Provider - Feature Update

## 🎯 What Changed

DraCode.Web now allows multiple simultaneous connections to the same provider, with each connection being a separate agent instance on the WebSocket server.

## ✨ Features

### Multiple Agent Instances
- ✅ Connect to the same provider multiple times
- ✅ Each connection is an independent agent instance
- ✅ Separate conversation history per instance
- ✅ Individual task queues per instance
- ✅ Independent reset/disconnect per instance

### Smart Naming
- **First connection**: Shows provider name (e.g., "OpenAI")
- **Additional connections**: Auto-numbered (e.g., "OpenAI #2", "OpenAI #3")
- Clear distinction between multiple instances in tabs

### Connection Count Display
- Provider cards show active connection count
- Updates in real-time as you connect/disconnect
- Visual indicator: "🔗 2 active connections"
- Shows "○ Not connected" when no connections

## 🔧 Technical Implementation

### Code Changes

**File**: `DraCode.Web/src/client.ts`

#### 1. Removed Connection Limit
**Before** (prevented multiple connections):
```typescript
// Check if already connected
if (Array.from(this.agents.values()).some((a) => a.provider === providerName)) {
    alert('Already connected to this provider');
    return;
}
```

**After** (allows multiple connections with smart naming):
```typescript
// Count existing connections to this provider
const existingConnections = Array.from(this.agents.values())
    .filter((a) => a.provider === providerName).length;

// Create a display name with instance number if multiple connections
const displayName = existingConnections > 0 
    ? `${providerName} #${existingConnections + 1}`
    : providerName;
```

#### 2. Updated Tab Creation
Modified `createAgentTab()` to accept both display name and provider name:
```typescript
private createAgentTab(agentId: string, displayName: string, providerName?: string): void {
    // Use displayName for tab, providerName for internal tracking
    const provider = providerName || displayName;
    // ... creates tab with displayName
}
```

#### 3. Connection Count Display
Updated `displayProviders()` to show connection count:
```typescript
const connectionCount = Array.from(this.agents.values())
    .filter((a) => a.provider === provider.name).length;

const connectionStatus = connectionCount > 0
    ? `🔗 ${connectionCount} active connection${connectionCount > 1 ? 's' : ''}`
    : '○ Not connected';
```

#### 4. Real-time Updates
Added provider grid refresh on connection changes:
```typescript
// In closeAgent():
if (this.availableProviders.length > 0) {
    this.displayProviders(this.availableProviders);
}

// In handleServerMessage():
if (response.Status === 'success' || response.Status === 'error') {
    if (this.availableProviders.length > 0) {
        this.displayProviders(this.availableProviders);
    }
}
```

**File**: `DraCode.Web/wwwroot/styles.css`

Added styles for connection count:
```css
.provider-connections {
    font-size: var(--font-size-sm);
    color: var(--text-secondary);
    margin-top: var(--spacing-xs);
    font-weight: 500;
}

.provider-card.connected .provider-connections {
    color: var(--primary-color);
}
```

## 📝 Use Cases

### 1. **Response Comparison**
Connect to the same provider twice with different prompts to compare responses:
```
Tab 1: OpenAI - "Explain quantum computing simply"
Tab 2: OpenAI #2 - "Explain quantum computing technically"
```

### 2. **Parallel Task Execution**
Run multiple independent tasks on the same provider simultaneously:
```
Tab 1: Claude - "Create a REST API"
Tab 2: Claude #2 - "Write unit tests"
Tab 3: Claude #3 - "Generate documentation"
```

### 3. **Context Isolation Testing**
Test that agent instances don't share context:
```
Tab 1: Gemini - Set up project A
Tab 2: Gemini #2 - Set up project B (should not know about project A)
```

### 4. **Load Testing**
Test provider performance with multiple concurrent connections:
```
Multiple connections to same provider with identical tasks
Compare response times and quality
```

### 5. **A/B Testing**
Test different approaches with the same provider:
```
Tab 1: OpenAI - Approach A
Tab 2: OpenAI #2 - Approach B
Compare results side-by-side
```

## 🎨 Visual Design

### Provider Cards
- **No connections**: Gray text "○ Not connected"
- **One connection**: Blue text "🔗 1 active connection"
- **Multiple connections**: Blue text "🔗 3 active connections"
- Updates automatically when connections change

### Agent Tabs
- **First instance**: "OpenAI"
- **Second instance**: "OpenAI #2"
- **Third instance**: "OpenAI #3"
- Clear distinction in tab names

### Connection Status
- Provider card shows highlighted border when connected
- Connection count updates in real-time
- Visual feedback when connecting/disconnecting

## 🧪 Testing

### Manual Testing Steps

1. **Start the application**:
   ```bash
   dotnet run --project DraCode.AppHost
   ```

2. **Connect to server**:
   - Open http://localhost:5001
   - Click "Connect to Server"

3. **Test multiple connections**:
   - Click on a provider card (e.g., "OpenAI")
   - Wait for connection confirmation
   - Click the same provider card again
   - Observe: New tab "OpenAI #2" created
   - Click again: "OpenAI #3" created

4. **Verify independence**:
   - Send different tasks to each instance
   - Verify separate activity logs
   - Verify no cross-contamination of context

5. **Test connection count**:
   - Observe provider card shows "🔗 3 active connections"
   - Close one tab
   - Observe count updates to "🔗 2 active connections"

6. **Test reconnection after closing**:
   - Close all instances of a provider
   - Observe count shows "○ Not connected"
   - Reconnect: Should be named without number again

## 🔄 Behavior

### Agent Naming Logic
```
No existing connections → "OpenAI"
1 existing connection   → "OpenAI #2"
2 existing connections  → "OpenAI #3"
...
```

### Connection Count Logic
```
0 connections → "○ Not connected" (gray)
1 connection  → "🔗 1 active connection" (blue)
2+ connections → "🔗 N active connections" (blue)
```

### Provider Grid Refresh
Updates when:
- New agent connects
- Agent disconnects (tab closed)
- Connection status changes (success/error)

## 🌟 Benefits

### 1. **Flexibility**
- No artificial limits on connections
- Use as many instances as needed
- Independent operation of each instance

### 2. **Productivity**
- Parallel task execution
- Compare responses side-by-side
- No need to wait for one task to finish

### 3. **Testing**
- Context isolation verification
- Performance comparison
- A/B testing capabilities

### 4. **Transparency**
- Clear visual feedback on connection count
- Easy to track active instances
- Numbered tabs for easy reference

### 5. **Resource Management**
- Users control number of connections
- Can close unused instances
- Real-time status updates

## ⚠️ Considerations

### Server Load
- Each connection creates a separate agent instance on the server
- More connections = more server resources used
- Users should be mindful of resource usage

### API Rate Limits
- Multiple instances may hit rate limits faster
- Each instance makes independent API calls
- Consider provider rate limits when using multiple connections

### Cost Management
- Multiple instances = multiple API calls
- Token usage multiplies with number of connections
- Monitor costs when using multiple instances

## 🚀 Future Enhancements

Possible improvements:
- [ ] Configurable max connections per provider
- [ ] Resource usage indicator per instance
- [ ] Batch operations across multiple instances
- [ ] Connection templates (save connection configs)
- [ ] Instance grouping/organization
- [ ] Cross-instance comparison view
- [ ] Connection queue management
- [ ] Shared context mode (optional)

## 📊 Technical Details

### Agent Identification
- Each agent has unique `agentId`: `agent-${provider.name}-${Date.now()}`
- Timestamp ensures uniqueness
- Server handles multiple agents with same provider

### Internal Tracking
```typescript
agents.set(agentId, {
    provider: provider,        // e.g., "openai" (for filtering)
    name: displayName,         // e.g., "OpenAI #2" (for display)
    tabElement: tab,
    contentElement: content
});
```

### Connection Count Calculation
```typescript
const connectionCount = Array.from(this.agents.values())
    .filter((a) => a.provider === provider.name).length;
```

Filters agents by provider name, counts matches.

## 📋 Checklist

- ✅ Removed connection limit check
- ✅ Added smart naming with instance numbers
- ✅ Updated tab creation with display name
- ✅ Added connection count display
- ✅ Added real-time grid refresh
- ✅ Added CSS styling for connections
- ✅ TypeScript compiled successfully
- ✅ Documentation written
- ⏳ User testing pending

## 🎯 Impact

### Before
- ❌ Could only connect to each provider once
- ❌ Had to disconnect to start new conversation
- ❌ No parallel task execution
- ❌ Limited comparison capabilities

### After
- ✅ Unlimited connections per provider
- ✅ Multiple independent conversations
- ✅ Parallel task execution
- ✅ Side-by-side comparison
- ✅ Clear visual feedback on connections

---

**Feature Version**: 2.0.4  
**Date Added**: January 22, 2026  
**Status**: ✅ Complete
