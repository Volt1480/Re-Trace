# Re:Trace – Forensic Anti-Cheat Scanner

**Re:Trace** is a modular, forensic-based anti-cheat scanning platform for Windows with a focus on FiveM environments.

It is **not** a kernel anti-cheat. It does **not** hook processes or block execution in real-time.

Instead, Re:Trace detects **post-execution traces**, **residual artifacts**, and **suspicious evidence chains** left behind by loaders, injectors, temp payloads, and related tooling.

---

## How It Works

Re:Trace follows a strict pipeline architecture:

```
Collect → Normalize → Evaluate Rules → Correlate → Package Evidence
```

1. **Collectors** gather raw forensic artifacts from the system (Prefetch, Amcache, temp files, recent files, browser downloads)
2. **Normalizer** converts all raw data into a standardized format
3. **Rule Engine** evaluates JSON-based detection rules against normalized artifacts
4. **Correlation Engine** combines weak signals into stronger findings
5. **Evidence Packager** exports results as JSON + optional ZIP bundle

---

## Quick Start

### Prerequisites

- Windows 10/11
- .NET 8 SDK

### Build & Run

```powershell
cd src/scanner/ReTrace.Scanner
dotnet build
dotnet run -- --rules ../../../rules --output ../../../output --verbose
```

### CLI Options

```
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

Collector toggles:
      --no-prefetch        Disable Prefetch collector
      --no-amcache         Disable Amcache collector
      --no-temp            Disable Temp files collector
      --no-recent          Disable Recent files collector
      --no-browser         Disable Browser downloads collector
```

---

## Output

After a scan, the output directory contains:

| File | Description |
|------|-------------|
| `scan-result.json` | Complete scan result with findings, evidence manifest, and statistics |
| `findings.json` | Findings-only export for quick review |
| `statistics.json` | Scan statistics (artifact counts, duration, severity breakdown) |
| `retrace-evidence-*.zip` | ZIP bundle with JSON + referenced evidence files |
| `retrace-scanner.log` | Detailed scan log |

---

## Architecture

### Collectors

Collectors **only** gather raw data. They contain **no** detection logic.

| Collector | Source | What it collects |
|-----------|--------|-----------------|
| `PrefetchCollector` | `C:\Windows\Prefetch` | Execution history (.pf files) |
| `AmcacheCollector` | Windows Registry | Application execution records |
| `TempFilesCollector` | `%TEMP%`, `%WINDIR%\Temp` | Files in temporary directories |
| `RecentFilesCollector` | `%APPDATA%\...\Recent` | Recently opened file shortcuts |
| `BrowserDownloadsCollector` | Chrome/Edge/Brave + Downloads folder | Browser download traces |

### Rule Engine

Rules are JSON files in the `rules/` directory. Supported rule types:

| Type | Description |
|------|-------------|
| `exact` | Field value equals rule value exactly |
| `contains` | Field value contains rule value as substring |
| `regex` | Field value matches regex pattern |
| `hash` | Artifact SHA256 matches known IOC hash |
| `path` | Directory path contains suspicious location |
| `filename` | File name contains suspicious pattern |
| `correlation` | Combines multiple rule matches into stronger finding |

### Correlation

Single artifact ≠ detection. The correlation engine:

- Evaluates explicit correlation rules (e.g., "loader + injector = cheat workflow")
- Auto-correlates when 3+ different rules flag the same file path
- Boosts confidence when multiple sources agree

---

## Rules

Rules live in `rules/` and are loaded recursively. Each `.json` file contains an array of rule objects.

Example rule:
```json
{
  "id": "FIVEM-001",
  "type": "contains",
  "field": "path",
  "value": "loader",
  "severity": "high",
  "confidence": 0.5,
  "title": "Suspicious loader path",
  "description": "File path contains 'loader'.",
  "tags": ["fivem", "loader"]
}
```

Example correlation rule:
```json
{
  "id": "FIVEM-CORR-001",
  "type": "correlation",
  "field": "_",
  "severity": "critical",
  "confidence": 0.9,
  "title": "Loader + Injector chain",
  "description": "Both loader and injector artifacts found.",
  "requires": ["FIVEM-001", "FIVEM-002"],
  "confidenceBoost": 0.3
}
```

---

## Project Structure

```
retrace/
├── ReTrace.sln
├── rules/
│   └── fivem/
│       └── fivem-rules.json
├── src/
│   ├── shared/
│   │   └── contracts.json
│   └── scanner/
│       └── ReTrace.Scanner/
│           ├── Program.cs
│           ├── Config/
│           ├── Collectors/
│           ├── Models/
│           ├── Normalization/
│           ├── Rules/
│           │   └── Evaluators/
│           ├── Correlation/
│           ├── Evidence/
│           └── Pipeline/
├── samples/
├── docs/
└── output/          (generated at runtime)
```

---

## Design Principles

- **Evidence-driven**: Every finding must be explainable and traceable
- **Correlation over single indicators**: Weak signals combine into strong evidence
- **Modular**: Collectors, normalizers, evaluators are all interchangeable
- **No hardcoded detection**: All detection logic lives in external JSON rules
- **Testable**: Each component can be unit-tested in isolation
- **FiveM-focused, architecturally generic**: Domain-specific logic stays in rule packs

---

## License

MIT
