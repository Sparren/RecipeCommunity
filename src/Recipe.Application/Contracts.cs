using Recipe.Domain;

namespace Recipe.Application;

public sealed record QuantityInput(decimal? Value, decimal? MaximumValue);
public sealed record YieldInput(QuantityInput Quantity, Guid UnitId);
public sealed record IngredientInput(Guid? Id, Guid IngredientId, QuantityInput Quantity, Guid? UnitId,
    string? AmountDescription, string? Preparation, string? Note);
public sealed record GroupInput(Guid? Id, string? Heading, List<IngredientInput> Ingredients);
public sealed record StepInput(Guid? Id, string Instruction);
public sealed record SaveRecipeInput(string Name, string? Description, Guid RecipeTypeId, YieldInput Yield,
    int? TotalTimeMinutes, PublicationState PublicationState, List<Guid> TagIds,
    List<GroupInput> IngredientGroups, List<StepInput> CookingSteps);
public sealed record RecipeSearch(string? Name, Guid? RecipeTypeId, Guid[] IngredientIds, Guid[] TagIds,
    int Page = 1, int PageSize = 20);
public sealed record Page<T>(List<T> Items, int Total, int PageNumber, int PageSize);
public sealed record LookupItem(Guid Id, string Name);
public sealed record UnitItem(Guid Id, string Name, string Symbol, string Dimension, bool ForIngredients, bool ForYield);
public sealed record CatalogueData(List<LookupItem> RecipeTypes, List<LookupItem> Tags, List<UnitItem> Units);
public sealed record NameInput(string Language, string Name);
public sealed record AliasInput(string? Language, string Name);
public sealed record SaveIngredientInput(List<NameInput> Names, List<AliasInput> Aliases);
public sealed record IngredientItem(Guid Id, string Name, List<NameInput> Names, List<AliasInput> Aliases);
public sealed record RecipeSummary(Guid Id, string Name, string? Description, Guid RecipeTypeId,
    string RecipeTypeName, YieldInput Yield, int? TotalTimeMinutes, PublicationState PublicationState,
    List<Guid> TagIds, string? PrimaryImageUrl);
public sealed record IngredientDetail(Guid Id, Guid IngredientId, string IngredientName, QuantityInput Quantity,
    Guid? UnitId, string? AmountDescription, string? Preparation, string? Note, int SortOrder);
public sealed record GroupDetail(Guid Id, string? Heading, int SortOrder, List<IngredientDetail> Ingredients);
public sealed record StepDetail(Guid Id, string Instruction, int SortOrder);
public sealed record ImageDetail(Guid Id, string Url, bool IsPrimary, string? Caption, int SortOrder);
public sealed record RecipeDetail(Guid Id, Guid AuthorId, string AuthorName, string Name, string? Description,
    Guid RecipeTypeId, YieldInput Yield, int? TotalTimeMinutes, PublicationState PublicationState,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, List<Guid> TagIds, List<GroupDetail> IngredientGroups,
    List<StepDetail> CookingSteps, List<ImageDetail> Images);
public sealed record ImageInput(Guid Id, string? Caption, bool IsPrimary);

public sealed class InputException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
public sealed class NotFoundException() : Exception("The requested item was not found.");
public sealed class ForbiddenException() : Exception("Only the local administrator may modify the ingredient catalogue.");
