using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Collectors;

/// <summary>
/// A collector gathers raw artifacts from a specific source.
/// Collectors must NOT contain detection logic — they only collect data.
/// </summary>
public interface ICollector
{
    /// <summary>Unique name identifying this collector.</summary>
    string Name { get; }

    /// <summary>The type of artifacts this collector produces.</summary>
    string ArtifactType { get; }

    /// <summary>Collect raw artifacts from the system.</summary>
    IReadOnlyList<RawArtifact> Collect();
}
