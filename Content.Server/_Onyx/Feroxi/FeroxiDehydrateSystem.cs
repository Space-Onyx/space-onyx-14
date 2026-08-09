using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Metabolism;
using Content.Shared.Nutrition.Components;

namespace Content.Server._Onyx.Feroxi;

public sealed partial class FeroxiDehydrateSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private MetabolizerSystem _metabolizer = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FeroxiDehydrateComponent, ThirstComponent>();
        while (query.MoveNext(out var uid, out var dehydrate, out var thirst))
            SetDehydrated((uid, dehydrate), thirst.CurrentThirst <= dehydrate.DehydrationThreshold);
    }

    private void SetDehydrated(Entity<FeroxiDehydrateComponent> ent, bool dehydrated)
    {
        if (ent.Comp.Dehydrated == dehydrated)
            return;

        ent.Comp.Dehydrated = dehydrated;
        Dirty(ent);

        var metabolizerType = dehydrated ? ent.Comp.DehydratedMetabolizer : ent.Comp.HydratedMetabolizer;
        foreach (var (organ, _) in _body.GetBodyOrgans(ent.Owner))
        {
            if (!HasComp<LungComponent>(organ) || !TryComp<MetabolizerComponent>(organ, out var metabolizer))
                continue;

            _metabolizer.SetMetabolizerTypes((organ, metabolizer), metabolizerType);
        }
    }
}
