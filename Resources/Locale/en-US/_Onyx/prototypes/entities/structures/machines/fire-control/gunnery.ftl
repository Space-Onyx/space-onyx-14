gunnery-window-title = Gunnery Control
gunnery-window-disconnected = DISCONNECTED
gunnery-window-connected = CONNECTED
gunnery-select-all = Select All
gunnery-unselect-all = Unselect All
gunnery-guns = Guns
gunnery-server-examine-detail = The server is using [color={$valueColor}]{$usedProcessingPower}/{$processingPower}[/color] of its processing power.
gunnery-select-ballistics = Ballistics
gunnery-select-energy = Energy
gunnery-select-missiles = Missiles
gunnery-select-mining = Mining
gunner-console-display-label = Display
ship-gun-class-component-examine-detail = This weapon costs [color=yellow]{ $processingPower }[/color] processing power to control.

ent-GunneryServerBase = gunnery control server
    .desc = Manages the remote operation of ship weapons.
ent-GunneryServerLow = low-power gunnery control server
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerMedium = medium-power gunnery control server
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerHigh = high-power gunnery control server
    .desc = { ent-GunneryServerBase.desc }
ent-GunneryServerUltra = ultra-high-power gunnery control server
    .desc = { ent-GunneryServerBase.desc }
ent-ComputerGunneryConsole = gunnery control console
    .desc = Interfaces with the gunnery control server to operate ship weapons.

ent-GunneryControlComputerCircuitboard = gunnery control computer board
    .desc = A computer printed circuit board for a gunnery control computer.
ent-MachineGCSLowCircuitboard = low-power gunnery control server board
    .desc = A machine board for a GCS.
ent-MachineGCSMediumCircuitboard = medium-power gunnery control server board
    .desc = A machine board for a GCS.
ent-MachineGCSHighCircuitboard = high-power gunnery control server board
    .desc = A machine board for a GCS.
ent-MachineGCSUltraCircuitboard = ultra-high-power gunnery control server board
    .desc = A machine board for a GCS.
