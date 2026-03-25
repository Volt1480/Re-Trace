using System.Text.RegularExpressions;
using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Rules.Evaluators;

/// <summary>
/// Matches when the field value matches the rule's regex pattern.
/// Uses compiled regex with a timeout to prevent ReDoS.
/// </summary>
public sealed class RegexEvaluator : IRuleEvaluator
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RegexEvaluator>();
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    // Cache compiled regex patterns to avoid recompilation
    private readonly Dictionary<string, Regex?> _cache = new();

    public string RuleType => "regex";

    public bool Matches(NormalizedArtifact artifact, Rule rule)
    {
        var fieldValue = FieldResolver.Resolve(artifact, rule.Field);
        if (string.IsNullOrEmpty(fieldValue)) return false;

        var patterns = new List<string>();
        if (!string.IsNullOrEmpty(rule.Value)) patterns.Add(rule.Value);
        if (rule.Values != null) patterns.AddRange(rule.Values);

        foreach (var pattern in patterns)
        {
            var regex = GetOrCompile(pattern);
            if (regex == null) continue;

            try
            {
                if (regex.IsMatch(fieldValue))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                Log.Warning("Regex timeout for rule {RuleId} pattern: {Pattern}", rule.Id, pattern);
            }
        }

        return false;
    }

    private Regex? GetOrCompile(string pattern)
    {
        if (_cache.TryGetValue(pattern, out var cached))
            return cached;

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
            _cache[pattern] = regex;
            return regex;
        }
        catch (ArgumentException ex)
        {
            Log.Warning("Invalid regex pattern '{Pattern}': {Error}", pattern, ex.Message);
            _cache[pattern] = null;
            return null;
        }
    }
}
