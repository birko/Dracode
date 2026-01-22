# Documentation Structure Overview

This file provides a visual overview of the DraCode documentation structure.

## 📂 Directory Tree

```
DraCode/
│
├── README.md                           # Main project overview and quick start
├── LICENSE                             # Project license
│
├── docs/                               # 📚 Central documentation hub
│   ├── README.md                       # Documentation index and navigation
│   ├── CHANGELOG.md                    # Complete version history
│   ├── STRUCTURE_OVERVIEW.md           # This file
│   │
│   ├── architecture/                   # 🏗️ Technical specifications (3 files)
│   │   ├── ARCHITECTURE_SPECIFICATION.md
│   │   ├── TECHNICAL_SPECIFICATION.md
│   │   └── TOOL_SPECIFICATIONS.md
│   │
│   ├── setup-guides/                   # 🚀 Setup and configuration (8 files)
│   │   ├── CLI_OPTIONS.md
│   │   ├── WEBSOCKET_QUICKSTART.md
│   │   ├── WEB_CLIENT_MULTI_PROVIDER_GUIDE.md
│   │   ├── AZURE_OPENAI_SETUP.md
│   │   ├── CLAUDE_SETUP.md
│   │   ├── GEMINI_SETUP.md
│   │   ├── GITHUB_OAUTH_SETUP.md
│   │   └── OLLAMA_SETUP.md
│   │
│   ├── development/                    # 🛠️ Developer documentation (1 file)
│   │   └── IMPLEMENTATION_PLAN.md
│   │
│   └── troubleshooting/               # 🔧 Problem-solving guides (3 files)
│       ├── TROUBLESHOOTING.md
│       ├── PROVIDER_GRID_TROUBLESHOOTING.md
│       └── WEB_CLIENT_DEBUGGING.md
│
├── DraCode/                           # CLI application
├── DraCode.Agent/                     # Agent library
├── DraCode.WebSocket/                 # WebSocket API server
│   └── README.md                      # WebSocket-specific documentation
├── DraCode.Web/                       # Web client
│   └── README.md                      # Web client-specific documentation
├── DraCode.AppHost/                   # .NET Aspire orchestration
│   └── README.md                      # Aspire-specific documentation
└── DraCode.ServiceDefaults/           # Shared Aspire configuration
```

## 🎯 Documentation Categories

### 1. 🏗️ Architecture (`docs/architecture/`)
**Purpose**: Technical specifications and system design documentation

**Target Audience**: Developers, architects, technical leads

**Contents**:
- System architecture and design patterns
- Comprehensive technical specifications
- Tool system documentation

### 2. 🚀 Setup Guides (`docs/setup-guides/`)
**Purpose**: Step-by-step configuration and setup instructions

**Target Audience**: End users, developers, DevOps

**Contents**:
- CLI usage and options
- WebSocket quick start guide
- Provider-specific setup instructions
- Web client configuration

### 3. 🛠️ Development (`docs/development/`)
**Purpose**: Developer-focused documentation and guidelines

**Target Audience**: Contributors, maintainers

**Contents**:
- Implementation plan and roadmap
- Development guidelines
- Contribution guidelines

### 4. 🔧 Troubleshooting (`docs/troubleshooting/`)
**Purpose**: Problem-solving and debugging guides

**Target Audience**: All users experiencing issues

**Contents**:
- General troubleshooting guide
- Component-specific debugging
- Common issues and solutions

### 5. 📝 Version History (`docs/CHANGELOG.md`)
**Purpose**: Complete changelog and version history

**Target Audience**: All users, maintainers

**Contents**:
- Version release notes
- Feature additions and changes
- Bug fixes and improvements
- Migration notes

## 📊 Quick Statistics

| Category | File Count | Purpose |
|----------|------------|---------|
| Architecture | 3 | Technical specifications |
| Setup Guides | 8 | Configuration and setup |
| Development | 1 | Developer documentation |
| Troubleshooting | 3 | Problem-solving guides |
| Root Docs | 3 | Index, changelog, overview |
| **Total** | **18** | **Complete documentation** |

## 🔍 Finding Documentation

### By User Type

#### **End Users** (Using DraCode)
Start here:
1. [Main README](../README.md)
2. [WebSocket Quick Start](setup-guides/WEBSOCKET_QUICKSTART.md)
3. [Provider Setup Guides](setup-guides/)
4. [Changelog](CHANGELOG.md) - See what's new

#### **Developers** (Contributing to DraCode)
Start here:
1. [Architecture Specification](architecture/ARCHITECTURE_SPECIFICATION.md)
2. [Technical Specification](architecture/TECHNICAL_SPECIFICATION.md)
3. [Implementation Plan](development/IMPLEMENTATION_PLAN.md)

#### **Troubleshooters** (Fixing Issues)
Start here:
1. [General Troubleshooting](troubleshooting/TROUBLESHOOTING.md)
2. [Provider Grid Issues](troubleshooting/PROVIDER_GRID_TROUBLESHOOTING.md)
3. [Web Client Debugging](troubleshooting/WEB_CLIENT_DEBUGGING.md)

### By Task

| Task | Documentation |
|------|---------------|
| **First-time setup** | [WebSocket Quick Start](setup-guides/WEBSOCKET_QUICKSTART.md) |
| **Configure OpenAI** | Main [README.md](../README.md) |
| **Configure Claude** | [Claude Setup](setup-guides/CLAUDE_SETUP.md) |
| **Configure Gemini** | [Gemini Setup](setup-guides/GEMINI_SETUP.md) |
| **Configure Azure** | [Azure OpenAI Setup](setup-guides/AZURE_OPENAI_SETUP.md) |
| **Configure Ollama** | [Ollama Setup](setup-guides/OLLAMA_SETUP.md) |
| **Configure GitHub** | [GitHub OAuth Setup](setup-guides/GITHUB_OAUTH_SETUP.md) |
| **Use CLI** | [CLI Options](setup-guides/CLI_OPTIONS.md) |
| **Use web client** | [DraCode.Web README](../DraCode.Web/README.md) |
| **Understand architecture** | [Architecture Spec](architecture/ARCHITECTURE_SPECIFICATION.md) |
| **Contribute code** | [Implementation Plan](development/IMPLEMENTATION_PLAN.md) |
| **Fix provider issues** | [Provider Grid Troubleshooting](troubleshooting/PROVIDER_GRID_TROUBLESHOOTING.md) |
| **Debug web client** | [Web Client Debugging](troubleshooting/WEB_CLIENT_DEBUGGING.md) |
| **See version history** | [Changelog](CHANGELOG.md) |

## 🗺️ Navigation Guide

### Top-Down Navigation
```
Start: Main README
  ↓
  → Setup Guide → Provider Setup → Configuration
  → Architecture → Technical Details → Tool Specs
  → Troubleshooting → Specific Issue → Solution
```

### Bottom-Up Navigation
```
Error Message
  ↓
  → Search Troubleshooting Docs
  ↓
  → Find Root Cause
  ↓
  → Check Architecture/Technical Specs
  ↓
  → Apply Solution
```

## 📱 Quick Access Paths

### Setup Tasks
```
Quick Start:
docs/README.md → setup-guides/WEBSOCKET_QUICKSTART.md → Configure & Run

Provider Setup:
docs/README.md → setup-guides/<PROVIDER>_SETUP.md → API Key → Test
```

### Development Tasks
```
Contributing:
docs/README.md → development/IMPLEMENTATION_PLAN.md → Code → Test

Architecture Review:
docs/README.md → architecture/ARCHITECTURE_SPECIFICATION.md → Understand → Implement
```

### Troubleshooting Tasks
```
Issue Found:
docs/README.md → troubleshooting/TROUBLESHOOTING.md → Specific Guide → Fix

Debug Session:
Issue → troubleshooting/<COMPONENT>_DEBUGGING.md → Debug Steps → Resolution
```

## 🔗 Inter-Document Linking

Documents are cross-linked for easy navigation:
- Setup guides reference architecture docs for technical details
- Troubleshooting guides link to relevant setup guides
- Architecture docs reference implementation plan
- Changelog entries link to related guides

## 📅 Maintenance

### Adding New Documentation
1. Identify the appropriate category
2. Create file in correct subdirectory
3. Update `docs/README.md` index
4. Update main `README.md` if applicable
5. Add cross-references in related docs

### Updating Existing Documentation
1. Make changes to the file
2. Update last modified date
3. Update version if applicable
4. Check and update cross-references
5. Update index if structure changed

### Recording Changes
1. Add entry to `docs/CHANGELOG.md`
2. Follow semantic versioning
3. Include migration notes if breaking
4. Link to related documentation

---

**Last Updated**: January 22, 2026  
**Maintained By**: DraCode Team
