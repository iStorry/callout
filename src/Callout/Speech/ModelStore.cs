using System.IO;
using Callout.Core.Settings;
using Whisper.net.Ggml;

namespace Callout.Speech;

public sealed record DownloadProgress(string FileName, long BytesDownloaded);

/// <summary>Keeps Whisper and VAD model files on disk, downloading each once on first use.</summary>
internal sealed class ModelStore(string directory)
{
    private const int CopyBufferSize = 1 << 20;
    private const SileroVadType VadModel = SileroVadType.V6_2_0;

    public Task<string> EnsureWhisperModelAsync(TranslationModel model, IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var (type, quantization) = model switch
        {
            TranslationModel.Small => (GgmlType.Small, QuantizationType.Q5_1),
            TranslationModel.Medium => (GgmlType.Medium, QuantizationType.Q5_0),
            TranslationModel.LargeV3 => (GgmlType.LargeV3, QuantizationType.Q5_0),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, null),
        };

        var fileName = $"ggml-{type}-{quantization}.bin".ToLowerInvariant();
        return EnsureAsync(fileName, token => WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, quantization, token), progress, cancellationToken);
    }

    public Task<string> EnsureVadModelAsync(IProgress<DownloadProgress> progress, CancellationToken cancellationToken)
    {
        var fileName = $"ggml-silero-{VadModel}.bin".ToLowerInvariant();
        return EnsureAsync(fileName, token => WhisperGgmlDownloader.Default.GetGgmlSileroVadModelAsync(VadModel, token), progress, cancellationToken);
    }

    private async Task<string> EnsureAsync(
        string fileName,
        Func<CancellationToken, Task<Stream>> openDownload,
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, fileName);
        if (File.Exists(path))
        {
            return path;
        }

        Directory.CreateDirectory(directory);
        var partialPath = path + ".part";

        await using (var download = await openDownload(cancellationToken))
        await using (var file = File.Create(partialPath))
        {
            var buffer = new byte[CopyBufferSize];
            long total = 0;
            int read;
            while ((read = await download.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                progress.Report(new DownloadProgress(fileName, total));
            }
        }

        File.Move(partialPath, path, overwrite: true);
        return path;
    }
}
