using Content.Shared._GoobStation.Disease.Components;
using Content.Shared.StatusIcon.Components;

namespace Content.Client._GoobStation.Disease;

public sealed class ShowDiseaseIconsSystem : EntitySystem
{
    public override void Initialize() => SubscribeLocalEvent<ShowDiseaseIconsComponent, ComponentStartup>(OnStartup);

    private void OnStartup(Entity<ShowDiseaseIconsComponent> entity, ref ComponentStartup args)
    {
        EnsureComp<StatusIconComponent>(entity);
    }
}
