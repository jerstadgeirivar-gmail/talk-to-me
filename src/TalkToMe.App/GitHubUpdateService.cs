using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace TalkToMe.App;

internal sealed class GitHubUpdateService
{
    private const string Repository = "jerstadgeirivar-gmail/talk-to-me";
    private const string InstallerAssetName = "TalkToMe-Setup.exe";
    private readonly Version _currentVersion =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    public string CurrentVersion => FormatVersion(_currentVersion);

    public async Task<UpdateAvailability> CheckAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await RunGitHubCliAsync(
            ["release", "view", "--repo", Repository, "--json", "tagName,assets"],
            cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return new UpdateAvailability(
                false,
                null,
                null,
                result.NotFound
                    ? "GitHub CLI is required for private updates"
                    : "Unable to check private GitHub release");
        }

        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        string tag = document.RootElement.GetProperty("tagName").GetString() ?? string.Empty;
        if (!TryParseVersion(tag, out Version? latestVersion))
        {
            return new UpdateAvailability(false, null, null, "Latest release has an invalid version");
        }

        string[] assetNames = document.RootElement
            .GetProperty("assets")
            .EnumerateArray()
            .Select(asset => asset.GetProperty("name").GetString() ?? string.Empty)
            .ToArray();
        string? checksumAsset = assetNames.FirstOrDefault(
            name => name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
        if (!assetNames.Contains(InstallerAssetName, StringComparer.OrdinalIgnoreCase) ||
            checksumAsset is null)
        {
            return new UpdateAvailability(false, latestVersion, tag, "Latest release is incomplete");
        }

        return latestVersion > _currentVersion
            ? new UpdateAvailability(true, latestVersion, tag, "Update available", checksumAsset)
            : new UpdateAvailability(false, latestVersion, tag, "Up to date");
    }

    public static async Task<string> DownloadAndVerifyAsync(
        UpdateAvailability update,
        CancellationToken cancellationToken)
    {
        if (!update.IsAvailable ||
            update.Version is null ||
            update.Tag is null ||
            update.ChecksumAssetName is null)
        {
            throw new InvalidOperationException("No complete update is available.");
        }

        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TalkToMe",
            "Updates",
            FormatVersion(update.Version));
        Directory.CreateDirectory(directory);
        ProcessResult download = await RunGitHubCliAsync(
            [
                "release", "download", update.Tag,
                "--repo", Repository,
                "--pattern", InstallerAssetName,
                "--pattern", update.ChecksumAssetName,
                "--dir", directory,
                "--clobber",
            ],
            cancellationToken).ConfigureAwait(false);
        if (!download.Succeeded)
        {
            throw new InvalidOperationException("The update could not be downloaded.");
        }

        string installerPath = Path.Combine(directory, InstallerAssetName);
        string checksumPath = Path.Combine(directory, update.ChecksumAssetName);
        string checksumText = await File.ReadAllTextAsync(checksumPath, cancellationToken)
            .ConfigureAwait(false);
        string expectedHash = checksumText
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        await using FileStream installerStream = File.OpenRead(installerPath);
        string actualHash = Convert.ToHexString(
            await SHA256.HashDataAsync(installerStream, cancellationToken)
                .ConfigureAwait(false));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(installerPath);
            throw new InvalidOperationException("The downloaded update failed checksum verification.");
        }

        return installerPath;
    }

    public static void StartInstaller(string installerPath)
    {
        string installDirectory = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException("The application directory is unavailable.");
        string logPath = Path.Combine(
            Path.GetDirectoryName(installerPath)
                ?? throw new InvalidOperationException("The update directory is unavailable."),
            "install.log");
        _ = Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true,
            ArgumentList =
            {
                "/VERYSILENT",
                "/SUPPRESSMSGBOXES",
                "/NORESTART",
                "/SP-",
                "/CLOSEAPPLICATIONS",
                "/TALKTOMERESTART",
                $"/DIR={installDirectory}",
                $"/LOG={logPath}",
            },
        }) ?? throw new InvalidOperationException("The update installer did not start.");
    }

    internal static bool TryParseVersion(string value, out Version? version)
    {
        string normalized = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(normalized, out version);
    }

    private static string FormatVersion(Version version) =>
        version.Build > 0
            ? version.ToString(3)
            : $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";

    private static async Task<ProcessResult> RunGitHubCliAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "gh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("GitHub CLI did not start.");
        }
        catch (Win32Exception)
        {
            return new ProcessResult(false, string.Empty, string.Empty, true);
        }

        using (process)
        {
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                throw;
            }

            return new ProcessResult(
                process.ExitCode == 0,
                await outputTask.ConfigureAwait(false),
                await errorTask.ConfigureAwait(false),
                false);
        }
    }

    private sealed record ProcessResult(
        bool Succeeded,
        string StandardOutput,
        string StandardError,
        bool NotFound);
}

internal sealed record UpdateAvailability(
    bool IsAvailable,
    Version? Version,
    string? Tag,
    string Status,
    string? ChecksumAssetName = null);
