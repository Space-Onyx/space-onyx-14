using System.Linq;
using Content.Shared.Body.Part;
using Robust.Shared.Audio;

namespace Content.Shared._Onyx.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeHellRipMoves()
    {
        SubscribeLocalEvent<CanPerformComboComponent, HellRipDropKickEvent>((Entity<CanPerformComboComponent> ent, ref HellRipDropKickEvent args) => PerformMove(ent, PerformHellRipDropKick));
        SubscribeLocalEvent<CanPerformComboComponent, HellRipHeadRipEvent>((Entity<CanPerformComboComponent> ent, ref HellRipHeadRipEvent args) => PerformMove(ent, PerformHellRipHeadRip));
        SubscribeLocalEvent<CanPerformComboComponent, HellRipTearDownEvent>((Entity<CanPerformComboComponent> ent, ref HellRipTearDownEvent args) => PerformMove(ent, PerformHellRipTearDown));
        SubscribeLocalEvent<CanPerformComboComponent, HellRipSlamEvent>((Entity<CanPerformComboComponent> ent, ref HellRipSlamEvent args) => PerformMove(ent, PerformHellRipSlam));
    }

    private MoveResult PerformHellRipDropKick(MoveContext context)
    {
        if (!context.Downed)
            return MoveResult.Failed;
        StopPull(context.Target, context.Performer);
        ThrowAway(context.Performer, context.Target, 25f);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg"), context.Performer);
        return new();
    }

    private MoveResult PerformHellRipHeadRip(MoveContext context)
    {
        if (!_mobState.IsDead(context.Target))
            return MoveResult.Failed;
        StopPull(context.Target, context.Performer);
        var head = _body.GetBodyChildrenOfType(context.Target, BodyPartType.Head).FirstOrDefault().Id;
        if (head != default)
            _body.TryDetachPart(head);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/gib1.ogg"), context.Target);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg"), context.Performer);
        return new();
    }

    private MoveResult PerformHellRipTearDown(MoveContext context)
    {
        StopPull(context.Target, context.Performer);
        _bloodstream.TryModifyBleedAmount(context.Target, 5f);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/Fluids/blood1.ogg"), context.Target);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg"), context.Performer);
        return new();
    }

    private MoveResult PerformHellRipSlam(MoveContext context)
    {
        if (context.Downed)
            return MoveResult.Failed;
        Knockdown(context.Target, context.Move, GetStaminaResistance(context.Target));
        _standing.Stand(context.Performer);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/demon_attack1.ogg"), context.Performer);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/metal_slam5.ogg"), context.Target);
        return new();
    }
}
