using Content.Shared._Onyx.Medical.Surgery;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    public event Action? OnRefresh;
    private float _refreshAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IncisionOpenComponent, ComponentStartup>(Refresh);
        SubscribeLocalEvent<IncisionOpenComponent, ComponentShutdown>(Refresh);
        SubscribeLocalEvent<BleedersClampedComponent, ComponentStartup>(Refresh);
        SubscribeLocalEvent<BleedersClampedComponent, ComponentShutdown>(Refresh);
        SubscribeLocalEvent<SkinRetractedComponent, ComponentStartup>(Refresh);
        SubscribeLocalEvent<SkinRetractedComponent, ComponentShutdown>(Refresh);
        SubscribeLocalEvent<RibcageSawedComponent, ComponentStartup>(Refresh);
        SubscribeLocalEvent<RibcageSawedComponent, ComponentShutdown>(Refresh);
        SubscribeLocalEvent<RibcageOpenComponent, ComponentStartup>(Refresh);
        SubscribeLocalEvent<RibcageOpenComponent, ComponentShutdown>(Refresh);
    }

    private void Refresh<TComponent, TEvent>(Entity<TComponent> ent, ref TEvent args) where TComponent : IComponent
        => OnRefresh?.Invoke();

    public override void Update(float frameTime)
    {
        if (OnRefresh == null)
            return;

        _refreshAt -= frameTime;
        if (_refreshAt > 0)
            return;

        _refreshAt = 0.25f;
        OnRefresh.Invoke();
    }
}
