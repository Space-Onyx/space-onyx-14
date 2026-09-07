// Content taken from Wega (https://github.com/wega-team/ss14-wega), licensed under GNU GPL v3.
using System.Numerics;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;

namespace Content.Client._Onyx.DetailExaminable;

public sealed class DetailExaminableSystem : EntitySystem
{
    private DetailExaminableWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DetailExaminableComponent, OpenDetailedDescriptionEvent>(OnOpenDetailedDescription);
    }

    private void OnOpenDetailedDescription(Entity<DetailExaminableComponent> ent, ref OpenDetailedDescriptionEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(ent, out var humanoid))
            return;

        _window?.Close();
        _window = new DetailExaminableWindow();
        _window.SetEntity(ent, ent.Comp, humanoid);
        _window.OpenCenteredAt(new Vector2(0f, 0.75f));
    }
}
