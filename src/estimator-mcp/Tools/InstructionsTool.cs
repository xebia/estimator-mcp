using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.Logging;

namespace EstimatorMcp.Tools;

/// <summary>
/// MCP tool that provides instructions for using the Estimator MCP server.
/// </summary>
[McpServerToolType]
public sealed class InstructionsTool(ILogger<InstructionsTool> logger)
{
    private const string InstructionsFilePath = "data/instructions.md";

    [McpServerTool, Description("Returns comprehensive instructions for AI assistants on how to use the Estimator MCP server to help users create project estimates. This tool should be called first to understand the workflow and available capabilities.")]
    public async Task<string> GetInstructions()
    {
        logger.LogInformation("[InstructionsTool.GetInstructions] Loading instructions from {FilePath}", InstructionsFilePath);
        
        try
        {
            if (!File.Exists(InstructionsFilePath))
            {
                logger.LogError("[InstructionsTool.GetInstructions] Instructions file not found at {FilePath}", InstructionsFilePath);
                return $"Error: Instructions file not found at {InstructionsFilePath}";
            }

            var instructions = await File.ReadAllTextAsync(InstructionsFilePath);
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
