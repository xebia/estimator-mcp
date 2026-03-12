using CatalogEditor.Components;
using CatalogEditor.Components.Account;
using CatalogEditor.Data;
using CatalogEditor.Services;
using CatalogEditor.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Register catalog data provider
builder.Services.AddSingleton<ICatalogDataProvider, JsonCatalogDataProvider>();

// Auth state revalidation for Blazor Server (cookie expiry enforced by middleware)
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddCascadingAuthenticationState();

// Cookie authentication — 7-day hard expiry, no sliding window
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

// OTP services
builder.Services.Configure<AzureEmailOptions>(builder.Configuration.GetSection("AzureEmailService"));
builder.Services.AddScoped<IEmailService, AzureEmailService>();
builder.Services.AddScoped<IVerificationService, VerificationService>();

// EF Core with SQLite (verification codes only)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=catalog-editor.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Apply any pending migrations on startup (safe to run against existing databases)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Logout endpoint
app.MapPost("/account/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CatalogEditor.Client._Imports).Assembly);

app.Run();
