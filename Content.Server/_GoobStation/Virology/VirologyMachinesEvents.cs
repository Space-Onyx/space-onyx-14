namespace Content.Server._GoobStation.Virology;

[ByRefEvent]
public record struct VirologyMachineCheckEvent(bool Cancelled = false);

public record struct VirologyMachineDoneEvent(bool Success);
