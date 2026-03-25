using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Collects browser download artifacts from Chromium-based browsers.
/// Downloads are a common initial vector for cheat tooling.
///
/// Supported browsers:
/// - Google Chrome
/// - Microsoft Edge
/// - Brave
///
/// Reads the "History" SQLite database (downloads table).
/// Since we avoid adding a SQLite dependency for MVP, we parse the raw file
/// for recognizable string patterns. For production, switch to System.Data.SQLite.
/// </summary>
public sealed class BrowserDownloadsCollector : ICollector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<BrowserDownloadsCollector>();

    public string Name => "BrowserDownloadsCollector";
    public string ArtifactType => "BrowserDownload";

    public IReadOnlyList<RawArtifact> Collect()
    {
        var artifacts = new List<RawArtifact>();

        var browsers = GetBrowserProfiles();
        foreach (var (browserName, profilePath) in browsers)
        {
            CollectFromProfile(browserName, profilePath, artifacts);
        }

        // Also scan the user's Downloads folder directly
        CollectDownloadsFolder(artifacts);

        Log.Information("Collected {Count} browser download artifacts", artifacts.Count);
        return artifacts;
    }

    private List<(string BrowserName, string ProfilePath)> GetBrowserProfiles()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profiles = new List<(string, string)>();

        var browserPaths = new Dictionary<string, string>
        {
            ["Chrome"] = Path.Combine(localAppData, "Google", "Chrome", "User Data"),
            ["Edge"] = Path.Combine(localAppData, "Microsoft", "Edge", "User Data"),
            ["Brave"] = Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data")
        };

        foreach (var (name, basePath) in browserPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            // Default profile
            var defaultProfile = Path.Combine(basePath, "Default");
            if (Directory.Exists(defaultProfile))
                profiles.Add((name, defaultProfile));

            // Additional profiles (Profile 1, Profile 2, etc.)
            foreach (var dir in CollectorUtils.SafeGetDirectories(basePath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    profiles.Add(($"{name} ({dirName})", dir));
            }
        }

        return profiles;
    }

    private void CollectFromProfile(string browserName, string profilePath, List<RawArtifact> artifacts)
    {
        var historyDb = Path.Combine(profilePath, "History");
        if (!File.Exists(historyDb))
        {
            Log.Debug("No History database found for {Browser} at {Path}", browserName, profilePath);
            return;
        }

        // Record the history file itself as an artifact
        artifacts.Add(new RawArtifact
        {
            Source = Name,
            ArtifactType = "BrowserHistoryDb",
            FilePath = historyDb,
            Sha256Hash = CollectorUtils.ComputeSha256(historyDb),
            CreatedUtc = CollectorUtils.GetCreatedUtc(historyDb),
            ModifiedUtc = CollectorUtils.GetModifiedUtc(historyDb),
            FileSizeBytes = CollectorUtils.GetFileSize(historyDb),
            Metadata = new Dictionary<string, string>
            {
                ["browser"] = browserName,
                ["profilePath"] = profilePath,
                ["databaseType"] = "History"
            }
        });

        // Try to extract download paths from the SQLite file via string scanning
        // This is a best-effort approach without a SQLite dependency
        TryExtractDownloadPaths(historyDb, browserName, artifacts);
    }

    /// <summary>
    /// Scans the SQLite History database for download file paths.
    /// This is a lightweight approach that looks for path-like strings in the raw bytes.
    /// For production, use a proper SQLite reader.
    /// </summary>
    private void TryExtractDownloadPaths(string dbPath, string browserName, List<RawArtifact> artifacts)
    {
        try
        {
            // Copy the database to temp to avoid lock issues (browser may have it locked)
            var tempCopy = Path.Combine(Path.GetTempPath(), $"retrace_history_{Guid.NewGuid():N}.tmp");
            try
            {
                File.Copy(dbPath, tempCopy, overwrite: true);
                var content = File.ReadAllBytes(tempCopy);

                // Look for Windows file paths in the binary content
                var text = System.Text.Encoding.UTF8.GetString(content);
                ExtractPathsFromText(text, browserName, artifacts);
            }
            finally
            {
                try { File.Delete(tempCopy); } catch { /* cleanup best effort */ }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Could not extract download paths from {Db}: {Error}", dbPath, ex.Message);
        }
    }

    private void ExtractPathsFromText(string text, string browserName, List<RawArtifact> artifacts)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find Windows-style paths (C:\Users\...) that look like downloads
        var idx = 0;
        while (idx < text.Length - 10)
        {
            // Look for drive letter patterns
            if (idx + 2 < text.Length && text[idx + 1] == ':' && text[idx + 2] == '\\' &&
                char.IsLetter(text[idx]))
            {
                var pathStart = idx;
                var pathEnd = idx + 3;

                // Extend to find the end of the path
                while (pathEnd < text.Length && text[pathEnd] >= ' ' && text[pathEnd] < 127 &&
                       text[pathEnd] != '"' && text[pathEnd] != '\'' && text[pathEnd] != '\0')
                {
                    pathEnd++;
                }

                var path = text[pathStart..pathEnd].Trim();

                if (path.Length > 10 && !seen.Contains(path) && LooksLikeDownloadPath(path))
                {
                    seen.Add(path);

                    artifacts.Add(new RawArtifact
                    {
                        Source = Name,
                        ArtifactType = ArtifactType,
                        FilePath = path,
                        Sha256Hash = File.Exists(path) ? CollectorUtils.ComputeSha256(path) : null,
                        FileSizeBytes = File.Exists(path) ? CollectorUtils.GetFileSize(path) : null,
                        Metadata = new Dictionary<string, string>
                        {
                            ["browser"] = browserName,
                            ["extractionMethod"] = "stringSearch",
                            ["fileExists"] = File.Exists(path).ToString()
                        }
                    });
                }
            }
            idx++;
        }
    }

    private static bool LooksLikeDownloadPath(string path)
    {
        // Must have a file extension
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext)) return false;

        // Filter out obviously irrelevant paths (system files, etc.)
        if (path.Contains(@"\Windows\System32", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.Contains(@"\Windows\WinSxS", StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private void CollectDownloadsFolder(List<RawArtifact> artifacts)
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        if (!Directory.Exists(downloads)) return;

        var files = CollectorUtils.SafeGetFiles(downloads);
        foreach (var file in files)
        {
            try
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                var isExecutable = ext is ".exe" or ".dll" or ".msi" or ".bat" or ".cmd"
                    or ".ps1" or ".vbs" or ".scr" or ".com" or ".sys";

                // Only collect executables and archives from Downloads
                var isArchive = ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz";

                if (!isExecutable && !isArchive) continue;

                artifacts.Add(new RawArtifact
                {
                    Source = Name,
                    ArtifactType = "DownloadedFile",
                    FilePath = file,
                    Sha256Hash = CollectorUtils.ComputeSha256(file),
                    CreatedUtc = CollectorUtils.GetCreatedUtc(file),
                    ModifiedUtc = CollectorUtils.GetModifiedUtc(file),
                    FileSizeBytes = CollectorUtils.GetFileSize(file),
                    Metadata = new Dictionary<string, string>
                    {
                        ["downloadDirectory"] = downloads,
                        ["isExecutable"] = isExecutable.ToString(),
                        ["isArchive"] = isArchive.ToString()
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Debug("Error processing download {File}: {Error}", file, ex.Message);
            }
        }
    }
}
