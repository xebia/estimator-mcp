using EstimatorMcp.Web.Api;
using EstimatorMcp.Web.Auth;
using EstimatorMcp.Web.Components;
using EstimatorMcp.Web.Data;
using EstimatorMcp.Web.Services;
using EstimatorMcp.Web.Services.Auth;
using EstimatorMcp.Web.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
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
    // Only applied on Linux; Windows native sqlite has no equivalent vfs and rejects it.
    // Pooling=False ensures each DbContext open gets a fresh SQLite handle with no
    // stale page-cache, which prevents read misses after a write on a different handle.
    var connStr = OperatingSystem.IsLinux()
        ? $"Data Source=file:{dbPath}?vfs=unix-none;Pooling=False"
        : $"Data Source={dbPath};Pooling=False";
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

    // JwtBearer for Entra-issued JWTs (Copilot Studio, az CLI tokens, etc.). Reads the
    // same AzureAd config section; Authority/Audience derived from TenantId/ClientId.
    builder.Services.AddAuthentication()
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
    {
        // Token-only policy for /mcp and /api/catalog: accepts EITHER an Entra JWT
        // (new flow) OR a legacy opaque token (transitional, removed in Phase 5).
        // Either scheme that succeeds satisfies the policy. Pinning these schemes
        // also stops unauthenticated callers from being 302'd to Xebia sign-in.
        options.AddPolicy("BearerOnly", policy =>
        {
            policy.AuthenticationSchemes =
            [
                JwtBearerDefaults.AuthenticationScheme,
                BearerTokenAuthHandler.SchemeName,
            ];
            policy.RequireAuthenticatedUser();
        });
    });

    // Lets <AuthorizeView> work inside Blazor components.
    builder.Services.AddCascadingAuthenticationState();

    // Honor X-Forwarded-Proto / X-Forwarded-For from the Container Apps ingress so that
    // Request.Scheme reflects the public-facing protocol (https) rather than the internal
    // hop (http). Without this, generated URLs (e.g. PRM resource, OIDC redirect_uri,
    // resource_metadata) come out as http:// and break spec-compliant clients. Trust
    // all proxies here — only the Container Apps ingress can reach the pod, so widening
    // the trust boundary is safe.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

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

    // ForwardedHeaders must run very early so that downstream middleware (HTTPS redirect,
    // OIDC redirect-URI generation, our PRM middleware) sees the correct scheme/host.
    app.UseForwardedHeaders();

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

    // Status-code pages re-execute the request to /not-found and produce HTML, which is
    // wrong for /mcp and /api callers — they need clean 401/404 responses with their
    // original headers (e.g. WWW-Authenticate). Skip the rewrite for those paths.
    app.UseWhen(
        ctx => !ctx.Request.Path.StartsWithSegments("/mcp")
            && !ctx.Request.Path.StartsWithSegments("/api")
            && !ctx.Request.Path.StartsWithSegments("/.well-known"),
        branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    // Per the MCP spec (RFC 9728), unauthenticated requests to a protected MCP endpoint
    // must include a WWW-Authenticate header with resource_metadata pointing at the
    // OAuth Protected Resource Metadata document. This middleware must wrap the auth
    // pipeline (registered before UseAuthentication) so that when the authorization
    // middleware short-circuits with 401 the unwind path still runs through here and
    // can overwrite the WWW-Authenticate set by the inner challenge handlers.
    app.Use(async (ctx, next) =>
    {
        await next();
        if (!ctx.Response.HasStarted
            && ctx.Response.StatusCode == 401
            && ctx.Request.Path.StartsWithSegments("/mcp"))
        {
            var prmUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}/.well-known/oauth-protected-resource/mcp";
            ctx.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{prmUrl}\"";
        }
    });

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // OAuth Protected Resource Metadata for the /mcp resource (RFC 9728). Anonymous —
    // it advertises the authorization server (Xebia Entra) and the scope required to call /mcp.
    app.MapGet("/.well-known/oauth-protected-resource/mcp", (HttpRequest req, IConfiguration cfg) =>
    {
        var tenantId = cfg["AzureAd:TenantId"];
        var clientId = cfg["AzureAd:ClientId"];
        return Results.Json(new
        {
            resource = $"{req.Scheme}://{req.Host}/mcp",
            authorization_servers = new[] { $"https://login.microsoftonline.com/{tenantId}/v2.0" },
            scopes_supported = new[] { $"api://{clientId}/access_as_user" },
            bearer_methods_supported = new[] { "header" },
        });
    }).AllowAnonymous();

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
