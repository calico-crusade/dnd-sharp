using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DndSharp.PokeDex;

public interface IPokeApiDataInitializer
{
    Task PrepareAsync(CancellationToken token = default);
}

/// <summary>Creates or safely fast-forwards the configured PokéAPI static data checkout.</summary>
public sealed class PokeApiDataInitializer : IPokeApiDataInitializer
{
    public const string OfficialRepository = "https://github.com/PokeAPI/api-data.git";
    private readonly IConfiguration configuration;
    private readonly ILogger<PokeApiDataInitializer> logger;
    private readonly string repositoryUrl;

    public PokeApiDataInitializer(IConfiguration configuration, ILogger<PokeApiDataInitializer> logger)
        : this(configuration, logger, OfficialRepository) { }

    internal PokeApiDataInitializer(IConfiguration configuration, ILogger<PokeApiDataInitializer> logger, string repositoryUrl)
    {
        this.configuration = configuration;
        this.logger = logger;
        this.repositoryUrl = repositoryUrl;
    }

    public async Task PrepareAsync(CancellationToken token = default)
    {
        if (bool.TryParse(configuration["PokeApi:AutoUpdate"], out var enabled) && !enabled)
        {
            logger.LogInformation("Automatic PokéAPI data updates are disabled.");
            return;
        }

        var configuredPath = configuration["PokeApi:BasePath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            logger.LogWarning("PokéAPI data is not configured; Pokémon commands may be unavailable.");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            var dataPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredPath));
            var repoPath = Path.GetDirectoryName(dataPath);
            if (!Path.GetFileName(dataPath).Equals("data", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(repoPath) ||
                string.IsNullOrEmpty(Path.GetFileName(repoPath)) || Path.GetDirectoryName(repoPath) is null)
            {
                logger.LogWarning("PokéAPI data path must be the data directory inside a checkout; automatic update skipped.");
                return;
            }

            if (Directory.Exists(repoPath))
                await UpdateAsync(repoPath, dataPath, timeout.Token);
            else if (File.Exists(repoPath))
                logger.LogWarning("The configured PokéAPI checkout path is a file; automatic update skipped.");
            else
                await CloneAsync(repoPath, dataPath, timeout.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("PokéAPI data setup timed out; the bot will continue with any existing data.");
        }
        catch (Exception ex)
        {
            // Do not log Git output: a configured remote URL may contain credentials.
            logger.LogWarning("PokéAPI data setup failed ({ErrorType}); the bot will continue with any existing data.", ex.GetType().Name);
        }
    }

    private async Task CloneAsync(string repoPath, string dataPath, CancellationToken token)
    {
        var parent = Path.GetFullPath(Path.GetDirectoryName(repoPath)!);
        Directory.CreateDirectory(parent);
        var repoName = Path.GetFileName(repoPath);
        var stage = Path.GetFullPath(Path.Combine(parent, $".{repoName}.clone-{Guid.NewGuid():N}"));
        // Both the staging directory and final target must be immediate children of
        // the configured checkout's parent, including on Windows.
        if (!SamePath(Path.GetDirectoryName(stage)!, parent) || !SamePath(Path.GetDirectoryName(Path.GetFullPath(repoPath))!, parent))
            throw new InvalidOperationException("The PokéAPI staging path is invalid.");

        logger.LogInformation("Cloning the PokéAPI data repository; the first startup may take a while.");
        try
        {
            var result = await GitAsync(token, "clone", "--depth", "1", "--single-branch", "--no-tags", "--no-hardlinks", "--", repositoryUrl, stage);
            if (result.ExitCode != 0 || !HasData(Path.Combine(stage, "data")))
                throw new InvalidOperationException("Git did not produce a usable PokéAPI checkout.");
            Directory.Move(stage, Path.GetFullPath(repoPath));
            if (!HasData(dataPath))
                throw new InvalidOperationException("The PokéAPI data directory is missing.");
            logger.LogInformation("PokéAPI data is ready.");
        }
        finally
        {
            // Only delete our own randomly named staging directory, never the configured checkout.
            CleanupStage(stage, parent, repoName);
        }
    }

    private static void CleanupStage(string stage, string parent, string repoName)
    {
        if (!Directory.Exists(stage) || !SamePath(Path.GetDirectoryName(Path.GetFullPath(stage))!, parent) ||
            !Path.GetFileName(stage).StartsWith($".{repoName}.clone-", StringComparison.Ordinal) ||
            (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0)
            return;

        // Git marks object files read-only on Windows. Never follow reparse points
        // while clearing those attributes inside our verified staging directory.
        if (OperatingSystem.IsWindows())
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var entry in Directory.EnumerateFileSystemEntries(stage, "*", options))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
            }
        }
        Directory.Delete(stage, recursive: true);
    }

    private async Task UpdateAsync(string repoPath, string dataPath, CancellationToken token)
    {
        var root = await GitAsync(token, "-C", repoPath, "rev-parse", "--show-toplevel");
        if (root.ExitCode != 0 || !SamePath(root.Output, repoPath))
        {
            logger.LogWarning("The configured PokéAPI directory is not the root of a Git checkout; automatic update skipped.");
            return;
        }
        var remote = await GitAsync(token, "-C", repoPath, "remote", "get-url", "origin");
        if (remote.ExitCode != 0 || !IsExpectedRemote(remote.Output))
        {
            logger.LogWarning("The configured PokéAPI checkout does not use the expected origin; automatic update skipped.");
            return;
        }
        var status = await GitAsync(token, "-C", repoPath, "status", "--porcelain", "--untracked-files=normal");
        if (status.ExitCode != 0 || !string.IsNullOrWhiteSpace(status.Output))
        {
            logger.LogWarning("The PokéAPI checkout has local changes or cannot be inspected; automatic update skipped.");
            return;
        }
        var upstream = await GitAsync(token, "-C", repoPath, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{upstream}");
        var defaultBranch = await GitAsync(token, "-C", repoPath, "symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD");
        if (upstream.ExitCode != 0 || defaultBranch.ExitCode != 0 || upstream.Output != defaultBranch.Output)
        {
            logger.LogWarning("The PokéAPI checkout is not tracking the origin's default branch; automatic update skipped.");
            return;
        }
        logger.LogInformation("Updating the PokéAPI data checkout.");
        var pull = await GitAsync(token, "-C", repoPath, "pull", "--ff-only", "--no-rebase");
        if (pull.ExitCode != 0 || !HasData(dataPath))
        {
            logger.LogWarning("The PokéAPI checkout could not be fast-forwarded or is missing data; keeping the existing checkout.");
            return;
        }
        logger.LogInformation("PokéAPI data is up to date.");
    }

    private bool IsExpectedRemote(string remote)
    {
        var value = remote.Trim().TrimEnd('/');
        if (value.Equals(repositoryUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            return true;
        if (repositoryUrl != OfficialRepository)
            return Path.IsPathFullyQualified(value) && Path.IsPathFullyQualified(repositoryUrl) && SamePath(value, repositoryUrl);
        return value.Equals("https://github.com/PokeAPI/api-data", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("git@github.com:PokeAPI/api-data.git", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("git@github.com:PokeAPI/api-data", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasData(string path) => File.Exists(Path.Combine(path, "api", "v2", "pokemon", "1", "index.json"));
    private static bool SamePath(string a, string b) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)).Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static async Task<GitResult> GitAsync(CancellationToken token, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git could not be started.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }
        var output = (await stdout).Trim();
        await stderr;
        return new GitResult(process.ExitCode, output);
    }

    private sealed record GitResult(int ExitCode, string Output);
}
