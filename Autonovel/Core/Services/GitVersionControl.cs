using System.Diagnostics;

namespace Autonovel.Core.Services;

public interface IVersionControl
{
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

    private async Task<string> RunGitAsync(string[] args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = string.Join(" ", args),
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