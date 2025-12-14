using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace EstimatorMcp;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure Serilog first - NO console output, only file
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.File(
                path: "logs/estimator-mcp-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: 100_000_000,
                rollOnFileSizeLimit: true,
                shared: true
            )
            .CreateLogger();

        try
        {
            Log.Information("Starting estimator-mcp application");

            var host = CreateHostBuilder(args).Build();
            
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog() // Use Serilog for logging
            .ConfigureLogging(logging =>
            {
                // Clear all default logging providers to prevent console output
                logging.ClearProviders();
                logging.AddSerilog(dispose: true);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Configure OpenTelemetry
                services.AddOpenTelemetry()
                    .ConfigureResource(resource => resource
                        .AddService(
                            serviceName: "estimator-mcp",
                            serviceVersion: "1.0.0"))
                    .WithTracing(tracing => tracing
                        .AddSource("estimator-mcp")
                        .SetSampler(new AlwaysOnSampler())
                        .AddHttpClientInstrumentation()
                        .AddAspNetCoreInstrumentation());

                // Register your services here
                // Example:
                // services.AddSingleton<IMyService, MyService>();
                
                // Register the background service that will run the MCP server
                services.AddHostedService<McpServerHostedService>();
            });
}

/// <summary>
/// Hosted service to run the MCP server
/// </summary>
public class McpServerHostedService : BackgroundService
{
    private readonly ILogger<McpServerHostedService> _logger;

    public McpServerHostedService(ILogger<McpServerHostedService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MCP Server starting");

        try
        {
            // TODO: Initialize and run your MCP server here
            
            while (!stoppingToken.IsCancellationRequested)
            {
                // Your MCP server logic
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MCP Server");
            throw;
        }
        finally
        {
            _logger.LogInformation("MCP Server stopping");
        }
    }
}
