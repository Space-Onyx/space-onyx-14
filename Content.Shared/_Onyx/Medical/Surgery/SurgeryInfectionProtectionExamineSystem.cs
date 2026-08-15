using Content.Shared.Examine;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._Onyx.Medical.Surgery;

public sealed partial class SurgeryInfectionProtectionExamineSystem : EntitySystem
{
    private const string InfectionIcon = "/Textures/Clothing/Mask/sterile.rsi/icon.png";

    [Dependency] private ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryInfectionProtectionComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<SurgeryInfectionProtectionComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var protection = MathF.Round((1f - ent.Comp.ChanceMultiplier) * 100f);
        var tier = protection switch
        {
            >= 100f => "full",
            >= 60f => "high",
            >= 30f => "medium",
            _ => "low",
        };

        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("surgery-infection-protection-examine",
            ("tier", tier), ("protection", protection)));

        _examine.AddDetailedExamineVerb(
            args,
            ent.Comp,
            message,
            Loc.GetString("surgery-infection-protection-examine-verb-text"),
            InfectionIcon,
            Loc.GetString("surgery-infection-protection-examine-verb-message"));
    }
}