# Data Transparency - Quick Reference

## Current State

✅ **All data is REAL** - No mock/demo data in the system  
✅ **Empty state clearly shown** - Users know they're starting fresh  
✅ **Save locations documented** - Users know where data is stored  

## UI Indicators

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

### Mock Data (If Added)
If mock/demo data is ever added, users will see:

**Warning Banner:**
```
⚠️  Demo Mode Active
You are viewing sample data for demonstration.
[Create a real project] to get started.
```

**Item Badges:**
```
[DEMO] Sample Project Name
```

**Footer:**
```
Data Source: Mock Data (for demonstration only)
```

## For Developers

### How to Add Mock Data Indicators

1. **Server-side flag:**
```csharp
return Results.Json(new {
    projects,
    isMockData = true,  // Add this flag
    dataSource = "mock"
});
```

2. **Client-side detection:**
```javascript
if (data.isMockData || checkIfMockData(data)) {
    showMockDataIndicator();
}
```

3. **Item marking:**
```json
{
  "id": "mock-001",
  "name": "Sample Project (DEMO)",
  "_isMock": true
}
```

## User Expectations

Users should ALWAYS be able to tell:

1. ✅ Is this real data or demo data?
2. ✅ Where is my data stored?
3. ✅ Will this data persist?
4. ✅ What happens when I create something?

## Compliance

- [ ] No mock data without indicators
- [ ] Clear empty state messaging
- [ ] Data source always visible
- [ ] Persistence behavior documented

## Files

- `dragon.html` - Info banner HTML
- `dragon.css` - Styling
- `dragon.js` - Detection logic
- `MOCK_DATA_INDICATORS.md` - Full guidelines

## Quick Check

**To verify transparency:**
1. Open Dragon page with empty system
2. Info banner should be visible
3. Dropdown should show helpful message
4. No confusion about data state

**If mock data added:**
1. Warning banner must appear
2. Each item must be marked
3. Footer must show data source
4. Console warning logged
