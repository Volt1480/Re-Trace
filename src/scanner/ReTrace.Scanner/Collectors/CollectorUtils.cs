using System.Security.Cryptography;
using Serilog;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// Shared utilities for collectors — hashing, safe file access, timestamp extraction.
/// </summary>
internal static class CollectorUtils
{
    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(CollectorUtils));

    public static string? ComputeSha256(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            Log.Debug("Failed to hash {Path}: {Error}", filePath, ex.Message);
            return null;
        }
    }

    public static DateTime? GetCreatedUtc(string filePath)
    {
        try { return File.GetCreationTimeUtc(filePath); }
        catch { return null; }
    }

    public static DateTime? GetModifiedUtc(string filePath)
    {
        try { return File.GetLastWriteTimeUtc(filePath); }
        catch { return null; }
    }

    public static long? GetFileSize(string filePath)
    {
        try { return new FileInfo(filePath).Length; }
        catch { return null; }
    }

    public static string[] SafeGetFiles(string directory, string searchPattern = "*", SearchOption option = SearchOption.TopDirectoryOnly)
    {
        try
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.GetFiles(directory, searchPattern, option);
        }
        catch (Exception ex)
        {
            Log.Warning("Could not enumerate {Dir}: {Error}", directory, ex.Message);
            return Array.Empty<string>();
        }
    }

    public static string[] SafeGetDirectories(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return Array.Empty<string>();
            return Directory.GetDirectories(directory);
        }
        catch (Exception ex)
        {
            Log.Warning("Could not enumerate directories in {Dir}: {Error}", directory, ex.Message);
            return Array.Empty<string>();
        }
    }
}
