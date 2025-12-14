using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EstimatorMcp.Tools;

/// <summary>
/// MCP tool that provides instructions for using the Estimator MCP server.
/// </summary>
[McpServerToolType]
public sealed class InstructionsTool(IConfiguration configuration, ILogger<InstructionsTool> logger)
{
    private readonly string _dataPath = GetDataPath(configuration);

    private static string GetDataPath(IConfiguration configuration)
    {
        // Check environment variable first
        var envPath = Environment.GetEnvironmentVariable("ESTIMATOR_DATA_PATH");
        if (!string.IsNullOrEmpty(envPath))
        {
            return envPath;
        }

        // Check configuration
        var configPath = configuration["EstimatorMcp:DataPath"];
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }

        // Default: resolve relative to the executing assembly location
        // When running from bin/Debug/net10.0, go up 3 levels to project root, then into data
        var assemblyLocation = AppContext.BaseDirectory;
        return Path.Combine(assemblyLocation, "..", "..", "..", "data");
    }

    [McpServerTool, Description("Returns comprehensive instructions for AI assistants on how to use the Estimator MCP server to help users create project estimates. This tool should be called first to understand the workflow and available capabilities.")]
    public async Task<string> GetInstructions()
    {
        var instructionsFilePath = Path.Combine(_dataPath, "instructions.md");
        var fullPath = Path.GetFullPath(instructionsFilePath);
        
        logger.LogInformation("[InstructionsTool.GetInstructions] Loading instructions from {FilePath}", fullPath);
        
        try
        {
            if (!File.Exists(fullPath))
            {
                logger.LogError("[InstructionsTool.GetInstructions] Instructions file not found at {FilePath}", fullPath);
                return $"Error: Instructions file not found at {fullPath}";
            }

            var instructions = await File.ReadAllTextAsync(fullPath);
            logger.LogInformation("[InstructionsTool.GetInstructions] Successfully loaded instructions ({Length} characters)", instructions.Length);
            return instructions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[InstructionsTool.GetInstructions] Error reading instructions file");
            return $"Error reading instructions: {ex.Message}";
        }
    }
}
