using Content.Shared.Access.Systems; // <Onyx-MailDelivery>
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.Delivery;

namespace Content.Server.Objectives.Systems;

public sealed partial class MailFraudObjectiveSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!; // <Onyx-MailDelivery-edited>
    [Dependency] private CounterConditionSystem _counterCondition = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DeliveryComponent, DeliveryOpenedEvent>(OnDeliveryOpened);
    }

    private void OnDeliveryOpened(Entity<DeliveryComponent> ent, ref DeliveryOpenedEvent args)
    {
        if (!ent.Comp.WasPenalized)
            return; //not fraud

        // <Onyx-MailDelivery-edited>
        if (_idCard.TryFindIdCard(args.User, out var idCard) &&
            idCard.Comp.FullName == ent.Comp.RecipientName &&
            idCard.Comp.LocalizedJobTitle == ent.Comp.RecipientJobTitle)
            return; // cutting open your own letter
        // </Onyx-MailDelivery-edited>

        if (!_mind.TryGetMind(args.User, out var mindUid, out var mind))
            return;

        foreach (var obj in _mind.EnumerateObjectives<MailFraudConditionComponent>((mindUid, mind)))
        {
            _counterCondition.IncreaseCount(obj);
        }
    }
}
