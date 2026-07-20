using Content.Shared.Body.Part;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Onyx.Wounds;

public sealed partial class WoundScarSystem : EntitySystem
{
    private static readonly ProtoId<WoundPrototype> ScarWound = "MedicalScarWound";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private WoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, WoundStateChangedEvent>(OnWoundStateChanged);
        SubscribeLocalEvent<WoundScarComponent, WoundTreatmentAttemptEvent>(OnTreatmentAttempt);
    }

    private void OnWoundStateChanged(Entity<WoundComponent> wound, ref WoundStateChangedEvent args)
    {
        if (args.State is not WoundState.Closed and not WoundState.Healed)
        {
            wound.Comp.ScarCreatedForCurrentClosure = false;
            return;
        }

        if (args.OldState is WoundState.Closed or WoundState.Healed ||
            !_prototypes.TryIndex(wound.Comp.Prototype, out var source) ||
            source.ScarThreshold is not { } threshold || wound.Comp.PeakSeverity < threshold)
            return;

        CreateScar((wound.Owner, wound.Comp));
    }

    private void OnTreatmentAttempt(Entity<WoundScarComponent> scar, ref WoundTreatmentAttemptEvent args)
    {
        args.Cancelled = true;
    }

    public EntityUid? CreateScar(Entity<WoundComponent?> source)
    {
        if (!_net.IsServer || !Resolve(source, ref source.Comp, false) ||
            source.Comp.State == WoundState.Scarred || source.Comp.ScarCreatedForCurrentClosure ||
            !TryComp(source.Comp.HoldingPart, out BodyPartComponent? part))
            return null;

        var scar = _wounds.CreateOrMergeWound(source.Comp.HoldingPart, ScarWound, 1);
        if (scar is null)
            return null;

        var component = AddComp<WoundScarComponent>(scar.Value);
        component.SourcePrototype = source.Comp.Prototype;
        component.SourcePeakSeverity = source.Comp.PeakSeverity;
        Dirty(scar.Value, component);
        _wounds.SetWoundState(scar.Value, WoundState.Scarred);
        source.Comp.ScarCreatedForCurrentClosure = true;

        var created = new ScarCreatedEvent(part.Body, source.Comp.HoldingPart, scar.Value);
        RaiseLocalEvent(source.Comp.HoldingPart, ref created);
        RaiseLocalEvent(scar.Value, ref created);
        return scar;
    }
}
