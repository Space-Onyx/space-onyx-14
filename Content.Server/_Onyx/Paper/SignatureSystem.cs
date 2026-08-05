using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Access.Systems;
using Content.Server.Popups;
using Content.Server._Onyx.Language.Paper;
using Content.Shared._Onyx.Paper;
using Content.Shared.Cloning.Events;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Onyx.Paper;

public sealed partial class SignatureSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private PaperLanguageSystem _paperLanguage = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PaperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<SignatureIdentityComponent, MapInitEvent>(OnSignatureIdentityInit);
        SubscribeLocalEvent<SignatureIdentityComponent, CloningEvent>(OnClone);
    }

    private void OnSignatureIdentityInit(Entity<SignatureIdentityComponent> ent, ref MapInitEvent args)
    {
        if (!string.IsNullOrEmpty(ent.Comp.HandwritingId))
            return;

        ent.Comp.HandwritingId = Guid.NewGuid().ToString("N");
        Dirty(ent);
    }

    private void OnClone(Entity<SignatureIdentityComponent> ent, ref CloningEvent args)
    {
        var clone = EnsureComp<SignatureIdentityComponent>(args.CloneUid);
        clone.HandwritingId = ent.Comp.HandwritingId;
        Dirty(args.CloneUid, clone);
    }

    private void OnGetAltVerbs(Entity<PaperComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not {} pen || !_tags.HasTag(pen, WriteTag))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TrySignPaper(ent, user, pen),
            Text = Loc.GetString("paper-sign-verb"),
            DoContactInteraction = true,
            Priority = 10,
        });
    }

    public bool TrySignPaper(Entity<PaperComponent> paper, EntityUid signer, EntityUid pen)
    {
        var penEvent = new SignAttemptEvent(paper, signer);
        RaiseLocalEvent(pen, ref penEvent);
        if (penEvent.Cancelled)
            return false;

        var paperEvent = new BeingSignedAttemptEvent(paper, signer);
        RaiseLocalEvent(paper, ref paperEvent);
        if (paperEvent.Cancelled)
            return false;

        var name = DetermineSignature(signer);
        var signatureIdentity = EnsureComp<SignatureIdentityComponent>(signer);
        if (string.IsNullOrEmpty(signatureIdentity.HandwritingId))
        {
            signatureIdentity.HandwritingId = Guid.NewGuid().ToString("N");
            Dirty(signer, signatureIdentity);
        }

        var repeatLimit = ParseLimit(paper.Comp.Content, SignRepeatLimitRegex()) ?? 1;
        var totalLimit = ParseLimit(paper.Comp.Content, SignLimitRegex()) ?? int.MaxValue;
        if (paper.Comp.SignedBy.Count >= totalLimit ||
            paper.Comp.SignedBy.Count(signature => signature.SignedName == name) >= repeatLimit)
        {
            _popup.PopupEntity(Loc.GetString("paper-signed-failure", ("target", paper.Owner)), signer, signer,
                PopupType.SmallCaution);
            return false;
        }

        var signature = new SignatureDisplayInfo
        {
            SignedName = name,
            FontId = "Sign",
            FontSize = 16,
            SignColor = Color.DarkSlateGray,
            HandwritingId = signatureIdentity.HandwritingId,
        };
        if (TryComp<SignToolComponent>(pen, out var tool))
        {
            signature.FontId = tool.FontId;
            signature.FontSize = tool.FontSize;
            signature.SignColor = tool.SignColor;
        }

        paper.Comp.SignedBy.Add(signature);
        Dirty(paper);
        _paperLanguage.RefreshViews(paper, cancelEditors: true);

        var other = Loc.GetString("paper-signed-other", ("user", signer), ("target", paper.Owner));
        _popup.PopupEntity(other, signer, Filter.PvsExcept(signer, entityManager: EntityManager), true);
        _popup.PopupEntity(Loc.GetString("paper-signed-self", ("target", paper.Owner)), signer, signer);
        _audio.PlayPvs(paper.Comp.Sound, signer);

        var successful = new SignSuccessfulEvent(paper, signer);
        RaiseLocalEvent(paper, ref successful);
        return true;
    }

    private string DetermineSignature(EntityUid signer)
    {
        return _idCard.TryFindIdCard(signer, out var id) && !string.IsNullOrWhiteSpace(id.Comp.FullName)
            ? id.Comp.FullName
            : Name(signer);
    }

    private static int? ParseLimit(string content, Regex regex)
    {
        var match = regex.Match(content);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    [GeneratedRegex(@"(?:<|\[)\s*sign_repeat_limit\s*=\s*(\d+)\s*(?:>|\])", RegexOptions.IgnoreCase)]
    private static partial Regex SignRepeatLimitRegex();

    [GeneratedRegex(@"(?:<|\[)\s*sign_limit\s*=\s*(\d+)\s*(?:>|\])", RegexOptions.IgnoreCase)]
    private static partial Regex SignLimitRegex();

}
