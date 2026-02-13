using System.Text.Json.Serialization;

namespace EstimatorMcp.Models;

public class Role
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal CopilotMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// The techstack this role belongs to. Null means it is a global role.
    /// Not serialized in JSON because it is derived from the parent TechStack.
    /// </summary>
    [JsonIgnore]
    public string? TechStackId { get; set; }
}
