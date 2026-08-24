using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

public sealed partial class SurgeryToolExamineSystem : EntitySystem
{
    private const string ScalpelIcon = "/Textures/_Onyx/Interface/VerbIcons/scalpel.png";

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
        message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-uses"));

        var uses = GetUses(ent.Owner, ent.Comp);
        if (uses.Count == 0)
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-use-none"));
        }
        else
        {
            foreach (var (use, task) in uses)
            {
                message.PushNewline();
                var speed = ent.Comp.SpeedModifiers.GetValueOrDefault(task, 1f);
                var color = speed switch
                {
                    < 1f => "red",
                    > 1f => "green",
                    _ => "white",
                };
                message.AddMarkupOrThrow(Loc.GetString("surgery-tool-examine-use",
                    ("use", Loc.GetString(use)), ("multiplier", speed), ("color", color)));
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

    private List<(LocId Use, string Task)> GetUses(EntityUid tool, SurgeryToolComponent component)
    {
        var uses = new List<(LocId, string)>();
        AddUse<ScalpelComponent>(tool, "surgery-tool-use-scalpel", uses);
        AddUse<HemostatComponent>(tool, "surgery-tool-use-hemostat", uses);
        AddUse<RetractorComponent>(tool, "surgery-tool-use-retractor", uses);
        AddUse<BoneSawComponent>(tool, "surgery-tool-use-bone-saw", uses);
        AddUse<CauteryComponent>(tool, "surgery-tool-use-cautery", uses);
        AddUse<BoneGelComponent>(tool, "surgery-tool-use-bone-gel", uses);
        AddUse<TweezersComponent>(tool, "surgery-tool-use-tweezers", uses);
        AddUse<StitchesComponent>(tool, "surgery-tool-use-stitches", uses);
        AddUse<DrillComponent>(tool, "surgery-tool-use-drill", uses);
        AddUse<TendingComponent>(tool, "surgery-tool-use-tending", uses);

        foreach (var use in component.CustomUses)
        {
            var duplicate = false;
            foreach (var entry in uses)
            {
                if (entry.Item1 != use)
                    continue;

                duplicate = true;
                break;
            }

            if (!duplicate)
                uses.Add((use, string.Empty));
        }

        return uses;
    }

    private void AddUse<T>(EntityUid tool, LocId use, List<(LocId, string)> uses) where T : IComponent
    {
        if (HasComp<T>(tool))
            uses.Add((use, Factory.GetComponentName(typeof(T))));
    }
}
