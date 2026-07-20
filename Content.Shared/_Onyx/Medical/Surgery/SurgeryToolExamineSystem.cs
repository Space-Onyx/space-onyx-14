using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

public sealed partial class SurgeryToolExamineSystem : EntitySystem
{
    private const string ScalpelIcon = "/Textures/_Onyx/Objects/Specific/Medical/Surgery/scalpel.rsi/scalpel.png";

    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryToolComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<SurgeryToolComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-speed", ("multiplier", ent.Comp.SpeedModifier)));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-uses"));

        var uses = GetUses(ent.Owner, ent.Comp.CustomUses);
        if (uses.Count == 0)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-use-none"));
        }
        else
        {
            foreach (var use in uses)
            {
                message.PushNewline();
                message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-use", ("use", Loc.GetString(use))));
            }
        }

        _examine.AddDetailedExamineVerb(
            args,
            ent.Comp,
            message,
            Loc.GetString("surgery-tool-examine-verb-text"),
            ScalpelIcon,
            Loc.GetString("surgery-tool-examine-verb-message"));
    }

    private List<LocId> GetUses(EntityUid tool, List<LocId> customUses)
    {
        var uses = new List<LocId>();
        AddUse<ScalpelComponent>(tool, "surgery-tool-use-scalpel", uses);
        AddUse<HemostatComponent>(tool, "surgery-tool-use-hemostat", uses);
        AddUse<RetractorComponent>(tool, "surgery-tool-use-retractor", uses);
        AddUse<BoneSawComponent>(tool, "surgery-tool-use-bone-saw", uses);
        AddUse<CauteryComponent>(tool, "surgery-tool-use-cautery", uses);
        AddUse<BoneGelComponent>(tool, "surgery-tool-use-bone-gel", uses);
        AddUse<TweezersComponent>(tool, "surgery-tool-use-tweezers", uses);

        foreach (var use in customUses)
            if (!uses.Contains(use))
                uses.Add(use);

        return uses;
    }

    private void AddUse<T>(EntityUid tool, LocId use, List<LocId> uses) where T : IComponent
    {
        if (HasComp<T>(tool))
            uses.Add(use);
    }
}
