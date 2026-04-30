# Spine for .NET — v0.1.0 Claude Code Handoff Blueprint

**Owner:** M87 Studio LLC
**Target:** MacFall7/spine-lite.NET (MIT, currently private)
**Parity target:** Spine Lite v0.1.0 (Feb 16, 2026) — local enforcement, single-process, manifest-gated, receipt-chained
**Session envelope:** One Claude Code session, ~120 messages, scaffold to working filter to 23-test invariant suite green to NuGet-ready package

---

## 0. Strategic Frame (read before building)

This is a port, not a new product. The architecture is locked. The job is to translate the Spine Lite enforcement model from the Claude Code PreToolUse hook surface into the Microsoft Semantic Kernel function-invocation pipeline, preserving every invariant.

**Wedge sentence (for README, NuGet description, LinkedIn announcement):**

> Spine Lite ports the authority-separation pattern into Semantic Kernel's function invocation pipeline. Six effect classes, fail-closed default, cryptographic receipt chain, enforced as an `IFunctionInvocationFilter` before the function executes.

**What this is not:** Spine Pro. No distributed gate, no Ed25519 signing across machines, no PAR tracking over time, no central authority service. v0.1.0 is single-process local enforcement. Cross-session and distributed coordination is a v0.2+ concern, and likely belongs in a separate `M87.Spine.Pro` package.

**Why .NET, why now:** Semantic Kernel adoption in regulated verticals (financial services, healthcare, government) is the credentialing surface for Spine Pro buyer conversations. This package is a category-positioning artifact first and a community-maintained reference port second. Build it accordingly: invariant-tight, doc-clear, dependency-minimal.

---

## 1. Repo Identity

```
Repo:    MacFall7/spine-lite.NET
License: MIT (matches Spine Lite)
Package: M87.Spine (NuGet)
Target:  net8.0 (LTS, current as of 2026-04)
Stack:   C# 12, Microsoft.SemanticKernel pinned at session 1, xUnit, no other runtime deps
Owner:   M87 Studio LLC
Role:    Governance enforcement filter for Semantic Kernel agent runtimes
```

---

## 2. Governance Constraints (Non-Negotiable)

These are the seven Founder Kernel invariants, restated for this repo. Every design decision in this session must preserve them.

1. **Proposal not equal to Execution.** The kernel proposes a function call. The filter intercepts before invocation. Approval is a separate code path, not the same call site.
2. **Authority Separation.** EffectClassifier, PolicyEvaluator, and ReceiptEmitter are distinct services. No god-class. No single function that classifies, evaluates, and logs.
3. **Fail-Closed Default.** Unknown function denies. Missing manifest entry denies. Classification failure denies. Exception during evaluation denies. Never throw NotImplementedException and proceed.
4. **Artifact-Backed Completion.** Every approved invocation emits a receipt. Every denied invocation emits a receipt. Receipts are SHA-256 chained. No invocation completes without a receipt.
5. **Structured Memory.** Receipt log is append-only JSONL. No in-memory-only state. Manifest is hashed at load and verified per invocation.
6. **Model Interchangeability.** No dependency on a specific LLM. The filter operates on Semantic Kernel function metadata, not on model output. Works equally with OpenAI, Azure OpenAI, Anthropic, Gemini, local models via SK connectors.
7. **Narrative not equal to Runtime.** Function descriptions in SK are advisory. They do not modify enforcement. Authority lives in the manifest, not in the function's Description attribute.

---

## 3. Architecture (v0.1.0)

### 3.1 Component Map

```
Semantic Kernel Function Invocation
            |
            v
   IFunctionInvocationFilter
   (M87.Spine.SpineFilter)
            |
            +--> ManifestGate ........ manifest.json hash check, function declared?
            |       |
            |       v (no) --> DENY (TOOL_NOT_IN_MANIFEST)
            |
            +--> EffectClassifier .... 6 effect classes, parameter inspection
            |       |
            |       v
            +--> PolicyEvaluator ..... posture-aware allow/deny decision
            |       |
            |       v
            +--> ReceiptEmitter ...... append-only JSONL, SHA-256 chained
                    |
                    v
            APPROVE --> invoke continues
            DENY    --> throw GovernanceVetoException
```

### 3.2 Six Effect Classes (parity with Spine Lite)

```csharp
public enum EffectClass
{
    Read,                    // SAFE_READ: filesystem reads, http GET on allowlisted hosts
    Write,                   // FILE_WRITE: filesystem writes within repo boundary
    Transmit,                // NETWORK_TRANSMIT: http POST/PUT/DELETE, outbound network
    DestructiveIrreversible, // IRREVERSIBLE_DESTRUCTIVE: rm, drop table, force push
    PrivilegeEscalation,     // PRIVILEGE_TRANSFER: chmod, sudo, role grants, key issuance
    Unknown                  // UNCLASSIFIED: fail-closed, treated as deny
}
```

Classification rules live in `EffectClassifier.cs`. Pattern: function name to metadata inspection to parameter inspection to most-restrictive-wins resolution. **Unknown classification is not an error state, it is a valid output, and policy denies it.**

### 3.3 Manifest Schema (v1.0.0, frozen)

```json
{
  "version": "1.0.0",
  "manifest_hash": "sha256:...",
  "posture": "NORMAL",
  "functions": [
    {
      "plugin": "FileSystemPlugin",
      "function": "ReadFile",
      "effect_class": "Read",
      "allowed": true,
      "max_calls_per_session": 100
    },
    {
      "plugin": "FileSystemPlugin",
      "function": "DeleteFile",
      "effect_class": "DestructiveIrreversible",
      "allowed": false,
      "reason": "Requires Spine Pro distributed approval"
    }
  ]
}
```

`manifest_hash` is computed at load and verified per invocation. `posture` accepts NORMAL, ELEVATED, or LOCKDOWN.

**Frozen schema.** Additive changes go to v1.1.0. Breaking changes go to v2.0.0. Receipts pin the manifest hash they were evaluated against.

### 3.4 Receipt Schema (v1.0.0, frozen, interop with Spine Lite)

```json
{
  "receipt_version": "1.0.0",
  "receipt_id": "uuid-v4",
  "timestamp_utc": "2026-04-30T...",
  "previous_receipt_sha": "sha256:...",
  "manifest_hash": "sha256:...",
  "plugin": "FileSystemPlugin",
  "function": "ReadFile",
  "effect_class": "Read",
  "decision": "APPROVE",
  "deny_reason": null,
  "parameter_hash": "sha256:...",
  "session_id": "uuid-v4"
}
```

`previous_receipt_sha` is null for the first receipt. `decision` is APPROVE or DENY. `deny_reason` is present only on DENY. `parameter_hash` is parameters serialized and hashed, not stored raw.

**Critical interop requirement:** receipt format must be byte-compatible with Spine Lite Python receipts. Cross-language verifier (the future m87-receipts tool) must validate both. Test this explicitly in the suite.

---

## 4. File Structure

```
spine-lite.NET/
  README.md                              # Wedge + quickstart + invariant statement
  LICENSE                                # MIT
  CLAUDE.md                              # Repo governance (already committed)
  BLUEPRINT.md                           # This file
  M87.Spine.sln
  src/
    M87.Spine/
      M87.Spine.csproj                   # net8.0, SK pinned
      SpineFilter.cs                     # IFunctionInvocationFilter entry point
      ManifestGate.cs                    # Load + verify + lookup
      EffectClassifier.cs                # 6-class classification
      PolicyEvaluator.cs                 # Posture-aware decision
      ReceiptEmitter.cs                  # Append-only JSONL, chained
      GovernanceVetoException.cs         # Thrown on DENY
      Models/
        EffectClass.cs
        Manifest.cs
        ManifestEntry.cs
        Receipt.cs
        Posture.cs
        Decision.cs
      Configuration/
        SpineOptions.cs                  # ManifestPath, ReceiptLogPath, Posture
  tests/
    M87.Spine.Tests/
      M87.Spine.Tests.csproj             # xUnit
      ManifestGateTests.cs               # 4 tests
      EffectClassifierTests.cs           # 6 tests (one per class)
      PolicyEvaluatorTests.cs            # 4 tests (posture matrix)
      ReceiptEmitterTests.cs             # 3 tests (chain integrity)
      SpineFilterIntegrationTests.cs     # 4 tests (end-to-end SK)
      ReceiptInteropTests.cs             # 2 tests (Spine Lite compat)
      fixtures/
        valid_manifest.json
        empty_manifest.json
        spine_lite_receipt_sample.jsonl  # DO NOT REGENERATE - real Spine Lite output
  samples/
    BasicSemanticKernelHost/
      BasicSemanticKernelHost.csproj
      Program.cs                         # 30-line minimal SK + Spine demo
      manifest.json
  .github/
    workflows/
      ci.yml                             # dotnet build + test on push
```

**23 tests total.** Same count as governance-sandbox's invariant suite. Not coincidence. Parity is the proof.

---

## 5. Implementation Constraints

### 5.1 Hard Rules

- **No reflection-based dynamic dispatch in the hot path.** Function lookup is dictionary-based, O(1).
- **No async-over-sync in the filter.** `IFunctionInvocationFilter.OnFunctionInvocationAsync` is async; stay async end-to-end.
- **No logging dependency.** No Serilog, no NLog, no `Microsoft.Extensions.Logging` in the runtime path. Receipts are the audit trail. Tests may use `ITestOutputHelper`.
- **No reading of function descriptions for authorization decisions.** Authority lives in the manifest. Descriptions are narrative.
- **No swallowed exceptions.** Any exception in classifier or evaluator emits DENY receipt and throws `GovernanceVetoException`.
- **No partial receipts.** Receipt write must complete before invocation proceeds (APPROVE) or exception is thrown (DENY). If receipt write fails, fail-closed and throw.

### 5.2 Soft Rules

- Prefer `record` types for `Manifest`, `ManifestEntry`, `Receipt`, `Decision`. Immutability matches the append-only model.
- Use `System.Text.Json` with source generation (`JsonSerializerContext`) for receipt and manifest serialization. AOT-friendly, no Newtonsoft.
- File I/O for receipts uses `FileStream` with `FileShare.Read` so external verifiers can tail the log without lock contention.

### 5.3 Threading

The filter must be safe to register as a singleton. `ReceiptEmitter` serializes writes via a `SemaphoreSlim`. `ManifestGate` is read-only after load. `EffectClassifier` and `PolicyEvaluator` are stateless.

---

## 6. Test Plan (23 tests, all green before ship)

### ManifestGateTests (4)
1. Loads valid manifest, computes hash, exposes lookup
2. Rejects manifest with missing version field (fail-closed)
3. Rejects manifest with hash mismatch on reload
4. Returns null for undeclared function (caller treats as DENY)

### EffectClassifierTests (6, one per effect class)
5. Classifies Read for known read-only function
6. Classifies Write for write function
7. Classifies Transmit for network-bound function
8. Classifies DestructiveIrreversible for delete or drop functions
9. Classifies PrivilegeEscalation for role or permission functions
10. Returns Unknown for unclassifiable function (fail-closed input)

### PolicyEvaluatorTests (4)
11. NORMAL posture approves allowed Read
12. NORMAL posture denies disallowed DestructiveIrreversible
13. ELEVATED posture denies all Transmit regardless of manifest
14. LOCKDOWN posture denies everything except Read on non-restricted

### ReceiptEmitterTests (3)
15. First receipt has null previous_receipt_sha
16. Second receipt's previous_receipt_sha matches first receipt's SHA-256
17. Tampered receipt log fails chain verification

### SpineFilterIntegrationTests (4)
18. End-to-end: SK invokes allowed function, filter approves, receipt emitted, function executes
19. End-to-end: SK invokes denied function, filter throws GovernanceVetoException, receipt emitted, function never executes
20. End-to-end: undeclared function denies (fail-closed, manifest gate)
21. End-to-end: classifier exception denies with deny_reason populated

### ReceiptInteropTests (2)
22. Reads Spine Lite Python-emitted receipt sample, verifies chain
23. .NET-emitted receipt round-trips through Python verifier schema (validate against fixture)

**All 23 tests must pass on `dotnet test` before v0.1.0 ships.** No skips. No `[Trait("Category", "Skip")]`.

---

## 7. Sample Host (samples/BasicSemanticKernelHost)

30-line `Program.cs` that:

1. Builds a Semantic Kernel
2. Registers two plugins: `SafeFileReader` (Read effect, allowed) and `DangerousFileDeleter` (DestructiveIrreversible, denied)
3. Loads `manifest.json`
4. Registers `SpineFilter` as `IFunctionInvocationFilter`
5. Invokes both functions in sequence
6. Catches `GovernanceVetoException` on the second, prints receipt chain

Output proves the wedge sentence in 30 seconds. This is the demo for LinkedIn.

---

## 8. CLAUDE.md Status

The repo-level governance file is already committed at repo root. Treat it as authoritative. If anything in this blueprint conflicts with `CLAUDE.md`, `CLAUDE.md` wins, surface the conflict, do not silently reconcile.

---

## 9. CI (.github/workflows/ci.yml)

Standard .NET 8 matrix on ubuntu-latest and windows-latest. Steps: `dotnet restore`, `dotnet build --no-restore`, `dotnet test --no-build --verbosity normal`. PR-blocking on test failure. No release automation in v0.1.0, manual `dotnet pack` and `nuget push` for the first release.

---

## 10. README Structure

1. **Wedge** (3 sentences max, the framing from §0)
2. **Install** (`dotnet add package M87.Spine`)
3. **Minimal example** (10 lines, copy-paste runnable against the sample host)
4. **The seven invariants** (verbatim from §2)
5. **What this is not** (single-process, no Spine Pro, no distributed gate)
6. **Receipt format** (link to schema doc)
7. **License** (MIT)
8. **Contributing** (link to CONTRIBUTING.md, note community-maintained beyond v0.1)

No marketing copy. No emoji. No "Get started in 30 seconds." This is a governance package; the README's job is to make the architecture legible, not to sell.

---

## 11. v0.1.0 Ship Checklist

- [ ] All 23 tests green on `dotnet test`
- [ ] Sample host runs end-to-end, produces visible receipt chain in console and JSONL file
- [ ] `dotnet pack` produces `M87.Spine.0.1.0.nupkg` with no warnings
- [ ] README wedge sentence matches §0 exactly
- [ ] CLAUDE.md present at repo root, line budget verified at or below 150
- [ ] CI green on push to main
- [ ] LICENSE file (MIT) present
- [ ] One Spine Lite Python receipt successfully verified by the .NET interop test
- [ ] No TODO, FIXME, or NotImplementedException in `src/`
- [ ] Receipt schema and manifest schema both marked `"version": "1.0.0"` with x-frozen semantics documented

---

## 12. Out of Scope (explicitly deferred)

- **Distributed gate / Ed25519 signing** to Spine Pro / `M87.Spine.Pro` package
- **PAR tracking over time** to Spine Pro
- **Cross-session memory** to Spine Pro
- **Custom UI / dashboard** not a v0.1 concern
- **Azure-specific integration** (Function Apps, AKS) community contribution welcome, not core
- **AutoGen .NET integration** separate adapter package, post-v0.1
- **OpenTelemetry exporters** receipts are the audit trail; OTel is a v0.2+ ergonomic addition
- **NuGet auto-publish** manual for v0.1.0; automate after first user

---

## 13. Completion Receipt (this blueprint)

- **Artifact path:** `BLUEPRINT.md` at repo root
- **Shape:** 13 sections, repo identity to governance constraints to architecture to file structure to implementation constraints to test plan (23 tests) to sample host to CLAUDE.md status to CI to README structure to ship checklist to out-of-scope to this receipt
- **Receipt:** Hand to Claude Code in a fresh session against the seeded repo. First instruction: "Read CLAUDE.md and BLUEPRINT.md. Confirm the seven invariants and the 23-test ship gate. Run a 5-minute spike: `dotnet add package Microsoft.SemanticKernel --version <pinned>` and verify the IFunctionInvocationFilter contract matches BLUEPRINT.md §3.1. If the API has shifted, stop and surface. Do not write production code yet. Propose your session 1 plan."
- **Assumptions invoked:**
  - Microsoft.SemanticKernel has stable `IFunctionInvocationFilter` API at the version pinned in session 1
  - .NET 8 LTS remains current target through 2026 (Microsoft LTS schedule confirms support through Nov 2026)
  - Spine Lite v0.1.0 receipt format is treated as the canonical v1.0.0 receipt schema for cross-language interop
  - MIT license matches Spine Lite licensing posture (not BSL like Governed Swarm — this is the wedge product, not the moat product)
  - The `tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl` fixture is committed manually before the build session opens, NOT generated by Claude Code

---

*M87 Studio LLC. Internal architecture reference. Not for external distribution until v0.1.0 ships.*
