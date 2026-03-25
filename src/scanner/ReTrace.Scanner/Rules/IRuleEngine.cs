using ReTrace.Scanner.Models;

namespace ReTrace.Scanner.Rules;

/// <summary>
/// Evaluates rules against normalized artifacts to produce findings.
/// </summary>
public interface IRuleEngine
{
    IReadOnlyList<Finding> Evaluate(IReadOnlyList<NormalizedArtifact> artifacts, IReadOnlyList<Rule> rules);
}
