using System.Diagnostics;
using DndSharp.PokeDex;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DndSharp.Tests;

[TestClass]
public sealed class PokeApiDataInitializerTests
{
    [TestMethod]
    public async Task ClonesUpdatesAndPreservesLocalChanges()
    {
        using var fixture = new RepositoryFixture();
        fixture.CreateRemote();
        var initializer = fixture.Initializer();
        await initializer.PrepareAsync();
        Assert.AreEqual("one", await File.ReadAllTextAsync(fixture.CheckoutData));

        await File.WriteAllTextAsync(fixture.RemoteData, "two");
        fixture.Commit("update upstream");
        await initializer.PrepareAsync();
        Assert.AreEqual("two", await File.ReadAllTextAsync(fixture.CheckoutData));

        await File.WriteAllTextAsync(fixture.CheckoutData, "local changes");
        await File.WriteAllTextAsync(fixture.RemoteData, "three");
        fixture.Commit("another upstream update");
        await initializer.PrepareAsync();
        Assert.AreEqual("local changes", await File.ReadAllTextAsync(fixture.CheckoutData));
    }

    [TestMethod]
    public async Task DoesNotModifyAnExistingUnrelatedDirectory()
    {
        using var fixture = new RepositoryFixture();
        fixture.CreateRemote();
        Directory.CreateDirectory(fixture.Checkout);
        var sentinel = Path.Combine(fixture.Checkout, "do-not-touch.txt");
        await File.WriteAllTextAsync(sentinel, "keep me");
        await fixture.Initializer().PrepareAsync();
        Assert.AreEqual("keep me", await File.ReadAllTextAsync(sentinel));
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.Checkout, ".git")));
    }

    [TestMethod]
    public async Task IncompleteCloneDoesNotReplaceTargetOrLeaveStagingData()
    {
        using var fixture = new RepositoryFixture();
        fixture.CreateRemote();
        File.Delete(fixture.RemoteData);
        fixture.Commit("remove expected data");
        await fixture.Initializer().PrepareAsync();
        Assert.IsFalse(Directory.Exists(fixture.Checkout));
        Assert.AreEqual(0, Directory.GetDirectories(Path.GetDirectoryName(fixture.Checkout)!, ".checkout.clone-*").Length);
    }

    [TestMethod]
    public async Task DoesNotPullFromAnUnexpectedOrigin()
    {
        using var fixture = new RepositoryFixture();
        fixture.CreateRemote();
        var initializer = fixture.Initializer();
        await initializer.PrepareAsync();
        fixture.ChangeCheckoutOrigin("https://example.invalid/unrelated.git");
        await File.WriteAllTextAsync(fixture.RemoteData, "new remote version");
        fixture.Commit("update upstream");
        await initializer.PrepareAsync();
        Assert.AreEqual("one", await File.ReadAllTextAsync(fixture.CheckoutData));
    }

    private sealed class RepositoryFixture : IDisposable
    {
        private readonly string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"dndsharp-poke-tests-{Guid.NewGuid():N}"));
        private string Remote => Path.Combine(root, "remote");
        public string Checkout => Path.Combine(root, "checkout");
        public string RemoteData => Path.Combine(Remote, "data", "api", "v2", "pokemon", "1", "index.json");
        public string CheckoutData => Path.Combine(Checkout, "data", "api", "v2", "pokemon", "1", "index.json");

        public void CreateRemote()
        {
            Directory.CreateDirectory(Remote);
            Git("-C", Remote, "init", "--initial-branch=main");
            Directory.CreateDirectory(Path.GetDirectoryName(RemoteData)!);
            File.WriteAllText(RemoteData, "one");
            Commit("initial data");
        }

        public void Commit(string message)
        {
            Git("-C", Remote, "add", "--all");
            Git("-C", Remote, "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false", "commit", "-m", message);
        }

        public void ChangeCheckoutOrigin(string origin) => Git("-C", Checkout, "remote", "set-url", "origin", origin);

        public PokeApiDataInitializer Initializer()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PokeApi:BasePath"] = Path.Combine(Checkout, "data")
            }).Build();
            return new PokeApiDataInitializer(config, NullLogger<PokeApiDataInitializer>.Instance, Remote);
        }

        private static void Git(params string[] args)
        {
            var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            foreach (var arg in args)
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Test Git command failed: {output} {error}");
        }

        public void Dispose()
        {
            // The only recursive deletion is this test's own explicitly verified temporary directory.
            var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetDirectoryName(root)!));
            if (!parent.Equals(temp, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !Path.GetFileName(root).StartsWith("dndsharp-poke-tests-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe test cleanup path.");
            if (!Directory.Exists(root))
                return;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }
}
