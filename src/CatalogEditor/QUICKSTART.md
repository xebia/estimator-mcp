# Catalog Editor - Quick Start Guide

## Overview

The Catalog Editor is a Blazor web application for managing the MCP Estimator catalog. It provides a clean UI to manage roles, features, and catalog entries with time estimates.

## Quick Start

1. **Navigate to the project directory**:
   ```bash
   cd src/CatalogEditor/CatalogEditor/CatalogEditor
   ```

2. **(Optional) Copy sample data**:
   ```bash
   # Windows
   mkdir data\catalogs
   copy ..\..\sample-data\catalog-2025-12-13T00-00-00Z.json data\catalogs\
   
   # Linux/Mac
   mkdir -p data/catalogs
   cp ../../sample-data/catalog-2025-12-13T00-00-00Z.json data/catalogs/
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

4. **Open your browser**:
   - Navigate to the URL shown in the console (typically `https://localhost:5001`)

## Application Features

### Main Sections

- **Home**: Dashboard with quick access to all sections
- **Roles**: Manage implementation roles with Copilot productivity multipliers
- **Features**: Manage work items and technical activities
- **Catalog Entries**: Map features to role-based time estimates

### Data Storage

- All data is stored in JSON files at `data/catalogs/`
- Files are named with timestamps: `catalog-{ISO8601}.json`
- The application loads the latest file automatically
- Old files are kept for version history

## Key Concepts

### Provider Pattern
The application uses dependency injection with a provider pattern:
- **Interface**: `ICatalogDataProvider`
- **Current Implementation**: `JsonCatalogDataProvider` (JSON files)
- **Future Options**: Easily swap to SQL, Azure Storage, API, etc.

### T-Shirt Sizing
Catalog entries store only **Medium (M)** baseline estimates. Other sizes are calculated using Fibonacci scaling:
- **XS**: M × 0.2 (1/5)
- **S**: M × 0.4 (2/5)
- **M**: M × 1.0 (baseline)
- **L**: M × 1.6 (8/5)
- **XL**: M × 2.6 (13/5)

### Copilot Multiplier
Each role has a productivity multiplier applied to all estimates:
- **0.70**: 30% faster with Copilot
- **1.0**: No AI acceleration

## Configuration

### Custom Data Path

Configure where catalog files are stored:

**appsettings.json**:
```json
{
  "CatalogDataPath": "C:\\custom\\path\\catalogs"
}
```

**Environment Variable**:
```
CatalogDataPath=C:\custom\path\catalogs
```

Default: `{CurrentDirectory}/data/catalogs`

## Development

### Prerequisites
- .NET 10.0 SDK

### Build
```bash
dotnet build
```

### Publish
```bash
dotnet publish -c Release -o ./publish
```

## Next Steps

1. **Add Roles**: Define your team roles with appropriate Copilot multipliers
2. **Add Features**: Create work items that will appear in estimates
3. **Add Catalog Entries**: Map features to role estimates (Medium baseline)
4. **Export Data**: The JSON file can be used by the MCP Server for estimates

## Documentation

- [Detailed README](README.md) - Full documentation
- [Data Structure Specification](../../spec/data-structure.md) - JSON schema details
- [Overview](../../spec/overview.md) - System overview

## Troubleshooting

### Port Already in Use
If port 5001 is busy, specify a different port:
```bash
dotnet run --urls="https://localhost:5002"
```

### Data Not Loading
Ensure your JSON file is in the correct format and location:
```
data/catalogs/catalog-{timestamp}.json
```

### Form Components Not Working
If you see errors about missing components, try rebuilding:
```bash
dotnet clean
dotnet build
```
