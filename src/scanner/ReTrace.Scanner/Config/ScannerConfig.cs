namespace ReTrace.Scanner.Config;

/// <summary>
/// Configuration for a scanner run. Can be populated from CLI args, appsettings, or env vars.
/// </summary>
public sealed class ScannerConfig
{
    public string RulesPath { get; set; } = "rules";
    public string OutputPath { get; set; } = "output";
    public string OutputFileName { get; set; } = "scan-result.json";
    public bool PackageEvidence { get; set; } = true;
    public bool IncludeArtifactsInOutput { get; set; } = false;
    public bool OfflineMode { get; set; } = true;
    public string? UploadUrl { get; set; }
    public string? ApiKey { get; set; }
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    // Collector toggles
    public bool EnablePrefetch { get; set; } = true;
    public bool EnableAmcache { get; set; } = true;
    public bool EnableTempFiles { get; set; } = true;
    public bool EnableRecentFiles { get; set; } = true;
    public bool EnableBrowserDownloads { get; set; } = true;
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}
