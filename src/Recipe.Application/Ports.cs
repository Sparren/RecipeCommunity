using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Application;

public interface ICurrentUser { Guid Id { get; } }

// Focused persistence ports: no generic repository or exposed EF queries.
public interface IRecipeStore
{
    Task<RecipeEntity?> Find(Guid id, CancellationToken ct);
    Task<RecipeDetail?> Read(Guid id, string language, CancellationToken ct);
    Task<Page<RecipeSummary>> Search(RecipeSearch search, string language, CancellationToken ct);
    void Add(RecipeEntity recipe);
    Task Save(CancellationToken ct);
}

public interface ICatalogueStore
{
    Task<User?> FindUser(Guid id, CancellationToken ct);
    Task<CatalogueData> Read(string language, CancellationToken ct);
    Task<List<TagItem>> ListTags(string? query, bool? isActive, string language, CancellationToken ct);
    Task<List<RecipeTypeItem>> ListRecipeTypes(string? query, bool? isActive, string language, CancellationToken ct);
    Task<Tag?> FindTag(Guid id, CancellationToken ct);
    Task<RecipeType?> FindRecipeType(Guid id, CancellationToken ct);
    void Add(Tag tag);
    void Add(RecipeType recipeType);
    Task<List<IngredientItem>> SearchIngredients(string? query, string language, CancellationToken ct);
    Task<List<IngredientItem>> ListIngredients(string? query, IngredientStatus? status, string language, CancellationToken ct);
    Task<Ingredient?> FindIngredientForAdministration(Guid id, CancellationToken ct);
    Task<Ingredient?> FindIngredient(Guid id, CancellationToken ct);
    Task<HashSet<Guid>> ExistingIngredients(IEnumerable<Guid> ids, CancellationToken ct);
    Task<List<LookupItem>> PendingIngredients(IEnumerable<Guid> ids, CancellationToken ct);
    void Add(Ingredient ingredient);
    Task Save(CancellationToken ct);
}

public interface IImageStorage
{
    Task<string> Save(Stream content, string extension, CancellationToken ct);
    Task<Stream?> Open(string key, CancellationToken ct);
    Task Delete(string key, CancellationToken ct);
}
