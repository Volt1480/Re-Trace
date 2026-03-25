using System.Text.Json.Serialization;

namespace ReTrace.Scanner.Models;

/// <summary>
/// A detection rule loaded from JSON rule packs.
/// Rules define what patterns to look for and how to classify matches.
/// </summary>
public sealed class Rule
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; } // exact, contains, regex, hash, correlation

    [JsonPropertyName("field")]
    public required string Field { get; init; } // path, fileName, hash, extension, executableName, metadata.*

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("values")]
    public List<string>? Values { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; } = 0.5;

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = new();

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    // Correlation-specific
    [JsonPropertyName("requires")]
    public List<string>? Requires { get; init; } // rule IDs that must also match

    [JsonPropertyName("minimumMatches")]
    public int? MinimumMatches { get; init; }

    [JsonPropertyName("confidenceBoost")]
    public double? ConfidenceBoost { get; init; }
}
