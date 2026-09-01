using System.Linq;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared._Onyx.Medical.Surgery;
using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.Damage.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Medical.Surgery;

public sealed partial class BodyScannerSystem : EntitySystem
{
    private static readonly ProtoId<SinkPortPrototype> ReceiverPort = "BodyScannerReceiver";
    [Dependency] private HealthAnalyzerSystem _healthAnalyzer = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OperatingTableComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<OperatingTableComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<BodyScannerComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<BodyScannerComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<BodyScannerComponent, PortDisconnectedEvent>(OnDisconnected);
        SubscribeLocalEvent<BodyScannerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyScannerComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<BodyScannerComponent, ComponentShutdown>(OnScannerShutdown);
        SubscribeLocalEvent<OperatingTableComponent, ComponentShutdown>(OnTableShutdown);
    }

    private void OnLinkAttempt(Entity<BodyScannerComponent> scanner, ref LinkAttemptEvent args)
    {
        if (args.Source != scanner.Owner || args.SourcePort != BodyScannerComponent.LinkingPort ||
            args.SinkPort != ReceiverPort ||
            scanner.Comp.Table != null ||
            !TryComp(args.Sink, out OperatingTableComponent? table) || table.Scanner != null)
            args.Cancel();
    }

    private void OnNewLink(Entity<BodyScannerComponent> scanner, ref NewLinkEvent args)
    {
        if (args.Source != scanner.Owner || args.SourcePort != BodyScannerComponent.LinkingPort ||
            args.SinkPort != ReceiverPort ||
            !TryComp(args.Sink, out OperatingTableComponent? table) ||
            !TryComp(args.Sink, out StrapComponent? _) ||
            table.Scanner is { } other && other != scanner.Owner)
            return;

        scanner.Comp.Table = args.Sink;
        table.Scanner = scanner.Owner;
        SyncPatient(scanner);
    }

    private void OnDisconnected(Entity<BodyScannerComponent> scanner, ref PortDisconnectedEvent args)
    {
        if (args.Port == BodyScannerComponent.LinkingPort)
            ClearScanner(scanner);
    }

    private void OnStrapped(Entity<OperatingTableComponent> table, ref StrappedEvent args)
    {
        if (table.Comp.Scanner is { } scanner && TryComp(scanner, out BodyScannerComponent? scannerComp))
            SyncPatient((scanner, scannerComp));
    }

    private void OnUnstrapped(Entity<OperatingTableComponent> table, ref UnstrappedEvent args)
    {
        if (table.Comp.Scanner is { } scanner && TryComp(scanner, out BodyScannerComponent? scannerComp))
            SyncPatient((scanner, scannerComp));
    }

    private void OnMapInit(Entity<BodyScannerComponent> scanner, ref MapInitEvent args)
    {
        if (!TryComp(scanner, out DeviceLinkSourceComponent? source))
            return;

        EntityUid? linkedTable = null;
        foreach (var (sink, links) in source.LinkedPorts)
        {
            if (!links.Contains((BodyScannerComponent.LinkingPort, ReceiverPort)) ||
                !TryComp(sink, out OperatingTableComponent? table) || table.Scanner is { } other && other != scanner.Owner)
                continue;

            if (linkedTable != null)
            {
                SyncPatient(scanner);
                return;
            }

            linkedTable = sink;
        }

        if (linkedTable is { } tableUid && TryComp(tableUid, out OperatingTableComponent? tableComp))
        {
            scanner.Comp.Table = tableUid;
            tableComp.Scanner = scanner.Owner;
        }

        SyncPatient(scanner);
    }

    private void OnUiOpened(Entity<BodyScannerComponent> scanner, ref AfterActivatableUIOpenEvent args) =>
        SyncPatient(scanner);

    private void OnScannerShutdown(Entity<BodyScannerComponent> scanner, ref ComponentShutdown args) =>
        ClearScanner(scanner);

    private void OnTableShutdown(Entity<OperatingTableComponent> table, ref ComponentShutdown args)
    {
        if (table.Comp.Scanner is { } scanner && TryComp(scanner, out BodyScannerComponent? scannerComp))
            ClearScanner((scanner, scannerComp));
    }

    private void ClearScanner(Entity<BodyScannerComponent> scanner)
    {
        if (TryComp(scanner, out HealthAnalyzerComponent? analyzer))
            _healthAnalyzer.ClearAnalyzedEntity((scanner, analyzer));

        if (scanner.Comp.Table is { } tableUid && TryComp(tableUid, out OperatingTableComponent? table) &&
            table.Scanner == scanner.Owner)
            table.Scanner = null;

        scanner.Comp.Table = null;
    }

    private void SyncPatient(Entity<BodyScannerComponent> scanner)
    {
        if (!TryComp(scanner, out HealthAnalyzerComponent? analyzer))
            return;

        if (scanner.Comp.Table is not { } table || !TryComp(table, out StrapComponent? strap) ||
            strap.BuckledEntities.Count != 1)
        {
            _healthAnalyzer.ClearAnalyzedEntity((scanner, analyzer));
            return;
        }

        var patient = strap.BuckledEntities.First();
        if (!HasComp<DamageableComponent>(patient))
        {
            _healthAnalyzer.ClearAnalyzedEntity((scanner, analyzer));
            return;
        }

        if (analyzer.ScannedEntity != patient)
            _healthAnalyzer.BeginAnalyzingEntity((scanner, analyzer), patient);
        else
            _healthAnalyzer.UpdateScannedUser(scanner, patient, true);
    }
}
