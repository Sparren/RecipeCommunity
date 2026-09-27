namespace Recipe.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public bool IsAdministrator { get; set; }
}

public enum IngredientStatus { PendingReview, Active }

public sealed class Ingredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public IngredientStatus Status { get; set; } = IngredientStatus.PendingReview;
    public List<IngredientName> Names { get; set; } = [];
    public List<IngredientAlias> Aliases { get; set; } = [];

    public static Ingredient Create(User creator) => new()
    {
        CreatedByUserId = creator.Id,
        Status = creator.IsAdministrator ? IngredientStatus.Active : IngredientStatus.PendingReview
    };
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
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public List<RecipeTypeName> Names { get; set; } = [];
    public List<RecipeTypeAlias> Aliases { get; set; } = [];
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
    public bool IsActive { get; set; } = true;
    public List<TagName> Names { get; set; } = [];
    public List<TagAlias> Aliases { get; set; } = [];
}

public sealed class TagAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TagId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class RecipeTypeAlias
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeTypeId { get; set; }
    public string Language { get; set; } = "";
    public string Name { get; set; } = "";
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
