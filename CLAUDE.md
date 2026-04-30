# CLAUDE.md — M87 Governance Substrate

<!-- Line budget: 150 max. Do not exceed. -->

## Repo Identity

Repo: spine-lite.NET
Role: Governance enforcement filter for Semantic Kernel agent runtimes (.NET port of Spine Lite v0.1.0)
Owner: M87 Studio LLC
Parity target: MacFall7/M87-Spine-lite v0.1.0 (Python, Feb 16 2026)

## Governance Constraints (Non-Negotiable)

These invariants apply to every session in this repo:

1. Proposal ≠ Execution — Propose before acting. No direct execution.
2. Authority Separation — Decision logic and execution logic must remain separated. EffectClassifier, PolicyEvaluator, and ReceiptEmitter are distinct services.
3. Fail-Closed — If constraints are unclear or missing → halt and ask. Unknown function → DENY. Classification failure → DENY. Exception during evaluation → DENY.
4. Artifact-Backed Completion — No task is done without a verifiable artifact. Every approved AND denied invocation emits a receipt.
5. Structured Memory — Receipt log is append-only JSONL. Manifest hashed at load, verified per invocation. No raw blob accumulation.
6. Model Interchangeability — No dependency on a specific LLM. Filter operates on SK function metadata, not model output.
7. Narrative ≠ Runtime — Function `[Description]` attributes are advisory. Authority lives in the manifest, not in descriptions.

## Session Rules

- Do not execute destructive operations (delete, overwrite, transmit) without explicit confirmation.
- Do not call external services unless declared in task scope.
- Do not merge to main without 23/23 tests green and explicit ship approval.
- Do not use Programmatic Tool Calling (PTC) — bypasses PreToolUse hooks.
- Subagents must use Task tool — no bash-invoked subagent chains.
- No reflection-based dynamic dispatch in the hot path. Function lookup is dictionary-based, O(1).
- No logging dependency in runtime path (no Serilog, no NLog, no ILogger). Receipts are the audit trail.
- No reading of function descriptions for authorization decisions. Authority lives in the manifest.
- No swallowed exceptions. Any exception in classifier or evaluator → emit DENY receipt, throw GovernanceVetoException.
- No partial receipts. Receipt write must complete before invocation proceeds (APPROVE) or exception is thrown (DENY).

## Tool Permissions

Allowed:
- Read, Write, Edit within repo boundary
- Bash: `dotnet build`, `dotnet test`, `dotnet pack`, `dotnet add package`, `dotnet restore`, `git status`, `git diff`, `git log`, `git add`, `git commit`

Blocked:
- Bash: `rm -rf`, `curl | bash`, `chmod`, `sudo`, `dotnet publish` to remote, `nuget push`
- Direct file writes outside repo root
- Network egress beyond `dotnet restore` package fetch

## Artifact Definition

Every completed task must produce:
- Path — where the artifact lives
- Shape — what it contains (signatures, class members, test names)
- Receipt — `dotnet test` pass count, `dotnet build` success, or SHA-256 confirmation

No artifact → task is not complete.

## Escalation

Stop and ask before:
- Adding any NuGet package not already in `.csproj`
- Modifying the receipt schema (v1.0.0 frozen — interop with Spine Lite is load-bearing)
- Modifying the manifest schema (v1.0.0 frozen)
- Any deviation from BLUEPRINT.md §3 (architecture) or §6 (test plan)
- Any change that would break receipt byte-compatibility with Spine Lite Python receipts

## Repo-Specific Context

Stack: C# 12, .NET 8 LTS, Microsoft.SemanticKernel (pinned version — confirm in session 1), xUnit
Key files (post-scaffold):
- src/M87.Spine/SpineFilter.cs — IFunctionInvocationFilter entry point
- src/M87.Spine/ManifestGate.cs — manifest verification
- src/M87.Spine/EffectClassifier.cs — 6-class classification
- src/M87.Spine/ReceiptEmitter.cs — append-only chained audit log
- tests/M87.Spine.Tests/fixtures/spine_lite_receipt_sample.jsonl — DO NOT REGENERATE (real Spine Lite output)

Known constraints:
- Receipt format byte-compatible with Spine Lite v0.1.0 — tests 22 & 23 enforce this
- 23 tests must pass before v0.1.0 ships (parity with governance-sandbox 23/23 invariant suite)
- Single-process, local enforcement only — distributed gate is Spine Pro / out of scope for v0.1.0

Spec of record: BLUEPRINT.md at repo root.
