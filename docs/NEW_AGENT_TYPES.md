# New Agent Types - PHP, Python, and Media Specialists

## Overview

DraCode.Agent now includes 6 new specialized agent types for expanded development capabilities:

- **2 Backend/Scripting Languages**: PHP, Python
- **4 Media/Graphics Agents**: Media (base), Image, SVG, Bitmap

All agents follow the same patterns as existing specialized agents with deep domain expertise, best practices, and framework knowledge built into their system prompts.

## Coding Agents

### PhpCodingAgent

**Agent Type**: `php`  
**File**: `DraCode.Agent/Agents/PhpCodingAgent.cs`  
**Derives From**: `CodingAgent`

**Specialization**: Modern PHP development with framework expertise

**Expertise Areas**:
- Modern PHP 8.0+ (type declarations, attributes, enums)
- Object-oriented PHP (namespaces, traits, interfaces)
- Popular frameworks: Laravel, Symfony, WordPress
- Composer package management
- PSR standards (PSR-1, PSR-4, PSR-12)
- Database integration: PDO, Eloquent ORM, Doctrine
- Testing with PHPUnit and Pest
- Security best practices (SQL injection prevention, XSS protection, CSRF tokens)

**Best Practices**:
- Strict types: `declare(strict_types=1)`
- Type declarations for parameters and return types
- PSR-12 coding style
- Dependency injection instead of global state
- PHPDoc comments for classes and methods
- Exception-based error handling

**Usage**:
```csharp
var options = new AgentOptions 
{ 
    WorkingDirectory = "./workspace", 
    Verbose = true,
    ModelDepth = 5 
};

var config = new Dictionary<string, string> 
{ 
    ["apiKey"] = "your-api-key", 
    ["model"] = "gpt-4o" 
};

var phpAgent = AgentFactory.Create("openai", options, config, "php");
var result = await phpAgent.RunAsync("Create a Laravel controller for user authentication");
```

**Example Tasks**:
- "Create a Laravel REST API with authentication"
- "Implement Symfony form validation with custom constraints"
- "Build a WordPress plugin for custom post types"
- "Create PSR-15 middleware for request logging"

---

### PythonCodingAgent

**Agent Type**: `python`  
**File**: `DraCode.Agent/Agents/PythonCodingAgent.cs`  
**Derives From**: `CodingAgent`

**Specialization**: Python development with data science and web framework expertise

**Expertise Areas**:
- Modern Python 3.10+ (type hints, dataclasses, pattern matching)
- Standard library modules and best practices
- Web frameworks: Django, Flask, FastAPI
- Data science: NumPy, Pandas, Matplotlib
- Machine learning: TensorFlow, PyTorch, scikit-learn
- Package management: pip, poetry, conda
- Testing with pytest and unittest
- Async programming with asyncio
- PEP 8 style guide and type checking with mypy

**Best Practices**:
- PEP 8 style guide (snake_case, 4 spaces, max line 79-88 chars)
- Type hints for function parameters and return types
- Comprehensions over loops when readable
- Context managers (with statement) for resource management
- Docstrings for modules, classes, and functions
- Virtual environments for dependency isolation

**Usage**:
```csharp
var pythonAgent = AgentFactory.Create("claude", options, config, "python");
var result = await pythonAgent.RunAsync("Create a Flask API with JWT authentication");
```

**Example Tasks**:
- "Create a Django REST API with authentication and CRUD operations"
- "Build a pandas script to analyze CSV sales data and create visualizations"
- "Implement a FastAPI service with async endpoints and SQLAlchemy"
- "Create a scikit-learn model for customer churn prediction"

---

## Media Agent Hierarchy

### MediaAgent (Base)

**Agent Type**: `media`  
**File**: `DraCode.Agent/Agents/MediaAgent.cs`  
**Derives From**: `Agent`

**Specialization**: General digital media handling

**Expertise Areas**:
- Digital media formats (image, video, audio)
- Image formats: JPEG, PNG, GIF, WebP, AVIF
- Vector graphics: SVG, EPS, AI
- Video formats: MP4, WebM, AVI, MOV
- Audio formats: MP3, WAV, FLAC, AAC
- Media optimization and compression
- Color spaces: RGB, CMYK, HSL, Lab
- Resolution, DPI, and aspect ratios
- Accessibility in media (alt text, captions, transcripts)
- Web performance optimization for media

**Best Practices**:
- Choose appropriate formats for use case (vector vs raster, lossy vs lossless)
- Optimize file sizes without sacrificing necessary quality
- Consider responsive design and multiple resolutions
- Include accessibility features
- Follow web standards and best practices

**Usage**:
```csharp
var mediaAgent = AgentFactory.Create("openai", options, config, "media");
var result = await mediaAgent.RunAsync("Optimize media assets for web delivery");
```

**Example Tasks**:
- "Analyze media file formats and recommend optimization strategies"
- "Create a media conversion pipeline for web assets"
- "Implement accessibility features for multimedia content"

---

### ImageAgent

**Agent Type**: `image`  
**File**: `DraCode.Agent/Agents/ImageAgent.cs`  
**Derives From**: `MediaAgent`

**Specialization**: Image handling (raster and vector)

**Expertise Areas**:
- Raster formats: JPEG, PNG, GIF, WebP, AVIF, TIFF
- Vector formats: SVG, EPS
- Image editing and manipulation
- Canvas API, ImageMagick, Pillow/PIL
- Color theory and color management
- Image compression techniques (lossy vs lossless)
- Responsive images (srcset, picture element)
- Image optimization for web (lazy loading, format selection)
- Retina/HiDPI displays (@2x, @3x)
- Image metadata (EXIF, IPTC)
- Accessibility (alt text, decorative vs informative)

**Best Practices**:
- Choose format based on content: PNG (transparency), JPEG (photos), SVG (scalable), WebP (modern web)
- Optimize file size: compress JPEGs (80-90%), use PNG-8 when possible, minify SVG
- Provide multiple resolutions for responsive design
- Include meaningful alt text for accessibility
- Use appropriate color spaces (sRGB for web, CMYK for print)

**Usage**:
```csharp
var imageAgent = AgentFactory.Create("gemini", options, config, "image");
var result = await imageAgent.RunAsync("Create responsive images with multiple sizes");
```

**Example Tasks**:
- "Optimize product images for e-commerce site"
- "Create responsive image sets with srcset"
- "Convert and optimize images for different devices"

---

### SvgAgent

**Agent Type**: `svg`  
**File**: `DraCode.Agent/Agents/SvgAgent.cs`  
**Derives From**: `ImageAgent`

**Specialization**: SVG (Scalable Vector Graphics)

**Expertise Areas**:
- SVG specification and syntax
- SVG elements: path, circle, rect, polygon, line, text, g, defs
- SVG attributes: viewBox, preserveAspectRatio, transform
- SVG styling: inline styles, CSS classes, presentation attributes
- SVG animations: SMIL, CSS animations, JavaScript
- SVG filters and effects
- SVG optimization with SVGO
- Responsive SVG techniques
- Accessibility: title, desc, role, aria-label
- SVG as icons, illustrations, data visualizations
- D3.js for dynamic SVG generation

**Best Practices**:
- Use viewBox for scalability, not fixed width/height
- Group related elements with `<g>` tags
- Use `<defs>` for reusable elements (gradients, patterns, symbols)
- Prefer paths over basic shapes for optimization
- Use CSS classes instead of inline styles when possible
- Minimize decimal precision (2-3 places sufficient)
- Remove unnecessary metadata
- Add `<title>` and `<desc>` for accessibility

**Usage**:
```csharp
var svgAgent = AgentFactory.Create("openai", options, config, "svg");
var result = await svgAgent.RunAsync("Create a responsive logo with animated loading effect");
```

**Example Tasks**:
- "Create an SVG icon set for a web application"
- "Design an animated SVG logo with hover effects"
- "Generate D3.js visualization from data"
- "Optimize SVG files for production use"

---

### BitmapAgent

**Agent Type**: `bitmap`  
**File**: `DraCode.Agent/Agents/BitmapAgent.cs`  
**Derives From**: `ImageAgent`

**Specialization**: Bitmap/raster images

**Expertise Areas**:
- Bitmap formats: JPEG, PNG, GIF, WebP, AVIF, BMP, TIFF
- Image compression algorithms (lossy vs lossless)
- Color modes: RGB, RGBA, grayscale, indexed color
- Bit depth: 8-bit, 16-bit, 24-bit, 32-bit
- Image manipulation: resize, crop, rotate, flip
- Filters and effects: blur, sharpen, contrast, brightness
- Image processing libraries: ImageMagick, Pillow/PIL, Sharp, Canvas API
- Sprite sheets and texture atlases
- Progressive rendering (progressive JPEG, interlaced PNG)
- Image optimization for web performance
- Retina/HiDPI image handling (@1x, @2x, @3x)

**Best Practices**:
- Choose format wisely:
  * JPEG: Photos, complex images (lossy, no transparency)
  * PNG: Graphics, transparency needed (lossless, larger files)
  * WebP: Modern web (lossy/lossless, transparency, smaller than PNG)
  * AVIF: Next-gen web (excellent compression, limited browser support)
- Balance quality vs file size: JPEG 80-90% quality often optimal
- Resize images to actual display size
- Use progressive JPEG for large images
- Provide @2x versions for retina displays
- Strip metadata (EXIF) for web to reduce file size

**Usage**:
```csharp
var bitmapAgent = AgentFactory.Create("gemini", options, config, "bitmap");
var result = await bitmapAgent.RunAsync("Optimize all photos for web with retina support");
```

**Example Tasks**:
- "Optimize JPEG photos for web gallery"
- "Create responsive image set with @1x, @2x, @3x versions"
- "Convert PNG graphics to WebP format"
- "Generate sprite sheet from individual icons"

---

## Agent Type Summary

### All 17 Available Agent Types

**Coding (11 types)**:
1. `coding` - General purpose
2. `csharp` - C# / .NET
3. `cpp` - C++
4. `assembler` - Assembly
5. `javascript` / `typescript` - JS/TS
6. `css` - CSS styling
7. `html` - HTML markup
8. `react` - React framework
9. `angular` - Angular framework
10. `php` - PHP (NEW)
11. `python` - Python (NEW)

**Media (4 types)**:
12. `media` - General media (NEW)
13. `image` - Images (NEW)
14. `svg` - SVG graphics (NEW)
15. `bitmap` - Raster images (NEW)

**Visualization (1 type)**:
16. `diagramming` - Diagrams

**Special (1 type)**:
17. `wyvern` - Task orchestration (KoboldLair internal)

---

## Usage Patterns

### Creating Specialized Agents

```csharp
// Setup options and config
var options = new AgentOptions 
{ 
    WorkingDirectory = "./workspace",
    Verbose = true,
    ModelDepth = 5,
    MaxIterations = 20
};

var config = new Dictionary<string, string> 
{ 
    ["apiKey"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    ["model"] = "gpt-4o" 
};

// Create agents
var phpAgent = AgentFactory.Create("openai", options, config, "php");
var pythonAgent = AgentFactory.Create("claude", options, config, "python");
var svgAgent = AgentFactory.Create("openai", options, config, "svg");
var bitmapAgent = AgentFactory.Create("gemini", options, config, "bitmap");

// Execute tasks
await phpAgent.RunAsync("Create Laravel authentication system");
await pythonAgent.RunAsync("Build Flask API with SQLAlchemy");
await svgAgent.RunAsync("Design animated SVG logo");
await bitmapAgent.RunAsync("Optimize product photos for web");
```

### KoboldLair Integration

```csharp
// In KoboldLair, Wyvern automatically selects appropriate agent
// Drake then creates Kobolds with that agent type

var factory = new KoboldFactory(options, config);

// Create specialized Kobolds
var phpKobold = factory.CreateKobold("openai", "php");
var pythonKobold = factory.CreateKobold("claude", "python");
var svgKobold = factory.CreateKobold("openai", "svg");
var bitmapKobold = factory.CreateKobold("gemini", "bitmap");

// Assign and execute
phpKobold.AssignTask(taskId);
await phpKobold.StartWorkingAsync();
```

---

## Best Practices

### Choosing the Right Agent

**For Web Development**:
- Backend API: `php`, `python`, `csharp`
- Frontend: `react`, `angular`, `javascript`
- Styling: `css`
- Markup: `html`

**For Media Tasks**:
- Logos/icons: `svg`
- Photos: `bitmap`
- Mixed media: `image`
- General media: `media`

**For Data/ML**:
- Data analysis: `python`
- Visualization: `svg`, `diagramming`

**For System Programming**:
- Low-level: `cpp`, `assembler`
- Cross-platform: `csharp`

### Model Depth Recommendations

- **Quick tasks** (ModelDepth 1-3): Simple fixes, routine operations
- **Standard tasks** (ModelDepth 4-6): General development, moderate complexity
- **Complex tasks** (ModelDepth 7-10): Architecture, refactoring, critical systems

### Provider Recommendations

- **OpenAI (gpt-4o)**: Excellent for all agent types, best overall
- **Claude (claude-3-5-sonnet)**: Great for coding, analysis
- **Gemini (gemini-2.0-flash)**: Fast, good for media tasks
- **Ollama (local)**: Privacy-focused, good for simple tasks

---

## Integration Points

### DraCode.Agent

All new agents are available through `AgentFactory.Create()`:

```csharp
AgentFactory.Create(provider, options, config, agentType)
```

Where `agentType` can be any of the 17 types listed above.

### KoboldLair

All new agents are integrated with KoboldLair:

- **WyvernAgent**: Validates and recommends new agent types
- **KoboldFactory**: Creates Kobolds with new agent types
- **Drake**: Assigns tasks to new agent Kobolds
- **Background Services**: Process tasks with new agents automatically

### WebSocket API

New agent types available via WebSocket:

```json
{
  "command": "connect",
  "agentId": "php-agent-1",
  "config": {
    "provider": "openai",
    "agentType": "php",
    "workingDirectory": "./workspace"
  }
}
```

---

## Examples

### PHP Laravel Project

```csharp
var phpAgent = AgentFactory.Create("openai", options, config, "php");
await phpAgent.RunAsync(@"
Create a Laravel 10 REST API with:
- User authentication (JWT)
- CRUD operations for products
- Validation using Form Requests
- API resources for responses
- PHPUnit tests
- API documentation
");
```

### Python Data Analysis

```csharp
var pythonAgent = AgentFactory.Create("claude", options, config, "python");
await pythonAgent.RunAsync(@"
Analyze sales data from sales.csv:
1. Load data with pandas
2. Calculate monthly revenue trends
3. Identify top 10 products
4. Create matplotlib visualizations
5. Export report to PDF
");
```

### SVG Icon Set

```csharp
var svgAgent = AgentFactory.Create("openai", options, config, "svg");
await svgAgent.RunAsync(@"
Create an SVG icon set:
- Home, Search, Settings, Profile, Logout icons
- 24x24px artboard
- Single color with CSS variables
- Optimized file size
- Accessible with titles
");
```

### Image Optimization

```csharp
var bitmapAgent = AgentFactory.Create("gemini", options, config, "bitmap");
await bitmapAgent.RunAsync(@"
Optimize all images in ./assets/photos/:
- Compress JPEGs to 85% quality
- Create @2x versions for retina
- Convert to WebP for modern browsers
- Maintain aspect ratios
- Strip EXIF data
");
```

---

## Documentation Updates

This information has been added to:
- ✅ Main README.md
- ✅ DraCode.Agent/README.md (if exists)
- ✅ DraCode.KoboldLair.Server/README.md
- ✅ DraCode.KoboldLair.Client/README.md
- ✅ docs/README.md
- ✅ This file (NEW_AGENT_TYPES.md)

For more information, see:
- [KoboldLair Server Documentation](../DraCode.KoboldLair.Server/README.md)
- [KoboldLair Client Documentation](../DraCode.KoboldLair.Client/README.md)
- [Agent Options](AGENT_OPTIONS.md)
- [Kobold System](Kobold-System.md)

---

**Version**: 1.0  
**Date**: 2026-01-26  
**Status**: ✅ Production Ready
