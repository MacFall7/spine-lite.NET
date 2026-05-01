using M87.Spine.Models;

namespace M87.Spine;

/// <summary>
/// Resolves a function's effect class via the manifest. Stateless; safe to share.
/// Per BLUEPRINT §2 invariant 7: authority lives in the manifest, not in descriptions.
/// Per BLUEPRINT §2 invariant 3: unknown function returns Unknown (caller treats as DENY).
/// </summary>
public class EffectClassifier
{
    private readonly ManifestGate _gate;

    public EffectClassifier(ManifestGate gate)
    {
        _gate = gate;
    }

    public virtual EffectClass Classify(string plugin, string function)
    {
        var entry = _gate.Lookup(plugin, function);
        return entry is null ? EffectClass.Unknown : entry.EffectClass;
    }
}
