using Microsoft.Win32;
using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Collects artifacts from the Windows Amcache registry hive.
/// Amcache tracks application execution, installation, and compatibility data.
///
/// Primary sources:
/// - HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AppCompatFlags\Amcache
/// - Registry hive: C:\Windows\appcompat\Programs\Amcache.hve
///
/// The collector reads from the live registry for currently accessible entries.
/// Key: InventoryApplicationFile contains paths, hashes, publisher info.
/// </summary>
public sealed class AmcacheCollector : ICollector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<AmcacheCollector>();

    private const string AmcacheInventoryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppCompatFlags\Amcache\InventoryApplicationFile";

    private const string AmcacheAppPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppCompatFlags\Amcache\InventoryApplication";

    // Fallback: shimcache / AppCompatCache
    private const string ShimCachePath =
        @"SYSTEM\CurrentControlSet\Control\Session Manager\AppCompatCache";

    public string Name => "AmcacheCollector";
    public string ArtifactType => "Amcache";

    public IReadOnlyList<RawArtifact> Collect()
    {
        var artifacts = new List<RawArtifact>();

        CollectInventoryApplicationFile(artifacts);
        CollectFromAppCompatFlags(artifacts);

        Log.Information("Collected {Count} Amcache artifacts", artifacts.Count);
        return artifacts;
    }

    private void CollectInventoryApplicationFile(List<RawArtifact> artifacts)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AmcacheInventoryPath);
            if (key == null)
            {
                Log.Debug("Amcache InventoryApplicationFile key not found");
                return;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var filePath = subKey.GetValue("LowerCaseLongPath")?.ToString()
                                ?? subKey.GetValue("Name")?.ToString();

                    if (string.IsNullOrEmpty(filePath)) continue;

                    var metadata = new Dictionary<string, string>
                    {
                        ["registryKey"] = subKeyName
                    };

                    AddValueIfExists(subKey, "Publisher", metadata, "publisher");
                    AddValueIfExists(subKey, "Version", metadata, "version");
                    AddValueIfExists(subKey, "BinaryType", metadata, "binaryType");
                    AddValueIfExists(subKey, "ProductName", metadata, "productName");
                    AddValueIfExists(subKey, "ProductVersion", metadata, "productVersion");
                    AddValueIfExists(subKey, "LinkDate", metadata, "linkDate");
                    AddValueIfExists(subKey, "Size", metadata, "size");
                    AddValueIfExists(subKey, "Language", metadata, "language");
                    AddValueIfExists(subKey, "IsPeFile", metadata, "isPeFile");

                    var sha1 = subKey.GetValue("FileId")?.ToString()?.TrimStart('0');

                    if (!string.IsNullOrEmpty(sha1))
                        metadata["sha1"] = sha1;

                    var executableName = Path.GetFileName(filePath);
                    if (!string.IsNullOrEmpty(executableName))
                        metadata["executableName"] = executableName;

                    artifacts.Add(new RawArtifact
                    {
                        Source = Name,
                        ArtifactType = ArtifactType,
                        FilePath = filePath,
                        Sha256Hash = null, // Amcache stores SHA1, not SHA256
                        CreatedUtc = null,
                        ModifiedUtc = null,
                        FileSizeBytes = long.TryParse(metadata.GetValueOrDefault("size"), out var s) ? s : null,
                        Metadata = metadata
                    });
                }
                catch (Exception ex)
                {
                    Log.Debug("Error reading Amcache subkey {Key}: {Error}", subKeyName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Could not read Amcache InventoryApplicationFile: {Error}", ex.Message);
        }
    }

    private void CollectFromAppCompatFlags(List<RawArtifact> artifacts)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(AmcacheAppPath);
            if (key == null)
            {
                Log.Debug("Amcache InventoryApplication key not found");
                return;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    var name = subKey.GetValue("Name")?.ToString();
                    var installPath = subKey.GetValue("RootDirPath")?.ToString();

                    if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(installPath)) continue;

                    var metadata = new Dictionary<string, string>
                    {
                        ["registryKey"] = subKeyName,
                        ["source"] = "InventoryApplication"
                    };

                    if (!string.IsNullOrEmpty(name)) metadata["appName"] = name;
                    AddValueIfExists(subKey, "Publisher", metadata, "publisher");
                    AddValueIfExists(subKey, "Version", metadata, "version");
                    AddValueIfExists(subKey, "InstallDate", metadata, "installDate");
                    AddValueIfExists(subKey, "UninstallString", metadata, "uninstallString");

                    artifacts.Add(new RawArtifact
                    {
                        Source = Name,
                        ArtifactType = "AmcacheApp",
                        FilePath = installPath ?? name ?? subKeyName,
                        Metadata = metadata
                    });
                }
                catch (Exception ex)
                {
                    Log.Debug("Error reading Amcache app subkey {Key}: {Error}", subKeyName, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Could not read Amcache InventoryApplication: {Error}", ex.Message);
        }
    }

    private static void AddValueIfExists(RegistryKey key, string valueName, Dictionary<string, string> dict, string targetKey)
    {
        var value = key.GetValue(valueName)?.ToString();
        if (!string.IsNullOrEmpty(value))
            dict[targetKey] = value;
    }
}
