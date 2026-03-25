namespace ReTrace.Scanner.Models;

/// <summary>
/// Raw artifact produced by a collector before normalization.
/// Each collector outputs its own fields via the Metadata dictionary.
/// </summary>
public sealed class RawArtifact
{
    public required string Source { get; init; }
    public required string ArtifactType { get; init; }
    public required string FilePath { get; init; }
    public string? Sha256Hash { get; init; }
    public DateTime? CreatedUtc { get; init; }
    public DateTime? ModifiedUtc { get; init; }
    public long? FileSizeBytes { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
