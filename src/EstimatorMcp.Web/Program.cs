using EstimatorMcp.Web.Api;
using EstimatorMcp.Web.Auth;
using EstimatorMcp.Web.Components;
using EstimatorMcp.Web.Data;
using EstimatorMcp.Web.Services;
using EstimatorMcp.Web.Services.Auth;
using EstimatorMcp.Web.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
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

    // Persist Data Protection keys to the mounted volume so antiforgery tokens
    // and encrypted cookies survive container restarts.
    var dbPath = builder.Configuration["DatabasePath"] ?? "estimator.db";
    var dataDir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".";
    var keysDir = Path.Combine(dataDir, "DataProtection-Keys");
    Directory.CreateDirectory(keysDir);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysDir));

    // SQLite via EF Core.
    // Vfs=unix-none bypasses flock() — required for Azure Files (SMB) which doesn't
    // support POSIX advisory locks. Safe because maxReplicas is 1 (single writer).
    // Pooling=False ensures each DbContext open gets a fresh SQLite handle with no
    // stale page-cache, which prevents read misses after a write on a different handle.
    var connStr = $"Data Source=file:{dbPath}?vfs=unix-none;Pooling=False";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connStr));

    // Catalog provider (scoped to match DbContext lifetime)
    builder.Services.AddScoped<ICatalogDataProvider, DbCatalogDataProvider>();

    // Auth services (legacy email/Bearer-token path — removed in Phase 5)
    builder.Services.Configure<AzureEmailOptions>(builder.Configuration.GetSection("AzureEmailService"));
    builder.Services.AddScoped<IEmailService, AzureEmailService>();
    builder.Services.AddScoped<IVerificationService, VerificationService>();
    builder.Services.AddScoped<ITokenService, TokenService>();
    builder.Services.AddSingleton<TokenDisplayService>();

    // Authentication: OIDC (Xebia Entra) for the Blazor UI; BearerToken scheme for /mcp
    // and /api/catalog. AddMicrosoftIdentityWebApp wires Cookies + OpenIdConnect handlers
    // and reads ClientCredentials (federated MI assertion) from the AzureAd config section.
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        .EnableTokenAcquisitionToCallDownstreamApi()
        .AddInMemoryTokenCaches();

    builder.Services.AddAuthentication()
        .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthHandler>(BearerTokenAuthHandler.SchemeName, null);

    builder.Services.AddAuthorization(options =>
    {
        // Bearer-only policy for /mcp and /api/catalog: explicitly pins the scheme so
        // unauthenticated clients get 401, not a 302 redirect to Xebia sign-in.
        options.AddPolicy("BearerOnly", policy =>
        {
            policy.AuthenticationSchemes = [BearerTokenAuthHandler.SchemeName];
            policy.RequireAuthenticatedUser();
        });
    });

    // Lets <AuthorizeView> work inside Blazor components.
    builder.Services.AddCascadingAuthenticationState();

    // Microsoft.Identity.Web.UI ships embedded MVC controllers at
    // /MicrosoftIdentity/Account/SignIn and /SignOut.
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();

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

    app.UseRequestLocalization("en-US");
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // MCP endpoint — Bearer token only (no OIDC redirects)
    app.MapMcp("/mcp").RequireAuthorization("BearerOnly");

    // REST API — Bearer token only
    app.MapCatalogApi();

    // Microsoft.Identity.Web.UI sign-in/sign-out controller routes
    app.MapControllers();

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
