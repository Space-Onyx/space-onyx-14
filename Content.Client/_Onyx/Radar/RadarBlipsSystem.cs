using System.Numerics;
using Content.Shared._Onyx.Radar;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Radar;

public sealed partial class RadarBlipsSystem : EntitySystem
{
    private static readonly List<(Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> Empty = new();
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StaleTime = TimeSpan.FromSeconds(3);

    [Dependency] private IGameTiming _timing = default!;

    private List<(Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> _blips = Empty;
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> _lines = new();
    private TimeSpan _lastRequest;
    private TimeSpan _lastUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(OnBlipsReceived);
    }

    private void OnBlipsReceived(GiveBlipsEvent ev)
    {
        _blips = ev.Blips;
        _lines = ev.Lines;
        _lastUpdate = _timing.CurTime;
    }

    public void RequestBlips(EntityUid console)
    {
        if (!Exists(console) || _timing.CurTime - _lastRequest < RequestThrottle)
            return;

        _lastRequest = _timing.CurTime;
        RaiseNetworkEvent(new RequestBlipsEvent(GetNetEntity(console)));
    }

    public IReadOnlyList<(Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> GetCurrentBlips()
    {
        return _timing.CurTime - _lastUpdate <= StaleTime ? _blips : Empty;
    }

    public IReadOnlyList<(Vector2 Start, Vector2 End, float Thickness, Color Color)> GetCurrentLines()
    {
        return _timing.CurTime - _lastUpdate <= StaleTime ? _lines : [];
    }
}
