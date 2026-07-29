using System.Linq;
using Content.Server.Administration;
using Content.Shared._Onyx.CollectiveMind;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Onyx.Chat.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Fun)]
public sealed class CollectiveMindCommand : ToolshedCommand
{
    private CollectiveMindSystem? _system;

    [CommandImplementation("add")]
    public EntityUid Add([PipedArgument] EntityUid entity, ProtoId<CollectiveMindPrototype> channel)
    {
        _system ??= GetSys<CollectiveMindSystem>();
        _system.Grant(entity, channel);
        return entity;
    }

    [CommandImplementation("add")]
    public IEnumerable<EntityUid> Add(
        [PipedArgument] IEnumerable<EntityUid> entities,
        ProtoId<CollectiveMindPrototype> channel)
        => entities.Select(entity => Add(entity, channel));

    [CommandImplementation("remove")]
    public EntityUid Remove([PipedArgument] EntityUid entity, ProtoId<CollectiveMindPrototype> channel)
    {
        _system ??= GetSys<CollectiveMindSystem>();
        _system.Remove(entity, channel);
        return entity;
    }

    [CommandImplementation("remove")]
    public IEnumerable<EntityUid> Remove(
        [PipedArgument] IEnumerable<EntityUid> entities,
        ProtoId<CollectiveMindPrototype> channel)
        => entities.Select(entity => Remove(entity, channel));
}
