using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Collects artifacts from Windows temp directories.
/// Temp directories are common drop locations for loaders, injectors, and payloads.
///
/// Scanned locations:
/// - %TEMP% (user temp)
/// - %WINDIR%\Temp (system temp)
/// - %LOCALAPPDATA%\Temp
/// </summary>
public sealed class TempFilesCollector : ICollector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<TempFilesCollector>();

    // Suspicious extensions commonly associated with cheat tooling
    private static readonly HashSet<string> InterestingExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".sys", ".bat", ".cmd", ".ps1", ".vbs",
        ".scr", ".com", ".pif", ".msi", ".tmp", ".dat", ".bin",
        ".log", ".cfg", ".ini", ".lua", ".js"
    };

    public string Name => "TempFilesCollector";
    public string ArtifactType => "TempFile";

    public IReadOnlyList<RawArtifact> Collect()
    {
        var artifacts = new List<RawArtifact>();
        var tempDirs = GetTempDirectories();

        foreach (var dir in tempDirs)
        {
            CollectFromDirectory(dir, artifacts);
        }

        Log.Information("Collected {Count} temp file artifacts from {Dirs} directories",
            artifacts.Count, tempDirs.Count);
        return artifacts;
    }

    private List<string> GetTempDirectories()
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfExists(dirs, Path.GetTempPath());
        AddIfExists(dirs, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
        AddIfExists(dirs, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"));

        return dirs.ToList();
    }

    private void CollectFromDirectory(string directory, List<RawArtifact> artifacts)
    {
        var files = CollectorUtils.SafeGetFiles(directory);

        foreach (var file in files)
        {
            try
            {
                var ext = Path.GetExtension(file);
                var isInteresting = InterestingExtensions.Contains(ext);

                var metadata = new Dictionary<string, string>
                {
                    ["tempDirectory"] = directory,
                    ["extension"] = ext,
                    ["isInterestingExtension"] = isInteresting.ToString()
                };

                artifacts.Add(new RawArtifact
                {
                    Source = Name,
                    ArtifactType = ArtifactType,
                    FilePath = file,
                    Sha256Hash = isInteresting ? CollectorUtils.ComputeSha256(file) : null,
                    CreatedUtc = CollectorUtils.GetCreatedUtc(file),
                    ModifiedUtc = CollectorUtils.GetModifiedUtc(file),
                    FileSizeBytes = CollectorUtils.GetFileSize(file),
                    Metadata = metadata
                });
            }
            catch (Exception ex)
            {
                Log.Debug("Error processing temp file {File}: {Error}", file, ex.Message);
            }
        }

        // Also scan one level of subdirectories (loaders often create temp subdirs)
        foreach (var subDir in CollectorUtils.SafeGetDirectories(directory))
        {
            var subFiles = CollectorUtils.SafeGetFiles(subDir);
            foreach (var file in subFiles)
            {
                try
                {
                    var ext = Path.GetExtension(file);
                    var isInteresting = InterestingExtensions.Contains(ext);

                    artifacts.Add(new RawArtifact
                    {
                        Source = Name,
                        ArtifactType = ArtifactType,
                        FilePath = file,
                        Sha256Hash = isInteresting ? CollectorUtils.ComputeSha256(file) : null,
                        CreatedUtc = CollectorUtils.GetCreatedUtc(file),
                        ModifiedUtc = CollectorUtils.GetModifiedUtc(file),
                        FileSizeBytes = CollectorUtils.GetFileSize(file),
                        Metadata = new Dictionary<string, string>
                        {
                            ["tempDirectory"] = directory,
                            ["subDirectory"] = Path.GetFileName(subDir),
                            ["extension"] = ext,
                            ["isInterestingExtension"] = isInteresting.ToString()
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Debug("Error processing temp subdir file {File}: {Error}", file, ex.Message);
                }
            }
        }
    }

    private static void AddIfExists(HashSet<string> set, string path)
    {
        if (Directory.Exists(path))
            set.Add(Path.GetFullPath(path));
    }
}
