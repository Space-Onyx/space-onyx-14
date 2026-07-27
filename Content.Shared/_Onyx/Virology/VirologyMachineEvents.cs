namespace Content.Shared._Onyx.Virology;

public sealed class VirologyMachineCheckEvent : EntityEventArgs { public bool Cancelled; }
public sealed class VirologyMachineDoneEvent(bool success) : EntityEventArgs { public bool Success { get; } = success; }
