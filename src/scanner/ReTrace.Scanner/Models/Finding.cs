namespace ReTrace.Scanner.Models;

/// <summary>
/// A finding represents a single detection result produced by the rule engine.
/// It references the rule that triggered it and the evidence that supports it.
/// </summary>
public sealed class Finding
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required Severity Severity { get; init; }
    public required double Confidence { get; init; }
    public required string Source { get; init; }
    public required string ArtifactType { get; init; }
    public List<string> Tags { get; init; } = new();
    public List<EvidenceReference> EvidenceRefs { get; init; } = new();
    public DateTime DetectedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// If this finding was produced by correlation, lists the contributing finding IDs.
    /// </summary>
    public List<string>? CorrelatedFindingIds { get; init; }
}

/// <summary>
/// Lightweight reference to a piece of evidence supporting a finding.
/// </summary>
public sealed class EvidenceReference
{
    public required string ArtifactId { get; init; }
    public required string FilePath { get; init; }
    public string? Sha256Hash { get; init; }
    public DateTime? Timestamp { get; init; }
}
