namespace ReTrace.Scanner.Models;

/// <summary>
/// Complete result of a scan, containing all findings, evidence, and metadata.
/// This is what gets serialized to JSON and optionally uploaded to the backend.
/// </summary>
public sealed class ScanResult
{
    public string ScanId { get; init; } = Guid.NewGuid().ToString("N");
    public string ScannerVersion { get; init; } = "0.2.0";
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;
    public string MachineName { get; init; } = Environment.MachineName;
    public string UserName { get; init; } = Environment.UserName;

    public int ArtifactCount { get; set; }
    public int FindingCount => Findings.Count;

    public List<Finding> Findings { get; init; } = new();
    public List<Evidence> EvidenceManifest { get; init; } = new();
    public List<NormalizedArtifact> Artifacts { get; init; } = new();

    public ScanStatistics Statistics { get; set; } = new();
}

public sealed class ScanStatistics
{
    public int TotalArtifactsCollected { get; set; }
    public int TotalRulesEvaluated { get; set; }
    public int TotalFindingsGenerated { get; set; }
    public int CorrelatedFindings { get; set; }
    public Dictionary<string, int> FindingsBySeverity { get; set; } = new();
    public Dictionary<string, int> ArtifactsBySource { get; set; } = new();
    public double ScanDurationMs { get; set; }
}
