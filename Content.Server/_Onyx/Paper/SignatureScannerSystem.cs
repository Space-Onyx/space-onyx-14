using System.Text;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Onyx.Paper;

public sealed partial class SignatureScannerSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SignatureScannerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<SignatureScannerComponent> scanner, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not {} target)
            return;

        if (!TryComp<PaperComponent>(target, out var paper))
            return;

        PrintReport(scanner, target, paper, args.User);
        args.Handled = true;
    }

    private void PrintReport(Entity<SignatureScannerComponent> scanner, EntityUid paper, PaperComponent paperComp, EntityUid user)
    {
        if (_gameTiming.CurTime < scanner.Comp.PrintReadyAt)
        {
            _audio.PlayPvs(scanner.Comp.SoundPrint, scanner);
            return;
        }

        var printed = Spawn(scanner.Comp.MachineOutput, Transform(scanner).Coordinates);
        _hands.PickupOrDrop(user, printed, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(printed, out var printedPaper))
        {
            Log.Error("Printed paper did not have PaperComponent.");
            return;
        }

        _metaData.SetEntityName(printed, Loc.GetString("signature-scanner-report-title"));

        var text = new StringBuilder();
        foreach (var signature in paperComp.SignedBy)
        {
            text.AppendLine(Loc.GetString("signature-scanner-report-line",
                ("name", signature.SignedName),
                ("handwriting", signature.HandwritingId)));
        }

        if (paperComp.SignedBy.Count == 0)
            text.AppendLine(Loc.GetString("signature-scanner-no-signatures"));

        _paper.SetContent((printed, printedPaper), text.ToString());

        var audioParams = scanner.Comp.SoundPrint.Params;
        audioParams = audioParams.WithVariation(0.25f).AddVolume(3f).WithRolloffFactor(2.8f).WithMaxDistance(4.5f);
        _audio.PlayPvs(scanner.Comp.SoundPrint, scanner, audioParams);

        scanner.Comp.PrintReadyAt = _gameTiming.CurTime + scanner.Comp.PrintCooldown;
    }
}