using EstimatorMcp.Web.Api;
using EstimatorMcp.Web.Auth;
using EstimatorMcp.Web.Components;
using EstimatorMcp.Web.Data;
using EstimatorMcp.Web.Services;
using EstimatorMcp.Web.Services.Auth;
using EstimatorMcp.Web.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var logsPath = Environment.GetEnvironmentVariable("ESTIMATOR_LOGS_PATH") ?? "logs";
Directory.CreateDirectory(logsPath);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logsPath, "estimator-web-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 100_000_000,
        rollOnFileSizeLimit: true,
        shared: true)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // SQLite via EF Core.
    // unix-none VFS bypasses flock() — required for Azure Files (SMB) which doesn't
    // support POSIX advisory locks. Safe because maxReplicas is 1 (single writer).
    var dbPath = builder.Configuration["DatabasePath"] ?? "estimator.db";
    var connStr = $"Data Source=file:{Uri.EscapeDataString(dbPath)}?vfs=unix-none&mode=rwc";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connStr));

    // Catalog provider (scoped to match DbContext lifetime)
    builder.Services.AddScoped<ICatalogDataProvider, DbCatalogDataProvider>();

    // Auth services
    builder.Services.Configure<AzureEmailOptions>(builder.Configuration.GetSection("AzureEmailService"));
    builder.Services.AddScoped<IEmailService, AzureEmailService>();
    builder.Services.AddScoped<IVerificationService, VerificationService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddSingleton<TokenDisplayService>();

    // Bearer token authentication for MCP endpoint
    builder.Services.AddAuthentication(BearerTokenAuthHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthHandler>(BearerTokenAuthHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    // Blazor
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // MCP Server (HTTP/Streamable transport via MapMcp)
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithTools<InstructionsTool>()
        .WithTools<CatalogTool>()
        .WithTools<CalculateEstimateTool>();

    var app = builder.Build();

    // Apply EF Core migrations and seed data on startup
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedFromJsonIfEmptyAsync(scope.ServiceProvider, builder.Configuration);
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // MCP endpoint (HTTP/Streamable) — requires Bearer token
    app.MapMcp("/mcp").RequireAuthorization();

    // REST API
    app.MapCatalogApi();

    // Blazor
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
