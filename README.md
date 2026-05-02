# Spine Lite — .NET

[![NuGet](https://img.shields.io/nuget/v/M87.Spine.svg)](https://www.nuget.org/packages/M87.Spine/) [![CI](https://github.com/MacFall7/spine-lite.NET/actions/workflows/ci.yml/badge.svg)](https://github.com/MacFall7/spine-lite.NET/actions/workflows/ci.yml)

Spine Lite ports the authority-separation pattern into Semantic Kernel's function invocation pipeline. Six effect classes, fail-closed default, cryptographic receipt chain, enforced as an `IFunctionInvocationFilter` before the function executes.

`v0.1.0` is single-process local enforcement. Distributed gating, Ed25519 signing, and PAR tracking belong in a future `M87.Spine.Pro` package.

## Install

```
dotnet add package M87.Spine
```

Targets `net8.0`. Pinned to `Microsoft.SemanticKernel` 1.75.0.

## Minimal example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using M87.Spine;
using M87.Spine.Configuration;
using M87.Spine.Models;

var gate = ManifestGate.Load("manifest.json");
var classifier = new EffectClassifier(gate);
var evaluator = new PolicyEvaluator();
using var emitter = new ReceiptEmitter("receipts.jsonl", sessionId: "demo");

var options = new SpineOptions
{
    ManifestPath = "manifest.json",
    ReceiptLogPath = "receipts.jsonl",
    Posture = Posture.Normal,
};

var filter = new SpineFilter(gate, classifier, evaluator, emitter, options);

var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IFunctionInvocationFilter>(filter);
var kernel = builder.Build();
```

A full runnable demo lives at `samples/BasicSemanticKernelHost/`.

## The seven invariants

1. **Proposal ≠ Execution.** The kernel proposes a function call; the filter intercepts before invocation.
2. **Authority Separation.** `EffectClassifier`, `PolicyEvaluator`, and `ReceiptEmitter` are distinct services.
3. **Fail-Closed Default.** Unknown function denies. Missing manifest entry denies. Classification failure denies. Exception during evaluation denies.
4. **Artifact-Backed Completion.** Every approve and every deny emits a SHA-256-chained receipt. No invocation completes without a receipt.
5. **Structured Memory.** Receipt log is append-only JSONL. Manifest is hashed at load and verified.
6. **Model Interchangeability.** No dependency on a specific LLM. The filter operates on Semantic Kernel function metadata.
7. **Narrative ≠ Runtime.** SK `[Description]` attributes are advisory. Authority lives in the manifest.

## Six effect classes (wire-faithful with Spine Lite Python)

| Wire string         | Meaning                                              |
|---------------------|------------------------------------------------------|
| `SHELL_SAFE`        | Read-only shell (git status, ls, cat)                |
| `SHELL_MUTATING`    | State-changing shell (git add, mkdir, cp)            |
| `SHELL_DANGEROUS`   | Destructive (rm -rf, sudo, chmod)                    |
| `NETWORK_ATTEMPT`   | Outbound network (curl, wget, pip install)           |
| `SCOPED_WRITE`      | Allowed file writes within repo boundary             |
| `RESTRICTED_WRITE`  | Blocked writes (.env, secrets, credentials)          |

## Posture matrix

| Posture    | Behavior                                                                |
|------------|-------------------------------------------------------------------------|
| `NORMAL`   | Honors manifest's `allowed` flag.                                       |
| `ELEVATED` | Denies all `NETWORK_ATTEMPT` regardless of manifest; otherwise NORMAL.  |
| `LOCKDOWN` | Denies everything except `SHELL_SAFE` on allowed entries.               |

## What this is not

- Not Spine Pro. No distributed gate. No cross-machine signing. No PAR tracking over time.
- Not auto-function-calling aware. v0.1.0 registers `IFunctionInvocationFilter` only; `IAutoFunctionInvocationFilter` is out of scope.
- Not a replacement for runtime sandboxing. Spine enforces a manifest contract; it does not constrain what an executing function can reach inside the host process.

## Receipt format

Receipts are byte-compatible with [Spine Lite Python v0.1.0](https://github.com/MacFall7/M87-Spine-lite). Twelve top-level fields (`receipt_id`, `session_id`, `proposal_id`, `sequence_number`, `timestamp`, `executor`, `action`, `result`, `budget_snapshot`, `git_context`, `previous_receipt_hash`, `receipt_hash`); SHA-256 chain. Schema at `tests/M87.Spine.Tests/fixtures/receipt.schema.json`.

## License

MIT. See `LICENSE`.

## Contributing

Community-maintained beyond v0.1.0. Open issues and PRs welcome. Receipt schema (v1.0.0) and manifest schema (v1.0.0) are frozen for cross-language interop; additive changes go to v1.1.0, breaking changes to v2.0.0.
