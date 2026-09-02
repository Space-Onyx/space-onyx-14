neuro-interface-ui-chip = Processing chip
neuro-interface-ui-cache = Neuromorphic cache
neuro-interface-ui-expansion-module = Expansion module
neuro-interface-ui-status-online = Stable link
neuro-interface-ui-status-throttled = Reduced output
neuro-interface-ui-status-offline = Link unavailable
neuro-interface-ui-status-disabled = Manually disconnected
neuro-interface-ui-status-emp = Signal lost: EMP

neuro-interface-title = Neural network
neuro-interface-heading = Neuro-interface
neuro-interface-subheading = Link to installed augmentations
neuro-interface-current-mode = Mode: { $mode }
neuro-interface-mode-short = { $mode }
neuro-interface-bandwidth-label = Link capacity
neuro-interface-bandwidth-value = { $value } units
neuro-interface-demand-label = Neural load
neuro-interface-demand-value = { $value } units
neuro-interface-channels-value = channels: { $current } / { $max }
neuro-interface-overload-label = Over capacity
neuro-interface-overload-value = { $value } units
neuro-interface-slot-chip = Neurochip: { $name }
neuro-interface-slot-cache = Neuromorphic cache: { $name }
neuro-interface-slot-empty = not installed
neuro-interface-mode-heading = Overload response
neuro-interface-mode-throttle = Safe throttling
neuro-interface-mode-overclock = Forced operation
neuro-interface-mode-hint = Safe mode reduces output on lower-priority links. Forced operation preserves output at the cost of neural and interface damage.
neuro-interface-module-telemetry = load { $demand } u · power { $power } W · output { $efficiency }%
neuro-interface-priority-down = -
neuro-interface-priority-down-tooltip = Lower priority. This augmentation disconnects sooner when channels are scarce.
neuro-interface-priority-value = { $value }
neuro-interface-priority-up = +
neuro-interface-priority-up-tooltip = Raise priority. This augmentation keeps its channel ahead of lower-priority devices.

neuro-interface-nav-overview = Network overview
neuro-interface-nav-hardware = Hardware
neuro-interface-nav-augments = Augmentations
neuro-interface-overview-subtitle = Neural-bus load and overload response
neuro-interface-hardware-subtitle = Core components and installed expansions
neuro-interface-augments-subtitle = Search and control linked channels
neuro-interface-components-heading = Core components
neuro-interface-extensions-heading = Expansion modules
neuro-interface-extensions-empty = No expansion modules installed.
neuro-interface-extension-entry = • { $name }
neuro-interface-module-connect = Connect link
neuro-interface-module-disconnect = Disconnect link

neuro-interface-region-head = Head
neuro-interface-region-chest = Torso
neuro-interface-region-groin = Groin
neuro-interface-region-leftarm = Left arm
neuro-interface-region-rightarm = Right arm
neuro-interface-region-lefthand = Left hand
neuro-interface-region-righthand = Right hand
neuro-interface-region-leftleg = Left leg
neuro-interface-region-rightleg = Right leg
neuro-interface-region-leftfoot = Left foot
neuro-interface-region-rightfoot = Right foot
neuro-interface-region-other = Other nodes
neuro-interface-region-all = All regions
neuro-interface-region-header = { $region } · { $count }
neuro-interface-search = Search augmentation...
neuro-interface-augment-count = Found: { $count }
neuro-interface-augments-empty = No matching augmentations found.
neuro-interface-button-enable = Enable
neuro-interface-button-disable = Disable
neuro-interface-entry-brief = load { $load } u · output { $efficiency }%
neuro-interface-entry-tooltip = { $name }
    Status: { $status }
    Neural load: { $demand } u
    Power: { $power } W
    Output: { $efficiency }%
    Priority: { $priority }

neuro-interface-examine-base-bandwidth = Native bus capacity: [color=lightblue]{ $bandwidth } units[/color].
neuro-interface-examine-total-bandwidth = Installed components provide [color=cyan]{ $bandwidth } units[/color] total.
neuro-interface-examine-channels = Simultaneous channels available: [color=cyan]{ $channels }[/color].
neuro-interface-examine-expansion-modules = Expansion modules occupied: [color=lightblue]{ $count }[/color].
neuro-interface-examine-chip = Provides [color=cyan]{ $bandwidth } units[/color] of neural capacity and [color=cyan]{ $channels }[/color] channels.
neuro-interface-examine-cache = Stores [color=cyan]{ $channels }[/color] additional active augmentation contexts.
neuro-interface-power-heading = Augmentation power network
neuro-interface-power-balance = +{ $generation } / −{ $consumption } W
neuro-interface-power-sources-empty = No active charging sources detected.
neuro-interface-power-source-entry = Source: { $source }
neuro-interface-batteries-empty = No batteries installed.
neuro-interface-battery-values = { $charge } / { $capacity } J · { $percent }% · { $rate } W
neuro-interface-examine-module = This is a neuro-interface [color=lightblue]expansion module[/color].
neuro-interface-examine-emp-protection = Suppresses EMP strength by [color=cyan]{ $strength }%[/color] and interference duration by [color=cyan]{ $duration }%[/color].
neuro-interface-examine-consumer = Requires [color=cyan]{ $demand } units[/color] of neural link; steady draw is [color=lightblue]{ $power } W[/color].
neuro-interface-examine-scalable = Its output [color=yellow]scales smoothly[/color] when link capacity is limited.
neuro-interface-examine-binary = It requires a [color=yellow]full link[/color] to operate.

neuro-interface-tooltip-current-mode = Controls behavior when resources run short: safely limit some augmentations or keep them running at the cost of overload.
neuro-interface-tooltip-neuro-limit = The maximum combined signal complexity the neuro-interface can process without overload.
neuro-interface-tooltip-neuro-load = Processing capacity currently requested by linked augmentations. The lower line shows occupied and available channels.
neuro-interface-tooltip-overload = Load above the safe limit. Safe mode restricts it, while forced operation damages the brain and interface.
neuro-interface-tooltip-power-network = The shared augmentation power network. Plus is current generation and minus is consumption. Sources and batteries are listed below.
neuro-interface-tooltip-chip = The main processing component. It increases both the neural limit and the number of augmentations supported at once.
neuro-interface-tooltip-cache = Stores ready control states and adds simultaneous channels without increasing the neural limit.
neuro-interface-tooltip-region-filter = Shows only augmentations installed in the selected body region.
neuro-interface-tooltip-augment-count = Number of augmentations matching the current search and filter.
neuro-interface-tooltip-battery-values = Current charge, capacity, percentage and energy flow. Positive flow charges the battery; negative flow drains it.
neuro-interface-tooltip-entry-brief = Load is the signal complexity of this augmentation. Output shows how fully it is currently operating.
neuro-interface-tooltip-priority = When channels are scarce, augmentations with a larger number keep their connection ahead of lower-priority devices.
