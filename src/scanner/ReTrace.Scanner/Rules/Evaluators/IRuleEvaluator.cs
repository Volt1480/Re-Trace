using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Evaluates a single rule type against a normalized artifact.
/// Each rule type (exact, contains, regex, hash) has its own evaluator.
/// </summary>
public interface IRuleEvaluator
{
    /// <summary>The rule type this evaluator handles (e.g. "exact", "contains").</summary>
    string RuleType { get; }

    /// <summary>Returns true if the artifact matches the rule.</summary>
    bool Matches(NormalizedArtifact artifact, Rule rule);
}
