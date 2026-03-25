using System.Text.Json;
using ReTrace.Scanner.Models;
using Serilog;

namespace ReTrace.Scanner.Rules;

/// <summary>
/// Loads detection rules from JSON files in the rules directory.
/// Supports recursive loading from subdirectories (e.g. rules/fivem/).
/// Validates rules on load and skips malformed entries.
/// </summary>
public sealed class RuleLoader
{
    private static readonly ILogger Log = Serilog.Log.ForContext<RuleLoader>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Loads all rules from the given directory (recursively).
    /// Each .json file is expected to contain an array of Rule objects.
    /// </summary>
    public IReadOnlyList<Rule> LoadFromDirectory(string rulesPath)
    {
        var allRules = new List<Rule>();

        if (!Directory.Exists(rulesPath))
        {
            Log.Warning("Rules directory not found: {Path}", rulesPath);
            return allRules;
        }

        var files = Directory.GetFiles(rulesPath, "*.json", SearchOption.AllDirectories);
        Log.Information("Found {Count} rule files in {Path}", files.Length, rulesPath);

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var rules = JsonSerializer.Deserialize<List<Rule>>(json, JsonOptions);

                if (rules == null || rules.Count == 0)
                {
                    Log.Warning("Empty or null rule file: {File}", file);
                    continue;
                }

                var valid = 0;
                foreach (var rule in rules)
                {
                    if (ValidateRule(rule, file))
                    {
                        allRules.Add(rule);
                        valid++;
                    }
                }

                Log.Information("Loaded {Valid}/{Total} rules from {File}", valid, rules.Count, file);
            }
            catch (JsonException ex)
            {
                Log.Error("Malformed JSON in rule file {File}: {Error}", file, ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load rule file {File}: {Error}", file, ex.Message);
            }
        }

        Log.Information("Total rules loaded: {Count}", allRules.Count);
        return allRules;
    }

    private bool ValidateRule(Rule rule, string sourceFile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Id))
            errors.Add("Missing 'id'");
        if (string.IsNullOrWhiteSpace(rule.Type))
            errors.Add("Missing 'type'");
        if (string.IsNullOrWhiteSpace(rule.Field))
            errors.Add("Missing 'field'");
        if (string.IsNullOrWhiteSpace(rule.Title))
            errors.Add("Missing 'title'");
        if (string.IsNullOrWhiteSpace(rule.Severity))
            errors.Add("Missing 'severity'");

        // Non-correlation rules need at least one value
        if (rule.Type != "correlation")
        {
            if (string.IsNullOrWhiteSpace(rule.Value) && (rule.Values == null || rule.Values.Count == 0))
                errors.Add("Missing 'value' or 'values'");
        }

        // Correlation rules need 'requires'
        if (rule.Type == "correlation" && (rule.Requires == null || rule.Requires.Count == 0))
            errors.Add("Correlation rule missing 'requires'");

        var validTypes = new HashSet<string> { "exact", "contains", "regex", "hash", "path", "filename", "correlation" };
        if (!string.IsNullOrEmpty(rule.Type) && !validTypes.Contains(rule.Type.ToLowerInvariant()))
            errors.Add($"Unknown rule type: '{rule.Type}'");

        if (errors.Count > 0)
        {
            Log.Warning("Invalid rule '{Id}' in {File}: {Errors}",
                rule.Id ?? "(no id)", sourceFile, string.Join("; ", errors));
            return false;
        }

        return true;
    }
}
