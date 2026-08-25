using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Onyx.Research.Admin;

[Serializable, NetSerializable]
public sealed class ResearchTreeEditorEuiState(
    List<ResearchTreeEditorTechnology> technologies,
    List<ResearchTreeEditorDiscipline> disciplines,
    List<ResearchTreeEditorRecipe> recipes,
    List<ResearchTreeEditorIcon> icons,
    string savePath) : EuiStateBase
{
    public List<ResearchTreeEditorTechnology> Technologies = technologies;
    public List<ResearchTreeEditorDiscipline> Disciplines = disciplines;
    public List<ResearchTreeEditorRecipe> Recipes = recipes;
    public List<ResearchTreeEditorIcon> Icons = icons;
    public string SavePath = savePath;
}

[Serializable, NetSerializable]
public sealed class ResearchTreeEditorTechnology(
    string id,
    string originalId,
    string name,
    string icon,
    string discipline,
    int tier,
    int cost,
    bool hidden,
    bool startingTechnology,
    string group,
    int positionX,
    int positionY,
    List<string> prerequisites,
    List<string> recipeUnlocks)
{
    public string Id = id;
    public string OriginalId = originalId;
    public string Name = name;
    public string Icon = icon;
    public string Discipline = discipline;
    public int Tier = tier;
    public int Cost = cost;
    public bool Hidden = hidden;
    public bool StartingTechnology = startingTechnology;
    public string Group = group;
    public int PositionX = positionX;
    public int PositionY = positionY;
    public List<string> Prerequisites = prerequisites;
    public List<string> RecipeUnlocks = recipeUnlocks;
}

[Serializable, NetSerializable]
public sealed class ResearchTreeEditorDiscipline(string id, string name)
{
    public string Id = id;
    public string Name = name;
}

[Serializable, NetSerializable]
public sealed class ResearchTreeEditorRecipe(string id, string name)
{
    public string Id = id;
    public string Name = name;
}

[Serializable, NetSerializable]
public sealed class ResearchTreeEditorIcon(string id, string displayName)
{
    public string Id = id;
    public string DisplayName = displayName;
}

public static class ResearchTreeEditorEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class Save(ResearchTreeEditorTechnology technology) : EuiMessageBase
    {
        public ResearchTreeEditorTechnology Technology = technology;
    }

    [Serializable, NetSerializable]
    public sealed class SaveAll(List<ResearchTreeEditorTechnology> technologies) : EuiMessageBase
    {
        public List<ResearchTreeEditorTechnology> Technologies = technologies;
    }

    [Serializable, NetSerializable]
    public sealed class Delete(string id) : EuiMessageBase
    {
        public string Id = id;
    }

    [Serializable, NetSerializable]
    public sealed class Result(string message, bool success) : EuiMessageBase
    {
        public string Message = message;
        public bool Success = success;
    }
}
