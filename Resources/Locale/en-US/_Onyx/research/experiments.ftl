# Space Onyx
# Copyright (C) 2026 Space Onyx contributors
#
# This file is licensed under AGPL-3.0-or-later.
# See LICENSES for the full license text.

research-experiment-ui-task = { $goal }: { $progress }/{ $target }
research-experiment-ui-empty = No compatible experiments.
research-experiment-ui-status-active = Active
research-experiment-ui-status-other-scanner = Use another scanner
research-experiment-ui-status-locked = Locked
research-experiment-ui-status-completed = Completed
research-experiment-ui-details = Scanner: { $source }
    Unlock technology: { $technologies }
    Reward: { $reward }
research-experiment-ui-source-any = handheld or stationary analyzer
research-experiment-ui-source-handheld = handheld scanner only
research-experiment-ui-source-machine = stationary analyzer only
research-experiment-ui-source-console = research console
research-experiment-ui-technology-none = available from shift start
research-experiment-ui-reward-none = no research points
research-experiment-ui-reward-entry = { $amount } { $type }
research-experiment-ui-source-label = Analyzer
research-experiment-ui-technology-label = Unlock
research-experiment-ui-reward-label = Reward
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
research-experiment-cryoxadone-purity-description = Scan a sample containing at least 60 units of cryoxadone at 90% purity or higher.
research-experiment-cryoxadone-purity-goal = Record a high-purity cryoxadone sample

research-experiment-seed-diversity-name = Seed diversity analysis
research-experiment-seed-diversity-description = Scan three different types of seed packets.
research-experiment-seed-diversity-goal = Record different seed packets

research-experiment-tiered-parts-tier2-name = Calibrated Stock Parts Benchmark
research-experiment-tiered-parts-tier2-description = Our newly-designed machinery components require practical application tests for hints at possible further advancements. Scan any machinery with calibrated parts and report the results.
research-experiment-tiered-parts-tier2-goal = Record tier 2 machine parts

research-experiment-tiered-parts-tier3-name = Precision Stock Parts Benchmark
research-experiment-tiered-parts-tier3-description = Our newly-designed machinery components require practical application tests for hints at possible further advancements. Scan any machinery with precision parts and report the results.
research-experiment-tiered-parts-tier3-goal = Record tier 3 machine parts

research-experiment-rare-fish-name = Rare fish survey
research-experiment-rare-fish-description = Record two distinct rare fish species in their habitat.
research-experiment-rare-fish-goal = Record different rare fish

research-experiment-tritium-synthesis-name = Tritium synthesis analysis
research-experiment-tritium-synthesis-description = Analyze a tank or canister containing at least 50 moles of tritium at 90% purity or higher.
research-experiment-tritium-synthesis-goal = Record a synthesized tritium batch

research-experiment-bluespace-crystal-name = Bluespace crystal spectrometry
research-experiment-bluespace-crystal-description = Analyze a synthesized bluespace crystal to validate stable field containment before developing bluespace storage.
research-experiment-bluespace-crystal-goal = Record a synthesized bluespace crystal

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
