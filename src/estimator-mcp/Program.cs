using EstimatorMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Serilog;
using Serilog.Events;

namespace EstimatorMcp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Get logs directory from environment variable, fallback to current directory
        var logsPath = Environment.GetEnvironmentVariable("ESTIMATOR_LOGS_PATH") ?? "logs";
        
        // Ensure logs directory exists
        Directory.CreateDirectory(logsPath);
        
        // Configure Serilog for file logging only (no console output to avoid interfering with stdio)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.File(
                path: Path.Combine(logsPath, "estimator-mcp-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 100_000_000,
                rollOnFileSizeLimit: true,
                shared: true
            )
            .CreateLogger();

        try
        {
            Log.Information("Starting estimator-mcp MCP server");

            var builder = Host.CreateApplicationBuilder(args);

            // Clear default logging providers and use Serilog (file-only, no console to stdout)
            builder.Logging.ClearProviders();
            builder.Services.AddSerilog();

            builder.Services.AddMcpServer()
                .WithTools<InstructionsTool>()
                .WithTools<CatalogTool>()
                .WithTools<CalculateEstimateTool>()
                .WithStdioServerTransport();

            var host = builder.Build();
            
            Log.Information("MCP Server configured with stdio transport and InstructionsTool");
            
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
