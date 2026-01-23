# DraCode.Web - Modern TypeScript Web Client

A modern, vanilla TypeScript/CSS web client for DraCode's multi-agent WebSocket system. No frameworks, no Bootstrap—just pure modern web technologies.

## 🚀 Features

- **TypeScript**: Fully typed codebase
- **ES Modules**: Modern JavaScript module system
- **Flexbox Layout**: Responsive design
- **CSS Custom Properties**: Themeable with CSS variables
- **Zero Dependencies**: No runtime dependencies
- **Modular Architecture**: Clean separation of concerns
- **Secure**: Supports token-based authentication for WebSocket connections

## 🛠️ Quick Start

```bash
# Build (automatically compiles TypeScript)
dotnet build

# Run with Aspire
dotnet run --project ../DraCode.AppHost

# Open: http://localhost:5001
```

## 🔐 WebSocket Authentication

If the WebSocket server has authentication enabled, you may need to configure the connection token:

1. Get your authentication token from the server administrator
2. The web client will need to be updated to include the token when connecting
3. See [WebSocket README](../DraCode.WebSocket/README.md#authentication) for server-side configuration

**Note**: By default, authentication is disabled for development convenience.

## 📁 Structure

- `src/` - TypeScript source files
- `wwwroot/` - Static files
- `wwwroot/js/` - Compiled JavaScript (auto-generated)

## 💡 Why Vanilla?

1. **Performance**: No framework overhead
2. **Simplicity**: Easy to maintain
3. **Modern**: Latest web standards
4. **Lightweight**: Minimal bundle size

## 🌐 Compatibility

Chrome 90+, Firefox 88+, Safari 14+, Edge 90+

---

**Built with**: TypeScript 5.7, Vanilla CSS, ES2020
