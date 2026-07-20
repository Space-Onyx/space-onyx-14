using Content.Shared._GoobStation.Disease.Components;
using Content.Shared._GoobStation.Disease.Systems;
using Content.Shared._GoobStation.Virology;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Random;

namespace Content.Server._GoobStation.Virology;

public sealed partial class DiseaseSwabSystem : EntitySystem
{
    [Dependency] private SharedDiseaseSystem _disease = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DiseaseSwabComponent, AfterInteractEvent>(OnInteract);
        SubscribeLocalEvent<DiseaseSwabComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DiseaseSwabComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteract(Entity<DiseaseSwabComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target is not { } target || !TryComp<DiseaseCarrierComponent>(target, out var carrier))
            return;
        if (carrier.Diseases.Count == 0) return;
        SetDisease(ent, _random.Pick(carrier.Diseases.ContainedEntities));
        args.Handled = true;
    }

    private void OnExamined(Entity<DiseaseSwabComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.DiseaseUid != null) args.PushMarkup(Loc.GetString("disease-swab-unclean"));
    }

    private void OnShutdown(Entity<DiseaseSwabComponent> ent, ref ComponentShutdown args) => QueueDel(ent.Comp.DiseaseUid);

    private void SetDisease(Entity<DiseaseSwabComponent> ent, EntityUid disease)
    {
        QueueDel(ent.Comp.DiseaseUid);
        ent.Comp.DiseaseUid = _disease.TryClone(disease);
        Dirty(ent);
    }
}
