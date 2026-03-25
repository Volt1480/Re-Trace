namespace ReTrace.Scanner.Models;

/// <summary>
/// Normalized artifact with standardized fields for rule evaluation.
/// All collector-specific data is flattened into consistent properties.
/// </summary>
public sealed class NormalizedArtifact
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string ArtifactType { get; init; }

    // File identity
    public required string FilePath { get; init; }
    public string FileName => Path.GetFileName(FilePath);
    public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();
    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? string.Empty;

    // Hashes
    public string? Sha256Hash { get; init; }

    // Timestamps
    public DateTime? CreatedUtc { get; init; }
    public DateTime? ModifiedUtc { get; init; }
    public DateTime? LastExecutedUtc { get; init; }

    // Size
    public long? FileSizeBytes { get; init; }

    // Execution context (from Prefetch / Amcache)
    public int? RunCount { get; init; }
    public string? ExecutableName { get; init; }

    // Tags for enrichment
    public HashSet<string> Tags { get; init; } = new();

    // All additional metadata
    public Dictionary<string, string> Metadata { get; init; } = new();
}
