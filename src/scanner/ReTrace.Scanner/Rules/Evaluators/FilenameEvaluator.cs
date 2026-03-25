using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the artifact's file name contains or equals the rule value.
/// Used to detect files with suspicious naming patterns.
/// </summary>
public sealed class FilenameEvaluator : IRuleEvaluator
{
    public string RuleType => "filename";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var fileName = artifact.FileName;
        if (string.IsNullOrEmpty(fileName)) return false;

        if (!string.IsNullOrEmpty(rule.Value))
        {
            if (fileName.Contains(rule.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (rule.Values != null)
        {
            foreach (var v in rule.Values)
            {
                if (fileName.Contains(v, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
