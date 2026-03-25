namespace ReTrace.Scanner.Models;

/// <summary>
/// Full evidence record included in the evidence package manifest.
/// </summary>
public sealed class Evidence
{
    public required string ArtifactId { get; init; }
    public required string FilePath { get; init; }
    public string? Sha256Hash { get; init; }
    public DateTime? CreatedUtc { get; init; }
    public DateTime? ModifiedUtc { get; init; }
    public long? FileSizeBytes { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}
