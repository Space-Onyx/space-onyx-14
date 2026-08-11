using Content.Server.Chat.Managers;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Onyx.AlternativeJobs;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Configuration;

namespace Content.Server._Onyx.AlternativeJobs;

public sealed partial class AlternativeJobSystem : EntitySystem
{
    [Dependency] private  IPrototypeManager _prototypeManager = default!;
    [Dependency] private  IChatManager _chat = default!;
    [Dependency] private  SharedIdCardSystem _idCard = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is null || args.Profile is null ||
            !TryComp<ActorComponent>(args.Mob, out var actor) ||
            !TryGetAlternativeJob(args.JobId, args.Profile, actor.PlayerSession, out var alternative) ||
            !_idCard.TryFindIdCard(args.Mob, out var idCard))
            return;

        _idCard.TryChangeJobTitle(idCard, alternative.LocalizedJobName);
        if (alternative.JobIconProtoId is { } iconId && _prototypeManager.TryIndex(iconId, out var icon))
            _idCard.TryChangeJobIcon(idCard, icon);

        if (_prototypeManager.TryIndex<JobPrototype>(alternative.ParentJobId, out var parent))
        {
            _chat.DispatchServerMessage(actor.PlayerSession,
                Loc.GetString("alternative-job-notify", ("newJobName", alternative.LocalizedJobName), ("parentJobName", parent.LocalizedName)));
        }
    }

    public bool TryGetAlternativeJob(ProtoId<JobPrototype> parentJobId, HumanoidCharacterProfile profile,
        ICommonSession player,
        out AlternativeJobPrototype alternative)
    {
        if (profile.JobAlternatives.TryGetValue(parentJobId, out var alternativeId) &&
            _prototypeManager.TryIndex(alternativeId, out AlternativeJobPrototype? resolved) &&
            resolved.ParentJobId == parentJobId &&
            (!_configuration.GetCVar(CCVars.GameRoleTimers) ||
             JobRequirements.TryRequirementsMet(resolved.Requirements, _playTime.GetPlayTimes(player),
                 out _, EntityManager, _prototypeManager, profile)))
        {
            alternative = resolved;
            return true;
        }

        alternative = default!;
        return false;
    }
}
