neuro-interface-ui-chip = Neurochip
neuro-interface-ui-cache = Cache
neuro-interface-ui-router = Router
neuro-interface-ui-status-online = Online
neuro-interface-ui-status-throttled = Limited
neuro-interface-ui-status-offline = Offline
neuro-interface-ui-status-disabled = Disabled
neuro-interface-ui-status-emp = EMP

neuro-interface-title = Neuro-interface
neuro-interface-heading = Neuro-interface
neuro-interface-mode-short = { $mode }
neuro-interface-demand-label = Neural load
neuro-interface-demand-value = { $current } / { $max } units
neuro-interface-channels-value = channels: { $current } / { $max }
neuro-interface-overload-label = Overload
neuro-interface-overload-value = { $value } units
neuro-interface-channel-overload-value = excess channels: { $value }
neuro-interface-slot-chip = Neurochip: { $name }
neuro-interface-slot-cache = Cache: { $name }
neuro-interface-slot-router = Router: { $name }
neuro-interface-slot-empty = none
neuro-interface-mode-heading = Overload mode
neuro-interface-mode-throttle = Limit
neuro-interface-mode-throttle-tooltip = Excess load is diverted into output limits. Neural tissue remains protected.
neuro-interface-mode-overclock = Force
neuro-interface-mode-overclock-tooltip = Output limits are removed. Overload damages neural tissue and interface.

neuro-interface-nav-overview = Network
neuro-interface-nav-hardware = Hardware
neuro-interface-nav-augments = Augmentations
neuro-interface-overview-subtitle = Load, power and augmentations
neuro-interface-hardware-subtitle = Chip, cache, router and modules
neuro-interface-augments-subtitle = Search and control
neuro-interface-components-heading = Core modules
neuro-interface-extensions-heading = Extensions
neuro-interface-extensions-empty = No extensions.
neuro-interface-extension-entry = • { $name }
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
neuro-interface-region-other = Other
neuro-interface-region-all = All regions
neuro-interface-region-header = { $region } · { $count }
neuro-interface-search = Search...
neuro-interface-augment-count = { $count } shown
neuro-interface-augments-empty = Nothing found.
neuro-interface-button-enable = Enable
neuro-interface-button-disable = Disable
neuro-interface-behavior-scalable = Output scales down smoothly.
neuro-interface-behavior-binary = Requires a full channel.
neuro-interface-tooltip-section-resources = Channel parameters
neuro-interface-tooltip-resource-load = Neural load: { $value } u
neuro-interface-tooltip-resource-power = Draw: { $value } W
neuro-interface-tooltip-resource-output = Output: { $value }%
neuro-interface-tooltip-section-behavior = Channel type
neuro-interface-tooltip-section-integrated-item = Integrated item
neuro-interface-tooltip-item-power-cost = Extend: { $extend } J. Retract: { $retract } J.
neuro-interface-tooltip-item-no-power = Self-powered actuator.
neuro-interface-tooltip-section-tool-panel = Tool panel
neuro-interface-tooltip-tool-panel-power = Tool switch: { $value } J.
neuro-interface-tooltip-tool-panel-no-power = Self-powered selector.
neuro-interface-tooltip-section-tool-panel-contents = Tools
neuro-interface-tooltip-section-effect = Actuator output
neuro-interface-tooltip-strength-effect = Strike amplification: +{ $value }%.
neuro-interface-tooltip-section-generation = Power
neuro-interface-tooltip-reactor-generation = Output: { $value } W.
neuro-interface-tooltip-reactor-hunger = Nutrient draw: { $value } units/J.

neuro-interface-examine-base-bandwidth = Native bus capacity: [color=lightblue]{ $bandwidth } units[/color].
neuro-interface-examine-total-bandwidth = Installed components provide [color=cyan]{ $bandwidth } units[/color] total.
neuro-interface-examine-channels = Simultaneous channels available: [color=cyan]{ $channels }[/color].
neuro-interface-examine-expansion-modules = Expansion modules occupied: [color=lightblue]{ $count }[/color].
neuro-interface-examine-chip = Provides [color=cyan]{ $bandwidth } units[/color] of neural capacity and [color=cyan]{ $channels }[/color] channels.
neuro-interface-examine-cache = Stores [color=cyan]{ $channels }[/color] additional active augmentation contexts.
neuro-interface-examine-router = Maintains a strict queue of [color=cyan]{ $capacity }[/color] augmentations.
neuro-interface-chip-effect = +{ $bandwidth } limit · +{ $channels } channels
neuro-interface-cache-effect = +{ $channels } channels
neuro-interface-router-effect = Queue: { $current } / { $capacity }
neuro-interface-router-effect-missing = No router installed
neuro-interface-power-heading = Power
neuro-interface-power-balance = +{ $generation } / −{ $consumption } W
neuro-interface-power-sources-empty = No sources.
neuro-interface-power-source-entry = Source: { $source }
neuro-interface-batteries-empty = No batteries.
neuro-interface-battery-values = { $charge } / { $capacity } J · { $percent }% · { $rate } W
neuro-interface-examine-consumer = Requires [color=cyan]{ $demand } units[/color] of neural link; steady draw is [color=lightblue]{ $power } W[/color].
neuro-interface-examine-scalable = Its output [color=yellow]scales smoothly[/color] when link capacity is limited.
neuro-interface-examine-binary = It requires a [color=yellow]full link[/color] to operate.

neuro-interface-tooltip-current-mode = Active overload protocol.
neuro-interface-tooltip-neuro-load = Bus load and occupied channels.
neuro-interface-tooltip-overload = Excess load and channels.
neuro-interface-tooltip-power-network = Shared augmentation power grid.
neuro-interface-tooltip-chip = Expands neural limit and channel count.
neuro-interface-tooltip-cache = Adds active channels.
neuro-interface-tooltip-router = Controls channel priority.
neuro-interface-routing-position = Queue: No. { $position }
neuro-interface-routing-auto = Auto
neuro-interface-routing-add = Queue
neuro-interface-routing-remove = Auto
neuro-interface-routing-up-tooltip = Raise priority.
neuro-interface-routing-down-tooltip = Lower priority.
neuro-interface-routing-toggle-tooltip = Manual or automatic priority.
neuro-interface-routing-router-required = No router installed.
neuro-interface-routing-queue-full = No free routing channels.
neuro-interface-tooltip-routing = Manual resource priority queue.
neuro-interface-tooltip-region-filter = Filter by implant location.
neuro-interface-tooltip-augment-count = Nodes listed.
neuro-interface-tooltip-battery-values = Charge, capacity and power flow.
