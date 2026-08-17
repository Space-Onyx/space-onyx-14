using Content.Shared._Onyx.Medical.Surgery;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Onyx.Medical.Surgery;

public sealed partial class SurgerySystem : SharedSurgerySystem
{
    [Dependency] private IPlayerManager _player = default!;

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
        if (OnRefresh == null)
            return;

        _refreshAt -= frameTime;
        if (_refreshAt > 0)
            return;

        _refreshAt = 0.25f;
        OnRefresh.Invoke();
    }

    public IReadOnlyList<EntProtoId> GetPredictedSteps(EntityUid body, EntityUid part, EntProtoId surgery)
    {
        if (_player.LocalEntity is not { } user ||
            GetSurgeryEntity(surgery) is not { } surgeryEntity ||
            !TryComp(surgeryEntity, out SurgeryComponent? surgeryComponent))
            return [];

        return GetSurgerySteps(body, part, (surgeryEntity, surgeryComponent), GetActiveTool(user));
    }
}
