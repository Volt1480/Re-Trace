using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Collects artifacts from Windows Recent Items and related locations.
/// Recent items (.lnk shortcut files) indicate files a user has opened.
///
/// Scanned locations:
/// - %APPDATA%\Microsoft\Windows\Recent
/// - %APPDATA%\Microsoft\Windows\Recent\AutomaticDestinations
/// - %APPDATA%\Microsoft\Windows\Recent\CustomDestinations
/// </summary>
public sealed class RecentFilesCollector : ICollector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RecentFilesCollector>();

    public string Name => "RecentFilesCollector";
    public string ArtifactType => "RecentFile";

    public IReadOnlyList<RawArtifact> Collect()
    {
        var artifacts = new List<RawArtifact>();
        var recentDirs = GetRecentDirectories();

        foreach (var dir in recentDirs)
        {
            CollectFromDirectory(dir, artifacts);
        }

        Log.Information("Collected {Count} recent file artifacts", artifacts.Count);
        return artifacts;
    }

    private List<string> GetRecentDirectories()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var recentBase = Path.Combine(appData, "Microsoft", "Windows", "Recent");

        var dirs = new List<string>();
        if (Directory.Exists(recentBase)) dirs.Add(recentBase);

        var autoDest = Path.Combine(recentBase, "AutomaticDestinations");
        if (Directory.Exists(autoDest)) dirs.Add(autoDest);

        var customDest = Path.Combine(recentBase, "CustomDestinations");
        if (Directory.Exists(customDest)) dirs.Add(customDest);

        return dirs;
    }

    private void CollectFromDirectory(string directory, List<RawArtifact> artifacts)
    {
        var files = CollectorUtils.SafeGetFiles(directory);

        foreach (var file in files)
        {
            try
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var fileName = Path.GetFileName(file);
                var isShortcut = ext == ".lnk";

                var metadata = new Dictionary<string, string>
                {
                    ["recentDirectory"] = directory,
                    ["isShortcut"] = isShortcut.ToString()
                };

                // For .lnk files, the filename before .lnk often indicates the target
                if (isShortcut)
                {
                    var targetHint = Path.GetFileNameWithoutExtension(fileName);
                    metadata["targetHint"] = targetHint;

                    // Try to extract target extension from the shortcut name
                    var targetExt = Path.GetExtension(targetHint);
                    if (!string.IsNullOrEmpty(targetExt))
                        metadata["targetExtension"] = targetExt;
                }

                // For AutomaticDestinations / CustomDestinations files
                if (ext == ".automaticDestinations-ms" || ext == ".customDestinations-ms")
                {
                    metadata["jumpListType"] = ext.Contains("automatic") ? "automatic" : "custom";
                }

                artifacts.Add(new RawArtifact
                {
                    Source = Name,
                    ArtifactType = ArtifactType,
                    FilePath = file,
                    Sha256Hash = CollectorUtils.ComputeSha256(file),
                    CreatedUtc = CollectorUtils.GetCreatedUtc(file),
                    ModifiedUtc = CollectorUtils.GetModifiedUtc(file),
                    FileSizeBytes = CollectorUtils.GetFileSize(file),
                    Metadata = metadata
                });
            }
            catch (Exception ex)
            {
                Log.Debug("Error processing recent file {File}: {Error}", file, ex.Message);
            }
        }
    }
}
