# Data Transparency & Mock Data Guidelines

## Current State

✅ **All data is REAL** - No mock/demo data in the system  
✅ **Empty state clearly shown** - Users know they're starting fresh  
✅ **Save locations documented** - Users know where data is stored  

## User Experience

### Empty State (Active)
When no projects exist, users see:

**Info Banner:**
```
ℹ️  Starting Fresh
No projects exist yet. All data is real and will be saved 
when you create your first project.
```

**Dropdown Message:**
```
No projects yet - create your first one below!
```

### Data Expectations

Users should ALWAYS be able to tell:
1. ✅ Is this real data or demo data?
2. ✅ Where is my data stored?
3. ✅ Will this data persist?
4. ✅ What happens when I create something?

## Mock Data Guidelines

**If mock/demo data is ever added**, it MUST be clearly indicated to users.

### Required Visual Indicators

**1. Warning Banner:**
```html
<div class="mock-data-banner">
  <span class="warning-icon">⚠️</span>
  <strong>Demo Mode:</strong> You are viewing sample data. Create real projects to get started.
</div>
```

**2. Item Badges:**
```html
<span class="mock-badge">DEMO</span>
```

**3. Footer Indicator:**
```html
<div class="data-source">
  Data Source: <strong>Mock Data</strong> (for demonstration only)
</div>
```

### CSS Styling

```css
.mock-data-banner {
    background: rgba(245, 158, 11, 0.15);
    border: 2px solid var(--warning-color);
    padding: 1rem 1.5rem;
    border-radius: 0.75rem;
    margin-bottom: 1.5rem;
    display: flex;
    align-items: center;
    gap: 0.75rem;
    animation: pulse 2s ease-in-out infinite;
}

.mock-badge {
    display: inline-block;
    background: rgba(245, 158, 11, 0.2);
    color: var(--warning-color);
    padding: 0.25rem 0.5rem;
    border-radius: 0.25rem;
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    border: 1px solid var(--warning-color);
}

.data-source {
    text-align: center;
    color: var(--text-muted);
    font-size: 0.85rem;
    padding: 0.5rem;
}

.data-source strong {
    color: var(--warning-color);
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.7; }
}
```

### JavaScript Detection

```javascript
// Check if data has mock indicators
function checkIfMockData(data) {
    if (Array.isArray(data) && data.length > 0) {
        // Check first item for mock flag
        if (data[0]._isMock || data[0]._isDemoData) {
            return true;
        }
        
        // Check if all IDs follow mock pattern
        const allMock = data.every(item => 
            item.id && item.id.startsWith('mock-')
        );
        return allMock;
    }
    return false;
}

function showMockDataIndicator() {
    const banner = document.createElement('div');
    banner.className = 'mock-data-banner';
    banner.innerHTML = `
        <span class="warning-icon">⚠️</span>
        <div>
            <strong>Demo Mode Active</strong>
            <p style="margin: 0; font-size: 0.9em;">
                You are viewing sample data for demonstration. 
                <a href="#" onclick="createRealProject()">Create a real project</a> to get started.
            </p>
        </div>
    `;
    
    const container = document.querySelector('.container');
    container.insertBefore(banner, container.firstChild);
}

// Use in API responses
async loadProjects() {
    const response = await fetch('/api/projects');
    const data = await response.json();
    
    if (data.isMockData || checkIfMockData(data.projects)) {
        showMockDataIndicator();
        this.isMockMode = true;
    }
    
    this.projects = data.projects || data;
    this.populateProjectSelect();
}
```

### Server-Side Implementation

**Add mock data flag to responses:**

```csharp
// In Server Program.cs
app.MapGet("/api/projects", (ProjectService projectService, IConfiguration config) =>
{
    var projects = projectService.GetAllProjects();
    var isMockMode = config.GetValue<bool>("UseMockData", false);
    
    return Results.Json(new
    {
        projects,
        isMockData = isMockMode,
        dataSource = isMockMode ? "mock" : "real"
    });
});
```

**Configuration:**
```json
// appsettings.Development.json
{
  "UseMockData": false,  // Set to true to enable demo mode
  "MockDataSettings": {
    "ProjectCount": 5,
    "IncludeSpecifications": true,
    "ShowWarningBanner": true
  }
}
```

### Mock Data Structure

```json
{
  "projects": [
    {
      "id": "mock-001",
      "name": "Sample Todo App (DEMO)",
      "status": "Analyzed",
      "_isMock": true,
      "_demoData": true
    }
  ],
  "isMockData": true,
  "dataSource": "mock"
}
```

## Best Practices

### DO:
- ✅ Show clear, persistent warnings about mock data
- ✅ Use distinct visual styling (colors, badges, borders)
- ✅ Provide easy way to switch to real data mode
- ✅ Log to console when mock mode is active
- ✅ Disable destructive actions on mock data

### DON'T:
- ❌ Hide or obscure mock data indicators
- ❌ Make mock data look identical to real data
- ❌ Allow users to modify mock data without warning
- ❌ Store mock data in real data locations

## Testing Mock Data Indicators

If you add mock data for testing:

1. **Enable mock mode:**
   ```bash
   export USE_MOCK_DATA=true
   ```

2. **Verify indicators appear:**
   - Warning banner at top
   - Badge on each mock item
   - Console warning message
   - Footer shows "Mock Data" source

3. **Test user actions:**
   - Creating new project should switch to real mode
   - Editing mock data should show warning
   - Deleting mock data should be prevented or warned

## Compliance Checklist

Before releasing with mock data:

- [ ] Warning banner visible on all affected pages
- [ ] Mock badge on all mock items
- [ ] Console warning on page load
- [ ] Footer shows data source
- [ ] Documentation updated
- [ ] Configuration option to disable
- [ ] Easy switch to real mode
- [ ] No mock data mixed with real data

## Related Files

- `dragon.html` - Info banner implementation
- `dragon.css` - Info banner styling
- `dragon.js` - Empty state detection
- `Program.cs` - API endpoint responses

## Related Documentation

- [Dragon Requirements Agent](Dragon-Requirements-Agent.md)
- [KoboldLair Client README](../DraCode.KoboldLair.Client/README.md)
- [KoboldLair Server README](../DraCode.KoboldLair.Server/README.md)
