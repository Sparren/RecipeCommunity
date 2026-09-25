using Recipe.Application;
using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Application.Tests;

public class ServiceTests
{
    [Fact]
    public async Task Create_preserves_range_and_default_group_and_deduplicates_tags()
    {
        var store = new Store(); var service = new RecipeService(store, store, store);
        var input = store.Valid() with { TagIds = [store.TagId, store.TagId] };
        var id = await service.Save(null, input);
        Assert.Equal(store.Recipe!.Id, id); Assert.Equal(store.Id, store.Recipe.AuthorId);
        Assert.Single(store.Recipe.IngredientGroups); Assert.Single(store.Recipe.Tags);
        Assert.Equal(24, store.Recipe.Yield.Quantity.MaximumValue);
    }
    [Fact]
    public async Task Unknown_ingredient_and_invalid_unit_are_not_saved()
    {
        var store = new Store(); var service = new RecipeService(store, store, store);
        var item = new IngredientInput(null, Guid.NewGuid(), new(2, 3), store.UnitId, null, null, null);
        var input = store.Valid() with { IngredientGroups = [new(null, null, [item])] };
        var error = await Assert.ThrowsAsync<InputException>(() => service.Save(null, input));
        Assert.Contains("ingredientId", error.Field); Assert.False(store.Saved);
        input = input with { IngredientGroups = [new(null, null, [item with { IngredientId = store.IngredientId, UnitId = Guid.NewGuid() }])] };
        await Assert.ThrowsAsync<InputException>(() => service.Save(null, input)); Assert.False(store.Saved);
    }
    [Fact]
    public async Task Unknown_recipe_type_and_tag_are_rejected()
    {
        var store = new Store(); var service = new RecipeService(store, store, store);
        await Assert.ThrowsAsync<InputException>(() => service.Save(null, store.Valid() with { RecipeTypeId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<InputException>(() => service.Save(null, store.Valid() with { TagIds = [Guid.NewGuid()] }));
        Assert.False(store.Saved);
    }
    [Fact]
    public async Task Only_administrator_can_change_ingredient_names()
    {
        var store = new Store { Admin = false }; var service = new CatalogueService(store, store);
        var input = new SaveIngredientInput([new("sv", "Purjolök")], [new(null, "leek")]);
        await Assert.ThrowsAsync<ForbiddenException>(() => service.Save(null, input)); Assert.False(store.Saved);
        store.Admin = true; await service.Save(null, input); Assert.True(store.Saved);
        Assert.Equal("leek", Assert.Single(store.Ingredient!.Aliases).Name);
    }
    [Fact]
    public async Task Preferred_language_names_must_be_unique()
    {
        var store = new Store();
        await Assert.ThrowsAsync<InputException>(() => new CatalogueService(store, store).Save(null, new([new("sv", "A"), new("SV", "B")], [])));
    }
    private sealed class Store : IRecipeStore, ICatalogueStore, ICurrentUser
    {
        public Guid Id { get; } = Guid.NewGuid(); public Guid TypeId { get; } = Guid.NewGuid();
        public Guid UnitId { get; } = Guid.NewGuid(); public Guid TagId { get; } = Guid.NewGuid(); public Guid IngredientId { get; } = Guid.NewGuid();
        public bool Admin { get; set; } = true; public bool Saved { get; private set; }
        public RecipeEntity? Recipe; public Ingredient? Ingredient;
        public SaveRecipeInput Valid() => new("Test", null, TypeId, new(new(20, 24), UnitId), null, PublicationState.Draft, [], [], []);
        public Task<User?> FindUser(Guid id, CancellationToken ct) => Task.FromResult<User?>(new() { Id = Id, IsAdministrator = Admin });
        public Task<CatalogueData> Read(string language, CancellationToken ct) => Task.FromResult(new CatalogueData([new(TypeId, "Type")], [new(TagId, "Tag")], [new(UnitId, "Piece", "pc", "Count", true, true)]));
        public Task<HashSet<Guid>> ExistingIngredients(IEnumerable<Guid> ids, CancellationToken ct) => Task.FromResult(new HashSet<Guid> { IngredientId });
        public void Add(RecipeEntity recipe) => Recipe = recipe;
        public void Add(Ingredient ingredient) => Ingredient = ingredient;
        public Task Save(CancellationToken ct) { Saved = true; return Task.CompletedTask; }
        public Task<RecipeEntity?> Find(Guid id, CancellationToken ct) => Task.FromResult(Recipe);
        public Task<Ingredient?> FindIngredient(Guid id, CancellationToken ct) => Task.FromResult(Ingredient);
        public Task<RecipeDetail?> Read(Guid id, string language, CancellationToken ct) => throw new NotSupportedException();
        public Task<Page<RecipeSummary>> Search(RecipeSearch search, string language, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<IngredientItem>> SearchIngredients(string? query, string language, CancellationToken ct) => throw new NotSupportedException();
    }
}
