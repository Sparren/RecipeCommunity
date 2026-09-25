namespace Recipe.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAdministrator { get; set; }
}

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public List<IngredientName> Names { get; set; } = [];
    public List<IngredientAlias> Aliases { get; set; } = [];
}

public sealed class IngredientName
{
    public Guid IngredientId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class IngredientAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientId { get; set; }
    public string? Language { get; set; }
    public string Name { get; set; } = "";
}

public sealed class RecipeType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public List<RecipeTypeName> Names { get; set; } = [];
}

public sealed class RecipeTypeName
{
    public Guid RecipeTypeId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public List<TagName> Names { get; set; } = [];
}

public sealed class TagName
{
    public Guid TagId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
}

public enum UnitDimension { Mass, Volume, Count, Length, Temperature, Package, Other }

public sealed class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public UnitDimension Dimension { get; set; }
    public bool ForIngredients { get; set; }
    public bool ForYield { get; set; }
    public List<UnitName> Names { get; set; } = [];
}

public sealed class UnitName
{
    public Guid UnitId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
}
