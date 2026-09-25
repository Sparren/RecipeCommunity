namespace Recipe.Domain;

public enum PublicationState { Draft, Published }

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid RecipeTypeId { get; set; }
    public Yield Yield { get; set; } = null!;
    public int? TotalTimeMinutes { get; set; }
    public PublicationState PublicationState { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecipeTag> Tags { get; set; } = [];
    public List<IngredientGroup> IngredientGroups { get; set; } = [];
    public List<CookingStep> CookingSteps { get; set; } = [];
    public List<RecipeImage> Images { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200)
            throw new DomainException("Recipe name is required and must be at most 200 characters.");
        if (AuthorId == Guid.Empty || RecipeTypeId == Guid.Empty || Yield is null)
            throw new DomainException("Author, recipe type and yield are required.");
        if (TotalTimeMinutes is <= 0) throw new DomainException("Total time must be positive.");
        if (!Enum.IsDefined(PublicationState)) throw new DomainException("Unknown publication state.");
        if (IngredientGroups.Count == 0) throw new DomainException("A recipe requires at least one ingredient group.");
        foreach (var ingredient in IngredientGroups.SelectMany(g => g.Ingredients)) ingredient.Validate();
        if (CookingSteps.Any(s => string.IsNullOrWhiteSpace(s.Instruction)))
            throw new DomainException("Cooking steps cannot be blank.");
        if (PublicationState == PublicationState.Published &&
            (!IngredientGroups.Any(g => g.Ingredients.Count > 0) || CookingSteps.Count == 0))
            throw new DomainException("A published recipe needs ingredients and cooking steps.");
        if (Images.Count(i => i.IsPrimary) > 1) throw new DomainException("Only one image may be primary.");
    }
}

public sealed class RecipeTag
{
    public Guid RecipeId { get; set; }
    public Guid TagId { get; set; }
}

public sealed class IngredientGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public string? Heading { get; set; }
    public int SortOrder { get; set; }
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class RecipeIngredient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IngredientGroupId { get; set; }
    public Guid IngredientId { get; set; }
    public Quantity Quantity { get; set; } = new(null);
    public Guid? UnitId { get; set; }
    public string? AmountDescription { get; set; }
    public string? Preparation { get; set; }
    public string? Note { get; set; }
    public int SortOrder { get; set; }

    public void Validate()
    {
        if (IngredientId == Guid.Empty) throw new DomainException("Select an existing ingredient.");
        if (Quantity.Value.HasValue != UnitId.HasValue || UnitId == Guid.Empty)
            throw new DomainException("Numeric ingredient amounts require a unit; unspecified amounts have no unit.");
    }
}

public sealed class CookingStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public int SortOrder { get; set; }
    public string Instruction { get; set; } = "";
}

public sealed class RecipeImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipeId { get; set; }
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public bool IsPrimary { get; set; }
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
