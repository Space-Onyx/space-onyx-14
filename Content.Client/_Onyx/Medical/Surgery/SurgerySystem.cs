using Content.Shared._Onyx.Medical.Surgery;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    public event Action? OnRefresh;
    private float _refreshAt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryMarkerComponent, ComponentStartup>(Refresh);
    }

    private void Refresh<TComponent, TEvent>(Entity<TComponent> ent, ref TEvent args) where TComponent : IComponent
        => OnRefresh?.Invoke();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (OnRefresh == null)
            return;

        _refreshAt -= frameTime;
        if (_refreshAt > 0)
            return;

        _refreshAt = 0.25f;
        OnRefresh.Invoke();
    }

}
