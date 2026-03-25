using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the field value exactly equals the rule value (case-insensitive).
/// Supports multiple values via the 'values' array (any match = hit).
/// </summary>
public sealed class ExactMatchEvaluator : IRuleEvaluator
{
    public string RuleType => "exact";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var fieldValue = FieldResolver.Resolve(artifact, rule.Field);
        if (string.IsNullOrEmpty(fieldValue)) return false;

        // Check single value
        if (!string.IsNullOrEmpty(rule.Value))
        {
            if (fieldValue.Equals(rule.Value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check multiple values
        if (rule.Values != null)
        {
            foreach (var v in rule.Values)
            {
                if (fieldValue.Equals(v, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
