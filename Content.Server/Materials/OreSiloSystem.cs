using Content.Server.Pinpointer;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Player;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed partial class OreSiloSystem : SharedOreSiloSystem
{
    // <Onyx-MaterialSiloDeviceLink-edited>
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;
    // </Onyx-MaterialSiloDeviceLink-edited>

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen

    // <Onyx-MaterialSiloDeviceLink-edited>
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    // </Onyx-MaterialSiloDeviceLink-edited>
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientInformation.Clear();

        var xform = Transform(ent);
        // <Onyx-MaterialSiloDeviceLink-edited>
        // Connections are created with a multitool. The silo UI only lists linked clients for convenient removal.
        foreach (var client in ent.Comp.Clients)
        {
            if (Deleted(client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }
        // </Onyx-MaterialSiloDeviceLink-edited>

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out _, out var actorComp, out var actorXform))
        {
            _silosToAdd.Clear();
            _silosToRemove.Clear();

            var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
            while (clientQuery.MoveNext(out _, out var clientComp, out var clientXform))
            {
                if (clientComp.Silo == null)
                    continue;

                // We limit it to same-grid checks only for peak perf
                if (actorXform.GridUid != clientXform.GridUid)
                    continue;

                if ((actorXform.LocalPosition - clientXform.LocalPosition).LengthSquared() <= OreSiloPreloadRangeSquared)
                {
                    _silosToAdd.Add(clientComp.Silo.Value);
                }
                else
                {
                    _silosToRemove.Add(clientComp.Silo.Value);
                }
            }

            foreach (var toRemove in _silosToRemove)
            {
                _pvsOverride.RemoveSessionOverride(toRemove, actorComp.PlayerSession);
            }
            foreach (var toAdd in _silosToAdd)
            {
                _pvsOverride.AddSessionOverride(toAdd, actorComp.PlayerSession);
            }
        }
    }
}
