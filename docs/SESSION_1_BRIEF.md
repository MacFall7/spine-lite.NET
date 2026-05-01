# spine-lite.NET v0.1.0 — Session 1 Build Brief

**For:** the Claude Code session opening against this repo.
**Goal:** scaffold + implement + 23/23 green + NuGet-ready package, in one session.
**Source of truth ordering:** Python source > BLUEPRINT_PATCH_receipt_schema.md > CANONICALIZATION.md > BLUEPRINT.md (§3.2 and §3.4 of BLUEPRINT.md are SUPERSEDED). Where any of the above conflicts with CLAUDE.md governance, CLAUDE.md wins.

---

## Pre-flight already done (do not redo, but confirm in your first response)

- `Microsoft.SemanticKernel` pinned version: **1.75.0** (prefix-reserved official Microsoft package, targets `.NET 8.0`).
- `IFunctionInvocationFilter` contract verified to match BLUEPRINT.md §3.1: `Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)`.
- Veto mechanism: throw `GovernanceVetoException` after emitting DENY receipt. Do not call `next(context)` on DENY.
- `IAutoFunctionInvocationFilter` exists separately. v0.1.0 sample host does not use auto-function-calling, so register only `IFunctionInvocationFilter`. Note this in README.

---

## Critical: BLUEPRINT.md sections that are SUPERSEDED

- **BLUEPRINT.md §3.2 (effect classes) is superseded.** The original taxonomy (`Read, Write, Transmit, DestructiveIrreversible, PrivilegeEscalation, Unknown`) is wrong for receipt interop. Canonical strings, from BLUEPRINT_PATCH_receipt_schema.md and CANONICALIZATION.md §1.6:
  - `SHELL_SAFE`
  - `SHELL_MUTATING`
  - `SHELL_DANGEROUS`
  - `NETWORK_ATTEMPT`
  - `SCOPED_WRITE`
  - `RESTRICTED_WRITE`

  The .NET enum naming may differ; **the wire format is fixed**. Mapping recommendation in CANONICALIZATION.md §1.6.

- **BLUEPRINT.md §3.4 (receipt schema) is superseded.** The canonical receipt has 12 top-level fields including nested `executor`, `action`, `result`, `budget_snapshot`, `git_context` objects. See BLUEPRINT_PATCH_receipt_schema.md §3.4 (corrected) and `tests/M87.Spine.Tests/fixtures/receipt.schema.json` for the schema, plus `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl` for 5 real fixture receipts.

---

## Order of operations

### 1. Read all six required documents verbatim before writing any code

- `CLAUDE.md` — repo governance, line budget 150
- `BLUEPRINT.md` — architecture (§3.2 and §3.4 superseded — read for context only)
- `BLUEPRINT_PATCH_receipt_schema.md` — canonical effect classes + canonical receipt schema (load-bearing)
- `tests/M87.Spine.Tests/fixtures/CANONICALIZATION.md` — hash algorithm spec (byte-load-bearing)
- `tests/M87.Spine.Tests/fixtures/receipt.schema.json` — JSON Schema for the canonical receipt (12 required top-level fields)
- `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl` — 5 real Python receipts (do not regenerate; this is the test answer key)

In your first response, confirm:
- (a) the seven invariants from CLAUDE.md / BLUEPRINT.md §2
- (b) the 23-test ship gate from BLUEPRINT.md §6 (with the §6 patch from BLUEPRINT_PATCH for test 23)
- (c) the canonical effect-class strings are SHELL_SAFE / SHELL_MUTATING / SHELL_DANGEROUS / NETWORK_ATTEMPT / SCOPED_WRITE / RESTRICTED_WRITE — not the original BLUEPRINT taxonomy
- (d) the canonical receipt has 12 top-level fields including nested action / result / budget_snapshot / executor / git_context objects
- (e) the hash algorithm is SHA-256 over JSON canonicalized with `sort_keys=True, separators=(",",":")`, with `receipt_hash` and `previous_receipt_hash` excluded from the hash input

Do not write production code in your first response. Propose your session 1 plan.

### 2. Scaffold the project (BLUEPRINT.md §4)

Use dotnet CLI:
- `dotnet new sln -n M87.Spine`
- `dotnet new classlib -o src/M87.Spine -f net8.0`
- `dotnet new xunit -o tests/M87.Spine.Tests -f net8.0`
- `dotnet sln add src/M87.Spine/M87.Spine.csproj tests/M87.Spine.Tests/M87.Spine.Tests.csproj`
- `dotnet add src/M87.Spine/M87.Spine.csproj package Microsoft.SemanticKernel --version 1.75.0`
- `dotnet add tests/M87.Spine.Tests/M87.Spine.Tests.csproj reference src/M87.Spine/M87.Spine.csproj`

### 3. Implement Models/ first

Record types for `Manifest`, `ManifestEntry`, `Receipt`, `EffectClass`, `Posture`, `Decision`. These are the contract surfaces. Get them right before any logic.

`Receipt` must serialize to the canonical 12-field structure with nested objects. Use `JsonStringEnumConverter` with a custom naming policy (or `[JsonStringEnumMemberName]` / `[EnumMember(Value=...)]`) so `EffectClass` enum members serialize to the SCREAMING_SNAKE_CASE wire strings.

### 4. Implement the four services as separate classes (authority-separation invariant)

- `ManifestGate` — load + verify hash + lookup
- `EffectClassifier` — 6-class classification, parameter inspection, fail-closed Unknown
- `PolicyEvaluator` — posture-aware allow/deny decision (NORMAL / ELEVATED / LOCKDOWN)
- `ReceiptEmitter` — append-only JSONL, SHA-256 chained per CANONICALIZATION.md §2

No god-class. No single function that classifies and evaluates and logs.

### 5. Implement SpineFilter as IFunctionInvocationFilter

Wire the four services in the pipeline order from BLUEPRINT.md §3.1. Implement `GovernanceVetoException` (single class, derives from `Exception`, immutable).

### 6. Write tests in BLUEPRINT.md §6 order (1-23)

Run `dotnet test` after each test class lands. Do not move on if any test fails. No skips. No `[Trait("Category", "Skip")]`.

### 7. Sample host (BLUEPRINT.md §7)

30-line `Program.cs` in `samples/BasicSemanticKernelHost/`. Verify visible receipt chain output.

### 8. CI (BLUEPRINT.md §9)

`.github/workflows/ci.yml`. `dotnet build` + `dotnet test` matrix on `ubuntu-latest` and `windows-latest`. PR-blocking on test failure.

### 9. README (BLUEPRINT.md §10)

Wedge sentence verbatim from BLUEPRINT.md §0. No marketing copy. No emoji.

### 10. Final ship checklist

Every box in BLUEPRINT.md §11 must check before declaring done.

---

## Hard constraints (BLUEPRINT.md §5.1)

- No reflection in the hot path. Dictionary lookup only.
- No async-over-sync.
- No logging dependency in runtime. Receipts ARE the audit trail. Tests may use `ITestOutputHelper`.
- No swallowed exceptions. Any exception in classifier or evaluator emits DENY receipt and throws `GovernanceVetoException`.
- No partial receipts. Receipt write completes before invocation proceeds (APPROVE) or exception throws (DENY). If receipt write fails, fail-closed and throw.
- No reading function descriptions for authorization decisions. Authority lives in the manifest.
- No TODO, FIXME, or NotImplementedException in `src/` at ship time.
- Threading: filter must be safe to register as singleton. `ReceiptEmitter` serializes writes via `SemaphoreSlim`. `ManifestGate` is read-only after load. `EffectClassifier` and `PolicyEvaluator` are stateless.

---

## Receipt interop (BLUEPRINT_PATCH_receipt_schema.md §3.4 + CANONICALIZATION.md + tests 22-23)

The fixture file `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl` is real Spine Lite Python output (5 receipts, NORMAL → ELEVATED posture transition, mix of APPROVE and DENY). Do not regenerate.

### Test 22 — fixture chain verification

Read the fixture line-by-line, deserialize each line into a .NET `Receipt` record, then verify:
- `receipts[0].previous_receipt_hash == null` (genesis)
- For i in `[1..n-1]`: `receipts[i].previous_receipt_hash == receipts[i-1].receipt_hash` (chain link)
- For every i: `compute_hash(receipts[i]) == receipts[i].receipt_hash` (self-hash)

All three conditions must hold across all 5 fixture receipts.

### Test 23 — round-trip parity

Generate a .NET receipt, serialize via the canonical hash function, validate against `receipt.schema.json`. Recommended schema-validate library: `JsonSchema.Net` from JsonEverything.net (test-time only; not a runtime dependency).

### The seven canonicalization rules (CANONICALIZATION.md §2)

These are byte-load-bearing. Get them wrong and tests 22 + 23 fail.

1. **Exclude `receipt_hash` and `previous_receipt_hash` from hash input.** Both are circular / chain-meta, not content.
2. **Sort keys alphabetically at every level.** Recursive. `System.Text.Json` does NOT sort by default — implement a canonicalizer that walks the object tree and emits sorted-key JSON.
3. **No whitespace.** Separators are exactly `,` and `:` — no spaces. `{"a":1,"b":2}` not `{"a": 1, "b": 2}`.
4. **UTF-8 encoding** of the canonical string before SHA-256.
5. **No trailing newline** in the hashable input.
6. **`0.0` ≠ `0`.** Python preserves float vs int. `risk_delta: 0.0` (float) → `"risk_delta":0.0` in canonical form. Do not let `System.Text.Json` collapse `0.0` to `0`. This is the single most likely interop break.
7. **Null fields are present in the hash input.** `"command":null` is in the hashed string, not omitted. Configure `System.Text.Json` with `DefaultIgnoreCondition.Never`.

Genesis receipt has `previous_receipt_hash: null` (literal JSON null, not the string `"null"`, not omitted).

---

## Ship gate (BLUEPRINT.md §11 with patches)

Every item must check before declaring done:

- [ ] All 23 tests pass on `dotnet test` (no skips)
- [ ] `dotnet pack -c Release` produces `M87.Spine.0.1.0.nupkg` with no warnings
- [ ] Sample host runs end-to-end, produces visible receipt chain in console and JSONL file
- [ ] CI green on the push that lands the v0.1.0 work
- [ ] README wedge sentence matches BLUEPRINT.md §0 verbatim
- [ ] CLAUDE.md present at repo root, line budget verified at or below 150
- [ ] LICENSE file (MIT) present (committed pre-session)
- [ ] One Spine Lite Python receipt successfully verified by the .NET interop test (test 22)
- [ ] No TODO, FIXME, or NotImplementedException in `src/`
- [ ] Receipt schema and manifest schema both marked `"version": "1.0.0"` with x-frozen semantics documented
- [ ] Annotated tag `v0.1.0` created locally with `git tag -a v0.1.0 -m "spine-lite.NET v0.1.0 — Semantic Kernel governance filter, .NET 8, MIT, 23/23 green"` and pushed via `git push origin v0.1.0`

**DO NOT auto-flip repo visibility.** Per CLAUDE.md "Stop and ask before destructive/shared-state operations" — surface the visibility flip for explicit Mac approval before running `gh repo edit MacFall7/spine-lite.NET --visibility public --accept-visibility-change-consequences`. Print the exact command and wait. Public is a one-way state change with shared-state implications.

---

## Out of scope for v0.1.0 (BLUEPRINT.md §12)

- Distributed gate / Ed25519 signing → Spine Pro / `M87.Spine.Pro` package
- PAR tracking over time → Spine Pro
- Cross-session memory → Spine Pro
- Custom UI / dashboard → not a v0.1 concern
- Azure-specific integration (Function Apps, AKS) → community contribution welcome, not core
- AutoGen .NET integration → separate adapter package, post-v0.1
- OpenTelemetry exporters → receipts are the audit trail; OTel is a v0.2+ ergonomic addition
- NuGet auto-publish → manual for v0.1.0; automate after first user

If you encounter a need that maps to any of the above, surface it and stop. Do not silently expand scope.

---

## End-of-Session Wrap Report (REQUIRED)

Before closing this build session, output a final report block in this exact format:

```
=== SPINE LITE .NET — SESSION 1 WRAP ===
1. Public repo URL (post-visibility-flip): <url>
2. Annotated tag pushed: <tag>
3. dotnet test result: <green>/<total> (e.g., 23/23)
4. Deviations from BLUEPRINT estimate: <none | list scope cuts, extra modules, surprises>
5. Visibility flip: <approved by user Y/N> | <executed Y/N>
=== END WRAP ===
```

This block is the sole handoff artifact between this build session and the post-build Cowork session. Do not bury it — emit it as the last thing before exit. If the visibility flip was not approved during this session, leave it as `N | N` and note the blocker.
