// Content taken from Goob-Station (https://github.com/Goob-Station/Goob-Station), licensed under AGPL-3.0-or-later.
using Content.Shared.Damage.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Onyx.Stunnable;

public sealed partial class OvertimeStaminaDamageSystem : EntitySystem
{
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OvertimeStaminaDamageComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<OvertimeStaminaDamageComponent> ent, ref ComponentInit args)
    {
        if (_net.IsClient)
        {
            RemComp<OvertimeStaminaDamageComponent>(ent);
            return;
        }

        ent.Comp.Timer = ent.Comp.Delay;
        ent.Comp.Damage = ent.Comp.Amount;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var overtime in EntityQuery<OvertimeStaminaDamageComponent>())
        {
            overtime.Timer -= frameTime;
            if (overtime.Timer > 0f)
                continue;

            var damage = overtime.Amount / overtime.Delta;
            _stamina.TakeStaminaDamage(overtime.Owner, damage, visual: false);
            overtime.Damage -= damage;
            overtime.Timer = overtime.Delay;

            if (overtime.Damage <= 0f)
                RemComp<OvertimeStaminaDamageComponent>(overtime.Owner);
        }
    }
}
