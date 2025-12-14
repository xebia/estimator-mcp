# Instructions & Guidelines

This folder contains detailed development guidelines and best practices for the Estimator MCP project. These documents help AI coding agents and developers understand the technical standards, patterns, and conventions used throughout the codebase.

## Quick Start

**Start here:** [GitHub Copilot Instructions](../copilot-instructions.md) – High-level architecture, key patterns, and project overview.

## Guidelines in This Folder

### [.NET Development Guidelines](dotnet-guidelines.md)

Complete standards for .NET 10 development including:
- Framework and language version requirements (.NET 10, C# latest)
- Async/await patterns (`Task`/`ValueTask` over `void`)
- Dependency injection setup and patterns
- MCP server implementation using official ModelContextProtocol package
- Blazor Web App configuration (InteractiveServer render mode)
- Console app development with Spectre.Console
- Logging and observability (OpenTelemetry + Serilog fallback)
- Nullable reference types and null safety

## Detailed Specifications

For comprehensive project specifications, requirements, and data structures, see the [`/spec`](../../spec/) folder:

### [Overview](../../spec/overview.md)
- Project goals and objectives
- Key features and requirements
- MCP tool definitions (`instructions`, `estimate`, `catalog-query`)
- MVP scope and future phases
- LLM workflow and integration patterns

### [Data Structure](../../spec/data-structure.md)
- Complete JSON catalog schema
- Role, countable, feature, and catalog entry definitions
- Fibonacci scaling model for t-shirt sizing
- LINQ patterns for querying catalog data
- File storage and versioning strategy
- Copilot productivity multiplier system

## Document Purpose

| Document | Purpose | When to Use |
|----------|---------|-------------|
| [copilot-instructions.md](../copilot-instructions.md) | High-level architecture, key patterns, project context | First document to read; understanding the "big picture" |
| [dotnet-guidelines.md](dotnet-guidelines.md) | .NET implementation standards and code conventions | Writing or reviewing C# code |
| [spec/overview.md](../../spec/overview.md) | Requirements, features, MVP scope | Understanding project goals and what to build |
| [spec/data-structure.md](../../spec/data-structure.md) | JSON schema, calculation formulas, data patterns | Working with catalog data or estimation logic |

## Contributing

When adding new guidelines or instructions:
1. Create a new `.md` file in this folder with a descriptive name.
2. Update this README to include the new document in the index.
3. Cross-reference related documents to maintain navigation.
4. Use concrete code examples from the project context.
5. Focus on project-specific patterns, not generic advice.
