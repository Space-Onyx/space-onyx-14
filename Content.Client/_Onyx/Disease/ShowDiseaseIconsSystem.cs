using Content.Client.Overlays;
using Content.Shared._Onyx.Disease;
using Content.Shared._Onyx.Disease.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Disease;

public sealed partial class ShowDiseaseIconsSystem : EquipmentHudSystem<ShowDiseaseIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private float? _lowThreshold;
    private float? _mediumThreshold;
    private float? _highThreshold;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DiseaseCarrierComponent, GetStatusIconsEvent>(OnGetStatusIcons);
        SubscribeLocalEvent<ShowDiseaseIconsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowDiseaseIconsComponent> args)
    {
        base.UpdateInternal(args);
        foreach (var component in args.Components)
        {
            if (component.LowThreshold != null)
                _lowThreshold = MathF.Min(_lowThreshold ?? float.MaxValue, component.LowThreshold.Value);
            if (component.MediumThreshold != null)
                _mediumThreshold = MathF.Min(_mediumThreshold ?? float.MaxValue, component.MediumThreshold.Value);
            if (component.HighThreshold != null)
                _highThreshold = MathF.Min(_highThreshold ?? float.MaxValue, component.HighThreshold.Value);
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();
        _lowThreshold = null;
        _mediumThreshold = null;
        _highThreshold = null;
    }

    private void OnHandleState(Entity<ShowDiseaseIconsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    private void OnGetStatusIcons(Entity<DiseaseCarrierComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        var total = 0f;
        foreach (var disease in ent.Comp.Diseases.ContainedEntities)
        {
            if (TryComp<DiseaseComponent>(disease, out var component))
                total += component.InfectionProgress * component.Complexity;
        }

        DiseaseIconPrototype? icon = null;
        if (total > (_highThreshold ?? float.MaxValue))
            _prototype.TryIndex(ent.Comp.HighIcon, out icon);
        else if (total > (_mediumThreshold ?? float.MaxValue))
            _prototype.TryIndex(ent.Comp.MediumIcon, out icon);
        else if (total > (_lowThreshold ?? float.MaxValue))
            _prototype.TryIndex(ent.Comp.LowIcon, out icon);

        if (icon != null)
            args.StatusIcons.Add(icon);
    }
}
