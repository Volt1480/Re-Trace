# Re:Trace – Rule Format Documentation

## Overview

Rules are the core detection mechanism in Re:Trace. They are external JSON files that define what the scanner looks for and how to classify matches. Rules can be created, versioned, and tested without modifying scanner code.

## File Location

Rules are stored in the `rules/` directory and loaded recursively. Each `.json` file must contain a JSON array of rule objects.

```
rules/
├── fivem/
│   └── fivem-rules.json
├── generic/
│   └── generic-suspicion.json      (example)
└── custom/
    └── my-server-rules.json        (example)
```

## Rule Schema

### Standard Rule

```json
{
  "id": "RULE-001",
  "type": "contains",
  "field": "path",
  "value": "injector",
  "severity": "high",
  "confidence": 0.5,
  "title": "Suspicious injector path",
  "description": "File path contains the keyword 'injector'.",
  "tags": ["fivem", "injector"],
  "author": "your-name",
  "version": "1.0.0"
}
```

### Multiple Values

Use `values` (array) instead of `value` (string) to match any of several patterns:

```json
{
  "id": "RULE-002",
  "type": "contains",
  "field": "fileName",
  "values": ["eulen", "redengine", "skript.gg"],
  "severity": "critical",
  "confidence": 0.85,
  "title": "Known cheat tool name",
  "description": "File name matches a known cheat tool."
}
```

### Correlation Rule

```json
{
  "id": "CORR-001",
  "type": "correlation",
  "field": "_",
  "severity": "critical",
  "confidence": 0.9,
  "title": "Loader + Injector chain",
  "description": "Both loader and injector artifacts found.",
  "requires": ["RULE-001", "RULE-002"],
  "confidenceBoost": 0.3,
  "minimumMatches": 2
}
```

## Field Reference

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique rule identifier (e.g., `FIVEM-001`) |
| `type` | string | Rule type (see below) |
| `field` | string | Which artifact field to match against |
| `severity` | string | `info`, `low`, `medium`, `high`, `critical` |
| `title` | string | Human-readable title shown in findings |

### Optional Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `value` | string | null | Single match value |
| `values` | string[] | null | Multiple match values (any = hit) |
| `confidence` | number | 0.5 | Base confidence score (0.0 to 1.0) |
| `description` | string | "" | Detailed description for finding summary |
| `tags` | string[] | [] | Tags for filtering and categorization |
| `author` | string | null | Who wrote this rule |
| `version` | string | null | Rule version |

### Correlation-Only Fields

| Field | Type | Description |
|-------|------|-------------|
| `requires` | string[] | Rule IDs that must all have findings |
| `minimumMatches` | int | Minimum total findings across required rules |
| `confidenceBoost` | number | Added to the highest contributing confidence |

## Rule Types

### `exact`
Matches when the field value exactly equals the rule value (case-insensitive).

### `contains`
Matches when the field value contains the rule value as a substring (case-insensitive).

### `regex`
Matches when the field value matches the regex pattern. Uses `IgnoreCase` flag. Has a 2-second timeout to prevent ReDoS.

### `hash`
Matches when the artifact's SHA256 hash matches the rule value. Case-insensitive hex comparison.

### `path`
Matches when the artifact's directory path contains the rule value.

### `filename`
Matches when the artifact's file name contains the rule value.

### `correlation`
Does not match individual artifacts. Instead, it checks whether other rules (listed in `requires`) have produced findings. If all required rules matched, a composite finding is created with boosted confidence.

## Matchable Fields

These field names can be used in the `field` property:

| Field Name | Source |
|------------|--------|
| `path` / `filePath` | Full file path |
| `fileName` | File name only |
| `extension` / `fileExtension` | File extension (e.g., `.exe`) |
| `directory` / `directoryPath` | Directory containing the file |
| `hash` / `sha256` | SHA256 hash of the file |
| `source` | Collector name |
| `artifactType` / `type` | Artifact type |
| `executableName` | Executable name (from Prefetch/Amcache) |
| `runCount` | Run count (from Prefetch) |
| `fileSize` / `fileSizeBytes` | File size in bytes |
| `metadata.*` | Any metadata key (e.g., `metadata.browser`) |

## Confidence Scoring

- Base confidence comes from the rule definition (0.0 to 1.0)
- Correlation rules boost confidence via `confidenceBoost`
- Auto-correlation adds +0.15 when 3+ rules flag the same file
- Maximum confidence is capped at 1.0

### Guidelines

| Confidence | Meaning |
|------------|---------|
| 0.0–0.2 | Weak signal, informational |
| 0.2–0.5 | Suspicious, needs corroboration |
| 0.5–0.7 | Moderate confidence |
| 0.7–0.9 | Strong indicator |
| 0.9–1.0 | Very high confidence (usually correlated) |

## Tips for Writing Rules

1. Start with low confidence and let correlation boost it
2. Use `tags` consistently for filtering in the dashboard
3. Prefer `contains` over `regex` when possible (faster)
4. Use `values` arrays instead of multiple similar rules
5. Write clear `description` fields — they appear in findings
6. Version your rules so changes can be tracked
7. Test with `--verbose` to see which rules match
