# Project Structure Context Feature

## Overview

As of this update, Wyvern now captures and persists project file structure information during analysis. This context is automatically passed to Kobolds, helping them understand where files should be created or modified.

## What's Captured

### 1. Existing Files List
- All files in the workspace at analysis time
- Excludes common build/cache directories (.git, node_modules, bin, obj, etc.)
- Limited to first 50 files in Kobold context to avoid token bloat

### 2. Naming Conventions
- Discovered patterns (e.g., "PascalCase for C# classes", "kebab-case for configs")
- Language-specific conventions

### 3. Directory Purposes
- Purpose of each directory (e.g., "src/" → "Main source code")
- Organizational patterns

### 4. File Location Guidelines
- Where specific file types should go (e.g., "controller" → "src/controllers/")
- Type-to-path mappings

### 5. Architecture Notes
- High-level architecture observations
- Project organization principles

## How It Works

### During Wyvern Analysis

1. **File Scanning**: Wyvern scans the workspace directory recursively
2. **LLM Analysis**: Wyvern's LLM analyzes the file structure and specification
3. **Structure Extraction**: Conventions and guidelines are extracted as JSON
4. **Persistence**: Stored in `analysis.json` alongside tasks

### When Kobolds Work

1. **Context Loading**: Drake loads specification + structure from analysis
2. **Context Assembly**: Structure info appended to specification context
3. **Prompt Building**: Kobold includes structure in task prompt
4. **File Operations**: Kobold has guidance for where files belong

## Example Structure Data

```json
{
  "structure": {
    "existingFiles": [
      "src/Program.cs",
      "src/Controllers/HomeController.cs",
      "tests/HomeControllerTests.cs"
    ],
    "namingConventions": {
      "csharp-classes": "PascalCase",
      "config-files": "camelCase.json"
    },
    "directoryPurposes": {
      "src/": "Main application source code",
      "tests/": "Unit and integration tests"
    },
    "fileLocationGuidelines": {
      "controller": "src/Controllers/",
      "model": "src/Models/",
      "test": "tests/"
    },
    "architectureNotes": "ASP.NET Core MVC application with separate test project"
  }
}
```

## Benefits

✅ **Consistency**: Files created in conventional locations
✅ **Resumability**: Structure info persists across sessions
✅ **Context Awareness**: Kobolds understand project organization
✅ **Error Reduction**: Fewer file location mistakes
✅ **Faster Execution**: Less time discovering structure

## Implementation Details

### Files Modified

1. **DraCode.KoboldLair/Models/Agents/ProjectStructure.cs** - New model
2. **DraCode.KoboldLair/Models/Agents/WyvernAnalysis.cs** - Added Structure property
3. **DraCode.KoboldLair/Orchestrators/Wyvern.cs** - Added scanning and analysis methods
4. **DraCode.KoboldLair/Orchestrators/Drake.cs** - Enhanced specification context assembly

### Performance Impact

- **Wyvern Analysis**: +1-3 seconds for structure analysis (one-time cost)
- **Kobold Context**: +100-300 tokens per task (minimal)
- **Disk Storage**: +1-5 KB in analysis.json

### Limitations

- File list capped at 50 files in Kobold context (full list in analysis.json)
- Structure snapshot is point-in-time (not updated during execution)
- Requires workspace to exist with files (empty projects get basic structure)

## Configuration

No configuration required - feature is automatic. To disable structure analysis, you would need to modify `Wyvern.AnalyzeProjectAsync()` to skip the structure scanning step.

## Future Enhancements

Potential improvements:
- Incremental structure updates during task execution
- Per-language structure templates
- User-defined structure rules
- Structure validation warnings
