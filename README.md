# Estimator MCP Server

A Model Context Protocol (MCP) server system for generating project estimates in a consulting context.

## Project Structure

```
estimator-mcp/
├── spec/                      # Specification documents
│   ├── overview.md           # System overview and requirements
│   └── data-structure.md     # Data model specification
└── src/                      # Source code
    └── EstimatorApp/         # Blazor Web App for catalog management
```

## Components

### Blazor Web App (Catalog Manager)

A Blazor Server application for creating, editing, and managing the estimation catalog data.

**Features:**
- Interactive Server mode for real-time updates
- Manage roles with Copilot productivity multipliers
- Define countables (user stories, API endpoints, etc.)
- Create and edit features
- Build catalog entries with role-based time estimates
- T-shirt sizing with Fibonacci scaling (stores Medium baseline, auto-calculates other sizes)

**Technology Stack:**
- ASP.NET Core Blazor (.NET 10)
- Interactive Server render mode
- Repository pattern for future database migration
- JSON file storage with automatic versioning

### Data Storage

Catalog data is stored in JSON files with timestamp-based versioning:
- **Location**: `src/EstimatorApp/App_Data/Catalogs/`
- **Format**: `catalog-{ISO8601_TIMESTAMP}.json`
- **Version History**: Old files are preserved; latest file loaded at startup

### Repository Pattern

The application uses an interface-based repository pattern (`ICatalogRepository`) to abstract data access:
- **Current Implementation**: `JsonCatalogRepository` (file-based storage)
- **Future**: Easy migration to SQL Server, PostgreSQL, or other databases

## Getting Started

### Prerequisites
- .NET 10 SDK or later

### Running the Blazor App

```bash
cd src/EstimatorApp
dotnet run
```

Navigate to `https://localhost:5001` (or the URL shown in console).

### Initial Data

The application includes sample catalog data with:
- 3 roles (Developer, DevOps Engineer, Engagement Manager)
- 5 countables (User Story, API Endpoint, Database Table, etc.)
- 6 features (Basic CRUD, API Integration, CI/CD Pipeline, etc.)
- 3 catalog entries with role-based estimates

## T-Shirt Sizing Model

Catalog entries store only **Medium (M)** baseline estimates to minimize data entry. Other sizes are auto-calculated using Fibonacci scaling:

| Size | Fibonacci | Multiplier | Example (M=24h) |
|------|-----------|------------|-----------------|
| XS   | 1         | 0.2x (1/5) | 4.8h            |
| S    | 2         | 0.4x (2/5) | 9.6h            |
| M    | 5         | 1.0x       | 24h             |
| L    | 8         | 1.6x (8/5) | 38.4h           |
| XL   | 13        | 2.6x (13/5)| 62.4h           |

Final estimates also apply the role's Copilot multiplier (e.g., 0.7 for Developer = 30% faster with AI assistance).

## Future Development

### Phase 1 (Current)
- ✅ Blazor Web App for catalog management
- ✅ Repository pattern with JSON storage
- ✅ CRUD operations for all catalog entities

### Phase 2 (Planned)
- [ ] MCP Server implementation
- [ ] Estimate calculation service
- [ ] API endpoints for LLM integration
- [ ] Instructions tool for AI guidance
- [ ] Catalog query tool
- [ ] Estimate generation tool

### Phase 3 (Future)
- [ ] Database migration (SQL Server/PostgreSQL)
- [ ] User authentication and authorization
- [ ] Export functionality (PDF, CSV)
- [ ] Cost handling with rate sheets
- [ ] Non-functional requirements modeling
- [ ] Staffing plan generation

## Documentation

See the `spec/` directory for detailed specifications:
- `overview.md`: System goals, features, and requirements
- `data-structure.md`: Complete data model and JSON schema

## License

Copyright © 2025 Xebia
