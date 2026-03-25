using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the artifact's directory path contains or equals the rule value.
/// Used to detect files in suspicious locations.
/// </summary>
public sealed class PathEvaluator : IRuleEvaluator
{
    public string RuleType => "path";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var dirPath = artifact.DirectoryPath;
        if (string.IsNullOrEmpty(dirPath)) return false;

        if (!string.IsNullOrEmpty(rule.Value))
        {
            if (dirPath.Contains(rule.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (rule.Values != null)
        {
            foreach (var v in rule.Values)
            {
                if (dirPath.Contains(v, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
