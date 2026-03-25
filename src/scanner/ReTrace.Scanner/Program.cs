using ReTrace.Scanner.Collectors;
using ReTrace.Scanner.Config;
using ReTrace.Scanner.Correlation;
using ReTrace.Scanner.Evidence;
using ReTrace.Scanner.Models;
using ReTrace.Scanner.Normalization;
using ReTrace.Scanner.Pipeline;
using ReTrace.Scanner.Rules;
using Serilog;
using Serilog.Events;

namespace ReTrace.Scanner;

public static class Program
{
    public static int Main(string[] args)
    {
        var config = ParseArguments(args);
        ConfigureLogging(config);

        try
        {
            Log.Information("Re:Trace Scanner starting...");

            var pipeline = BuildPipeline(config);
            var result = pipeline.Execute();

            return result.FindingCount > 0 ? 0 : 0; // Always 0 on success
        }
        catch (Exception ex)
        {
            Log.Fatal("Scanner crashed: {Error}", ex.Message);
            Log.Debug(ex, "Full exception");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static ScannerPipeline BuildPipeline(ScannerConfig config)
    {
        // Build collector list based on config
        var collectors = new List<ICollector>();

        if (config.EnablePrefetch)
            collectors.Add(new PrefetchCollector());
        if (config.EnableAmcache)
            collectors.Add(new AmcacheCollector());
        if (config.EnableTempFiles)
            collectors.Add(new TempFilesCollector());
        if (config.EnableRecentFiles)
            collectors.Add(new RecentFilesCollector());
        if (config.EnableBrowserDownloads)
            collectors.Add(new BrowserDownloadsCollector());

        Log.Information("Enabled collectors: {Collectors}",
            string.Join(", ", collectors.Select(c => c.Name)));

        return new ScannerPipeline(
            config,
            collectors,
            new ArtifactNormalizer(),
            new RuleEngine(),
            new CorrelationEngine(),
            new EvidencePackager(),
            new RuleLoader()
        );
    }

    private static ScannerConfig ParseArguments(string[] args)
    {
        var config = new ScannerConfig();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "--rules" or "-r":
                    if (i + 1 < args.Length) config.RulesPath = args[++i];
                    break;

                case "--output" or "-o":
                    if (i + 1 < args.Length) config.OutputPath = args[++i];
                    break;

                case "--no-zip":
                    config.PackageEvidence = false;
                    break;

                case "--include-artifacts":
                    config.IncludeArtifactsInOutput = true;
                    break;

                case "--upload-url":
                    if (i + 1 < args.Length)
                    {
                        config.UploadUrl = args[++i];
                        config.OfflineMode = false;
                    }
                    break;

                case "--api-key":
                    if (i + 1 < args.Length) config.ApiKey = args[++i];
                    break;

                case "--verbose" or "-v":
                    config.LogLevel = Config.LogLevel.Debug;
                    break;

                case "--quiet" or "-q":
                    config.LogLevel = Config.LogLevel.Warning;
                    break;

                // Collector toggles
                case "--no-prefetch":
                    config.EnablePrefetch = false;
                    break;
                case "--no-amcache":
                    config.EnableAmcache = false;
                    break;
                case "--no-temp":
                    config.EnableTempFiles = false;
                    break;
                case "--no-recent":
                    config.EnableRecentFiles = false;
                    break;
                case "--no-browser":
                    config.EnableBrowserDownloads = false;
                    break;

                case "--help" or "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;

                default:
                    if (arg.StartsWith("-"))
                        Console.Error.WriteLine($"Unknown option: {args[i]}");
                    break;
            }
        }

        // Resolve relative paths
        config.RulesPath = Path.GetFullPath(config.RulesPath);
        config.OutputPath = Path.GetFullPath(config.OutputPath);

        return config;
    }

    private static void ConfigureLogging(ScannerConfig config)
    {
        var minLevel = config.LogLevel switch
        {
            Config.LogLevel.Debug => LogEventLevel.Debug,
            Config.LogLevel.Warning => LogEventLevel.Warning,
            Config.LogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minLevel)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(config.OutputPath, "retrace-scanner.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext} | {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Re:Trace Scanner - Forensic Anti-Cheat Scanner
================================================

Usage: ReTrace.Scanner [options]

Options:
  -r, --rules <path>       Path to rules directory (default: ./rules)
  -o, --output <path>      Output directory (default: ./output)
      --no-zip             Skip ZIP evidence packaging
      --include-artifacts   Include full artifact list in JSON output
      --upload-url <url>   Backend API URL for uploading results
      --api-key <key>      API key for backend authentication
  -v, --verbose            Enable debug logging
  -q, --quiet              Only show warnings and errors
  -h, --help               Show this help

Collector toggles:
      --no-prefetch        Disable Prefetch collector
      --no-amcache         Disable Amcache collector
      --no-temp            Disable Temp files collector
      --no-recent          Disable Recent files collector
      --no-browser         Disable Browser downloads collector

Examples:
  ReTrace.Scanner --rules ./rules --output ./scan-output
  ReTrace.Scanner --verbose --include-artifacts
  ReTrace.Scanner --no-browser --no-recent -o C:\scans\today
");
    }
}
