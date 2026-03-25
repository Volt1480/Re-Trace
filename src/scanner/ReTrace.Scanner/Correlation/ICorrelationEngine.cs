using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Correlation;

/// <summary>
/// Correlates findings from the rule engine to produce higher-confidence composite findings.
/// Single artifact != detection. Multiple artifacts correlated = stronger evidence.
/// </summary>
public interface ICorrelationEngine
{
    IReadOnlyList<Finding> Correlate(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Rule> correlationRules);
}
