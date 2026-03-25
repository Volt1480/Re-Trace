# Re:Trace – Architecture Overview

## System Components

Re:Trace consists of three main components that communicate through well-defined contracts.

### Scanner (C# / .NET 8)

The scanner runs locally on a Windows machine and executes the full forensic pipeline:

```
┌─────────────┐     ┌──────────────┐     ┌─────────────┐     ┌─────────────────┐     ┌──────────────┐
│  Collectors  │ ──▶ │  Normalizer  │ ──▶ │ Rule Engine │ ──▶ │   Correlation   │ ──▶ │   Evidence   │
│  (raw data)  │     │ (std format) │     │  (findings) │     │ (strong signals)│     │  (package)   │
└─────────────┘     └──────────────┘     └─────────────┘     └─────────────────┘     └──────────────┘
```

**Key design decisions:**
- Collectors NEVER contain detection logic
- Detection logic lives exclusively in external JSON rules
- Correlation transforms weak signals into strong evidence
- All output is structured, traceable, and explainable

### Backend API (ASP.NET Core) — Planned

- Accepts scan uploads
- Stores scans, findings, and evidence metadata in PostgreSQL
- Serves rule packs to scanners
- Provides data endpoints for the dashboard

### Dashboard (Next.js / React) — Planned

- Scan list and detail views
- Findings table with filtering/sorting
- Evidence drilldown
- Search and investigation tools

---

## Scanner Pipeline Detail

### 1. Collection

Each collector implements `ICollector` and produces `RawArtifact` objects. Collectors are isolated and failure-tolerant — if one collector crashes, the rest continue.

**Current collectors:**
- **PrefetchCollector** — Parses Windows Prefetch (.pf) files for execution history
- **AmcacheCollector** — Reads Amcache registry for application execution records
- **TempFilesCollector** — Scans temp directories for suspicious files
- **RecentFilesCollector** — Collects Windows Recent Items (.lnk shortcuts)
- **BrowserDownloadsCollector** — Parses Chromium browser download history

### 2. Normalization

The `ArtifactNormalizer` converts all `RawArtifact` objects into `NormalizedArtifact` objects with a uniform schema. This ensures the rule engine doesn't need collector-specific logic.

Normalization also includes automatic tag enrichment based on file properties (extension, location, metadata).

### 3. Rule Evaluation

The `RuleEngine` loads rules from JSON files and dispatches each rule to the appropriate `IRuleEvaluator`:

- **ExactMatchEvaluator** — String equality
- **ContainsEvaluator** — Substring matching
- **RegexEvaluator** — Pattern matching (with timeout protection)
- **HashEvaluator** — SHA256 IOC matching
- **PathEvaluator** — Directory location matching
- **FilenameEvaluator** — File name matching

Field resolution is handled by `FieldResolver`, which maps rule field names to artifact properties, including dynamic `metadata.*` fields.

### 4. Correlation

The `CorrelationEngine` processes findings in two ways:

1. **Explicit correlation rules** — Rules with `"type": "correlation"` and a `"requires"` list. All required rules must have produced findings for the correlation to trigger.

2. **Automatic entity correlation** — When 3+ different rules flag the same file path, a composite finding is auto-generated with boosted confidence.

### 5. Evidence Packaging

The `EvidencePackager` produces:
- `scan-result.json` — Complete scan output
- `findings.json` — Findings-only extract
- `statistics.json` — Scan metrics
- `evidence-manifest.json` — List of all referenced evidence
- Optional ZIP bundle containing all of the above plus the actual evidence files

---

## Data Flow

```
Windows System
    │
    ▼
┌──────────────────────────────────────┐
│  Collectors (Prefetch, Amcache, ...) │
│  Output: List<RawArtifact>           │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  ArtifactNormalizer                  │
│  Output: List<NormalizedArtifact>    │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  RuleLoader → RuleEngine             │
│  Input: Rules (JSON) + Artifacts     │
│  Output: List<Finding>               │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  CorrelationEngine                   │
│  Input: Findings + Correlation Rules │
│  Output: List<Finding> (additional)  │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  EvidencePackager                    │
│  Output: JSON files + ZIP bundle     │
└──────────────────────────────────────┘
```

---

## Rule Format

Rules are versioned, external, and testable. See `rules/fivem/fivem-rules.json` for examples.

A rule specifies:
- **What** to look for (`type` + `field` + `value`)
- **How serious** it is (`severity` + `confidence`)
- **Context** (`tags`, `description`, `author`)

Correlation rules additionally specify:
- **Which other rules** must match (`requires`)
- **How much to boost** confidence (`confidenceBoost`)

---

## Extensibility

Adding a new collector:
1. Create a class implementing `ICollector`
2. Return `RawArtifact` objects from `Collect()`
3. Register in `Program.cs` / `BuildPipeline()`

Adding a new rule type:
1. Create a class implementing `IRuleEvaluator`
2. Register in `RuleEngine` constructor

Adding new detection logic:
1. Write JSON rules in `rules/`
2. No code changes needed
