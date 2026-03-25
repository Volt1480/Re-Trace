using System.Text.RegularExpressions;
using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Collects artifacts from Windows Prefetch files (C:\Windows\Prefetch).
/// Prefetch files record execution history: which programs ran and when.
///
/// File naming convention: EXECUTABLE_NAME-XXXXXXXX.pf
/// where XXXXXXXX is a hash of the file path.
///
/// The collector extracts:
/// - Executable name from filename
/// - Path hash from filename
/// - File timestamps (creation = first run, modification = last run approx)
/// - Run count from binary header (if parseable)
/// </summary>
public sealed class PrefetchCollector : ICollector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<PrefetchCollector>();
    private static readonly string PrefetchDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    // Matches: EXECUTABLE_NAME-XXXXXXXX.pf
    private static readonly Regex PfFilePattern = new(
        @"^(.+)-([0-9A-Fa-f]{8})\.pf$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Name => "PrefetchCollector";
    public string ArtifactType => "Prefetch";

    public IReadOnlyList<RawArtifact> Collect()
    {
        var artifacts = new List<RawArtifact>();

        if (!Directory.Exists(PrefetchDir))
        {
            Log.Warning("Prefetch directory not found: {Dir}", PrefetchDir);
            return artifacts;
        }

        var files = CollectorUtils.SafeGetFiles(PrefetchDir, "*.pf");
        Log.Information("Found {Count} Prefetch files", files.Length);

        foreach (var file in files)
        {
            try
            {
                var artifact = ParsePrefetchFile(file);
                if (artifact != null)
                    artifacts.Add(artifact);
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to parse Prefetch file {File}: {Error}", file, ex.Message);
            }
        }

        return artifacts;
    }

    private RawArtifact? ParsePrefetchFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var match = PfFilePattern.Match(fileName);

        if (!match.Success)
        {
            Log.Debug("Skipping non-standard Prefetch file: {Name}", fileName);
            return null;
        }

        var executableName = match.Groups[1].Value;
        var pathHash = match.Groups[2].Value;

        var metadata = new Dictionary<string, string>
        {
            ["executableName"] = executableName,
            ["pathHash"] = pathHash
        };

        // Try to parse run count and timestamps from binary header
        TryParseBinaryHeader(filePath, metadata);

        return new RawArtifact
        {
            Source = Name,
            ArtifactType = ArtifactType,
            FilePath = filePath,
            Sha256Hash = CollectorUtils.ComputeSha256(filePath),
            CreatedUtc = CollectorUtils.GetCreatedUtc(filePath),
            ModifiedUtc = CollectorUtils.GetModifiedUtc(filePath),
            FileSizeBytes = CollectorUtils.GetFileSize(filePath),
            Metadata = metadata
        };
    }

    /// <summary>
    /// Attempts to parse the Prefetch binary header for run count and last execution time.
    /// Supports Windows 10/11 Prefetch format (version 30, MAM compressed).
    /// Falls back gracefully if the format is unrecognized.
    /// </summary>
    private void TryParseBinaryHeader(string filePath, Dictionary<string, string> metadata)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 16) return;

            // Check for MAM compression signature (Windows 10+)
            var signature = BitConverter.ToUInt32(bytes, 0);
            if (signature == 0x044D414D) // "MAM\x04"
            {
                metadata["compressed"] = "true";
                metadata["compressedSize"] = bytes.Length.ToString();

                // Decompressed size at offset 4
                var decompressedSize = BitConverter.ToUInt32(bytes, 4);
                metadata["decompressedSize"] = decompressedSize.ToString();

                // Binary parsing of compressed prefetch requires Xpress Huffman decompression
                // which is complex — for MVP we rely on filename + file timestamps
                return;
            }

            // Uncompressed prefetch (older Windows versions)
            // Version at offset 0, signature "SCCA" at offset 4
            if (bytes.Length >= 100)
            {
                var version = BitConverter.ToUInt32(bytes, 0);
                var scca = System.Text.Encoding.ASCII.GetString(bytes, 4, 4);

                if (scca == "SCCA")
                {
                    metadata["prefetchVersion"] = version.ToString();

                    // Run count location depends on version
                    if (version == 17 && bytes.Length >= 144) // Windows XP/2003
                    {
                        var runCount = BitConverter.ToUInt32(bytes, 144);
                        metadata["runCount"] = runCount.ToString();
                    }
                    else if (version == 23 && bytes.Length >= 152) // Vista/7
                    {
                        var runCount = BitConverter.ToUInt32(bytes, 152);
                        metadata["runCount"] = runCount.ToString();
                    }
                    else if (version == 26 && bytes.Length >= 208) // Windows 8
                    {
                        var runCount = BitConverter.ToUInt32(bytes, 208);
                        metadata["runCount"] = runCount.ToString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Could not parse Prefetch binary header for {File}: {Error}", filePath, ex.Message);
        }
    }
}
