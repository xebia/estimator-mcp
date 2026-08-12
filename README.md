# Estimator MCP Server

A Model Context Protocol (MCP) server system for generating software project time estimates in a consulting context. This system enables AI agents (like Claude) to collect task/feature descriptions from users, query a catalog of work items, and return detailed per-role, per-task time breakdowns.

## Project Overview

**Key Goal:** Enable AI-assisted project estimation by managing a catalog of features mapped to implementation roles with effort estimates, then serving those estimates via MCP tools to LLM-based interfaces.

**Versioning:** This project follows semantic versioning — see [VERSIONING.md](VERSIONING.md) for what counts as a breaking change to the MCP tool surface.

## Project Structure

```
estimator-mcp/
├── spec/                           # Specification documents
│   ├── overview.md                 # System requirements and MCP tool specs
│   ├── data-structure.md          # Data model and JSON schema
│   ├── process-flow.md            # Estimation workflow
│   └── innovation.md              # Innovation and future ideas
├── docs/                          # Additional documentation
│   └── plans/                     # Technical planning documents
├── src/                           # Source code
│   ├── EstimatorMcp.Web/          # Deployed app: HTTP MCP endpoint + Blazor catalog UI
│   ├── CatalogEditor/             # Standalone Blazor catalog editor (JSON storage)
│   ├── CatalogCli/                # CLI tool for bulk TSV import/export
│   └── EstimatorMcp.Models/       # Shared data models
└── tests/                         # Unit tests
```

## Components

### 1. MCP Server (EstimatorMcp.Web)

**Status:** ✅ Fully Implemented

This is the deployed product: a single ASP.NET Core app that serves the MCP endpoint over HTTP at **`/mcp`** and hosts the Blazor catalog UI. There is no stdio server — the solution is HTTP-only.

**MCP Tools:**
1. **`GetInstructions`** - Returns markdown guidance for AI assistants on how to conduct estimation interviews
2. **`GetCatalogFeatures`** - Returns catalog features, filterable by category, tech stack, or tags
3. **`GetCatalogTechStacks`** - Returns available tech stacks with their roles and feature counts
4. **`GetRolesForTechStack`** - Returns the roles available for a given tech stack, including global roles
5. **`CalculateEstimate`** - Accepts features with T-shirt sizes, returns detailed per-role hour breakdowns
6. **`GetServerVersion`** - Returns the server's semantic version and commit, plus the catalog's schema version and timestamp, so an agent can tell whether the server or its data has changed

**Features:**
- Streamable HTTP transport, so any remote-capable MCP client can connect
- OAuth-protected per the MCP authorization spec — see [`docs/auth-architecture.md`](docs/auth-architecture.md)
- Catalog served from SQLite via `ICatalogDataProvider`, the same source the UI edits
- Tech stack and tag-based filtering
- Fibonacci scaling for T-shirt sizes (XS, S, M, L, XL)
- Copilot productivity multipliers applied per role

**Technology Stack:**
- .NET 10 with nullable reference types
- `ModelContextProtocol.AspNetCore`
- EF Core + SQLite for catalog storage
- Microsoft.Identity.Web for Entra OIDC (UI) and JWT bearer (`/mcp`)
- Serilog

**Running Locally:**
```bash
cd src/EstimatorMcp.Web
dotnet run
# MCP endpoint at http://localhost:<port>/mcp, catalog UI at the site root
```

**Deployment:** Azure Container Apps via `azd deploy` (see [`azure.yaml`](azure.yaml) and the `Dockerfile`).

### 2. Catalog Editor (CatalogEditor)

**Status:** ✅ Fully Implemented

A Blazor web application for managing catalog data through an interactive UI.

**Features:**
- Manage implementation roles with Copilot productivity multipliers
- Create and edit catalog entries (features) with role-based time estimates
- Tech stack categorization (Salesforce, Blazor/Azure, Node.js, shared, etc.)
- Tag-based organization for multi-dimensional categorization
- T-shirt sizing with Fibonacci scaling (stores Medium baseline only)
- Real-time validation and auto-save

**Technology Stack:**
- ASP.NET Core Blazor (.NET 10)
- InteractiveServer render mode
- Provider pattern (`ICatalogDataProvider`) for future database migration
- JSON file storage with automatic versioning

**Running the Editor:**
```bash
cd src/CatalogEditor/CatalogEditor/CatalogEditor
dotnet build
dotnet run
# Navigate to https://localhost:5001
```

### 3. Catalog CLI (CatalogCli)

**Status:** ✅ Fully Implemented

Command-line tool for bulk editing via Excel/spreadsheet applications.

**Features:**
- Export catalog JSON to TSV files (roles.tsv, entries.tsv)
- Import edited TSV files back to JSON format
- Full validation of data integrity and role references
- Support for tech stacks and semicolon-separated tags
- Ideal for bulk updates to 50+ catalog features

**Use Case Example:**
```bash
# Step 1: Export to TSV
dotnet run -- export -i catalog.json -o ./export/

# Step 2: Edit in Excel (techstacks.tsv, roles.tsv, entries.tsv)

# Step 3: Import back to JSON
dotnet run -- import --techstacks ./export/techstacks.tsv --roles ./export/roles.tsv --entries ./export/entries.tsv -o updated.json

# Migrate a v1.0 catalog to v2.0 format
dotnet run -- migrate -i catalog-v1.json -o catalog-v2.json
```

See [CatalogCli README](src/CatalogCli/README.md) for detailed usage.

### 4. Shared Models (EstimatorMcp.Models)

**Status:** ✅ Fully Implemented

Shared data models used across all components:
- `CatalogData` - Root catalog structure with roles and entries
- `CatalogEntry` - Feature/work item with estimates and metadata
- `Role` - Implementation role with Copilot multiplier
- `TechStack` - Technology platform categorization

### Data Storage

Catalog data is stored in JSON files with timestamp-based versioning:
- **Location**: `src/CatalogEditor/CatalogEditor/CatalogEditor/data/catalogs/`
- **Format**: `catalog-{ISO8601_TIMESTAMP}.json`
- **Version History**: Old files are preserved; latest file loaded at startup by lexicographic sort

### Provider Pattern

The Catalog Editor uses a provider pattern to abstract data access:
- **Interface**: `ICatalogDataProvider`
- **Current Implementation**: `JsonCatalogDataProvider` (file-based storage)
- **Future**: Easy migration to SQL Server, PostgreSQL, Azure Storage, or API backends

## Getting Started

### Prerequisites
- .NET 10 SDK or later
- (Optional) An MCP client that supports remote/HTTP servers, for MCP integration
- (Optional) Excel or compatible spreadsheet app for CLI bulk editing

### Quick Start

**Option 1: Run the MCP Server**
1. Start the app:
   ```bash
   cd src/EstimatorMcp.Web
   dotnet run
   ```

2. Point your MCP client at `/mcp` on that host (see [MCP Integration](#mcp-integration) below)

3. Ask your assistant to help estimate a project - it will use the MCP tools automatically

**Option 2: Manage Catalog via Web UI**
```bash
cd src/CatalogEditor/CatalogEditor/CatalogEditor
dotnet run
# Navigate to https://localhost:5001
```

**Option 3: Bulk Edit via CLI + Excel**
```bash
cd src/CatalogCli
dotnet run -- export -i <catalog.json> -o ./export/
# Edit TSV files in Excel
dotnet run -- import --roles ./export/roles.tsv --entries ./export/entries.tsv -o updated.json
```

### Sample Catalog Data

The system includes a comprehensive catalog with:
- **7 roles**: Developer, DevOps Engineer, Engagement Manager, Architect, QA Engineer, Security Engineer, UX Designer
- **50+ catalog entries** across multiple tech stacks and categories
- **Tech stacks**: Salesforce, Blazor/Azure, Node.js, .NET, shared
- **Categories**: Feature, Backend, DevOps, Data, QA, Security
- **Tags**: Platform-specific, technology, layer, and function-based tags

## T-Shirt Sizing Model

Catalog entries store only **Medium (M)** baseline estimates to minimize data entry. Other sizes are auto-calculated using Fibonacci scaling:

| Size | Fibonacci | Multiplier | Example (M=24h) |
|------|-----------|------------|-----------------|
| XS   | 1         | 0.2x (1/5) | 4.8h            |
| S    | 2         | 0.4x (2/5) | 9.6h            |
| M    | 5         | 1.0x       | 24h             |
| L    | 8         | 1.6x (8/5) | 38.4h           |
| XL   | 13        | 2.6x (13/5)| 62.4h           |

**Final estimates** also apply the role's Copilot multiplier (e.g., 0.6 for Developer = 40% faster with AI assistance).

**Calculation Formula:**
```
Final Hours = (Medium Hours × Size Multiplier) × Copilot Multiplier
```

**Example:**
- Feature: "REST API Integration"
- Medium baseline: Developer = 24h
- Selected size: Large (L) = 1.6x
- Developer Copilot multiplier: 0.6 (40% faster)
- **Final estimate: 24 × 1.6 × 0.6 = 23.04 hours**

## Tech Stack & Tag Organization

The system supports multi-dimensional categorization:

**Tech Stacks:**
- `salesforce` - Salesforce platform (Apex, LWC, Flows)
- `blazor-azure` - Blazor + Azure (AKS, Functions, CosmosDB)
- `dotnet` - .NET/ASP.NET Core
- `nodejs` - Node.js ecosystem
- `react-aws` - React + AWS
- `shared` - Cross-platform features

**Tags** (semicolon-separated):
- Platform: `salesforce`, `azure`, `aws`
- Layer: `frontend`, `backend`, `database`, `api`
- Function: `authentication`, `authorization`, `crud`, `search`
- Technology: `apex`, `lwc`, `blazor`, `terraform`
- Domain: `devops`, `security`, `testing`, `data`

**Filtering Examples:**
```csharp
// Get all Salesforce features
GetCatalogFeatures(techStack: "salesforce")

// Get all frontend features
GetCatalogFeatures(tag: "frontend")

// Get all authentication-related features
GetCatalogFeatures(tag: "authentication")
```

## MCP Integration

The server speaks MCP over **streamable HTTP** at `/mcp`. Any MCP client that supports remote servers can connect; there is no stdio transport and no local build required to consume it.

### Connecting a client

Point the client at the `/mcp` path of a running instance:

- **Deployed:** `https://<container-app-host>/mcp`
- **Local:** `http://localhost:<port>/mcp`

The endpoint is OAuth-protected and advertises its authorization server per the MCP authorization spec (RFC 9728), so a spec-compliant client discovers sign-in automatically rather than being configured out of band.

- Wiring up a Copilot Studio agent: [`docs/copilot-studio-setup.md`](docs/copilot-studio-setup.md)
- How the two auth flows fit together: [`docs/auth-architecture.md`](docs/auth-architecture.md)

A client that only speaks stdio can still reach the server through a stdio-to-HTTP bridge such as `mcp-remote`.

### AI Workflow

Once connected, the assistant can:

1. **Call `GetInstructions`** to learn how to conduct estimation interviews
2. **Call `GetCatalogFeatures`** to retrieve available features from catalog
3. **Interview user** to understand project scope and select relevant features
4. **Help user assign T-shirt sizes** (XS, S, M, L, XL) based on complexity
5. **Call `CalculateEstimate`** with selected features and sizes
6. **Present detailed breakdown** of hours per role per feature, plus totals

**Example conversation:**
```
User: "I need to estimate a Salesforce project with custom Apex classes and LWC components"

Claude: [Calls GetCatalogFeatures(techStack: "salesforce")]
        "I found these Salesforce features in the catalog:
        - Apex Class Development
        - Lightning Web Component
        - Custom Object with Fields
        ...
        
        Let's go through each one and size them for your project..."

User: "We need 3 Apex classes (Medium), 5 LWC components (Small), and 2 custom objects (Large)"

Claude: [Calls CalculateEstimate with the selections]
        "Here's your estimate breakdown:
        
        Developer: 123.4 hours (15.4 days)
        DevOps: 8.5 hours (1.1 days)
        QA: 24.0 hours (3.0 days)
        ..."
```

## Development Status

### ✅ Completed (MVP)

**Phase 1: Catalog Management**
- ✅ Blazor Web App for catalog CRUD operations
- ✅ Provider pattern with JSON storage
- ✅ T-shirt sizing with Fibonacci scaling
- ✅ Role management with Copilot multipliers
- ✅ Automatic catalog versioning (timestamp-based filenames)

**Phase 2: MCP Server**
- ✅ MCP Server implementation (streamable HTTP transport)
- ✅ GetInstructions tool (AI guidance)
- ✅ GetCatalogFeatures tool (catalog queries with filtering)
- ✅ GetCatalogTechStacks and GetRolesForTechStack tools
- ✅ CalculateEstimate tool (time breakdown per role/task)
- ✅ Tech stack categorization
- ✅ Tag-based organization and filtering
- ✅ Entra OIDC for the UI, JWT bearer for `/mcp`
- ✅ Deployed to Azure Container Apps

**Phase 3: Bulk Editing**
- ✅ CatalogCli tool for TSV import/export
- ✅ Excel-based bulk editing workflow
- ✅ Validation service for data integrity
- ✅ Support for tech stacks and tags

### 🔄 In Progress

**Phase 4: Advanced Features**
- 🔄 Multi-catalog support (different rate sheets per region/client)
- 🔄 Historical estimate tracking and accuracy metrics
- 🔄 AI-assisted feature matching (semantic search)

### 📋 Future Enhancements

**Database Migration**
- [ ] SQL Server provider implementation
- [ ] PostgreSQL provider implementation
- [ ] Azure Storage provider (blob-based)

**Security & Governance**
- [ ] User authentication and authorization
- [ ] Role-based access control (catalog admin, estimator)
- [ ] Audit logging (who changed what and when)

**Export & Reporting**
- [ ] PDF export (formatted estimate documents)
- [ ] CSV export (for finance systems)
- [ ] Staffing plan generation (timeline with resource allocation)

**Cost Handling**
- [ ] Rate sheets (cost per role per hour)
- [ ] Multi-region rates (US, EU, APAC)
- [ ] Currency conversion
- [ ] Cost breakdown by feature/role

**Advanced Estimation**
- [ ] Non-functional requirements modeling (% uplift for testing, deployment)
- [ ] Risk/contingency factors (optimistic/pessimistic scenarios)
- [ ] Feature dependencies and sequencing
- [ ] Bill-of-materials tracking (infrastructure/licensing costs)

**Integration**
- [ ] REST API for external systems
- [ ] Webhook notifications (catalog updates)
- [ ] Git-based catalog storage (version control)
- [ ] Jira/Azure DevOps integration (import epics/stories)

## Documentation

### Specifications (`spec/`)
- **[overview.md](spec/overview.md)** - System goals, features, requirements, and MCP tool definitions
- **[data-structure.md](spec/data-structure.md)** - Complete data model, JSON schema, Fibonacci math
- **[process-flow.md](spec/process-flow.md)** - Estimation workflow and user interactions
- **[innovation.md](spec/innovation.md)** - Future ideas and enhancements

### Component Documentation
- **[CLAUDE.md](CLAUDE.md)** - Comprehensive project overview for AI assistants (architecture, build commands, patterns)
- **[CatalogEditor README](src/CatalogEditor/README.md)** - Blazor app setup, configuration, and data model
- **[CatalogCli README](src/CatalogCli/README.md)** - CLI tool usage, TSV format, Excel workflow, validation rules
- **[CatalogCli QUICK-REFERENCE](src/CatalogCli/QUICK-REFERENCE.md)** - Quick command reference

### Developer Guidelines (`.github/instructions/`)
- **copilot-instructions.md** - High-level architecture, data flow, LINQ patterns, MCP tool specs
- **dotnet-guidelines.md** - .NET 10 standards, async patterns, DI setup, Blazor config

## Configuration

### Environment Variables

**MCP Server:**
- `ESTIMATOR_DATA_PATH` - Path to data directory (instructions.md)
- `ESTIMATOR_CATALOG_PATH` - Path to catalog JSON files
- `ESTIMATOR_LOGS_PATH` - Path for log files (default: `logs/`)

**Catalog Editor:**
- `CatalogDataPath` - Catalog JSON file storage location

### Logging

The server uses **Serilog**, writing to console and to a rolling file:
- Log location: `logs/estimator-web-{date}.log` (override with `ESTIMATOR_LOGS_PATH`)
- Log level: Information (configurable)

## Technology Stack

- **.NET 10** with nullable reference types enabled
- **ModelContextProtocol.AspNetCore** - MCP over streamable HTTP
- **Blazor** - InteractiveServer render mode
- **EF Core + SQLite** - Catalog storage
- **Microsoft.Identity.Web** - Entra OIDC and JWT bearer auth
- **Serilog** - Structured logging
- **Spectre.Console** - CLI formatting and validation
- **Central Package Management** - All NuGet versions in `Directory.Packages.props`

## Architecture Patterns

### Tool Implementation (MCP Server)
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
```csharp
// Interface for abstraction
public interface ICatalogDataProvider
{
    Task<CatalogData?> LoadCatalogAsync();
    Task SaveCatalogAsync(CatalogData catalog);
}

// JSON implementation (current)
public class JsonCatalogDataProvider : ICatalogDataProvider { ... }

// Easy to add SQL, Azure, API implementations later
```

## Contributing

This is an internal Xebia project. For changes:

1. Create a feature branch: `git checkout -b feature/your-feature-name`
2. Follow .NET 10 and Blazor conventions (see `.github/instructions/`)
3. Test with all three components (MCP server, web app, CLI)
4. Update relevant README files if adding features
5. Commit with clear messages describing the change

## Support & Troubleshooting

### Common Issues

**MCP Server not connecting:**
- Verify the client points at the `/mcp` path, not the site root
- A 401 means the token is missing, has the wrong audience, or lacks the `access_as_user` scope - see [`docs/copilot-studio-setup.md`](docs/copilot-studio-setup.md)
- Check logs: `src/EstimatorMcp.Web/logs/estimator-web-*.log`

**Catalog not loading:**
- On first start the database is seeded from `catalog-{ISO8601_TIMESTAMP}.json`; ensure a seed file exists
- Check filename format: `catalog-{ISO8601_TIMESTAMP}.json`
- Verify JSON is valid (use JSON validator)

**CLI import failing:**
- Check TSV file format matches specification
- Verify role IDs in entries.tsv match roles.tsv
- Look for validation errors in output

**Blazor app not starting:**
- Ensure .NET 10 SDK is installed
- Check appsettings.json for valid CatalogDataPath
- Verify port 5001 is not in use

### Getting Help

For additional support:
1. Check component-specific README files
2. Review CLAUDE.md for architecture overview
3. Check `spec/` folder for detailed specifications
4. Review logs for error messages

## License

Copyright © 2025 Xebia. All rights reserved.
