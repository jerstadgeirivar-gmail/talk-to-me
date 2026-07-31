using System.Security.Cryptography;
using TalkToMe.Core;

namespace TalkToMe.Infrastructure;

public static class LocalWhisperModel
{
    public const string FileName = "ggml-small-q5_1.bin";
    public const string DisplayName = "Whisper small-q5_1 (multilingual)";
    public const long SizeBytes = 190_085_487;
    public const string Sha256 = "ae85e4a935d7a567bd102fe55afc16bb595bdb618e11b2fc7591bc08120411bb";
    public static readonly Uri DownloadUri = new("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin?download=true");

    public static string ResolvePath()
    {
        string? configured = Environment.GetEnvironmentVariable("TALKTOME_WHISPER_MODEL_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        if (File.Exists(TalkToMeDataPaths.LocalWhisperModelFile)) return TalkToMeDataPaths.LocalWhisperModelFile;
        return Path.Combine(AppContext.BaseDirectory, "Models", FileName);
    }

    public static async Task<ProviderTestResult> VerifyAsync(CancellationToken cancellationToken)
    {
        string path = ResolvePath();
        if (!File.Exists(path))
        {
            return new(ProviderReadiness.ModelMissing, $"Local model is missing. Expected {path}. Use Repair model or reinstall TalkToMe.");
        }
        FileInfo info = new(path);
        if (info.Length != SizeBytes)
        {
            return new(ProviderReadiness.ModelCorrupt, $"Local model has the wrong size ({info.Length:N0} bytes). Use Repair model.");
        }
        await using FileStream stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        string actual = Convert.ToHexStringLower(hash);
        return actual == Sha256
            ? new(ProviderReadiness.Ready, $"Ready offline — {DisplayName}, CPU")
            : new(ProviderReadiness.ModelCorrupt, "Local model checksum is invalid. Use Repair model.");
    }

    public static async Task RepairAsync(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(TalkToMeDataPaths.ModelsDirectory);
        string destination = TalkToMeDataPaths.LocalWhisperModelFile;
        string temporary = destination + ".download";
        try
        {
            using HttpClient client = new() { Timeout = Timeout.InfiniteTimeSpan };
            using HttpResponseMessage response = await client.GetAsync(DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[128 * 1024];
            long copied = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                copied += read;
                progress?.Report(Math.Min(100, copied * 100d / SizeBytes));
            }
            await output.FlushAsync(cancellationToken);
            output.Close();
            FileInfo info = new(temporary);
            if (info.Length != SizeBytes) throw new InvalidDataException("Downloaded model size does not match the pinned model.");
            string actual;
            await using (FileStream verification = File.OpenRead(temporary))
            {
                actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(verification, cancellationToken));
            }
            if (actual != Sha256) throw new InvalidDataException("Downloaded model checksum does not match the pinned SHA-256.");
            File.Move(temporary, destination, overwrite: true);
            progress?.Report(100);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
