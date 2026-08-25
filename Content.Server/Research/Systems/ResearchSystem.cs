using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Robust.Shared.Random; // <Onyx-ResearchNetworks>

namespace Content.Server.Research.Systems
{
    [UsedImplicitly]
    public sealed partial class ResearchSystem : SharedResearchSystem
    {
        [Dependency] private IAdminLogManager _adminLog = default!;
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private AccessReaderSystem _accessReader = default!;
        [Dependency] private SharedIdCardSystem _idCard = default!; // <Onyx-ResearchNetworks>
        [Dependency] private EntityLookupSystem _lookup = default!;
        [Dependency] private UserInterfaceSystem _uiSystem = default!;
        [Dependency] private SharedPopupSystem _popup = default!;
        [Dependency] private RadioSystem _radio = default!;
        [Dependency] private IRobustRandom _random = default!; // <Onyx-ResearchNetworks>

        public override void Initialize()
        {
            base.Initialize();
            InitializeClient();
            InitializeConsole();
            InitializeSource();
            InitializeServer();

            SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchRegistrationChangedEvent>(OnDatabaseRegistrationChanged);
        }

        /// <summary>
        /// Gets a server based on its unique numeric id.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="id"></param>
        /// <param name="serverUid"></param>
        /// <param name="serverComponent"></param>
        /// <returns></returns>
        public bool TryGetServerById(EntityUid client, int id, [NotNullWhen(true)] out EntityUid? serverUid, [NotNullWhen(true)] out ResearchServerComponent? serverComponent)
        {
            serverUid = null;
            serverComponent = null;

            var query = GetServers(client);
            foreach (var (uid, server) in query)
            {
                if (server.Id != id)
                    continue;
                serverUid = uid;
                serverComponent = server;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the names of all the servers.
        /// </summary>
        /// <returns></returns>
        public string[] GetServerNames(EntityUid client)
        {
            return GetServers(client).OrderBy(x => x.Comp.Id).Select(x => x.Comp.ServerName).ToArray(); // <Onyx-ResearchNetworks-edited>
        }

        /// <summary>
        /// Gets the ids of all the servers
        /// </summary>
        /// <returns></returns>
        public int[] GetServerIds(EntityUid client)
        {
            return GetServers(client).OrderBy(x => x.Comp.Id).Select(x => x.Comp.Id).ToArray(); // <Onyx-ResearchNetworks-edited>
        }

        public HashSet<Entity<ResearchServerComponent>> GetServers(EntityUid client)
        {
            var clientXform = Transform(client);
            if (clientXform.GridUid is not { } grid)
                return [];

            var set = new HashSet<Entity<ResearchServerComponent>>();
            _lookup.GetGridEntities(grid, set);
            return set;
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<ResearchServerComponent>();
            while (query.MoveNext(out var uid, out var server))
            {
                // <Onyx-ResearchNetworks>
                if (!IsNetworkAuthority(uid, server))
                    continue;
                MoveNetworkClientsToAuthority(uid, server);
                // </Onyx-ResearchNetworks>
                if (server.NextUpdateTime > _timing.CurTime)
                    continue;
                server.NextUpdateTime = _timing.CurTime + server.ResearchConsoleUpdateTime;

                UpdateServer(uid, (int) server.ResearchConsoleUpdateTime.TotalSeconds, server);
            }
        }
    }
}
