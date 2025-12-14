# Catalog Editor

A Blazor web application for managing the MCP Estimator catalog data, including roles, features, and catalog entries with time estimates.

## Features

- **Roles Management**: Define implementation roles with Copilot productivity multipliers
- **Catalog Management**: Manage features/work items directly with role-based time estimates using Medium (M) baseline
  - Each catalog entry includes ID, Name, Description, Category, and role estimates
  - Other t-shirt sizes (XS, S, L, XL) are auto-calculated using Fibonacci scaling

## Architecture

### Data Access Pattern
The application uses a **provider pattern with Dependency Injection** for data access:

- **Interface**: `ICatalogDataProvider` - Defines all data operations
- **Implementation**: `JsonCatalogDataProvider` - JSON file-based storage
- **Future-proof**: Easy to swap implementations (SQL, Azure Storage, etc.)

### Data Storage

All catalog data is stored in a single JSON file with timestamp-based versioning:
- **Location**: `data/catalogs/` (configurable)
- **Format**: `catalog-{ISO8601_TIMESTAMP}.json`
- **Example**: `catalog-2025-12-13T10-30-00Z.json`

The application automatically loads the latest file at startup and caches it in memory.

## Configuration

### Data Path Configuration

You can customize the catalog data storage location via:

1. **appsettings.json**:
```json
{
  "CatalogDataPath": "C:\\custom\\path\\catalogs"
}
```

2. **Environment Variable**:
```
CatalogDataPath=C:\custom\path\catalogs
```

If not specified, defaults to `{CurrentDirectory}/data/catalogs`

## Running the Application

### Prerequisites
- .NET 10.0 SDK or later

### Development
```bash
cd src/CatalogEditor/CatalogEditor/CatalogEditor
dotnet run
```

Then navigate to `https://localhost:5001` (or the port shown in console)

### Build
```bash
dotnet build
```

### Publish
```bash
dotnet publish -c Release -o ./publish
```

## Project Structure

```
CatalogEditor/
├── Models/                          # Data models
│   ├── Role.cs
│   ├── CatalogEntry.cs
│   └── CatalogData.cs
├── Services/                        # Data access layer
│   ├── ICatalogDataProvider.cs      # Provider interface
│   └── JsonCatalogDataProvider.cs   # JSON implementation
├── Components/
│   ├── Pages/                       # Blazor pages
│   │   ├── Home.razor               # Dashboard
│   │   ├── Roles.razor              # Role list
│   │   ├── RoleEdit.razor           # Role add/edit
│   │   ├── Catalog.razor            # Catalog entry list
│   │   └── CatalogEdit.razor        # Catalog entry add/edit
│   └── Layout/                      # Layout components
│       └── NavMenu.razor            # Navigation menu
└── Program.cs                       # Application startup
```

## Data Model

### Role
- **Id**: Unique identifier (e.g., "developer")
- **Name**: Display name
- **Description**: Role responsibilities
- **CopilotMultiplier**: AI productivity factor (0.7 = 30% faster)

### Catalog Entry
- **Id**: Unique identifier (e.g., "basic-crud")
- **Name**: Display name
- **Description**: Detailed scope
- **Category**: Optional grouping (e.g., "feature", "integration", "devops", "data", "qa")
- **MediumEstimates**: List of role estimates for Medium size
  - **RoleId**: Reference to a role
  - **Hours**: Baseline hours for Medium size

## T-Shirt Sizing with Fibonacci Scaling

The catalog stores only **Medium (M)** baseline estimates. Other sizes are calculated:

| Size | Fibonacci | Multiplier | Example (M=24h) |
|------|-----------|------------|-----------------|
| XS   | 1         | 0.2x       | 4.8h            |
| S    | 2         | 0.4x       | 9.6h            |
| M    | 5         | 1.0x       | 24h             |
| L    | 8         | 1.6x       | 38.4h           |
| XL   | 13        | 2.6x       | 62.4h           |

## Future Enhancements

Potential improvements for swapping the data provider:

1. **SQL Database**: Implement `SqlCatalogDataProvider`
2. **Azure Storage**: Implement `AzureBlobCatalogDataProvider`
3. **API Backend**: Implement `ApiCatalogDataProvider`
4. **User Authentication**: Add role-based access control
5. **Audit Logging**: Track who changed what and when
6. **Validation**: Add data validation rules
7. **Import/Export**: Support CSV, Excel formats

## License

This project is part of the MCP Estimator system.
