using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Correlation;

/// <summary>
/// Implements correlation logic:
/// 1. Evaluates explicit correlation rules (rules that require multiple other rules to match)
/// 2. Applies automatic confidence boosting when the same entity is flagged by multiple rules
///
/// Correlation rules use the "requires" field to list rule IDs that must all have produced
/// findings. When all requirements are met, a new composite finding is generated with
/// boosted confidence.
/// </summary>
public sealed class CorrelationEngine : ICorrelationEngine
{
    private static readonly ILogger Log = Serilog.Log.ForContext<CorrelationEngine>();

    public IReadOnlyList<Finding> Correlate(
        IReadOnlyList<Finding> findings,
        IReadOnlyList<Rule> correlationRules)
    {
        var correlatedFindings = new List<Finding>();

        // Index findings by rule ID for quick lookup
        var findingsByRule = findings
            .GroupBy(f => f.RuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // 1. Evaluate explicit correlation rules
        foreach (var rule in correlationRules)
        {
            if (rule.Requires == null || rule.Requires.Count == 0) continue;

            var matchedRequirements = new List<Finding>();
            var allMet = true;

            foreach (var requiredRuleId in rule.Requires)
            {
                if (findingsByRule.TryGetValue(requiredRuleId, out var matchedFindings) && matchedFindings.Count > 0)
                {
                    matchedRequirements.AddRange(matchedFindings);
                }
                else
                {
                    allMet = false;
                    break;
                }
            }

            // Check minimum matches threshold
            if (rule.MinimumMatches.HasValue)
            {
                if (matchedRequirements.Count < rule.MinimumMatches.Value)
                    allMet = false;
            }

            if (allMet && matchedRequirements.Count > 0)
            {
                var correlatedFinding = CreateCorrelatedFinding(rule, matchedRequirements);
                correlatedFindings.Add(correlatedFinding);

                Log.Information("Correlation rule {RuleId} matched with {Count} contributing findings",
                    rule.Id, matchedRequirements.Count);
            }
        }

        // 2. Automatic confidence boosting for entity-level correlation
        var entityCorrelated = ApplyEntityCorrelation(findings);
        correlatedFindings.AddRange(entityCorrelated);

        Log.Information("Correlation produced {Count} additional findings", correlatedFindings.Count);
        return correlatedFindings;
    }

    private Finding CreateCorrelatedFinding(Rule rule, List<Finding> contributing)
    {
        var boost = rule.ConfidenceBoost ?? 0.2;
        var maxConfidence = contributing.Max(f => f.Confidence);
        var boostedConfidence = Math.Min(1.0, maxConfidence + boost);

        var allEvidence = contributing.SelectMany(f => f.EvidenceRefs).ToList();
        var contributingIds = contributing.Select(f => f.Id).ToList();

        return new Finding
        {
            Id = $"CF-{Guid.NewGuid().ToString("N")[..8]}",
            RuleId = rule.Id,
            Title = rule.Title,
            Summary = $"Correlated finding: {rule.Description} " +
                      $"[{contributing.Count} contributing findings from rules: {string.Join(", ", rule.Requires!)}]",
            Severity = SeverityExtensions.Parse(rule.Severity),
            Confidence = boostedConfidence,
            Source = "CorrelationEngine",
            ArtifactType = "Correlation",
            Tags = new List<string>(rule.Tags) { "correlated" },
            EvidenceRefs = allEvidence,
            CorrelatedFindingIds = contributingIds
        };
    }

    /// <summary>
    /// Automatic entity-level correlation: if the same file path is flagged by 3+ different rules,
    /// generate a high-confidence composite finding.
    /// </summary>
    private List<Finding> ApplyEntityCorrelation(IReadOnlyList<Finding> findings)
    {
        var result = new List<Finding>();

        // Group findings by the primary file path in their evidence
        var byPath = new Dictionary<string, List<Finding>>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            foreach (var evidence in finding.EvidenceRefs)
            {
                if (!string.IsNullOrEmpty(evidence.FilePath))
                {
                    if (!byPath.ContainsKey(evidence.FilePath))
                        byPath[evidence.FilePath] = new List<Finding>();
                    byPath[evidence.FilePath].Add(finding);
                }
            }
        }

        foreach (var (path, group) in byPath)
        {
            var distinctRules = group.Select(f => f.RuleId).Distinct().ToList();
            if (distinctRules.Count < 3) continue;

            var maxSeverity = group.Max(f => f.Severity);
            var maxConfidence = Math.Min(1.0, group.Max(f => f.Confidence) + 0.15);

            result.Add(new Finding
            {
                Id = $"AC-{Guid.NewGuid().ToString("N")[..8]}",
                RuleId = "AUTO-CORRELATE",
                Title = $"Multiple indicators for: {Path.GetFileName(path)}",
                Summary = $"Entity '{path}' matched {distinctRules.Count} different rules: " +
                          $"{string.Join(", ", distinctRules)}. Auto-correlated with boosted confidence.",
                Severity = maxSeverity,
                Confidence = maxConfidence,
                Source = "CorrelationEngine",
                ArtifactType = "AutoCorrelation",
                Tags = new List<string> { "auto-correlated", "multi-indicator" },
                EvidenceRefs = group.SelectMany(f => f.EvidenceRefs).ToList(),
                CorrelatedFindingIds = group.Select(f => f.Id).ToList()
            });

            Log.Information("Auto-correlation: {Path} matched {Count} distinct rules", path, distinctRules.Count);
        }

        return result;
    }
}
