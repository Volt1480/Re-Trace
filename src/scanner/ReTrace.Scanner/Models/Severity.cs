namespace ReTrace.Scanner.Models;

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public static class SeverityExtensions
{
    public static Severity Parse(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "info" => Severity.Info,
            "low" => Severity.Low,
            "medium" => Severity.Medium,
            "high" => Severity.High,
            "critical" => Severity.Critical,
            _ => Severity.Info
        };
    }
}
