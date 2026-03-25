using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Evidence;

/// <summary>
/// Packages scan results:
/// 1. Serializes findings + metadata to scan-result.json
/// 2. Builds an evidence manifest
/// 3. Optionally creates a ZIP bundle with the JSON + referenced evidence files
/// </summary>
public sealed class EvidencePackager : IEvidencePackager
{
    private static readonly ILogger Log = Serilog.Log.ForContext<EvidencePackager>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Package(ScanResult result, string outputPath, bool includeZip)
    {
        Directory.CreateDirectory(outputPath);

        // 1. Build evidence manifest from findings
        BuildEvidenceManifest(result);

        // 2. Write scan result JSON
        var jsonPath = Path.Combine(outputPath, "scan-result.json");
        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(jsonPath, json);
        Log.Information("Scan result written to {Path} ({Size} bytes)", jsonPath, json.Length);

        // 3. Write findings-only JSON (lighter file for quick review)
        var findingsPath = Path.Combine(outputPath, "findings.json");
        var findingsJson = JsonSerializer.Serialize(new
        {
            scanId = result.ScanId,
            scannedAtUtc = result.ScannedAtUtc,
            findingCount = result.FindingCount,
            findings = result.Findings
        }, JsonOptions);
        File.WriteAllText(findingsPath, findingsJson);

        // 4. Write statistics
        var statsPath = Path.Combine(outputPath, "statistics.json");
        File.WriteAllText(statsPath, JsonSerializer.Serialize(result.Statistics, JsonOptions));

        // 5. Optional ZIP bundle
        if (includeZip)
        {
            var zipPath = Path.Combine(outputPath, $"retrace-evidence-{result.ScanId[..8]}.zip");
            CreateZipBundle(result, outputPath, zipPath);
        }

        return outputPath;
    }

    private void BuildEvidenceManifest(ScanResult result)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in result.Findings)
        {
            foreach (var evidenceRef in finding.EvidenceRefs)
            {
                if (seen.Contains(evidenceRef.ArtifactId)) continue;
                seen.Add(evidenceRef.ArtifactId);

                // Find matching artifact for enrichment
                var artifact = result.Artifacts.FirstOrDefault(a => a.Id == evidenceRef.ArtifactId);

                result.EvidenceManifest.Add(new Models.Evidence
                {
                    ArtifactId = evidenceRef.ArtifactId,
                    FilePath = evidenceRef.FilePath,
                    Sha256Hash = evidenceRef.Sha256Hash,
                    CreatedUtc = artifact?.CreatedUtc,
                    ModifiedUtc = artifact?.ModifiedUtc,
                    FileSizeBytes = artifact?.FileSizeBytes,
                    Metadata = artifact?.Metadata ?? new Dictionary<string, string>()
                });
            }
        }

        Log.Information("Evidence manifest contains {Count} entries", result.EvidenceManifest.Count);
    }

    private void CreateZipBundle(ScanResult result, string outputDir, string zipPath)
    {
        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            // Add JSON files
            AddFileToZip(zip, Path.Combine(outputDir, "scan-result.json"), "scan-result.json");
            AddFileToZip(zip, Path.Combine(outputDir, "findings.json"), "findings.json");
            AddFileToZip(zip, Path.Combine(outputDir, "statistics.json"), "statistics.json");

            // Add evidence manifest
            var manifestJson = JsonSerializer.Serialize(result.EvidenceManifest, JsonOptions);
            var manifestEntry = zip.CreateEntry("evidence-manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(manifestJson);
            }

            // Try to include referenced evidence files (best effort - some may be locked/gone)
            var includedCount = 0;
            foreach (var evidence in result.EvidenceManifest)
            {
                if (File.Exists(evidence.FilePath))
                {
                    try
                    {
                        var entryName = $"evidence/{evidence.ArtifactId}_{Path.GetFileName(evidence.FilePath)}";
                        zip.CreateEntryFromFile(evidence.FilePath, entryName, CompressionLevel.Optimal);
                        includedCount++;
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Could not include evidence file {Path}: {Error}",
                            evidence.FilePath, ex.Message);
                    }
                }
            }

            Log.Information("ZIP evidence bundle created: {Path} ({FileCount} evidence files included)",
                zipPath, includedCount);
        }
        catch (Exception ex)
        {
            Log.Error("Failed to create ZIP bundle: {Error}", ex.Message);
        }
    }

    private static void AddFileToZip(ZipArchive zip, string filePath, string entryName)
    {
        if (File.Exists(filePath))
            zip.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
    }
}
