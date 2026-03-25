using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the artifact's SHA256 hash matches known IOC hashes.
/// Comparison is case-insensitive hex.
/// </summary>
public sealed class HashEvaluator : IRuleEvaluator
{
    public string RuleType => "hash";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var hash = artifact.Sha256Hash;
        if (string.IsNullOrEmpty(hash)) return false;

        if (!string.IsNullOrEmpty(rule.Value))
        {
            if (hash.Equals(rule.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (rule.Values != null)
        {
            foreach (var v in rule.Values)
            {
                if (hash.Equals(v, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
