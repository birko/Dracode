# Tab Switching Issue - Troubleshooting Guide

## 🐛 Issue

Tab switching between provider instances is not working in DraCode.Web.

## 🔍 Diagnosis

### Added Debug Logging

Modified `client.ts` to add console logging:

```typescript
// In switchToAgent():
console.log('🔄 Switching to agent:', agentId);
console.log('   Available agents:', Array.from(this.agents.keys()));
// ... logs whether agent found

// In tab click handler:
console.log('🖱️ Tab clicked:', displayName, 'agentId:', agentId);
```

### Test Page

Created `tab-test.html` to test basic tab switching functionality:
- Open: `http://localhost:5001/tab-test.html`
- Tests tab switching without WebSocket complexity
- Verifies CSS and event handlers work

## 🧪 Debugging Steps

### 1. Check Browser Console

After connecting to providers:

```javascript
// Open DevTools (F12) and check for:
1. "🖱️ Tab clicked" messages when clicking tabs
2. "🔄 Switching to agent" messages
3. Any error messages
4. Whether agent is found or not
```

### 2. Verify Tab Elements

In browser console:
```javascript
// Check if tabs exist
document.querySelectorAll('.tab').length
// Should match number of connected agents

// Check if content exists
document.querySelectorAll('.tab-content').length
// Should match number of connected agents

// Check active states
document.querySelectorAll('.tab.active').length  // Should be 1
document.querySelectorAll('.tab-content.active').length  // Should be 1
```

### 3. Inspect Agents Map

In browser console:
```javascript
// Access the client instance (if exposed globally)
window.draCodeClient.agents.size
// Should match number of connected agents

// Check agent IDs
Array.from(window.draCodeClient.agents.keys())
```

### 4. Manual Tab Switching Test

In browser console:
```javascript
// Manually switch tabs
document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));

// Activate first tab
document.querySelectorAll('.tab')[0].classList.add('active');
document.querySelectorAll('.tab-content')[0].classList.add('active');
```

## 🔧 Possible Causes

### 1. **Event Listener Not Attached**
- Tab click event listener not being registered
- Check console for "🖱️ Tab clicked" messages

### 2. **AgentId Mismatch**
- Agent stored with one ID, tab created with different ID
- Check console logs for agent IDs

### 3. **Elements Not Found**
- `agent.tabElement` or `agent.contentElement` is undefined
- DOM elements removed but agent still in map

### 4. **CSS Issue**
- `.active` class added but not visible
- Check computed styles in DevTools

### 5. **Z-Index Issue**
- Provider grid overlaying tabs
- Click not reaching tab element

### 6. **Close Button Interfering**
- Click event not using `stopPropagation()` properly
- All clicks treated as close button clicks

## ✅ Solutions

### Solution 1: Verify Event Attachment

Check if event listener is actually attached:

```typescript
// In createAgentTab(), verify this line executes:
tab.addEventListener('click', () => {
    console.log('🖱️ Tab clicked:', displayName, 'agentId:', agentId);
    this.switchToAgent(agentId);
});
```

### Solution 2: Check Agent Storage

Verify agent is stored correctly:

```typescript
// After createAgentTab():
console.log('Stored agent:', agentId, this.agents.get(agentId));
```

### Solution 3: Expose Client for Debugging

In `main.ts`, expose client globally:

```typescript
const client = new DraCodeClient();
(window as any).draCodeClient = client;
```

### Solution 4: Check DOM Hierarchy

Verify tab structure:
```html
<button class="tab">
  Provider Name <span class="tab-close">×</span>
</button>
```

Close button should use `stopPropagation()`:
```typescript
closeBtn.addEventListener('click', (e) => {
    e.stopPropagation();  // ← Must have this
    this.closeAgent(agentId);
});
```

### Solution 5: CSS Debugging

Check if active class is present:
```css
.tab.active {
    background-color: var(--bg-primary);
    color: var(--primary-color);
    border-color: var(--primary-color);
    /* etc */
}
```

## 📝 Quick Fix Checklist

- [ ] Clear browser cache and hard refresh (Ctrl+Shift+R)
- [ ] Check console for "🖱️ Tab clicked" messages
- [ ] Verify tab elements exist in DOM
- [ ] Check agent is in agents Map
- [ ] Test with tab-test.html page
- [ ] Verify CSS .active class applied
- [ ] Check no JavaScript errors in console
- [ ] Verify TypeScript compiled (npm run build)

## 🎯 Expected Behavior

When clicking a tab:

1. **Click Event Fires**
   ```
   Console: 🖱️ Tab clicked: OpenAI agentId: agent-openai-1234567890
   ```

2. **Switch Function Called**
   ```
   Console: 🔄 Switching to agent: agent-openai-1234567890
   Console:    Available agents: [...agent IDs...]
   ```

3. **Agent Found**
   ```
   Console:    ✅ Agent found: OpenAI
   ```

4. **Classes Updated**
   - All `.tab` elements: `active` class removed
   - Clicked `.tab`: `active` class added
   - All `.tab-content` elements: `active` class removed
   - Matching `.tab-content`: `active` class added

5. **Visual Change**
   - Clicked tab highlights (blue border, blue text)
   - Previous tab unhighlights (gray)
   - Content area switches

## 🛠️ Advanced Debugging

### Check Event Listeners

```javascript
// Get event listeners for a tab
getEventListeners(document.querySelectorAll('.tab')[0])
// Should show 'click' listener
```

### Monitor Class Changes

```javascript
// Watch for class changes
const observer = new MutationObserver((mutations) => {
    mutations.forEach(m => {
        if (m.attributeName === 'class') {
            console.log('Class changed:', m.target, m.target.className);
        }
    });
});

document.querySelectorAll('.tab').forEach(tab => {
    observer.observe(tab, { attributes: true });
});
```

### Test Switch Function Directly

```javascript
// If client is exposed globally
window.draCodeClient.switchToAgent('agent-openai-1234567890');
```

## 📞 Need Help?

If issue persists after trying these steps:

1. Check browser console for errors
2. Try tab-test.html to isolate issue
3. Share console logs (especially debug messages)
4. Share browser/OS information
5. Try different browser to rule out browser-specific issues

## 🔄 Workaround

If tab switching doesn't work, you can still:
- Close and reconnect to switch providers
- Use only one connection at a time
- Refresh page to reset state

---

**Status**: Investigating  
**Severity**: High (core functionality affected)  
**Debug logging**: Added in v2.0.4
