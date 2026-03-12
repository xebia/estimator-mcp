using System.ComponentModel;
using ModelContextProtocol.Server;

namespace EstimatorMcp.Web.Tools;

[McpServerToolType]
public sealed class InstructionsTool(IConfiguration configuration, ILogger<InstructionsTool> logger)
{
    private string GetDataPath()
    {
        var envPath = Environment.GetEnvironmentVariable("ESTIMATOR_DATA_PATH");
        if (!string.IsNullOrEmpty(envPath)) return envPath;

        var configPath = configuration["EstimatorMcp:DataPath"];
        if (!string.IsNullOrEmpty(configPath)) return configPath;

        return Path.Combine(AppContext.BaseDirectory, "data");
    }

    [McpServerTool, Description("Returns comprehensive instructions for AI assistants on how to use the Estimator MCP server to help users create project estimates. This tool should be called first to understand the workflow and available capabilities.")]
    public async Task<string> GetInstructions()
    {
        var fullPath = Path.GetFullPath(Path.Combine(GetDataPath(), "instructions.md"));
        logger.LogInformation("[InstructionsTool] Loading instructions from {FilePath}", fullPath);

        try
        {
            if (!File.Exists(fullPath))
                return $"Error: Instructions file not found at {fullPath}";

            return await File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[InstructionsTool] Error reading instructions file");
            return $"Error reading instructions: {ex.Message}";
        }
    }
}
