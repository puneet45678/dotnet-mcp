namespace DotnetMcp.Core.Models;

public sealed record BuildErrorExplanation
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Explanation { get; init; } = "";
    public List<string> CommonCauses { get; init; } = [];
    public string? ExampleFix { get; init; }
    public bool IsKnown { get; init; }
}
