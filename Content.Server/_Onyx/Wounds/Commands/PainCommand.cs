using System.Diagnostics;
using Content.Server.Administration;
using Content.Shared._Onyx.Targeting;
using Content.Shared._Onyx.Wounds;
using Content.Shared.Administration;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Maths;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Content.Server._Onyx.Wounds.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class PainCommand : ToolshedCommand
{
    private PainSystem? _pain;
    private TargetResolverSystem? _targetResolver;

    [CommandImplementation("add")]
    public EntityUid Add(IInvocationContext ctx, [PipedArgument] EntityUid body, TargetBodyPart target, float amount)
    {
        if (TryGetPart(ctx, body, target, amount, out var part))
            Pain.ChangePain(part, FixedPoint2.New(amount));

        return body;
    }

    [CommandImplementation("rem")]
    public EntityUid Remove(IInvocationContext ctx, [PipedArgument] EntityUid body, TargetBodyPart target, float amount)
    {
        if (TryGetPart(ctx, body, target, amount, out var part))
            Pain.ChangePain(part, -FixedPoint2.New(amount));

        return body;
    }

    [CommandImplementation("set")]
    public EntityUid Set(IInvocationContext ctx, [PipedArgument] EntityUid body, TargetBodyPart target, float amount)
    {
        if (TryGetPart(ctx, body, target, amount, out var part))
            Pain.SetPain(part, FixedPoint2.New(amount));

        return body;
    }

    private PainSystem Pain => _pain ??= GetSys<PainSystem>();
    private TargetResolverSystem TargetResolver => _targetResolver ??= GetSys<TargetResolverSystem>();

    private bool TryGetPart(
        IInvocationContext ctx,
        EntityUid body,
        TargetBodyPart target,
        float amount,
        out EntityUid part)
    {
        part = default;
        if (!HasComp<BodyComponent>(body))
            return ReportError(ctx, Loc.GetString("cmd-pain-no-body", ("entity", body)));

        if (!SharedTargetingSystem.IsSelectable(target))
            return ReportError(ctx, Loc.GetString("cmd-pain-invalid-part", ("part", target)));

        if (!TargetResolver.TryResolveExact(body, target, out part))
            return ReportError(ctx, Loc.GetString("cmd-pain-missing-part", ("entity", body), ("part", target)));

        if (!HasComp<PainComponent>(part))
            return ReportError(ctx, Loc.GetString("cmd-pain-no-component", ("entity", body), ("part", target)));

        if (!float.IsFinite(amount) || amount < 0f)
            return ReportError(ctx, Loc.GetString("cmd-pain-invalid-amount"));

        return true;
    }

    private static bool ReportError(IInvocationContext ctx, string message)
    {
        ctx.ReportError(new PainCommandError(message));
        return false;
    }
}

public record struct PainCommandError(string Message) : IConError
{
    public FormattedMessage DescribeInner() => FormattedMessage.FromUnformatted(Message);

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
