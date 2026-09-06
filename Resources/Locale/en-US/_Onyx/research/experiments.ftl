# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

research-experiment-ui-task = { $goal }: { $progress }/{ $target }
research-experiment-ui-empty = No compatible experiments.
research-experiment-ui-status-active = Active
research-experiment-ui-status-locked = Locked
research-experiment-ui-status-completed = Completed
research-experiment-network-completed = { $user } completed experiment "{ $experiment }".

research-experiment-uranium-name = Uranium analysis
research-experiment-uranium-description = Scan uranium ore, refined uranium, or a sample containing uranium.
research-experiment-uranium-goal = Record a uranium sample

research-experiment-ore-classification-name = Ore classification
research-experiment-ore-classification-description = Scan three different types of unrefined ore.
research-experiment-ore-classification-goal = Record different ore samples

research-experiment-explosive-yield-name = Explosive yield analysis
research-experiment-explosive-yield-description = Scan an explosive device with a measurable yield.
research-experiment-explosive-yield-goal = Record a viable explosive device

research-experiment-anomaly-core-name = Anomaly core analysis
research-experiment-anomaly-core-description = Scan an anomaly core.
research-experiment-anomaly-core-goal = Record an anomaly core

research-experiment-cyborg-architecture-name = Cyborg architecture analysis
research-experiment-cyborg-architecture-description = Scan a functioning cyborg chassis.
research-experiment-cyborg-architecture-goal = Record a cyborg chassis

research-experiment-cryoxadone-purity-name = Cryoxadone purity analysis
research-experiment-cryoxadone-purity-description = Scan a sample containing at least 90% cryoxadone.
research-experiment-cryoxadone-purity-goal = Record a high-purity cryoxadone sample

research-experiment-seed-diversity-name = Seed diversity analysis
research-experiment-seed-diversity-description = Scan three different types of seed packets.
research-experiment-seed-diversity-goal = Record different seed packets

research-experiment-scanner-window-title = Experiment Scanner
research-experiment-scanner-no-server = No R&D server connected.
research-experiment-scanner-no-compatible = No active experiments support this scanner.
research-experiment-scanner-no-match = Target does not match an active experiment.
research-experiment-scanner-duplicate = Target was already recorded.
research-experiment-scanner-success = Recorded { $target }.

research-experiment-machine-window-title = Experimental Analyzer
research-experiment-machine-run = Run analysis
research-experiment-machine-samples-title = Samples
research-experiment-machine-samples-hint = Leave loose unanchored items on the analyzer pad, then run the analysis. Samples are not destroyed.
research-experiment-machine-experiments-title = Experiment catalog
research-experiment-machine-status = Status: { $status }
research-experiment-machine-status-idle = ready
research-experiment-machine-status-processing = analyzing samples…
research-experiment-machine-busy = The analyzer is already running.
research-experiment-machine-no-samples = No samples on the analyzer pad.
research-experiment-machine-processing = Analyzing { $count } samples…
research-experiment-machine-progressed = Sample data recorded, experiment progress updated.
research-experiment-machine-completed = Experiments completed: { $count }.

ent-ResearchExperimentScanner = experiment scanner
    .desc = A handheld scanner for recording R&D experiment samples.
ent-ResearchExperimentMachine = experimental analyzer
    .desc = Analyzes samples for active R&D experiments without destroying them.
ent-ResearchExperimentMachineCircuitboard = experimental analyzer machine board
    .desc = A machine printed circuit board for the experimental analyzer.
