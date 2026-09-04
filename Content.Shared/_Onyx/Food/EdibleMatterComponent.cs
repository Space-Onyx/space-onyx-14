// This file contains code derived from Wega (https://github.com/wega-team/ss14-wega).
// Licensed under the GNU General Public License v3.0.
namespace Content.Shared.Edible.Matter;

[RegisterComponent]
public sealed partial class EdibleMatterComponent : Component
{
    [DataField("nutritionValue")]
    public float NutritionValue = 5f;

    [DataField("canBeEaten")]
    public bool CanBeEaten = true;
}
