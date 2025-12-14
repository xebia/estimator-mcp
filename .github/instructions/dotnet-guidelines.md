# .NET Development Guidelines

## Overview

This project standardizes on modern .NET practices to ensure consistency, maintainability, and best use of the platform's async-first design.

## Framework & Language

- **.NET Version:** .NET 10 (latest stable release)
- **C# Version:** Latest version for the target .NET (typically C# 14 for .NET 10)
- **Target Framework:** `net10.0`
- **Language Features:** Leverage modern C# features (records, nullable reference types, pattern matching, expressions, etc.)

### Example .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

## Async/Await & Task Return Types

**Rule:** Prefer `Task` and `ValueTask` return types over `void` (except for event handlers, which must return `void`).

### Why?

- `void` methods cannot be awaited and make error handling difficult.
- `Task`/`ValueTask` enable proper error propagation, cancellation, and composition.
- Allows callers to await completion and handle exceptions.

### Examples

❌ **Avoid:**
```csharp
public void ProcessEstimate(string catalogId)
{
  var estimate = await GetEstimateAsync(catalogId);
  // Error handling is lost if exception occurs
}
```

✅ **Correct:**
```csharp
public async Task ProcessEstimateAsync(string catalogId)
{
  var estimate = await GetEstimateAsync(catalogId);
  // Exceptions propagate to caller
}

// Or with return value:
public async Task<EstimateResult> GetEstimateAsync(string catalogId)
{
  var data = await _catalogService.LoadAsync(catalogId);
  return new EstimateResult { ... };
}
```

### ValueTask Optimization

Use `ValueTask<T>` for hot-path, high-frequency methods that are **often synchronous** (e.g., cache lookups):

```csharp
public async ValueTask<CatalogEntry?> GetCatalogEntryAsync(string id)
{
  // If entry is cached, return synchronously without heap allocation
  if (_cache.TryGetValue(id, out var entry))
    return entry;
  
  // Otherwise, async load from disk
  return await LoadFromDiskAsync(id);
}
```

For most other cases, use `Task<T>`.

## Dependency Injection

**Pattern:** Use constructor injection via `IServiceProvider` / Microsoft.Extensions.DependencyInjection.

### Service Registration (Startup)

```csharp
// In Program.cs or startup code
var services = new ServiceCollection();

// Register MCP server
services.AddScoped<IMcpServer, CatalogMcpServer>();

// Register catalog service (loads JSON catalog)
services.AddSingleton<ICatalogService, CatalogService>();

// Register repositories/stores
services.AddScoped<IEstimateCalculator, EstimateCalculator>();

var serviceProvider = services.BuildServiceProvider();
var server = serviceProvider.GetRequiredService<IMcpServer>();
```

### Constructor Injection

```csharp
public class CatalogMcpServer
{
  private readonly ICatalogService _catalogService;
  private readonly IEstimateCalculator _calculator;
  private readonly ILogger<CatalogMcpServer> _logger;
  
  public CatalogMcpServer(
    ICatalogService catalogService,
    IEstimateCalculator calculator,
    ILogger<CatalogMcpServer> logger)
  {
    _catalogService = catalogService;
    _calculator = calculator;
    _logger = logger;
  }
  
  public async Task<EstimateResponse> EstimateAsync(EstimateRequest request)
  {
    _logger.LogInformation("Calculating estimate for {FeatureCount} features", request.Tasks.Count);
    return await _calculator.CalculateAsync(request);
  }
}
```

## MCP Server Implementation

**Package:** Use the official [ModelContextProtocol NuGet package](https://www.nuget.org/packages/ModelContextProtocol/) from the GitHub organization.

### Setup

```bash
dotnet add package ModelContextProtocol
```

### Tool Implementation

Each MCP tool (e.g., `estimate`, `catalog-query`, `instructions`) is defined as a method returning `Task<ToolResult>`:

```csharp
using ModelContextProtocol.SDK;

public class CatalogMcpServer : IMcpServer
{
  private readonly ICatalogService _catalog;
  
  public CatalogMcpServer(ICatalogService catalog)
  {
    _catalog = catalog;
  }
  
  [MpcTool(
    name: "estimate",
    description: "Calculate time estimates for a list of catalog features",
    inputSchema: typeof(EstimateInput))]
  public async Task<ToolResult> EstimateAsync(EstimateInput input)
  {
    var results = new List<EstimateLineItem>();
    foreach (var task in input.Tasks)
    {
      var hours = await _catalog.CalculateHoursAsync(
        task.FeatureId, 
        task.TshirtSize);
      results.Add(hours);
    }
    return ToolResult.Success(results);
  }
  
  [MpcTool(
    name: "instructions",
    description: "Return usage instructions for the MCP server")]
  public async Task<ToolResult> GetInstructionsAsync()
  {
    var instructions = await File.ReadAllTextAsync("mcp-instructions.md");
    return ToolResult.Success(instructions);
  }
  
  [MpcTool(
    name: "catalog-query",
    description: "Search the catalog by ID, name, or category",
    inputSchema: typeof(CatalogQueryInput))]
  public async Task<ToolResult> CatalogQueryAsync(CatalogQueryInput input)
  {
    var results = await _catalog.SearchAsync(input.Query, input.Category);
    return ToolResult.Success(results);
  }
}
```

## Blazor Web App (For Any UI)

If a UI is needed (e.g., estimate dashboard, catalog manager), use **Blazor Web App** with **InteractiveServer** render mode.

### Project Setup

```bash
dotnet new blazor -n EstimatorUI -i
```

### Key Configuration

**Program.cs:**
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
  .AddRazorComponents()
  .AddInteractiveServerComponents();

// Register services
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IEstimateService, EstimateService>();

var app = builder.Build();
app.UseAntiforgery();
app.MapRazorComponents<App>()
  .AddInteractiveServerRenderMode();

app.Run();
```

### Interactive Page Example

```razor
@page "/estimate"
@rendermode InteractiveServer
@inject ICatalogService CatalogService
@inject IEstimateService EstimateService

<PageTitle>Project Estimator</PageTitle>

<div class="container">
  <h1>Estimate Project</h1>
  
  @foreach (var feature in features ?? [])
  {
    <FeatureSelector Feature="feature" 
                      OnSizeSelected="@((size) => SelectFeature(feature.Id, size))" />
  }
  
  @if (selectedTasks.Count > 0)
  {
    <button class="btn btn-primary" @onclick="CalculateEstimate">
      Calculate Estimate
    </button>
  }
  
  @if (estimateResult != null)
  {
    <EstimateBreakdown Result="estimateResult" />
  }
</div>

@code {
  private List<Feature> features = [];
  private Dictionary<string, string> selectedTasks = [];
  private EstimateResult? estimateResult;
  
  protected override async Task OnInitializedAsync()
  {
    features = await CatalogService.GetFeaturesAsync();
  }
  
  private void SelectFeature(string featureId, string tshirtSize)
  {
    selectedTasks[featureId] = tshirtSize;
  }
  
  private async Task CalculateEstimate()
  {
    var request = new EstimateRequest
    {
      Tasks = selectedTasks.Select(kvp => 
        new TaskEstimate { FeatureId = kvp.Key, TshirtSize = kvp.Value }
      ).ToList()
    };
    
    estimateResult = await EstimateService.CalculateAsync(request);
  }
}
```

**Use `@rendermode InteractiveServer` on all interactive pages** to enable server-side interactivity with real-time updates.

## Console Applications

For any console/CLI projects, use **Spectre.Console** to create user-friendly terminal interfaces with rich formatting, progress bars, tables, and prompts.

### Setup

```bash
dotnet add package Spectre.Console
```

### Examples

**Simple output with styling:**
```csharp
using Spectre.Console;

AnsiConsole.MarkupLine("[bold green]Catalog loaded successfully![/]");
AnsiConsole.MarkupLine($"[yellow]Found {catalog.Features.Count} features[/]");
```

**Table output:**
```csharp
var table = new Table();
table.AddColumn("Feature");
table.AddColumn(new TableColumn("Developer").RightAligned());
table.AddColumn(new TableColumn("DevOps").RightAligned());
table.AddColumn(new TableColumn("EM").RightAligned());

foreach (var item in estimate.LineItems)
{
  table.AddRow(
    item.FeatureName,
    $"{item.DeveloperHours:F1}h",
    $"{item.DevOpsHours:F1}h",
    $"{item.EngagementManagerHours:F1}h"
  );
}

AnsiConsole.Write(table);
```

**Progress indicators:**
```csharp
await AnsiConsole.Progress()
  .StartAsync(async ctx =>
  {
    var task = ctx.AddTask("[green]Loading catalog[/]");
    
    while (!ctx.IsFinished)
    {
      await Task.Delay(100);
      task.Increment(1.5);
    }
  });
```

**Interactive prompts:**
```csharp
var featureId = AnsiConsole.Prompt(
  new SelectionPrompt<string>()
    .Title("Select a [green]feature[/] to estimate:")
    .PageSize(10)
    .AddChoices(catalog.Features.Select(f => f.Id)));

var tshirtSize = AnsiConsole.Prompt(
  new SelectionPrompt<string>()
    .Title("What [blue]size[/] is this feature?")
    .AddChoices(new[] { "XS", "S", "M", "L", "XL" }));
```

**Status spinners:**
```csharp
await AnsiConsole.Status()
  .StartAsync("Calculating estimates...", async ctx =>
  {
    ctx.Spinner(Spinner.Known.Star);
    ctx.SpinnerStyle(Style.Parse("green"));
    
    var result = await estimateService.CalculateAsync(request);
    return result;
  });
```

Use Spectre.Console for all user-facing console output to ensure consistent, readable terminal experiences.

## Logging & Observability

Use `ILogger<T>` for all logging and configure **OpenTelemetry** for distributed tracing, metrics, and logging export.

### Logging with ILogger

```csharp
public class EstimateCalculator
{
  private readonly ILogger<EstimateCalculator> _logger;
  
  public EstimateCalculator(ILogger<EstimateCalculator> logger)
  {
    _logger = logger;
  }
  
  public async Task<decimal> CalculateAsync(string featureId, string size)
  {
    _logger.LogInformation("Calculating {Feature} at size {Size}", featureId, size);
    try
    {
      var hours = await GetHoursAsync(featureId, size);
      _logger.LogDebug("Calculated {Hours} hours", hours);
      return hours;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to calculate estimate for {Feature}", featureId);
      throw;
    }
  }
}
```

### OpenTelemetry Setup

Install required packages:

```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

**Program.cs configuration:**

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry resource (service identification)
var resourceBuilder = ResourceBuilder.CreateDefault()
  .AddService("estimator-mcp", serviceVersion: "1.0.0")
  .AddTelemetrySdk();

// Add OpenTelemetry Tracing
builder.Services.AddOpenTelemetry()
  .WithTracing(tracing =>
  {
    tracing
      .SetResourceBuilder(resourceBuilder)
      .AddAspNetCoreInstrumentation()
      .AddHttpClientInstrumentation()
      .AddSource("EstimatorMcp.*")  // Custom activity sources
      .AddConsoleExporter()  // Dev: export to console
      .AddOtlpExporter();    // Prod: export to OTLP collector
  })
  .WithMetrics(metrics =>
  {
    metrics
      .SetResourceBuilder(resourceBuilder)
      .AddAspNetCoreInstrumentation()
      .AddHttpClientInstrumentation()
      .AddMeter("EstimatorMcp.*")  // Custom meters
      .AddConsoleExporter()
      .AddOtlpExporter();
  });

// Add OpenTelemetry Logging
builder.Logging.AddOpenTelemetry(logging =>
{
  logging.SetResourceBuilder(resourceBuilder);
  logging.AddConsoleExporter();
  logging.AddOtlpExporter();
});

var app = builder.Build();
```

### Custom Tracing & Metrics

**Activity Source (for custom tracing):**

```csharp
using System.Diagnostics;

public class EstimateService
{
  private static readonly ActivitySource ActivitySource = new("EstimatorMcp.Services");
  private readonly ILogger<EstimateService> _logger;
  
  public async Task<EstimateResult> CalculateAsync(EstimateRequest request)
  {
    using var activity = ActivitySource.StartActivity("CalculateEstimate");
    activity?.SetTag("feature.count", request.Tasks.Count);
    
    _logger.LogInformation("Calculating estimate for {Count} features", request.Tasks.Count);
    
    var result = await PerformCalculationAsync(request);
    
    activity?.SetTag("total.hours", result.TotalHours);
    return result;
  }
}
```

**Meter (for custom metrics):**

```csharp
using System.Diagnostics.Metrics;

public class CatalogService
{
  private static readonly Meter Meter = new("EstimatorMcp.Services");
  private static readonly Counter<long> EstimateRequestCounter = 
    Meter.CreateCounter<long>("estimator.requests", "requests", "Number of estimate requests");
  private static readonly Histogram<double> EstimateHoursHistogram = 
    Meter.CreateHistogram<double>("estimator.hours", "hours", "Distribution of estimated hours");
  
  public async Task<EstimateResult> ProcessAsync(EstimateRequest request)
  {
    EstimateRequestCounter.Add(1, new("feature.count", request.Tasks.Count));
    
    var result = await CalculateAsync(request);
    
    EstimateHoursHistogram.Record(result.TotalHours, new("size", request.Tasks.FirstOrDefault()?.TshirtSize ?? "unknown"));
    
    return result;
  }
}
```

**Key Points:**
- Use structured logging with `ILogger<T>` (parameterized messages, not string interpolation).
- Add custom activity sources (`ActivitySource`) for domain-specific tracing spans.
- Export to console for development, OTLP for production (to observability backends like Grafana/Prometheus/Jaeger).
- Tag activities and metrics with relevant context (feature IDs, sizes, counts).

### Fallback: Serilog for Console Logging

If OpenTelemetry is not configured (e.g., local development, simple console apps), use **Serilog** to ensure `ILogger` output goes to stdout/stderr with rich formatting.

**Setup:**

```bash
dotnet add package Serilog.Extensions.Hosting
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Formatting.Compact
```

**Program.cs (without OpenTelemetry):**

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Information()
  .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
  .MinimumLevel.Override("System", LogEventLevel.Warning)
  .Enrich.FromLogContext()
  .WriteTo.Console(outputTemplate: 
    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
  .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();
// ... rest of app setup

try
{
  Log.Information("Starting Estimator MCP Server");
  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application failed to start");
  throw;
}
finally
{
  Log.CloseAndFlush();
}
```

**Console App Example:**

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Debug()
  .WriteTo.Console()
  .CreateLogger();

var services = new ServiceCollection();
services.AddLogging(loggingBuilder => 
  loggingBuilder.AddSerilog(dispose: true));

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Catalog loaded with {Count} features", catalog.Features.Count);
```

**When to use:**
- Local development without observability infrastructure.
- Simple console tools or utilities.
- Docker containers where logs should go to stdout for container log collection.

**Production:** Prefer OpenTelemetry for full observability stack; Serilog is the fallback for simpler scenarios.

## Nullable Reference Types

**Requirement:** Enable `<Nullable>enable</Nullable>` in all projects.

- Use `string` for non-nullable strings.
- Use `string?` for nullable strings.
- Use `T?` for nullable reference types.

```csharp
public record EstimateRequest(
  List<TaskEstimate> Tasks,
  string? Notes = null);  // Optional notes field
  
public async Task<EstimateResult?> GetEstimateAsync(string featureId)
{
  // Return type can be null; caller must check
  return await _service.FindAsync(featureId);
}
```

## Summary

| Aspect | Standard |
|--------|----------|
| .NET Version | 10.0 |
| C# Version | Latest (14+) |
| Async Pattern | Task/ValueTask, async/await |
| Dependency Injection | Microsoft.Extensions.DependencyInjection |
| MCP Server | Official ModelContextProtocol NuGet package |
| UI Framework | Blazor Web App, InteractiveServer render mode |
| Console Apps | Spectre.Console for user-friendly CLI |
| Logging | ILogger<T> + OpenTelemetry |
| Observability | OpenTelemetry (tracing, metrics, logs) |
| Null Safety | Nullable reference types enabled |
