using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Normalization;

/// <summary>
/// Converts raw artifacts from all collectors into the standardized NormalizedArtifact format.
/// This ensures the rule engine works against a uniform schema regardless of source.
/// </summary>
public sealed class ArtifactNormalizer : INormalizer
{
    private static readonly ILogger Log = Serilog.Log.ForContext<ArtifactNormalizer>();

    public IReadOnlyList<NormalizedArtifact> Normalize(IReadOnlyList<RawArtifact> rawArtifacts)
    {
        var normalized = new List<NormalizedArtifact>(rawArtifacts.Count);

        foreach (var raw in rawArtifacts)
        {
            try
            {
                var artifact = NormalizeOne(raw);
                EnrichTags(artifact);
                normalized.Add(artifact);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to normalize artifact from {Source} at {Path}: {Error}",
                    raw.Source, raw.FilePath, ex.Message);
            }
        }

        Log.Information("Normalized {Count} artifacts from {Raw} raw inputs",
            normalized.Count, rawArtifacts.Count);
        return normalized;
    }

    private NormalizedArtifact NormalizeOne(RawArtifact raw)
    {
        var id = Guid.NewGuid().ToString("N")[..12];

        // Extract execution-related metadata
        int? runCount = raw.Metadata.TryGetValue("runCount", out var rc) && int.TryParse(rc, out var rcVal)
            ? rcVal : null;

        var executableName = raw.Metadata.GetValueOrDefault("executableName");
        DateTime? lastExecuted = null;

        // For Prefetch, the file's last modified time approximates the last execution
        if (raw.ArtifactType == "Prefetch")
        {
            lastExecuted = raw.ModifiedUtc;
        }

        return new NormalizedArtifact
        {
            Id = id,
            Source = raw.Source,
            ArtifactType = raw.ArtifactType,
            FilePath = NormalizePath(raw.FilePath),
            Sha256Hash = raw.Sha256Hash,
            CreatedUtc = raw.CreatedUtc,
            ModifiedUtc = raw.ModifiedUtc,
            LastExecutedUtc = lastExecuted,
            FileSizeBytes = raw.FileSizeBytes,
            RunCount = runCount,
            ExecutableName = executableName,
            Metadata = new Dictionary<string, string>(raw.Metadata)
        };
    }

    /// <summary>
    /// Automatically tags artifacts based on their properties for easier filtering.
    /// </summary>
    private void EnrichTags(NormalizedArtifact artifact)
    {
        var tags = artifact.Tags;

        // Source tags
        tags.Add($"source:{artifact.Source}");
        tags.Add($"type:{artifact.ArtifactType}");

        // Extension-based tags
        var ext = artifact.FileExtension;
        if (ext is ".exe" or ".dll" or ".sys" or ".scr" or ".com")
            tags.Add("category:executable");
        if (ext is ".bat" or ".cmd" or ".ps1" or ".vbs" or ".js")
            tags.Add("category:script");
        if (ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz")
            tags.Add("category:archive");
        if (ext is ".tmp" or ".dat" or ".bin")
            tags.Add("category:suspicious-temp");

        // Path-based tags
        var pathLower = artifact.FilePath.ToLowerInvariant();
        if (pathLower.Contains(@"\temp") || pathLower.Contains(@"\tmp"))
            tags.Add("location:temp");
        if (pathLower.Contains(@"\downloads"))
            tags.Add("location:downloads");
        if (pathLower.Contains(@"\appdata"))
            tags.Add("location:appdata");
        if (pathLower.Contains(@"\desktop"))
            tags.Add("location:desktop");
        if (pathLower.Contains(@"\prefetch"))
            tags.Add("location:prefetch");

        // Execution tags
        if (artifact.RunCount.HasValue)
            tags.Add("has:runCount");
        if (artifact.LastExecutedUtc.HasValue)
            tags.Add("has:executionTime");
        if (!string.IsNullOrEmpty(artifact.Sha256Hash))
            tags.Add("has:hash");
    }

    /// <summary>
    /// Normalizes file paths to a consistent format.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;

        // Normalize separators
        path = path.Replace('/', '\\');

        // Remove trailing separators
        path = path.TrimEnd('\\');

        return path;
    }
}
