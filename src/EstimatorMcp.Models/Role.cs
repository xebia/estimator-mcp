namespace EstimatorMcp.Models;

public class Role
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CopilotMultiplier { get; set; } = 1.0m;
}
