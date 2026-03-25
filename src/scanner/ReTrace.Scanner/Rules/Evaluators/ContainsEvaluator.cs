using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the field value contains the rule value as a substring (case-insensitive).
/// Supports multiple values via the 'values' array (any match = hit).
/// </summary>
public sealed class ContainsEvaluator : IRuleEvaluator
{
    public string RuleType => "contains";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var fieldValue = FieldResolver.Resolve(artifact, rule.Field);
        if (string.IsNullOrEmpty(fieldValue)) return false;

        if (!string.IsNullOrEmpty(rule.Value))
        {
            if (fieldValue.Contains(rule.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (rule.Values != null)
        {
            foreach (var v in rule.Values)
            {
                if (fieldValue.Contains(v, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
