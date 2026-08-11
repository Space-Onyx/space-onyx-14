using Content.Shared._Onyx.Abilities;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Onyx.Abilities;

public sealed partial class CrawlUnderObjectsSystem : SharedCrawlUnderObjectsSystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrawlUnderObjectsComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(Entity<CrawlUnderObjectsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !_appearance.TryGetData(ent, CrawlUnderObjectsVisuals.Enabled, out bool enabled))
            return;

        if (enabled && ent.Comp.OriginalDrawDepth == null)
        {
            ent.Comp.OriginalDrawDepth = sprite.DrawDepth;
            _sprite.SetDrawDepth((ent, sprite), (int) DrawDepth.SmallMobs);
        }
        else if (!enabled && ent.Comp.OriginalDrawDepth is { } depth)
        {
            _sprite.SetDrawDepth((ent, sprite), depth);
            ent.Comp.OriginalDrawDepth = null;
        }
    }
}
