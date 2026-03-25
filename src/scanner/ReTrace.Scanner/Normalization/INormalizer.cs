using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Normalization;

/// <summary>
/// Normalizes raw artifacts into a standard format for rule evaluation.
/// </summary>
public interface INormalizer
{
    IReadOnlyList<NormalizedArtifact> Normalize(IReadOnlyList<RawArtifact> rawArtifacts);
}
