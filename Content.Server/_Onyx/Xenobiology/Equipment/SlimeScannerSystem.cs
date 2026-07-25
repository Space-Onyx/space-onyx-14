using System.Linq;
using Content.Shared._Onyx.Xenobiology.Equipment;
using Content.Shared._Onyx.Xenobiology.Extracts;
using Content.Shared._Onyx.Xenobiology.Slimes;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Xenobiology.Equipment;

public sealed partial class SlimeScannerSystem : EntitySystem
{
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenobioSlimeComponent, AfterInteractUsingEvent>(OnSlimeScanned);
        SubscribeLocalEvent<SlimeExtractComponent, AfterInteractUsingEvent>(OnExtractScanned);
    }

    private void OnSlimeScanned(Entity<XenobioSlimeComponent> slime, ref AfterInteractUsingEvent args)
    {
        if (!CanScan(args))
            return;

        var mutationNames = new List<(string Name, string Color)>();
        foreach (var mutation in slime.Comp.PotentialMutations)
        {
            if (!_prototypes.TryIndex(mutation, out var prototype) ||
                !prototype.TryComp<XenobioSlimeComponent>(out var mutationSlime, EntityManager.ComponentFactory))
                continue;
            mutationNames.Add((Loc.GetString(mutationSlime.BreedName), mutationSlime.Color.ToHex()));
        }
        mutationNames.Sort((left, right) => StringComparer.CurrentCulture.Compare(left.Name, right.Name));

        var mutations = mutationNames.Count == 0
            ? FormattedMessage.EscapeText(Loc.GetString("xenobio-scanner-none"))
            : string.Join(", ", mutationNames.Select(mutation =>
                $"[color={mutation.Color}]{FormattedMessage.EscapeText(mutation.Name)}[/color]"));

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("slime-scanner-examine-slime-description",
            ("color", slime.Comp.Color.ToHex()),
            ("name", FormattedMessage.EscapeText(Loc.GetString(slime.Comp.BreedName)))));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("slime-scanner-examine-slime-mutations",
            ("chance", MathF.Floor(slime.Comp.MutationChance * 100f)),
            ("mutations", mutations)));
        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("slime-scanner-examine-slime-extracts",
            ("num", slime.Comp.ExtractsProduced)));
        Send(args, slime, message);
    }

    private void OnExtractScanned(Entity<SlimeExtractComponent> extract, ref AfterInteractUsingEvent args)
    {
        if (!CanScan(args))
            return;

        if (extract.Comp.Used ||
            !TryComp<ReactiveComponent>(extract, out var reactive) ||
            reactive.Reactions == null)
        {
            Send(args,
                extract,
                FormattedMessage.FromMarkupOrThrow(Loc.GetString("slime-scanner-examine-extract-unreactive")));
            return;
        }

        var reactions = new List<string>();
        foreach (var reaction in reactive.Reactions)
        {
            if (reaction.Reagents == null)
                continue;

            var names = new List<(string Name, string Color)>();
            foreach (var reagent in reaction.Reagents)
            {
                if (_prototypes.TryIndex<ReagentPrototype>(reagent, out var prototype))
                    names.Add((prototype.LocalizedName, prototype.SubstanceColor.ToHex()));
            }

            names.Sort((left, right) => StringComparer.CurrentCulture.Compare(left.Name, right.Name));
            if (names.Count > 0)
                reactions.Add(string.Join(", ", names.Select(reagent =>
                    $"[color={reagent.Color}]{FormattedMessage.EscapeText(reagent.Name)}[/color]")));
        }

        var result = reactions.Count == 0
            ? FormattedMessage.EscapeText(Loc.GetString("xenobio-scanner-none"))
            : string.Join("; ", reactions);
        var message = FormattedMessage.FromMarkupOrThrow(Loc.GetString("slime-scanner-examine-extract",
            ("reagents", result)));
        Send(args, extract, message);
    }

    private bool CanScan(AfterInteractUsingEvent args)
    {
        return !args.Handled && args.CanReach && args.Target != null && HasComp<SlimeScannerComponent>(args.Used);
    }

    private void Send(AfterInteractUsingEvent args, EntityUid target, FormattedMessage message)
    {
        _examine.SendExamineTooltip(args.User, target, message, false, true);
        args.Handled = true;
    }
}
