namespace DotnetMcp.Core.Models;

public sealed class CommandResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public int ExitCode { get; init; }
}
