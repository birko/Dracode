# Dragon Project Selection Feature

## Overview

The Dragon page now requires users to select a project and configure the AI provider before starting a chat session. This ensures that all specifications created are properly associated with a project.

## Architecture

The feature uses a client-server architecture:
- **Client** (DraCode.KoboldLair.Client): Serves the web UI and proxies API requests
- **Server** (DraCode.KoboldLair.Server): Provides REST API endpoints and WebSocket connections
- **Service Discovery**: Aspire automatically resolves server URLs for the client

### API Proxy

The Client acts as a proxy for API requests:
```
Browser → Client (/api/projects) → Server (http://dracode-koboldlair-server/api/projects) → Response
```

This eliminates CORS issues and simplifies configuration since the browser only talks to one origin.

### Backend Changes (`Program.cs`)

**Added API Proxy to Client:**
- Configured HttpClient with Aspire service discovery
- Added `MapGet("/api/{**path}")` to proxy GET requests to server
- Added `MapPost("/api/{**path}")` to proxy POST requests to server  
- Handles error cases with proper status codes

**Service Discovery:**
```csharp
builder.Services.AddHttpClient("KoboldLairServer", client =>
{
    client.BaseAddress = new Uri("http://dracode-koboldlair-server");
})
.AddServiceDiscovery(); // Aspire resolves this automatically
```

## Changes Made

### 1. UI Updates (`dragon.html`)

- **Added Project Setup Panel**: A new initial screen that appears before the chat interface
  - Project selection dropdown (loads existing projects from API)
  - Option to create a new project with a custom name
  - Provider selection dropdown (shows only Dragon-compatible providers)
  - "Start Chat" button that becomes enabled when both project and provider are selected

- **Enhanced Chat Interface**: 
  - Added header showing current project name and selected provider
  - "Change Project" button to return to setup screen
  - Chat container is hidden initially and only shown after project setup

### 2. Styling (`dragon.css`)

Added new CSS classes for the project setup interface:
- `.project-setup` - Main container for setup panel
- `.setup-card` - Styled card for form elements
- `.form-group`, `.form-select`, `.form-input` - Form styling
- `.new-project-form` - Collapsible form for new project creation
- `.btn-primary`, `.btn-secondary`, `.btn-link` - Button styles
- `.chat-header` - Header bar showing current project/provider info

### 3. JavaScript Logic (`dragon.js`)

**New Properties:**
- `currentProject` - Stores selected or newly created project
- `currentProvider` - Stores selected AI provider
- `projects` - List of available projects from API
- `providers` - List of available AI providers from API

**New Methods:**
- `loadProjects()` - Fetches projects from `/api/projects`
- `loadProviders()` - Fetches providers from `/api/providers`
- `populateProjectSelect()` - Populates project dropdown
- `populateProviderSelect()` - Populates provider dropdown (filtered for Dragon compatibility)
- `showNewProjectForm()` - Shows form for creating new project
- `hideNewProjectForm()` - Hides new project form
- `createNewProject()` - Creates temporary project object for new projects
- `updateStartButton()` - Enables/disables start button based on selections
- `startChat()` - Initiates chat after project/provider selection
- `showProjectSetup()` - Returns to setup screen from chat

**Modified Workflow:**
1. Page loads → Shows project setup panel
2. User selects existing project or creates new one
3. User selects Dragon AI provider
4. User clicks "Start Chat" → Setup panel hides, chat interface shows
5. WebSocket connection establishes and chat begins
6. User can click "Change Project" to return to setup

## API Endpoints Used

- `GET /api/projects` - Retrieves list of all projects
- `GET /api/providers` - Retrieves list of AI providers with configuration status

## User Experience Flow

### First-time User:
1. Opens Dragon page
2. Sees "Start Your Project" screen
3. Clicks "+ Create New Project" (or selects from dropdown)
4. Enters project name
5. Selects AI provider (e.g., "OpenAI GPT-4")
6. Clicks "Start Chat"
7. Chat interface appears with project context
8. Can now discuss requirements with Dragon

### Returning User:
1. Opens Dragon page
2. Selects existing project from dropdown
3. Optionally changes provider
4. Clicks "Start Chat"
5. Continues conversation with project context

## Benefits

1. **Better Organization**: All specifications are tied to projects
2. **Provider Flexibility**: Users can choose which AI provider Dragon uses per session
3. **Clear Context**: Chat header shows current project and provider at all times
4. **Easy Switching**: Users can change projects without refreshing the page
5. **Validation**: Can't start chat without proper setup, preventing orphaned specifications

## Technical Notes

- Project selection is stored in client-side JavaScript state
- New projects are created with temporary IDs (e.g., `new-1234567890`)
- When Dragon creates a specification, it auto-registers the project on the server
- Provider selection only shows enabled and configured providers that support Dragon
- The UI is fully responsive and matches the existing Dragon page aesthetic

## Future Enhancements

Potential improvements:
- Save last used project/provider in localStorage
- Add project description field
- Show project metadata (created date, last modified)
- Allow provider switching mid-chat
- Add project settings configuration before chat
