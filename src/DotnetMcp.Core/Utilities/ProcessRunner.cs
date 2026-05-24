using System.Diagnostics;
using DotnetMcp.Core.Models;

namespace DotnetMcp.Core.Utilities;

public static class ProcessRunner
{
    public static async Task<CommandResult> RunAsync(
        string command,
        string arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new CommandResult
        {
            Success = process.ExitCode == 0,
            Output = outputBuilder.ToString().Trim(),
            Error = errorBuilder.ToString().Trim(),
            ExitCode = process.ExitCode,
        };
    }
}
