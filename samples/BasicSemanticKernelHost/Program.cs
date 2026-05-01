using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using M87.Spine;
using M87.Spine.Configuration;
using M87.Spine.Models;

var manifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
var receiptLog = Path.Combine(AppContext.BaseDirectory, "receipts.jsonl");
if (File.Exists(receiptLog)) File.Delete(receiptLog);

var gate = ManifestGate.Load(manifestPath);
var classifier = new EffectClassifier(gate);
var evaluator = new PolicyEvaluator();
using var emitter = new ReceiptEmitter(receiptLog, sessionId: Guid.NewGuid().ToString("N").Substring(0, 12));
var options = new SpineOptions { ManifestPath = manifestPath, ReceiptLogPath = receiptLog, Posture = Posture.Normal };
var filter = new SpineFilter(gate, classifier, evaluator, emitter, options);

var builder = Kernel.CreateBuilder();
builder.Services.AddSingleton<IFunctionInvocationFilter>(filter);
var kernel = builder.Build();
kernel.Plugins.AddFromObject(new SafeFileReader(), nameof(SafeFileReader));
kernel.Plugins.AddFromObject(new DangerousFileDeleter(), nameof(DangerousFileDeleter));

Console.WriteLine("== Spine Lite .NET — sample host ==");
var read = await kernel.InvokeAsync(kernel.Plugins[nameof(SafeFileReader)]["ReadDoc"], new() { ["path"] = "README.md" });
Console.WriteLine($"APPROVE: {read.GetValue<string>()}");

try { await kernel.InvokeAsync(kernel.Plugins[nameof(DangerousFileDeleter)]["DeletePath"], new() { ["path"] = "/" }); }
catch (Exception ex) when ((ex as GovernanceVetoException ?? ex.InnerException as GovernanceVetoException) is { } veto)
{ Console.WriteLine($"DENY: {veto.DenyReason} (receipt {veto.Receipt.ReceiptId})"); }

Console.WriteLine($"\nReceipt chain at {receiptLog}:");
foreach (var line in File.ReadAllLines(receiptLog)) Console.WriteLine("  " + line);

public sealed class SafeFileReader { [KernelFunction, Description("Read a doc.")] public string ReadDoc(string path) => $"contents of {path}"; }
public sealed class DangerousFileDeleter { [KernelFunction, Description("Delete a path.")] public string DeletePath(string path) => $"deleted {path}"; }
