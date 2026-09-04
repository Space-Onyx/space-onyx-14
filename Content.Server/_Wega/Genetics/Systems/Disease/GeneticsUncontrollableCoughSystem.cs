using Content.Server.Chat.Systems;
using Content.Server._Wega.Genetics.Components;
using Content.Shared.Standing;
using Robust.Shared.Random;

namespace Content.Server.Genetics.System;

public sealed partial class GeneticsUncontrollableCoughSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeneticsUncontrollableCoughComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<GeneticsUncontrollableCoughComponent> ent, ref ComponentStartup args)
    {
        ResetTimer(ent.Comp);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GeneticsUncontrollableCoughComponent>();
        while (query.MoveNext(out var uid, out var cough))
        {
            cough.NextIncidentTime -= frameTime;
            if (cough.NextIncidentTime >= 0)
                continue;

            ResetTimer(cough);
            var drop = new DropHandItemsEvent();
            RaiseLocalEvent(uid, ref drop);
            _chat.TryEmoteWithChat(uid, cough.Emote, forceEmote: true);
        }
    }

    private void ResetTimer(GeneticsUncontrollableCoughComponent component)
    {
        component.NextIncidentTime = _random.NextFloat(component.TimeBetweenIncidents.X, component.TimeBetweenIncidents.Y);
    }
}
