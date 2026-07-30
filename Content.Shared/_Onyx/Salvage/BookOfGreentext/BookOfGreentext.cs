using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Salvage.BookOfGreentext;

[RegisterComponent]
public sealed partial class BookOfGreentextComponent : Component
{
    [DataField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(3);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CurseOfBookOfGreentextComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Completed = true;

    [DataField]
    public EntityUid? Book;

    [DataField]
    public TimeSpan NextUpdate;
}

[Serializable, NetSerializable]
public sealed partial class BookOfGreentextDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class BookOfGreentextSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BookOfGreentextComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<BookOfGreentextComponent, BookOfGreentextDoAfterEvent>(OnDoAfter);
    }

    private void OnUseInHand(Entity<BookOfGreentextComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<CurseOfBookOfGreentextComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("book-of-greentext-already-taken"), args.User, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.UseDelay,
            new BookOfGreentextDoAfterEvent(),
            ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<BookOfGreentextComponent> ent, ref BookOfGreentextDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var curse = EnsureComp<CurseOfBookOfGreentextComponent>(args.User);
        curse.Book = ent;
        Dirty(args.User, curse);
        _popup.PopupEntity(Loc.GetString("book-of-greentext-use-message"), args.User, args.User);
    }
}

public sealed partial class WashCurseOfGreentext : EntityEffectBase<WashCurseOfGreentext>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-guidebook-wash-curse-of-greentext", ("chance", Probability));
}

public sealed partial class WashCurseOfGreentextSystem
    : EntityEffectSystem<CurseOfBookOfGreentextComponent, WashCurseOfGreentext>
{
    protected override void Effect(Entity<CurseOfBookOfGreentextComponent> ent,
        ref EntityEffectEvent<WashCurseOfGreentext> args)
    {
        RemCompDeferred<CurseOfBookOfGreentextComponent>(ent);
    }
}
