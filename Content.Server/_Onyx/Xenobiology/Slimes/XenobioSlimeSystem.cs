using Content.Shared._Onyx.Xenobiology.Slimes;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Xenobiology.Slimes;

public sealed partial class XenobioSlimeSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SlimeBreedingSystem _breeding = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioSlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnMapInit(Entity<XenobioSlimeComponent> ent, ref MapInitEvent args)
    {
        _breeding.InitializeSlime(ent);
        Validate(ent);
        UpdateAppearance(ent);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<EntityPrototype>())
            return;

        var query = EntityQueryEnumerator<XenobioSlimeComponent>();
        while (query.MoveNext(out var uid, out var slime))
        {
            Validate((uid, slime));
            UpdateAppearance((uid, slime));
        }
    }

    private void UpdateAppearance(Entity<XenobioSlimeComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, XenobioSlimeVisuals.Color, ent.Comp.Color, appearance);
        if (ent.Comp.Shader is { } shader)
            _appearance.SetData(ent, XenobioSlimeVisuals.Shader, shader, appearance);
        else
            _appearance.RemoveData(ent, XenobioSlimeVisuals.Shader, appearance);
    }

    private bool Validate(Entity<XenobioSlimeComponent> ent)
    {
        var slime = ent.Comp;
        var valid = ValidateEntityPrototype(slime.Breed, "breed", ent, requireSlime: true);

        if (slime.ProducedExtract is { } extract)
            valid &= ValidateEntityPrototype(extract, "extract", ent);

        foreach (var mutation in slime.PotentialMutations)
            valid &= ValidateEntityPrototype(mutation, "mutation", ent, requireSlime: true);

        if (slime.MinOffspring <= 0 || slime.MaxOffspring < slime.MinOffspring || slime.ExtractsProduced <= 0 ||
            !float.IsFinite(slime.MutationChance) || slime.MutationChance is < 0f or > 1f ||
            !float.IsFinite(slime.MitosisHunger) || slime.MitosisHunger < 0f ||
            !float.IsFinite(slime.JitterDifference) || slime.JitterDifference < 0f ||
            slime.MaxContainedEntities < 0 || slime.LatchDuration < TimeSpan.Zero || slime.OnReleaseStunDuration < TimeSpan.Zero)
        {
            Log.Error($"Xenobio slime {ToPrettyString(ent)} has invalid numeric configuration.");
            valid = false;
        }

        return valid;
    }

    private bool ValidateEntityPrototype(EntProtoId id, string field, EntityUid owner, bool requireSlime = false)
    {
        if (_prototypes.TryIndex(id, out var prototype) &&
            (!requireSlime || prototype.TryComp<XenobioSlimeComponent>(out _, EntityManager.ComponentFactory)))
            return true;

        Log.Error($"Xenobio slime {ToPrettyString(owner)} has unknown {field} prototype '{id}'.");
        return false;
    }
}
