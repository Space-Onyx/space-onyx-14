ent-ComputerResearchServerControl = R&D server control console
    .desc = Monitors local R&D networks and controls their server generation.
ent-ResearchServerControlComputerCircuitboard = R&D server control console board
    .desc = A computer printed circuit board for an R&D server control console.

research-server-control-title = R&D server control
research-server-control-servers = Local R&D networks
research-server-control-logs = Network log
research-server-control-network = Network: { $network }
research-server-control-server-name = [{ $id }] { $name }
research-server-control-authority = Network authority
research-server-control-forwarded = Forwards operations to server [{ $authorityId }]
research-server-control-telemetry = Power: { $power }
research-server-control-table-type = Point type
research-server-control-table-generation = Generation
research-server-control-table-generation-value = { $value }/s
research-server-control-table-balance = Network balance
research-server-control-powered = online
research-server-control-unpowered = offline
research-server-control-state-enabled = enabled
research-server-control-state-disabled = disabled
research-server-control-disable-generation = Disable server generation
research-server-control-enable-generation = Enable server generation
research-server-control-configure-network = Configure network
research-server-control-empty = No R&D servers found on this grid.

research-network-log-empty = No network events recorded.
research-network-log-search = Search network log...
research-network-log-user-unknown = unknown
research-network-log-user-with-job = { $name } ({ $job })
research-network-log-server-online = { $server } joined { $network }.
research-network-log-server-offline = { $server } left { $network }.
research-network-log-generation-toggled = { $user } set generation on { $server } to { $state }.
research-network-log-technology-unlocked = { $user } unlocked { $technology }.
research-network-log-network-changed = { $user } moved { $server } from { $oldNetwork } to { $newNetwork }.
research-network-log-network-left = { $user } disconnected { $server } from { $network }.

research-console-network-log-button = Network log
research-console-network-log-title = R&D network log

research-server-network-examine = Server [bold]{ $name }[/bold] | ID: [bold]{ $hash }[/bold]
    Network: [bold]{ $network }[/bold] | { $authority }
    Generation: [bold]{ $generation }[/bold]/s ([bold]{ $state }[/bold]) | Network balance: [bold]{ $points }[/bold]
research-server-network-examine-authority = network authority
research-server-network-examine-forwarded = forwards operations to server [{ $hash }]

research-server-network-title = R&D network settings
research-server-network-server = Server [{ $id }]: { $name }

research-client-server-selection-authority-entry = [{ $id }] { $serverName } | { $network } | authority
research-client-server-selection-follower-entry = [{ $id }] { $serverName } | { $network } | forwarded to [{ $authorityId }]
research-server-network-help = Use an existing ID to join its network, or a new ID to create a separate clean network. Renaming a single-server network keeps its progress. Allowed: A-Z, 0-9, - and _.
research-server-network-apply = Apply network ID
