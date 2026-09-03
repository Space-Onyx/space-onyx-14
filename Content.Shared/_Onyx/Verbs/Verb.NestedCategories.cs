namespace Content.Shared.Verbs;

public partial class Verb
{
    /// <summary>
    /// Categories nested below <see cref="Category" />, ordered from outermost to innermost.
    /// </summary>
    public List<VerbCategory>? SubCategories;

    /// <summary>
    /// Sets the root category and any nested categories in one call.
    /// </summary>
    public void SetCategoryPath(VerbCategory category, params VerbCategory[] subCategories)
    {
        Category = category;
        SubCategories = subCategories.Length == 0 ? null : new List<VerbCategory>(subCategories);
    }

    private int CompareSubCategories(Verb other)
    {
        var count = Math.Min(SubCategories?.Count ?? 0, other.SubCategories?.Count ?? 0);
        for (var i = 0; i < count; i++)
        {
            var comparison = string.Compare(
                SubCategories![i].Text,
                other.SubCategories![i].Text,
                StringComparison.CurrentCulture);
            if (comparison != 0)
                return comparison;
        }

        return (SubCategories?.Count ?? 0).CompareTo(other.SubCategories?.Count ?? 0);
    }
}
