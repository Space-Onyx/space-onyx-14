# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

tiered-machine-part-examine = Its efficiency tier is [color=cyan]{ $tier }[/color].
tiered-machine-part-panel-closed = Open maintenance panel first.
tiered-machine-part-unsupported = This machine cannot use this module.
tiered-machine-part-not-better = Installed module is already at least as efficient.
tiered-machine-part-installed = Tier { $tier } module installed.
tiered-machine-part-installed-examine = Installed { $kind }: [color=cyan]tier { $tier }[/color].
tiered-machine-part-kind-servo = Servo
tiered-machine-part-kind-capacitor = Capacitor
tiered-machine-part-kind-matterbin = Matter Bin
tiered-machine-part-kind-scanner = Scanning Module
tiered-machine-part-kind-laser = Micro-laser
machine-part-exchanger-nothing = No better compatible modules found.
machine-part-exchanger-complete = Machine modules upgraded.
tiered-machine-frame-missing = Missing required module: { $amount }× { $kind }.
machine-upgrade-bluespace-mining-speed = mining speed
machine-upgrade-bluespace-mining-output = material output
machine-upgrade-bluespace-mining-core-life = core lifetime
machine-upgrade-bluespace-mining-stability = instability suppression
machine-upgrade-bluespace-mining-temperature = thermal disturbance suppression

stack-servo-drive = micro-manipulators
stack-matter-recycler = matter bins
stack-capacitor-module = capacitors
stack-scanner-module = scanning modules
stack-laser-module = micro-lasers

ent-StandardServoDrive = micro-manipulator
    .desc = A tiny little manipulator used in the construction of certain devices.
ent-CalibratedServoDrive = nano-manipulator
    .desc = A tiny little manipulator used in the construction of certain devices.
ent-PrecisionServoDrive = pico-manipulator
    .desc = A tiny little manipulator used in the construction of certain devices.
ent-QuantumServoDrive = femto-manipulator
    .desc = A tiny little manipulator used in the construction of certain devices.
ent-StandardMatterRecycler = matter bin
    .desc = A container designed to hold compressed matter awaiting reconstruction.
ent-CalibratedMatterRecycler = advanced matter bin
    .desc = A container designed to hold compressed matter awaiting reconstruction.
ent-PrecisionMatterRecycler = super matter bin
    .desc = A container designed to hold compressed matter awaiting reconstruction.
ent-BluespaceMatterRecycler = bluespace matter bin
    .desc = A container designed to hold compressed matter awaiting reconstruction.
ent-StandardCapacitorModule = capacitor
    .desc = A basic capacitor used in the construction of a variety of devices.
ent-CalibratedCapacitorModule = advanced capacitor
    .desc = An advanced capacitor used in the construction of a variety of devices.
ent-PrecisionCapacitorModule = super capacitor
    .desc = A super-high capacity capacitor used in the construction of a variety of devices.
ent-QuadraticCapacitorModule = quadratic capacitor
    .desc = A quadratic capacitor used in the construction of a variety of devices.
ent-StandardScannerModule = scanning module
    .desc = A compact, high resolution scanning module used in the construction of certain devices.
ent-CalibratedScannerModule = advanced scanning module
    .desc = A compact, high resolution scanning module used in the construction of certain devices.
ent-PhasicScannerModule = phasic scanning module
    .desc = A compact, high resolution phasic scanning module used in the construction of certain devices.
ent-TriphasicScannerModule = triphasic scanning module
    .desc = A compact, ultra resolution triphasic scanning module used in the construction of certain devices.
ent-StandardLaserModule = micro-laser
    .desc = A tiny laser used in certain devices.
ent-FocusedLaserModule = high-power micro-laser
    .desc = A tiny laser used in certain devices.
ent-UltrafineLaserModule = ultra-high-power micro-laser
    .desc = A tiny laser used in certain devices.
ent-QuadphaseLaserModule = quad-ultra micro-laser
    .desc = A tiny laser used in certain devices.
ent-MachinePartExchanger = machine part exchanger
    .desc = A portable tool that stores machine modules and installs better ones through an open maintenance panel.
ent-BluespaceMachinePartExchanger = bluespace machine part exchanger
    .desc = An advanced machine module exchanger capable of remote installation through closed panels.
ent-BorgMachinePartExchanger = integrated machine part exchanger
    .desc = A machine module exchanger designed for cyborg use.
ent-BorgModuleMachineParts = machine parts cyborg module
    .desc = An engineering module containing an integrated machine part exchanger.
