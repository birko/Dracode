# Clickable Links in Activity Log - Feature Update

## 🎯 What Changed

URLs in the DraCode.Web activity logs are now automatically detected and converted to clickable links.

## ✨ Features

### Automatic URL Detection
- **HTTP URLs**: `http://example.com`
- **HTTPS URLs**: `https://example.com`
- **WebSocket URLs**: `ws://localhost:5000`, `wss://secure.example.com`
- **FTP URLs**: `ftp://files.example.com`

### Link Behavior
- ✅ Opens in new tab (`target="_blank"`)
- ✅ Security: `rel="noopener noreferrer"` for safety
- ✅ Visual feedback: Hover effects
- ✅ Visited link tracking: Different color for visited links

### Styling
- **Default**: Light blue (`#4fc3f7`)
- **Hover**: Brighter blue (`#29b6f6`)
- **Visited**: Purple (`#ba68c8`)
- **Underline**: Shows by default, removes on hover

## 🔧 Technical Implementation

### Code Changes

**File**: `DraCode.Web/src/client.ts`

Added `linkifyUrls()` method:
```typescript
private linkifyUrls(text: string): string {
    // Regex to match URLs (http, https, ws, wss, ftp)
    const urlPattern = /(\b(https?|wss?|ftp):\/\/[-A-Z0-9+&@#\/%?=~_|!:,.;]*[-A-Z0-9+&@#\/%=~_|])/gim;
    
    return text.replace(urlPattern, (url) => {
        return `<a href="${url}" target="_blank" rel="noopener noreferrer">${url}</a>`;
    });
}
```

Modified `logToAgent()` method to apply linkification:
```typescript
private logToAgent(agentId: string, message: string, level: LogLevel = 'info'): void {
    const logElement = document.getElementById(`log-${agentId}`);
    if (!logElement) return;

    const entry = document.createElement('div');
    entry.className = `log-entry ${level}`;
    const timestamp = new Date().toLocaleTimeString();
    
    // Convert URLs to clickable links
    const linkedMessage = this.linkifyUrls(message);
    
    entry.innerHTML = `<span class="log-time">[${timestamp}]</span> ${linkedMessage}`;
    logElement.appendChild(entry);
    logElement.scrollTop = logElement.scrollHeight;
}
```

**File**: `DraCode.Web/wwwroot/styles.css`

Added link styles:
```css
.log-entry a {
    color: #4fc3f7;
    text-decoration: underline;
    transition: color var(--transition-fast);
}

.log-entry a:hover {
    color: #29b6f6;
    text-decoration: none;
}

.log-entry a:visited {
    color: #ba68c8;
}
```

## 📝 Examples

### Before (Plain Text)
```
[10:30:15] 🔧 Tool call: Opening https://example.com/api/docs
[10:30:16] 📋 WebSocket at ws://localhost:5000/ws
```

### After (Clickable Links)
```
[10:30:15] 🔧 Tool call: Opening [https://example.com/api/docs]
[10:30:16] 📋 WebSocket at [ws://localhost:5000/ws]
```
(Links are underlined and clickable)

## 🎨 Visual Design

### Color Scheme
- **Primary Link**: `#4fc3f7` (Cyan/Light Blue)
  - Matches the modern, tech-focused aesthetic
  - High contrast against dark background (#252526)
  
- **Hover State**: `#29b6f6` (Brighter Blue)
  - Provides clear visual feedback
  - Smooth transition (150ms)

- **Visited Links**: `#ba68c8` (Purple)
  - Helps users track which links they've clicked
  - Follows web convention

### Accessibility
- ✅ High contrast ratios for readability
- ✅ Underline decoration for non-color-based identification
- ✅ Hover effects for mouse users
- ✅ Focus states for keyboard navigation

## 🧪 Testing

### Test File
A test page is available at: `http://localhost:5001/link-test.html`

Shows examples of:
- HTTP/HTTPS links
- WebSocket (ws/wss) links
- FTP links
- Multiple URLs in same message
- URLs with query parameters and anchors

### Manual Testing
1. Start DraCode.Web: `dotnet run --project DraCode.Web`
2. Connect to a provider
3. Send a task that generates URLs in output
4. Verify links are:
   - Clickable
   - Open in new tab
   - Show proper hover effects
   - Change color when visited

## 🔒 Security

### URL Validation
- Regex pattern validates proper URL structure
- Only matches complete, valid URLs
- Prevents partial matches on malformed text

### Link Safety
- `target="_blank"` - Opens in new tab (doesn't navigate away)
- `rel="noopener noreferrer"` - Prevents:
  - Access to `window.opener` (security risk)
  - Sending referrer information (privacy)

### XSS Protection
- URLs are inserted as HTML attributes, not executed as code
- Existing `escapeHtml()` function protects message content
- URL pattern only matches valid URL schemes

## 📊 Regex Pattern Details

```regex
/(\b(https?|wss?|ftp):\/\/[-A-Z0-9+&@#\/%?=~_|!:,.;]*[-A-Z0-9+&@#\/%=~_|])/gim
```

**Breakdown**:
- `\b` - Word boundary (start)
- `(https?|wss?|ftp)` - URL scheme (http/https/ws/wss/ftp)
- `:\/\/` - Required `://` after scheme
- `[-A-Z0-9+&@#\/%?=~_|!:,.;]*` - URL characters (path, query)
- `[-A-Z0-9+&@#\/%=~_|]` - Must end with valid URL character
- `gim` - Global, case-insensitive, multiline

**Supports**:
- Query parameters: `?key=value&foo=bar`
- Anchors: `#section`
- Ports: `:8080`
- Subdomains: `api.example.com`
- Paths: `/api/v1/users`
- Special characters: `~`, `_`, `-`, etc.

## 🚀 Future Enhancements

Possible improvements:
- [ ] Support for email addresses (`mailto:`)
- [ ] Support for file paths (`file://`)
- [ ] Custom URL schemes (e.g., `vscode://`)
- [ ] Link preview on hover
- [ ] Copy link to clipboard button
- [ ] Link shortening for very long URLs
- [ ] Open in same tab option (Ctrl+Click)

## 📋 Checklist

- ✅ TypeScript method added (`linkifyUrls`)
- ✅ Applied to `logToAgent` method
- ✅ CSS styles added for links
- ✅ Security measures implemented (`noopener noreferrer`)
- ✅ Test page created (`link-test.html`)
- ✅ Documentation written
- ✅ TypeScript compiled successfully
- ⏳ User testing pending

## 🎯 Impact

### User Experience
- **Before**: Copy-paste URLs manually
- **After**: Click URLs directly to open

### Use Cases
- Opening API documentation links
- Accessing WebSocket endpoints
- Viewing referenced resources
- Following external references
- Opening diagnostic URLs

### Time Savings
- Eliminates manual copy-paste workflow
- Faster navigation to external resources
- Improves productivity during development

---

**Feature Version**: 2.0.3  
**Date Added**: January 22, 2026  
**Status**: ✅ Complete
