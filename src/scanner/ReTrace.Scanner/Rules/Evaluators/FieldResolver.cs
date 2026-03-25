using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Resolves field names used in rules to actual values from a NormalizedArtifact.
/// Supports fixed fields (path, fileName, hash, etc.) and metadata.* for dynamic fields.
/// </summary>
internal static class FieldResolver
{
    /// <summary>
    /// Gets the value of the named field from the artifact.
    /// Returns null if the field is not found or empty.
    /// </summary>
    public static string? Resolve(NormalizedArtifact artifact, string field)
    {
        return field.ToLowerInvariant() switch
        {
            "path" or "filepath" => artifact.FilePath,
            "filename" => artifact.FileName,
            "extension" or "fileextension" => artifact.FileExtension,
            "directory" or "directorypath" => artifact.DirectoryPath,
            "hash" or "sha256" or "sha256hash" => artifact.Sha256Hash,
            "source" => artifact.Source,
            "artifacttype" or "type" => artifact.ArtifactType,
            "executablename" => artifact.ExecutableName,
            "runcount" => artifact.RunCount?.ToString(),
            "filesize" or "filesizebytes" => artifact.FileSizeBytes?.ToString(),
            _ => ResolveMetadata(artifact, field)
        };
    }

    private static string? ResolveMetadata(NormalizedArtifact artifact, string field)
    {
        // Support metadata.* syntax (e.g. metadata.browser, metadata.publisher)
        if (field.StartsWith("metadata.", StringComparison.OrdinalIgnoreCase))
        {
            var key = field["metadata.".Length..];
            return artifact.Metadata.GetValueOrDefault(key);
        }

        // Also try direct metadata key lookup
        return artifact.Metadata.GetValueOrDefault(field);
    }
}
