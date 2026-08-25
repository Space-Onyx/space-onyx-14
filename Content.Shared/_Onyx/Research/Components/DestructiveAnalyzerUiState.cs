using Content.Shared._Onyx.Research;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research.Components;

[Serializable, NetSerializable]
public enum DestructiveAnalyzerUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class OpenResearchServerMenuMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DestructiveAnalyzerSelectMethodMessage : BoundUserInterfaceMessage
{
    public string MethodId;

    public DestructiveAnalyzerSelectMethodMessage(string methodId)
    {
        MethodId = methodId;
    }
}

[Serializable, NetSerializable]
public sealed class DestructiveAnalyzerRunMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DestructiveAnalyzerEjectMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class DestructiveAnalyzerRequirementState
{
    public string Technology;
    public bool Reveals;
    public int Progress;
    public int Amount;

    public DestructiveAnalyzerRequirementState(string technology, bool reveals, int progress, int amount)
    {
        Technology = technology;
        Reveals = reveals;
        Progress = progress;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class DestructiveAnalyzerBoundInterfaceState : BoundUserInterfaceState
{
    public string? ConnectedServerName;
    public List<ResearchPointAmount> PointBalances;
    public string LastSubject;
    public string LastResult;
    public string? InsertedItem;
    public NetEntity? InsertedItemEntity;
    public string? SelectedMethod;
    public List<string> Methods;
    public Dictionary<string, DestructiveAnalyzerRequirementState> Requirements;

    public DestructiveAnalyzerBoundInterfaceState(string? connectedServerName,
        List<ResearchPointAmount> pointBalances,
        string lastSubject,
        string lastResult,
        string? insertedItem,
        NetEntity? insertedItemEntity,
        string? selectedMethod,
        List<string> methods,
        Dictionary<string, DestructiveAnalyzerRequirementState> requirements)
    {
        ConnectedServerName = connectedServerName;
        PointBalances = pointBalances;
        LastSubject = lastSubject;
        LastResult = lastResult;
        InsertedItem = insertedItem;
        InsertedItemEntity = insertedItemEntity;
        SelectedMethod = selectedMethod;
        Methods = methods;
        Requirements = requirements;
    }
}
