# DraCode Tool Specifications

## Tool System Overview

DraCode implements a tool-based architecture where LLMs can invoke predefined functions to interact with the workspace. This document specifies the tool interface, JSON Schema format, and all available tools.

---

## 1. Tool Interface Definition

### Abstract Base Class

```csharp
public abstract class Tool
{
    // Unique identifier for the tool (used by LLM to reference)
    public abstract string Name { get; }
    
    // Human-readable description for LLM context
    public abstract string Description { get; }
    
    // JSON Schema defining the tool's input parameters
    public abstract object? InputSchema { get; }
    
    // Execution method that performs the tool's action
    public abstract string Execute(string workingDirectory, Dictionary<string, object> input);
}
```

### Tool Contract

- **Name**: Lowercase, snake_case identifier (e.g., `read_file`)
- **Description**: Clear explanation of what the tool does
- **InputSchema**: JSON Schema object defining parameters
- **Execute**: Returns string result (success message or error)

---

## 2. JSON Schema Format

### Standard Structure

```json
{
  "type": "object",
  "properties": {
    "parameter_name": {
      "type": "string | number | boolean | object | array",
      "description": "Human-readable parameter description",
      "default": "optional_default_value"
    }
  },
  "required": ["required_parameter_1", "required_parameter_2"]
}
```

### Supported Types

| Type | JSON Schema | C# Type | Example |
|------|-------------|---------|---------|
| String | `"type": "string"` | `string` | `"hello.txt"` |
| Number | `"type": "number"` | `int`, `double` | `120` |
| Boolean | `"type": "boolean"` | `bool` | `true` |
| Object | `"type": "object"` | `Dictionary<string, object>` | `{"key": "value"}` |
| Array | `"type": "array"` | `List<object>` | `["item1", "item2"]` |

### Special Keywords

- **`description`**: Parameter explanation for LLM
- **`default`**: Default value if parameter not provided (use `@default` in C#)
- **`required`**: Array of required parameter names

---

## 3. Available Tools

### 3.1 list_files

**Purpose**: List files in a directory with optional recursive search

**Name**: `list_files`

**Schema**:
```json
{
  "type": "object",
  "properties": {
    "directory": {
      "type": "string",
      "description": "Optional path relative to workspace root to list files from"
    },
    "recursive": {
      "type": "boolean",
      "description": "List files recursively",
      "default": false
    }
  }
}
```

**Parameters**:
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `directory` | string | No | `"."` | Relative path to list files from |
| `recursive` | boolean | No | `false` | Include subdirectories |

**Return Format**:
```
file1.txt
src/program.cs
src/utils/helper.cs
```

**Example Usage**:
```json
{
  "directory": "src",
  "recursive": true
}
```

**Error Cases**:
- Directory not found: `"Error: Directory not found: {path}"`
- Access denied: `"Error: Access denied. Directory must be in {workspace}"`

---

### 3.2 read_file

**Purpose**: Read the contents of a file

**Name**: `read_file`

**Schema**:
```json
{
  "type": "object",
  "properties": {
    "file_path": {
      "type": "string",
      "description": "Path to the file relative to workspace root"
    }
  },
  "required": ["file_path"]
}
```

**Parameters**:
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `file_path` | string | Yes | - | Relative path to file |

**Return Format**:
```
[File contents as string]
```

**Example Usage**:
```json
{
  "file_path": "src/Program.cs"
}
```

**Error Cases**:
- File not found: `"Error reading file: Could not find file 'path'"`
- Access denied: `"Error: Access denied. File must be in {workspace}"`
- Encoding issues: Returns raw bytes as string (may have issues)

---

### 3.3 write_file

**Purpose**: Create or overwrite a file with text content

**Name**: `write_file`

**Schema**:
```json
{
  "type": "object",
  "properties": {
    "file_path": {
      "type": "string",
      "description": "Path to the file relative to workspace root"
    },
    "content": {
      "type": "string",
      "description": "Text content to write to the file"
    },
    "create_directories": {
      "type": "boolean",
      "description": "Create directories if they do not exist",
      "default": true
    }
  },
  "required": ["file_path", "content"]
}
```

**Parameters**:
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `file_path` | string | Yes | - | Relative path to file |
| `content` | string | Yes | - | Content to write |
| `create_directories` | boolean | No | `true` | Auto-create parent directories |

**Return Format**:
```
OK
```

**Example Usage**:
```json
{
  "file_path": "src/NewClass.cs",
  "content": "public class NewClass { }",
  "create_directories": true
}
```

**Error Cases**:
- Directory not exists (when `create_directories=false`): `"Error: Directory does not exist: {path}"`
- Access denied: `"Error: Access denied. File must be in {workspace}"`
- Disk full: `"Error writing file: {exception}"`

---

### 3.4 search_code

**Purpose**: Search for text or regex patterns in files with line numbers

**Name**: `search_code`

**Schema**:
```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "Text or regex to search for"
    },
    "directory": {
      "type": "string",
      "description": "Optional subdirectory relative to workspace root to search in"
    },
    "pattern": {
      "type": "string",
      "description": "Optional file glob pattern (e.g., *.cs)",
      "default": "*"
    },
    "recursive": {
      "type": "boolean",
      "description": "Search recursively",
      "default": true
    },
    "regex": {
      "type": "boolean",
      "description": "Treat query as regular expression",
      "default": false
    },
    "case_sensitive": {
      "type": "boolean",
      "description": "Case sensitive search",
      "default": false
    }
  },
  "required": ["query"]
}
```

**Parameters**:
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `query` | string | Yes | - | Search term or regex pattern |
| `directory` | string | No | `"."` | Subdirectory to search in |
| `pattern` | string | No | `"*"` | File glob pattern (e.g., `*.cs`, `*.{js,ts}`) |
| `recursive` | boolean | No | `true` | Search subdirectories |
| `regex` | boolean | No | `false` | Treat query as regex |
| `case_sensitive` | boolean | No | `false` | Case-sensitive matching |

**Return Format**:
```
src/Program.cs:15: using System;
src/Program.cs:42:     var result = await agent.RunAsync(task);
src/Agent.cs:10: using System.Collections.Generic;
```

**Example Usage**:
```json
{
  "query": "class.*Agent",
  "pattern": "*.cs",
  "regex": true,
  "recursive": true
}
```

**Binary File Exclusions**:
Files with these extensions are automatically skipped:
- `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp`
- `.pdf`, `.zip`

**Error Cases**:
- Directory not found: `"Error: Directory not found: {path}"`
- Access denied: `"Error: Access denied. Directory must be in {workspace}"`
- Invalid regex: `"Error searching code: {exception}"`

---

### 3.5 run_command

**Purpose**: Execute shell commands with timeout and output capture

**Name**: `run_command`

**Schema**:
```json
{
  "type": "object",
  "properties": {
    "command": {
      "type": "string",
      "description": "Executable or shell command to run"
    },
    "arguments": {
      "type": "string",
      "description": "Optional arguments string"
    },
    "timeout_seconds": {
      "type": "number",
      "description": "Optional timeout in seconds",
      "default": 120
    }
  },
  "required": ["command"]
}
```

**Parameters**:
| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| `command` | string | Yes | - | Command or executable to run |
| `arguments` | string | No | `""` | Command arguments |
| `timeout_seconds` | number | No | `120` | Execution timeout (seconds) |

**Return Format**:
```
[Command stdout]
[Command stderr if present]
```

**Example Usage**:
```json
{
  "command": "dotnet",
  "arguments": "build --no-restore",
  "timeout_seconds": 300
}
```

**Execution Details**:
- **Working Directory**: Always the agent's workspace
- **Output**: Both stdout and stderr captured
- **Process Handling**: 
  - No shell execute (direct process start)
  - Process killed if timeout exceeded
  - Exit code ignored (output returned regardless)

**Error Cases**:
- Command not found: `"Error running command: {exception}"`
- Timeout: `"Error: Process timed out"`
- Process start failure: `"Error: Failed to start process"`

---

## 4. Provider-Specific Schema Transformations

Different LLM providers require different tool schema formats. DraCode automatically transforms the standard schema.

### 4.1 OpenAI / Azure OpenAI / GitHub Copilot / Ollama

**Format**: OpenAI Function Calling

```json
{
  "type": "function",
  "function": {
    "name": "read_file",
    "description": "Read the contents of a file in the workspace.",
    "parameters": {
      "type": "object",
      "properties": {
        "file_path": {
          "type": "string",
          "description": "Path to the file relative to workspace root"
        }
      },
      "required": ["file_path"]
    }
  }
}
```

### 4.2 Anthropic Claude

**Format**: Claude Tools API

```json
{
  "name": "read_file",
  "description": "Read the contents of a file in the workspace.",
  "input_schema": {
    "type": "object",
    "properties": {
      "file_path": {
        "type": "string",
        "description": "Path to the file relative to workspace root"
      }
    },
    "required": ["file_path"]
  }
}
```

### 4.3 Google Gemini

**Format**: Gemini Function Declarations

```json
{
  "functionDeclarations": [
    {
      "name": "read_file",
      "description": "Read the contents of a file in the workspace.",
      "parameters": {
        "type": "object",
        "properties": {
          "file_path": {
            "type": "string",
            "description": "Path to the file relative to workspace root"
          }
        },
        "required": ["file_path"]
      }
    }
  ]
}
```

---

## 5. Tool Result Format

### Successful Tool Call

**LLM Request**:
```json
{
  "type": "tool_use",
  "id": "toolu_01A2B3C4D5E6F7",
  "name": "read_file",
  "input": {
    "file_path": "Program.cs"
  }
}
```

**Agent Response** (sent back to LLM):
```json
{
  "type": "tool_result",
  "tool_use_id": "toolu_01A2B3C4D5E6F7",
  "content": "using System;\n\nnamespace MyApp\n{\n    class Program..."
}
```

### Error Tool Call

```json
{
  "type": "tool_result",
  "tool_use_id": "toolu_01A2B3C4D5E6F7",
  "content": "Error reading file: Could not find file 'Program.cs'"
}
```

---

## 6. Security Constraints

### Path Safety

All file/directory operations validate paths using `PathHelper.IsPathSafe()`:

```csharp
public static bool IsPathSafe(string path, string workingDirectory)
{
    var fullPath = Path.GetFullPath(path);
    var workingPath = Path.GetFullPath(workingDirectory);
    return fullPath.StartsWith(workingPath);
}
```

**Blocked Operations**:
- `../../../etc/passwd` ❌
- `/tmp/malicious` ❌
- `C:\Windows\System32` ❌

**Allowed Operations**:
- `src/Program.cs` ✅
- `./utils/helper.js` ✅
- `subfolder/../other.txt` ✅ (resolves to workspace)

### Command Execution Safety

- **No shell expansion**: Commands run directly (no `cmd.exe /c` or `sh -c`)
- **Timeout protection**: Default 120s prevents infinite hangs
- **Working directory locked**: Always executes in workspace
- **No elevated privileges**: Runs with agent's user permissions

---

## 7. Adding Custom Tools

### Step 1: Create Tool Class

```csharp
using DraCode.Agent.Tools;
using DraCode.Agent.Helpers;

public class MyCustomTool : Tool
{
    public override string Name => "my_custom_tool";
    
    public override string Description => "Description of what the tool does";
    
    public override object? InputSchema => new
    {
        type = "object",
        properties = new
        {
            parameter1 = new
            {
                type = "string",
                description = "Parameter description"
            }
        },
        required = new[] { "parameter1" }
    };
    
    public override string Execute(string workingDirectory, Dictionary<string, object> input)
    {
        try
        {
            var param1 = input["parameter1"].ToString();
            
            // Validate path safety if needed
            if (!PathHelper.IsPathSafe(somePath, workingDirectory))
                return "Error: Access denied";
            
            // Perform action
            var result = DoSomething(param1);
            
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
```

### Step 2: Register Tool

In `Agent.cs`, add to the tools list:

```csharp
private readonly List<Tool> _tools =
[
    new ListFiles(),
    new ReadFile(),
    new WriteFile(),
    new SearchCode(),
    new RunCommand(),
    new MyCustomTool()  // Add here
];
```

### Best Practices

1. **Always validate inputs**: Check types, ranges, null values
2. **Use PathHelper**: For any file/directory operations
3. **Return errors as strings**: Don't throw exceptions
4. **Keep descriptions clear**: LLM uses these to decide when to call
5. **Use snake_case**: For consistency with existing tools
6. **Document edge cases**: In tool description if important

---

## 8. Tool Call Flow

```
1. LLM receives system prompt with tool schemas
   ↓
2. LLM decides to use a tool (e.g., read_file)
   ↓
3. LLM responds with tool_use content block:
   {
     "type": "tool_use",
     "id": "call_123",
     "name": "read_file",
     "input": {"file_path": "Program.cs"}
   }
   ↓
4. Agent extracts tool name and input
   ↓
5. Agent finds matching tool by name
   ↓
6. Agent calls tool.Execute(workingDir, input)
   ↓
7. Tool returns result string
   ↓
8. Agent sends tool_result back to LLM:
   {
     "type": "tool_result",
     "tool_use_id": "call_123",
     "content": "[file contents]"
   }
   ↓
9. LLM processes result and continues
```

---

## 9. Complete Tool Summary Table

| Tool | Purpose | Key Parameters | Return Type |
|------|---------|----------------|-------------|
| `list_files` | List directory contents | `directory`, `recursive` | File paths (one per line) |
| `read_file` | Read file contents | `file_path` | File content as string |
| `write_file` | Create/modify files | `file_path`, `content` | `"OK"` or error |
| `search_code` | Search code with grep | `query`, `pattern`, `regex` | `path:line: content` |
| `run_command` | Execute commands | `command`, `arguments`, `timeout` | Command output |

---

## 10. JSON Schema Reference

### Complete Example

```json
{
  "type": "object",
  "properties": {
    "string_param": {
      "type": "string",
      "description": "A string parameter",
      "default": "default_value"
    },
    "number_param": {
      "type": "number",
      "description": "A numeric parameter",
      "default": 100
    },
    "boolean_param": {
      "type": "boolean",
      "description": "A boolean parameter",
      "default": false
    },
    "optional_param": {
      "type": "string",
      "description": "An optional parameter"
    }
  },
  "required": ["string_param", "number_param"]
}
```

### Validation Rules

- **Required parameters**: Must be provided by LLM
- **Optional parameters**: Use default value or null if not provided
- **Type mismatches**: Tool should handle gracefully
- **Extra parameters**: Ignored by tool

---

**End of Tool Specifications Document**
