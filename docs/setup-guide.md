# Re:Trace – Local Setup Guide

## Prerequisites

- **Windows 10 or 11** (scanner uses Windows-specific APIs)
- **.NET 8 SDK** — download from https://dotnet.microsoft.com/download/dotnet/8.0
- **Git** (optional, for version control)

## Building the Scanner

```powershell
# Clone or extract the repository
cd retrace

# Restore NuGet packages and build
cd src\scanner\ReTrace.Scanner
dotnet restore
dotnet build
```

## Running the Scanner

### Basic Run

```powershell
# From the scanner project directory
dotnet run -- --rules ..\..\..\rules --output ..\..\..\output
```

### With Verbose Logging

```powershell
dotnet run -- --rules ..\..\..\rules --output ..\..\..\output --verbose
```

### Without ZIP Packaging

```powershell
dotnet run -- --rules ..\..\..\rules --output ..\..\..\output --no-zip
```

### Include Full Artifact Dump

```powershell
dotnet run -- --rules ..\..\..\rules --output ..\..\..\output --include-artifacts
```

### Selective Collectors

```powershell
# Only run Prefetch and Temp collectors
dotnet run -- --rules ..\..\..\rules --output ..\..\..\output --no-amcache --no-recent --no-browser
```

## Running as Published Binary

```powershell
# Publish as self-contained
dotnet publish -c Release -r win-x64 --self-contained true -o .\publish

# Run the binary
.\publish\ReTrace.Scanner.exe --rules .\rules --output .\output
```

## Checking Output

After a scan, check the output directory:

```powershell
# View findings
type output\findings.json | python -m json.tool

# View statistics
type output\statistics.json | python -m json.tool

# Full scan result
type output\scan-result.json | python -m json.tool
```

## Adding Custom Rules

1. Create a new `.json` file in the `rules/` directory (or a subdirectory)
2. Add rule objects following the schema in `docs/rule-format.md`
3. Re-run the scanner — new rules are loaded automatically

Example: Create `rules/custom/my-rules.json`:

```json
[
  {
    "id": "CUSTOM-001",
    "type": "contains",
    "field": "fileName",
    "value": "suspicious-tool",
    "severity": "high",
    "confidence": 0.6,
    "title": "My custom detection",
    "description": "Detects a specific tool by filename.",
    "tags": ["custom"]
  }
]
```

## Troubleshooting

### "Prefetch directory not found"
Run the scanner with Administrator privileges — Prefetch requires elevated access.

### "Could not read Amcache"
Amcache registry access may require Administrator privileges.

### "Rules not found"
Ensure the `--rules` path points to the directory containing your JSON rule files.

### Browser history locked
If the browser is running, the history database may be locked. The collector copies it to temp first, but some antivirus may block this. Close the browser or run with `--no-browser`.
