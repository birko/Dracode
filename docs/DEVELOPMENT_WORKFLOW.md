# Development Workflow Guide

## Overview

This document describes the development workflow for the DraCode KoboldLair web client (`DraCode.KoboldLair.Client`) with Birko.Web components, including watch mode for rapid TypeScript development.

## Quick Start

### Option 1: Watch Mode (Recommended for Development)

Watch mode automatically recompiles TypeScript files when you save changes.

```bash
# Terminal 1: Watch TypeScript files
cd DraCode.KoboldLair.Client
npm run watch

# Terminal 2: Run the application
dotnet watch --project DraCode.KoboldLair.Client
```

### Option 2: Manual Build

```bash
# Build manually
cd DraCode.KoboldLair.Client
npm run build

# Run the application
dotnet run --project DraCode.KoboldLair.Client
```

## Full Development Setup

For full-stack development with all services:

```bash
# Terminal 1: Run all services with Aspire
dotnet watch --project DraCode.AppHost

# Terminal 2: Watch KoboldLair.Client TypeScript
cd DraCode.KoboldLair.Client
npm run watch
```

Access points:
- **KoboldLair.Client**: served by the Aspire-launched service (see the dashboard for the URL)
- **Aspire Dashboard**: https://localhost:17094

## File Watching Details

### What's Watched

**DraCode.KoboldLair.Client:**
- `src/**/*.ts` - TypeScript source files

### What's Not Watched

- **Birko.Web packages** - These are source-only libraries consumed via path aliases
- **node_modules** - Dependencies
- **wwwroot/js/**/*.js** - Compiled JavaScript (output files)

## Development Tips

### 1. Fast Refresh Cycle

When working on TypeScript files:
1. Edit `.ts` file in your IDE
2. Save file (Ctrl+S)
3. Watch compiler automatically recompiles
4. Refresh browser (F5) to see changes

Average refresh time: **100-200ms**

### 2. Debugging TypeScript

Add `console.log` or `debugger` statements in your `.ts` files:

```typescript
export class MyView extends BaseComponent {
  protected onMount() {
    console.log('✅ MyView mounted', { data: this._data });
    debugger; // Pauses execution if DevTools open
  }
}
```

### 3. View Compiled JavaScript

Compiled files are in `wwwroot/js/`:

```bash
ls -la DraCode.KoboldLair.Client/wwwroot/js/
```

### 4. Clear Build Artifacts

If you encounter weird caching issues:

```bash
cd DraCode.KoboldLair.Client
rm -rf wwwroot/js/*.js
npm run build  # Rebuild

# Or clean the project
dotnet clean DraCode.KoboldLair.Client/DraCode.KoboldLair.Client.csproj
```

### 5. Check TypeScript Errors

```bash
cd DraCode.KoboldLair.Client
npx tsc --noEmit  # Check without emitting files
```

## IDE Integration

### VS Code

**Recommended Extensions:**
- TypeScript Nightly (for latest TS features)
- .NET Core Test Explorer (for C# tests)

The repo's `.vscode/tasks.json` and `.vscode/launch.json` already include build/watch tasks and launch configs for the KoboldLair server and client.

### JetBrains Rider

1. Open `DraCode.slnx`
2. Use the Run menu to start `DraCode.KoboldLair.Client` (or the AppHost) with auto-refresh

## Common Issues

### Issue 1: File not updating in browser
1. Hard refresh: `Ctrl+Shift+R` (Windows/Linux) or `Cmd+Shift+R` (Mac)
2. Clear browser cache
3. Check compiled JS file exists in `wwwroot/js/`

### Issue 2: TypeScript watch not detecting changes
1. Stop watch process (Ctrl+C)
2. Check file watcher limits: `fs.inotify.max_user_watches`
3. Restart watch process

### Issue 3: Path alias resolution errors
1. Verify `tsconfig.json` paths are correct
2. Check file exists at path location
3. Try `npx tsc --traceResolution` to debug path resolution

## Production Build

Before committing or deploying:

```bash
# Clean build
dotnet clean DraCode.slnx

# Release configuration
dotnet build DraCode.slnx -c Release

# Run tests
dotnet test DraCode.slnx

# Verify no TypeScript errors
cd DraCode.KoboldLair.Client && npx tsc --noEmit
```

## Resources

- **TypeScript Watch Mode**: https://www.typescriptlang.org/docs/handbook/compiler-options.html
- **dotnet watch**: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-watch
- **Birko.Web Docs**: See `README.md` files in Birko.Web.* packages
