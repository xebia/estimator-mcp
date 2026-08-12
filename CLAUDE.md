# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Estimator MCP is a Model Context Protocol (MCP) server system for generating software project time estimates in a consulting context. It manages a catalog of work items (features, tasks) mapped to implementation roles with effort estimates, then serves estimates via MCP tools to LLM-based interfaces.

**Key Goal:** Enable AI agents to collect task/feature descriptions from users, query the catalog, and return per-role, per-task time breakdowns.

## Build & Run Commands

### MCP Server + Catalog UI (EstimatorMcp.Web)
```bash
cd src/EstimatorMcp.Web
dotnet build
dotnet run
# MCP endpoint at /mcp, Blazor catalog UI at the site root
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

### Catalog CLI (bulk import/export/migrate)
```bash
cd src/CatalogCli

# Export catalog JSON to TSV files for Excel editing
dotnet run -- export -i <catalog.json> -o ./export/

# Import TSV files back to catalog JSON
dotnet run -- import --techstacks ./export/techstacks.tsv --roles ./export/roles.tsv --entries ./export/entries.tsv -o updated.json

# Migrate a v1.0 catalog to v2.0 format
dotnet run -- migrate -i catalog-v1.json -o catalog-v2.json
```

## Architecture

### Project Structure
```
estimator-mcp/
├── src/
│   ├── EstimatorMcp.Web/           # MCP endpoint (HTTP) + Blazor catalog UI; the deployed app
│   │   ├── Program.cs              # Host setup: auth, EF Core, MapMcp, Serilog
│   │   ├── Tools/
│   │   │   ├── InstructionsTool.cs # Returns AI assistant instructions
│   │   │   ├── CatalogTool.cs      # Catalog features, tech stacks, roles
│   │   │   └── CalculateEstimateTool.cs # Calculates estimates
│   │   ├── Services/               # DbCatalogDataProvider, auth services
│   │   └── content/
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
The server exposes six tools over streamable HTTP at `/mcp`:

1. **`GetInstructions`** - Returns markdown guidance for AI on how to use the server
2. **`GetCatalogFeatures`** - Returns catalog features, optionally filtered by category, tech stack, or tags
3. **`GetCatalogTechStacks`** - Returns tech stacks with roles and feature counts
4. **`GetRolesForTechStack`** - Returns tech-stack-specific plus global roles
5. **`CalculateEstimate`** - Accepts features with T-shirt sizes, returns per-role hour breakdowns
6. **`GetServerVersion`** - Returns server semantic version + commit and catalog schema version + timestamp

Versioning policy for the tool surface: [VERSIONING.md](VERSIONING.md). Version number lives in `Directory.Build.props`.

### Data Flow
- Catalog served from SQLite via `ICatalogDataProvider`, the same store the Blazor UI edits
- Seeded on first start from `catalog-{ISO8601_TIMESTAMP}.json` files (latest wins, lexicographic sort)
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
- `DatabasePath` - SQLite database path (also determines the Data Protection key directory)
- `CatalogSeedPath` - Override for the first-start catalog seed location
- `ESTIMATOR_LOGS_PATH` - Path for log files (default: `logs/`)
- `AzureAd:*` - Entra app registration settings (see `docs/auth-architecture.md`)
- `CatalogDataPath` - Catalog Editor data path

### Logging
Serilog writes to console and to `logs/estimator-web-{date}.log`.

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

- MCP is served over HTTP at `/mcp` - there is no stdio transport, so console logging is fine
- `/mcp` and `/api/catalog/*` are pinned to the `BearerOnly` policy so unauthenticated callers get a 401, not a login redirect
- Catalog seed files are versioned by timestamp filename, old files preserved
- All sizes derive from Medium baseline - only store M estimates
- Tool descriptions are critical - they guide LLM behavior
