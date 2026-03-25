using System.Diagnostics;
using ReTrace.Scanner.Collectors;
using ReTrace.Scanner.Config;
using ReTrace.Scanner.Correlation;
using ReTrace.Scanner.Evidence;
using ReTrace.Scanner.Models;
using ReTrace.Scanner.Normalization;
using ReTrace.Scanner.Rules;
using Serilog;

namespace ReTrace.Scanner.Pipeline;

/// <summary>
/// Orchestrates the full scanner pipeline:
/// 1. Collect raw artifacts from all enabled collectors
/// 2. Normalize artifacts into a standard format
/// 3. Load and evaluate rules
/// 4. Correlate findings
/// 5. Build statistics
/// 6. Package evidence
///
/// Each step is isolated and testable. The pipeline only connects them.
/// </summary>
public sealed class ScannerPipeline
{
    private static readonly ILogger Log = Serilog.Log.ForContext<ScannerPipeline>();

    private readonly ScannerConfig _config;
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly INormalizer _normalizer;
    private readonly IRuleEngine _ruleEngine;
    private readonly ICorrelationEngine _correlationEngine;
    private readonly IEvidencePackager _evidencePackager;
    private readonly RuleLoader _ruleLoader;

    public ScannerPipeline(
        ScannerConfig config,
        IReadOnlyList<ICollector> collectors,
        INormalizer normalizer,
        IRuleEngine ruleEngine,
        ICorrelationEngine correlationEngine,
        IEvidencePackager evidencePackager,
        RuleLoader ruleLoader)
    {
        _config = config;
        _collectors = collectors;
        _normalizer = normalizer;
        _ruleEngine = ruleEngine;
        _correlationEngine = correlationEngine;
        _evidencePackager = evidencePackager;
        _ruleLoader = ruleLoader;
    }

    public ScanResult Execute()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScanResult();

        Log.Information("=== Re:Trace Scanner v{Version} ===", result.ScannerVersion);
        Log.Information("Scan ID: {ScanId}", result.ScanId);
        Log.Information("Machine: {Machine}, User: {User}", result.MachineName, result.UserName);

        // Step 1: Collect
        Log.Information("--- Step 1: Collecting artifacts ---");
        var rawArtifacts = CollectAll();
        result.ArtifactCount = rawArtifacts.Count;

        // Step 2: Normalize
        Log.Information("--- Step 2: Normalizing artifacts ---");
        var normalized = _normalizer.Normalize(rawArtifacts);
        result.Artifacts.AddRange(normalized);

        // Step 3: Load rules
        Log.Information("--- Step 3: Loading rules ---");
        var allRules = _ruleLoader.LoadFromDirectory(_config.RulesPath);

        // Step 4: Evaluate
        Log.Information("--- Step 4: Evaluating rules ---");
        var findings = _ruleEngine.Evaluate(normalized, allRules);
        result.Findings.AddRange(findings);

        // Step 5: Correlate
        Log.Information("--- Step 5: Correlating findings ---");
        var correlationRules = allRules
            .Where(r => r.Type.Equals("correlation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var correlated = _correlationEngine.Correlate(findings, correlationRules);
        result.Findings.AddRange(correlated);

        // Step 6: Build statistics
        stopwatch.Stop();
        result.Statistics = BuildStatistics(result, allRules.Count, correlated.Count, stopwatch.ElapsedMilliseconds);

        // Step 7: Package
        Log.Information("--- Step 6: Packaging evidence ---");

        // Remove artifacts from output if not configured to include them (saves space)
        if (!_config.IncludeArtifactsInOutput)
        {
            var artifactsBackup = result.Artifacts.ToList();
            result.Artifacts.Clear();
            var outputDir = _evidencePackager.Package(result, _config.OutputPath, _config.PackageEvidence);
            // Restore artifacts (they're still needed in-memory for the pipeline)
            result.Artifacts.AddRange(artifactsBackup);
        }
        else
        {
            _evidencePackager.Package(result, _config.OutputPath, _config.PackageEvidence);
        }

        // Summary
        Log.Information("=== Scan complete ===");
        Log.Information("Artifacts: {Artifacts}, Findings: {Findings}, Correlated: {Correlated}, Duration: {Duration}ms",
            result.ArtifactCount, result.FindingCount, correlated.Count, stopwatch.ElapsedMilliseconds);

        PrintFindingsSummary(result.Findings);

        return result;
    }

    private List<RawArtifact> CollectAll()
    {
        var all = new List<RawArtifact>();

        foreach (var collector in _collectors)
        {
            try
            {
                Log.Information("Running collector: {Name}", collector.Name);
                var sw = Stopwatch.StartNew();
                var artifacts = collector.Collect();
                sw.Stop();

                all.AddRange(artifacts);
                Log.Information("Collector {Name} produced {Count} artifacts in {Duration}ms",
                    collector.Name, artifacts.Count, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Log.Error("Collector {Name} failed: {Error}", collector.Name, ex.Message);
            }
        }

        Log.Information("Total raw artifacts collected: {Count}", all.Count);
        return all;
    }

    private ScanStatistics BuildStatistics(ScanResult result, int rulesEvaluated, int correlatedCount, long durationMs)
    {
        var stats = new ScanStatistics
        {
            TotalArtifactsCollected = result.ArtifactCount,
            TotalRulesEvaluated = rulesEvaluated,
            TotalFindingsGenerated = result.FindingCount,
            CorrelatedFindings = correlatedCount,
            ScanDurationMs = durationMs
        };

        // Findings by severity
        foreach (var group in result.Findings.GroupBy(f => f.Severity))
        {
            stats.FindingsBySeverity[group.Key.ToString()] = group.Count();
        }

        // Artifacts by source
        foreach (var group in result.Artifacts.GroupBy(a => a.Source))
        {
            stats.ArtifactsBySource[group.Key] = group.Count();
        }

        return stats;
    }

    private void PrintFindingsSummary(List<Finding> findings)
    {
        if (findings.Count == 0)
        {
            Log.Information("No findings detected.");
            return;
        }

        Log.Information("--- Findings Summary ---");
        foreach (var finding in findings.OrderByDescending(f => f.Severity).ThenByDescending(f => f.Confidence))
        {
            Log.Information("[{Severity}] (confidence: {Confidence:F2}) {Title} | {Summary}",
                finding.Severity, finding.Confidence, finding.Title,
                finding.Summary.Length > 100 ? finding.Summary[..100] + "..." : finding.Summary);
        }
    }
}
