using ReTrace.Scanner.Models;
using ReTrace.Scanner.Rules.Evaluators;
using Serilog;

namespace ReTrace.Scanner.Rules;

/// <summary>
/// The core rule engine. Evaluates all non-correlation rules against all normalized artifacts.
/// Dispatches each rule to the appropriate evaluator based on rule type.
/// Produces findings for every rule-artifact match.
/// </summary>
public sealed class RuleEngine : IRuleEngine
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RuleEngine>();
    private readonly Dictionary<string, IRuleEvaluator> _evaluators;

    public RuleEngine()
    {
        var evaluatorList = new IRuleEvaluator[]
        {
            new ExactMatchEvaluator(),
            new ContainsEvaluator(),
            new RegexEvaluator(),
            new HashEvaluator(),
            new PathEvaluator(),
            new FilenameEvaluator()
        };

        _evaluators = evaluatorList.ToDictionary(e => e.RuleType, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Finding> Evaluate(IReadOnlyList<NormalizedArtifact> artifacts, IReadOnlyList<Rule> rules)
    {
        var findings = new List<Finding>();

        // Only evaluate non-correlation rules here; correlation is handled separately
        var standardRules = rules.Where(r =>
            !r.Type.Equals("correlation", StringComparison.OrdinalIgnoreCase)).ToList();

        Log.Information("Evaluating {RuleCount} rules against {ArtifactCount} artifacts",
            standardRules.Count, artifacts.Count);

        foreach (var rule in standardRules)
        {
            if (!_evaluators.TryGetValue(rule.Type, out var evaluator))
            {
                Log.Warning("No evaluator registered for rule type '{Type}' (rule {Id})", rule.Type, rule.Id);
                continue;
            }

            foreach (var artifact in artifacts)
            {
                try
                {
                    if (evaluator.Matches(artifact, rule))
                    {
                        var finding = CreateFinding(rule, artifact);
                        findings.Add(finding);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Error evaluating rule {RuleId} against artifact {ArtifactId}: {Error}",
                        rule.Id, artifact.Id, ex.Message);
                }
            }
        }

        Log.Information("Rule evaluation produced {Count} findings", findings.Count);
        return findings;
    }

    private static Finding CreateFinding(Rule rule, NormalizedArtifact artifact)
    {
        return new Finding
        {
            Id = $"F-{Guid.NewGuid().ToString("N")[..8]}",
            RuleId = rule.Id,
            Title = rule.Title,
            Summary = BuildSummary(rule, artifact),
            Severity = SeverityExtensions.Parse(rule.Severity),
            Confidence = rule.Confidence,
            Source = artifact.Source,
            ArtifactType = artifact.ArtifactType,
            Tags = new List<string>(rule.Tags),
            EvidenceRefs = new List<EvidenceReference>
            {
                new()
                {
                    ArtifactId = artifact.Id,
                    FilePath = artifact.FilePath,
                    Sha256Hash = artifact.Sha256Hash,
                    Timestamp = artifact.LastExecutedUtc ?? artifact.ModifiedUtc ?? artifact.CreatedUtc
                }
            }
        };
    }

    private static string BuildSummary(Rule rule, NormalizedArtifact artifact)
    {
        var matchedValue = Evaluators.FieldResolver.Resolve(artifact, rule.Field) ?? "(unknown)";
        return $"{rule.Description} [matched: {rule.Field}=\"{Truncate(matchedValue, 120)}\"]";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}
