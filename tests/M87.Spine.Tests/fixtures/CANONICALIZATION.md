# Receipt Canonicalization Rules — Spine Lite v0.1.0 (Python source of truth)

This document specifies the receipt schema, hash algorithm, and serialization
rules that the .NET implementation must match for cross-language interop.

**Source of truth:** `MacFall7/M87-Spine-lite/hooks/receipts.py` function
`_compute_receipt_hash` (lines 131-142).

**Source of truth schema:** `MacFall7/M87-Spine-lite/schemas/receipt.schema.json`

If this document conflicts with the Python source, the Python source wins.
Update this document; do not amend the Python.

---

## 1. Receipt Schema (v1.0.0, frozen)

The full schema lives at `schemas/receipt.schema.json` in the Python repo.
Reproduced here for reference; the JSON Schema file is canonical.

### Required top-level fields (12)

| Field                     | Type                  | Notes |
|---------------------------|-----------------------|-------|
| `receipt_id`              | string (UUID v4)      | Per-receipt unique ID |
| `session_id`              | string                | Stable across receipts in one session |
| `proposal_id`             | string                | Caller-supplied; demo uses `demo-1`, `demo-2`, ... |
| `sequence_number`         | integer (>=1)         | 1-indexed, monotonic within session |
| `timestamp`               | string (ISO 8601 UTC) | Format: `2026-04-30T22:57:32.612603+00:00` |
| `executor`                | object                | Nested; see §1.1 |
| `action`                  | object                | Nested; see §1.2 |
| `result`                  | object                | Nested; see §1.3 |
| `budget_snapshot`         | object                | Nested; see §1.4 |
| `git_context`             | object                | Nested; see §1.5 |
| `previous_receipt_hash`   | string OR null        | null on genesis (sequence 1) |
| `receipt_hash`            | string (SHA-256 hex)  | 64-char lowercase hex |

### 1.1 `executor` object (3 fields)
- `type` (string) — e.g. `"claude_code"`
- `model` (string) — e.g. `"unknown"` if not provided
- `instance_id` (string, UUID) — fresh UUID if not supplied

### 1.2 `action` object (8 fields)
- `tool` (string) — e.g. `"shell"`, `"file"`
- `operation` (string) — e.g. `"command"`, `"file_write"`
- `effect_class` (string) — see §1.6 for the 6-class enumeration
- `risk_delta` (number) — 0.0 for safe operations; non-zero for blocked/risky
- `description` (string)
- `command` (string OR null)
- `target_paths` (array of strings)
- `reversibility` (string) — e.g. `"REVERSIBLE"`, `"IRREVERSIBLE"`

### 1.3 `result` object (8 fields)
- `status` (string, enum: `"success"`, `"blocked"`, `"failed"`)
- `exit_code` (integer OR null)
- `blocked_by` (string OR null)
- `diff_hash` (string OR null)
- `files_created` (array of strings)
- `files_modified` (array of strings)
- `files_deleted` (array of strings)
- `stdout_truncated` (string OR null) — capped at 2000 chars

### 1.4 `budget_snapshot` object (8 fields)
- `steps_used` (integer >= 0)
- `steps_remaining` (integer >= 0)
- `commands_used` (integer >= 0)
- `writes_used` (integer >= 0)
- `files_touched` (integer >= 0)
- `runtime_elapsed_seconds` (number >= 0) — rounded to 2 decimal places
- `session_risk_score` (number >= 0) — rounded to 4 decimal places
- `current_posture` (string) — e.g. `"NORMAL"`, `"ELEVATED"`, `"LOCKDOWN"`

### 1.5 `git_context` object (3 fields)
- `branch` (string)
- `commit_before` (string OR null) — 40-char SHA-1 hex
- `commit_after` (string OR null) — 40-char SHA-1 hex

### 1.6 Spine Lite Effect Classes (Python canonical strings)

The Python implementation uses the following effect_class strings. The .NET
port's enum values must serialize to and deserialize from these exact strings:

| Python string         | Spine Lite v0.1.0 meaning                        |
|-----------------------|--------------------------------------------------|
| `SHELL_SAFE`          | Read-only shell commands (git status, ls, cat)   |
| `SHELL_MUTATING`      | State-changing shell (git add, mkdir, cp)        |
| `SHELL_DANGEROUS`     | Destructive (rm -rf, sudo, chmod)                |
| `NETWORK_ATTEMPT`     | Outbound network (curl, wget, pip install)       |
| `SCOPED_WRITE`        | Allowed file writes within repo boundary         |
| `RESTRICTED_WRITE`    | Blocked writes (.env, secrets, credentials)      |

**.NET port note:** BLUEPRINT.md §3.2 specified a different 6-class taxonomy
(Read, Write, Transmit, DestructiveIrreversible, PrivilegeEscalation, Unknown).
That taxonomy is an abstraction that maps onto these. For receipt interop, the
.NET emitter MUST write the Python-canonical strings above. Internal C# enum
naming may differ; serialization MUST match.

Mapping recommendation for the .NET port:

| .NET enum                  | Serialized string    |
|----------------------------|----------------------|
| `EffectClass.Read`         | `SHELL_SAFE`         |
| `EffectClass.Write`        | `SCOPED_WRITE`       |
| `EffectClass.Transmit`     | `NETWORK_ATTEMPT`    |
| `EffectClass.DestructiveIrreversible` | `SHELL_DANGEROUS` |
| `EffectClass.PrivilegeEscalation`     | `SHELL_DANGEROUS` (or extend) |
| `EffectClass.Unknown`      | `SHELL_DANGEROUS` (fail-closed) |
| (additional)               | `SHELL_MUTATING`     |
| (additional)               | `RESTRICTED_WRITE`   |

`SHELL_MUTATING` and `RESTRICTED_WRITE` exist in Python and have no clean
1:1 in the BLUEPRINT taxonomy. Two choices: extend the .NET enum to 8
classes, or treat the receipt schema's `effect_class` field as a free
string that the .NET enum maps into. Recommendation: extend the enum to
match the Python set exactly. Parity is the proof.

---

## 2. Hash Algorithm

**Algorithm:** SHA-256, lowercase hex output (64 characters).

**Hash input construction** (verbatim from `_compute_receipt_hash`):

```python
def _compute_receipt_hash(receipt_data: dict) -> str:
    hashable = {
        k: v for k, v in receipt_data.items()
        if k not in ("receipt_hash", "previous_receipt_hash")
    }
    canonical = json.dumps(hashable, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode()).hexdigest()
```

### Critical canonicalization rules

1. **Exclude two fields from the hash input:** `receipt_hash` and
   `previous_receipt_hash`. (Both are circular dependencies — the receipt's
   hash cannot depend on itself, and the previous hash is chain metadata,
   not content.)

2. **Sort keys alphabetically at every level.** Python's `json.dumps` with
   `sort_keys=True` sorts recursively. The .NET port must do the same.

3. **No whitespace.** Separators are exactly `(",", ":")` — no spaces.
   `{"a":1,"b":2}` not `{"a": 1, "b": 2}`.

4. **UTF-8 encoding** of the canonical string before SHA-256.

5. **No trailing newline** in the hashable input.

6. **Numbers:** Python serializes floats and ints distinctly. `0.0` is
   `"0.0"` in canonical JSON; `0` is `"0"`. The .NET port must preserve this.
   `risk_delta: 0.0` (a float) in Python produces `"risk_delta":0.0` in
   canonical form. Do not let `System.Text.Json` collapse `0.0` to `0`.

7. **Null fields are present in the hash input.** A receipt with
   `"command": null` includes `"command":null` in the hashed string, NOT
   omitted. Python's `json.dumps` does this by default. Confirm
   `System.Text.Json` is configured to write nulls (`DefaultIgnoreCondition.Never`).

### Genesis receipt

The first receipt in a chain (sequence_number=1) has
`previous_receipt_hash: null` (literal JSON null, not the string "null", not
omitted). After the chain forms, every subsequent receipt's
`previous_receipt_hash` equals the prior receipt's `receipt_hash`.

---

## 3. Persistence Format

### Python emitter on-disk format

The Python implementation writes one JSON file per receipt, indented:

```
governance/receipts/<session_id>/0001_<receipt_uuid>.json
governance/receipts/<session_id>/0002_<receipt_uuid>.json
...
```

Each file contains a single receipt object, pretty-printed with
`json.dumps(receipt, indent=2)`.

### Fixture format for .NET interop testing

`spine_lite_receipt_sample.jsonl` is a JSONL convenience fixture:
each line is one receipt, sorted-keys, no whitespace
(`json.dumps(r, sort_keys=True, separators=(",", ":"))`).

Both formats parse to identical receipt objects. The .NET interop test
should:

1. Read `spine_lite_receipt_sample.jsonl` line-by-line
2. Deserialize each line into a `Receipt` record
3. Verify the chain end-to-end:
   - `receipts[0].previous_receipt_hash == null`
   - For i in [1..n-1]: `receipts[i].previous_receipt_hash == receipts[i-1].receipt_hash`
   - For i in [0..n-1]: `compute_hash(receipts[i]) == receipts[i].receipt_hash`

For `compute_hash`, replicate §2 exactly. The .NET test passes if and only
if all three conditions hold across all 5 fixture receipts.

---

## 4. Fixture Provenance

The fixture file in `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl`
was generated by running:

```bash
cd M87-Spine-lite/
python3 scripts/demo.py
# Output: governance/receipts/<session_id>/000{1..5}_*.json
# Concatenated to JSONL with sort_keys=True, separators=(",",":")
```

5 receipts: 2 APPROVE (`SHELL_SAFE`, `SCOPED_WRITE`), 3 DENY
(`NETWORK_ATTEMPT` x2, `SHELL_DANGEROUS` x1). Posture transitions
NORMAL → ELEVATED. Chain verified by independent hasher.

**Do not regenerate this file in the .NET build session.** It is a known-good
fixture from authentic Python output. Regenerating it would mean the .NET test
is verifying its own answer key.

If the Python receipt schema or hash algorithm changes in a future Spine Lite
release, regenerate this fixture from that release and bump its provenance
header below.

```
Generated from: M87-Spine-lite v0.1.0 (commit hash: see Spine Lite repo)
Generated on:   2026-04-30
Receipt count:  5
Posture range:  NORMAL → ELEVATED
Chain verified: yes (independent SHA-256 verifier)
```
