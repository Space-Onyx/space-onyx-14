using Content.Shared._Onyx.Screens;
using Content.Client.GameTicking.Managers;
using Robust.Shared.Timing;

namespace Content.Client._Onyx.Screens;

public sealed partial class StatusDisplaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private OnyxTextVisualsSystem _textVisuals = default!;
    [Dependency] private ClientGameTicker _ticker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StatusDisplayComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<StatusDisplayComponent>();
        while (query.MoveNext(out var uid, out var display))
        {
            if (display.Content is StatusDisplayContent.CurrentTime or StatusDisplayContent.EstimatedTimeOfArrival)
                UpdateText((uid, display));
        }
    }

    private void OnAfterAutoHandleState(Entity<StatusDisplayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateText(ent);
    }

    private void UpdateText(Entity<StatusDisplayComponent> ent)
    {
        var visuals = EnsureComp<OnyxTextVisualsComponent>(ent);

        if (ent.Comp.Content == StatusDisplayContent.AlertLevel)
        {
            _textVisuals.SetText((ent, visuals), string.Empty, string.Empty);
            return;
        }

        switch (ent.Comp.Content)
        {
            case StatusDisplayContent.Text:
                _textVisuals.SetText((ent, visuals), true, ent.Comp.Line1, ent.Comp.Line2);
                break;
            case StatusDisplayContent.CurrentTime:
                _textVisuals.SetText((ent, visuals), Loc.GetString("status-display-time"), (_timing.CurTime - _ticker.RoundStartTimeSpan).Duration().ToString("hh\\:mm"));
                break;
            case StatusDisplayContent.EstimatedTimeOfArrival:
                if (ent.Comp.TargetTime <= _timing.CurTime)
                {
                    _textVisuals.SetText((ent, visuals), string.Empty, string.Empty);
                    break;
                }

                var time = (ent.Comp.TargetTime - _timing.CurTime).Duration();
                var title = ent.Comp.IsAtDestination
                    ? Loc.GetString("status-display-etd")
                    : Loc.GetString("status-display-eta");
                _textVisuals.SetText((ent, visuals), title, time.ToString("mm\\:ss"));
                break;
        }
    }
}
