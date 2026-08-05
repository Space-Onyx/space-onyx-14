using Content.Shared._Onyx.Disease.Components;
using Content.Shared._Onyx.Disease.Systems;
using Content.Shared._Onyx.Virology;
using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Random;

namespace Content.Server._Onyx.Virology;

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
        if (!args.CanReach || args.Target is not { } target || ent.Comp.DiseaseUid != null)
            return;

        if (!TryComp<DiseaseCarrierComponent>(target, out var carrier))
        {
            _popup.PopupEntity(Loc.GetString("disease-swab-cant-swab", ("target", target)), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("disease-swab-swabbed",
            ("target", target == args.User ? Loc.GetString("disease-swab-yourself") : target)), args.User, args.User);
        if (target != args.User)
            _popup.PopupEntity(Loc.GetString("disease-swab-swabbed-by", ("user", args.User)), target, target);

        if (carrier.Diseases.Count == 0)
            return;

        SetDisease(ent, _random.Pick(carrier.Diseases.ContainedEntities));
        args.Handled = true;
    }

    private void OnExamined(Entity<DiseaseSwabComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.DiseaseUid != null) args.PushMarkup(Loc.GetString("disease-swab-unclean"));
    }

    private void OnShutdown(Entity<DiseaseSwabComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.DiseaseUid != null)
            QueueDel(ent.Comp.DiseaseUid);
    }

    private void SetDisease(Entity<DiseaseSwabComponent> ent, EntityUid disease)
    {
        QueueDel(ent.Comp.DiseaseUid);
        ent.Comp.DiseaseUid = _disease.TryClone(disease);
        Dirty(ent);
    }
}
