# BLUEPRINT.md Patch — Receipt Schema Corrections

**Status:** Required amendments before v0.1.0 build session opens.
**Reason:** BLUEPRINT.md §3.2 and §3.4 were written before the Python source
of truth was inspected. The actual Spine Lite v0.1.0 receipt format diverges
from what was specified. Receipt interop is the load-bearing claim of the
package, so fixing this BEFORE the build session is non-optional.

This patch supersedes BLUEPRINT.md §3.2 and §3.4 in their entirety.

---

## §3.2 (corrected) — Effect Classes

The Python Spine Lite v0.1.0 emitter uses these 6 effect class strings in
the `action.effect_class` field of receipts:

| String              | Meaning                                       |
|---------------------|-----------------------------------------------|
| `SHELL_SAFE`        | Read-only shell (git status, ls, cat)         |
| `SHELL_MUTATING`    | State-changing shell (git add, mkdir, cp)     |
| `SHELL_DANGEROUS`   | Destructive (rm -rf, sudo, chmod)             |
| `NETWORK_ATTEMPT`   | Outbound network (curl, wget, pip install)    |
| `SCOPED_WRITE`      | Allowed file writes within repo boundary      |
| `RESTRICTED_WRITE`  | Blocked writes (.env, secrets, credentials)   |

The .NET port MUST serialize/deserialize these exact strings for receipt
interop. C# enum naming is independent; the wire format is fixed.

Recommended C# enum (8 values to cover all Python states plus an Unknown
fail-closed default):

```csharp
public enum EffectClass
{
    ShellSafe,         // -> "SHELL_SAFE"
    ShellMutating,     // -> "SHELL_MUTATING"
    ShellDangerous,    // -> "SHELL_DANGEROUS"
    NetworkAttempt,    // -> "NETWORK_ATTEMPT"
    ScopedWrite,       // -> "SCOPED_WRITE"
    RestrictedWrite,   // -> "RESTRICTED_WRITE"
    Unknown            // never serialized; classifier output that triggers DENY
}
```

Use `JsonStringEnumConverter` with a custom naming policy, OR define the
enum members with `[JsonStringEnumMemberName]` (or `[EnumMember(Value=...)]`
on .NET 8+) to map enum members to the Python-canonical SCREAMING_SNAKE_CASE
strings. Verify via test 22 (round-trip a Python receipt through the .NET
deserializer and back through the .NET hasher).

---

## §3.4 (corrected) — Receipt Schema (v1.0.0, frozen, full Python parity)

The Python source schema lives at `MacFall7/M87-Spine-lite/schemas/receipt.schema.json`.
The full canonical receipt has 12 top-level fields, several of which are
nested objects. The v0.1.0 .NET port MUST produce receipts that pass the
Python schema and that produce identical `receipt_hash` values when run
through the canonicalization in §2 of `CANONICALIZATION.md`.

### Top-level structure

```json
{
  "receipt_id": "uuid-v4",
  "session_id": "string",
  "proposal_id": "string",
  "sequence_number": 1,
  "timestamp": "2026-04-30T22:57:32.612603+00:00",
  "executor": { "type": "...", "model": "...", "instance_id": "..." },
  "action": {
    "tool": "...",
    "operation": "...",
    "effect_class": "SHELL_SAFE",
    "risk_delta": 0.0,
    "description": "...",
    "command": "git status",
    "target_paths": [],
    "reversibility": "REVERSIBLE"
  },
  "result": {
    "status": "success",
    "exit_code": 0,
    "blocked_by": null,
    "diff_hash": null,
    "files_created": [],
    "files_modified": [],
    "files_deleted": [],
    "stdout_truncated": null
  },
  "budget_snapshot": {
    "steps_used": 1,
    "steps_remaining": 19,
    "commands_used": 1,
    "writes_used": 0,
    "files_touched": 0,
    "runtime_elapsed_seconds": 0.0,
    "session_risk_score": 0.0,
    "current_posture": "NORMAL"
  },
  "git_context": {
    "branch": "master",
    "commit_before": "<sha1>",
    "commit_after": null
  },
  "previous_receipt_hash": null,
  "receipt_hash": "<sha256 hex>"
}
```

Field type rules: see `CANONICALIZATION.md` §1.

### Hash computation

See `CANONICALIZATION.md` §2. Verbatim Python:

```python
hashable = {k: v for k, v in receipt.items()
            if k not in ("receipt_hash", "previous_receipt_hash")}
canonical = json.dumps(hashable, sort_keys=True, separators=(",", ":"))
return hashlib.sha256(canonical.encode()).hexdigest()
```

The .NET port must replicate this byte-for-byte. Test 22 passes only if
a Python-emitted receipt's hash is identical to the .NET re-computation.

### Persistence

Python writes one indented JSON file per receipt. The .NET port may use
JSONL (line-delimited), individual files, or both — but the `effect_class`
strings, key sort order at hash time, and field set must match exactly,
or chain verification breaks.

For test 22, read the JSONL fixture in `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl`
line by line; each line is one full Python receipt.

---

## Test plan amendments (BLUEPRINT.md §6 patch)

Test 23 should read: ".NET-emitted receipt round-trips through Python
canonical hasher AND validates against `receipt.schema.json`." This means
the .NET test must produce a sample receipt, serialize it canonically (per
§2), and confirm:
  1. The receipt deserializes via `System.Text.Json` losslessly
  2. The canonical JSON string matches the byte sequence Python would produce
     for the same object
  3. The schema-validate against `receipt.schema.json` (use a .NET JSON Schema
     library — `Json.Schema` from JsonEverything.net is the cleanest pick; one
     extra dev-time dependency is acceptable for tests, not for runtime)

---

## Acknowledgment

This patch resolves a divergence between BLUEPRINT.md (the spec) and the
Python source of truth. The Python source wins. The blueprint will be
updated to match before the .NET v0.1.0 ships, but for the build session,
treat this patch as the authoritative §3.2 / §3.4 / §6 (test 23) override.
