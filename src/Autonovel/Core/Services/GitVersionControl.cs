using System.Diagnostics;
using System.Text;

namespace Autonovel.Core.Services;

public interface IVersionControl
{
    Task<bool> ConfirmCommitAsync(string message, CancellationToken ct = default);
    Task<string> CommitAsync(string message, CancellationToken ct = default);
    Task CommitAsync(string[] files, string message, CancellationToken ct = default);
    Task ResetHardAsync(string target, CancellationToken ct = default);
    Task HardResetAsync(CancellationToken ct = default);
    Task<string> GetCurrentCommitHashAsync(CancellationToken ct = default);
    Task AddAllAsync(CancellationToken ct = default);
}

public class GitVersionControl : IVersionControl
{
    private readonly string _workingDirectory;

    public GitVersionControl(string workingDirectory)
    {
        _workingDirectory = workingDirectory;
    }

    public async Task AddAllAsync(CancellationToken ct = default)
    {
        await RunGitAsync(["add", "-A"], ct);
    }

    public async Task<bool> ConfirmCommitAsync(string message, CancellationToken ct = default)
    {
        Console.Write($"Commit: {message}\nDo you want to proceed? (Y/N): ");
        var response = Console.ReadLine()?.Trim().ToUpper();
        return response != "N";
    }

    public async Task<string> CommitAsync(string message, CancellationToken ct = default)
    {
        await AddAllAsync(ct);
        var result = await RunGitAsync(["commit", "-m", message], ct);
        return result;
    }

    public async Task CommitAsync(string[] files, string message, CancellationToken ct = default)
    {
        foreach (var file in files)
        {
            await RunGitAsync(["add", file], ct);
        }
        await RunGitAsync(["commit", "-m", message], ct);
    }

    public async Task HardResetAsync(CancellationToken ct = default)
    {
        await ResetHardAsync("HEAD", ct);
    }

    public async Task ResetHardAsync(string target, CancellationToken ct = default)
    {
        await RunGitAsync(["reset", "--hard", target], ct);
    }

    public async Task<string> GetCurrentCommitHashAsync(CancellationToken ct = default)
    {
        var result = await RunGitAsync(["rev-parse", "--short", "HEAD"], ct);
        return result.Trim();
    }

    private static string BuildGitArguments(string[] args)
    {
        // Properly escape arguments for command-line parsing.
        // Arguments containing spaces, quotes, or special chars need quoting.
        var result = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) result.Append(' ');
            var arg = args[i];
            // Escape embedded double-quotes by doubling them
            if (arg.Contains('"') || arg.Contains(' ') || arg.Contains(':') || arg.Contains('='))
            {
                result.Append('"');
                result.Append(arg.Replace("\"", "\"\""));
                result.Append('"');
            }
            else
            {
                result.Append(arg);
            }
        }
        return result.ToString();
    }

    private async Task<string> RunGitAsync(string[] args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = BuildGitArguments(args),
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        
        var output = await Task.Run(() => process.StandardOutput.ReadToEnd(), ct);
        var error = await Task.Run(() => process.StandardError.ReadToEnd(), ct);
        
        await process.WaitForExitAsync(ct);
        
        if (process.ExitCode != 0)
            throw new Exception($"Git failed with exit code {process.ExitCode}: {error}");
            
        return output;
    }
}