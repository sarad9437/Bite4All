namespace Bite4All.Domain.Enums;

[Flags]
public enum DietaryTag
{
    None = 0,
    Vegetarian = 1,
    Vegan = 2,
    GlutenFree = 4,
    LactoseFree = 8,
    Halal = 16,
    Kosher = 32
}
