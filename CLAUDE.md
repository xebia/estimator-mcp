# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Estimator MCP is a Model Context Protocol (MCP) server system for generating software project time estimates in a consulting context. It manages a catalog of work items (features, tasks) mapped to implementation roles with effort estimates, then serves estimates via MCP tools to LLM-based interfaces.

**Key Goal:** Enable AI agents to collect task/feature descriptions from users, query the catalog, and return per-role, per-task time breakdowns.

## Build & Run Commands

### MCP Server
```bash
cd src/estimator-mcp
dotnet build
dotnet run
```

### Catalog Editor (Blazor Web App)
```bash
cd src/CatalogEditor/CatalogEditor/CatalogEditor
dotnet build
dotnet run
# Opens at https://localhost:5001
```

### Running with custom ports
```bash
dotnet run --urls="https://localhost:5002"
```

## Architecture

### Project Structure
```
estimator-mcp/
├── src/
│   ├── estimator-mcp/              # MCP Server (stdio transport)
│   │   ├── Program.cs              # Host setup with Serilog (file-only logging)
│   │   ├── Tools/
│   │   │   ├── InstructionsTool.cs # Returns AI assistant instructions
│   │   │   ├── CatalogTool.cs      # Returns catalog features
│   │   │   └── CalculateEstimateTool.cs # Calculates estimates
│   │   └── data/
│   │       └── instructions.md     # AI assistant guidance document
│   ├── EstimatorMcp.Models/        # Shared data models
│   │   ├── CatalogData.cs          # Root catalog structure
│   │   ├── CatalogEntry.cs         # Feature/work item with estimates
│   │   └── Role.cs                 # Implementation role with Copilot multiplier
│   └── CatalogEditor/              # Blazor Web App for catalog management
│       └── CatalogEditor/
│           └── CatalogEditor/
│               ├── Services/
│               │   ├── ICatalogDataProvider.cs
│               │   └── JsonCatalogDataProvider.cs
│               ├── Components/Pages/   # Blazor pages
│               └── data/catalogs/      # JSON catalog storage
└── spec/                           # Specifications
    ├── overview.md                 # System requirements and MCP tool specs
    └── data-structure.md           # JSON schema and Fibonacci scaling math
```

### MCP Tools
The server exposes three tools via stdio transport:

1. **`GetInstructions`** - Returns markdown guidance for AI on how to use the server
2. **`GetCatalogFeatures`** - Returns catalog features, optionally filtered by category
3. **`CalculateEstimate`** - Accepts features with T-shirt sizes, returns per-role hour breakdowns

### Data Flow
- Catalog stored as JSON files: `catalog-{ISO8601_TIMESTAMP}.json`
- Latest file loaded at startup (lexicographic sort on filename)
- Estimates calculated: `(MediumHours × SizeMultiplier) × CopilotMultiplier`

### T-Shirt Sizing (Fibonacci Scaling)
Catalog stores only Medium (M) baseline. Other sizes auto-calculated:
- XS: 0.2x, S: 0.4x, M: 1.0x, L: 1.6x, XL: 2.6x

### Copilot Productivity Multipliers
Per-role multiplier applied to all estimates:
- Developer: 0.70 (30% faster)
- DevOps Engineer: 0.75 (25% faster)
- Engagement Manager: 1.0 (no AI acceleration)

## Configuration

### Environment Variables
- `ESTIMATOR_DATA_PATH` - Path to data directory (instructions.md)
- `ESTIMATOR_CATALOG_PATH` - Path to catalog JSON files
- `ESTIMATOR_LOGS_PATH` - Path for log files (default: `logs/`)
- `CatalogDataPath` - Catalog Editor data path

### Logging
MCP server uses Serilog with **file-only logging** (no console output to avoid interfering with stdio transport). Logs written to `logs/estimator-mcp-{date}.log`.

## Technology Standards

- **.NET 10** with `<Nullable>enable</Nullable>`
- **Async/await**: Use `Task`/`ValueTask` return types, not `void`
- **MCP Package**: `ModelContextProtocol` NuGet package (0.5.0-preview.1)
- **Blazor**: InteractiveServer render mode with `@rendermode InteractiveServer`
- **DI**: Microsoft.Extensions.DependencyInjection

## Key Patterns

### Tool Implementation
```csharp
[McpServerToolType]
public sealed class MyTool(IConfiguration config, ILogger<MyTool> logger)
{
    [McpServerTool, Description("Tool description for LLM")]
    public async Task<string> MyMethod([Description("Param description")] string param)
    {
        // Implementation
    }
}
```

### Provider Pattern (Catalog Editor)
- Interface: `ICatalogDataProvider`
- Implementation: `JsonCatalogDataProvider`
- Registered via DI, supports future database migration

## Additional Documentation

For more detailed guidelines, see the `.github/instructions/` folder:

- **[copilot-instructions.md](.github/instructions/copilot-instructions.md)** - High-level architecture, data flow, LINQ patterns, and MCP tool specifications
- **[dotnet-guidelines.md](.github/instructions/dotnet-guidelines.md)** - .NET 10 standards, async patterns, DI setup, Blazor configuration, Spectre.Console for CLI apps, OpenTelemetry/Serilog logging

For specifications and data schemas, see the `spec/` folder:

- **[overview.md](spec/overview.md)** - System requirements, MCP tool definitions, MVP scope
- **[data-structure.md](spec/data-structure.md)** - Complete JSON schema, Fibonacci math, file versioning

## Important Notes

- MCP server runs via stdio - **no console logging** (would corrupt protocol)
- Catalog files are versioned by timestamp filename, old files preserved
- All sizes derive from Medium baseline - only store M estimates
- Tool descriptions are critical - they guide LLM behavior
