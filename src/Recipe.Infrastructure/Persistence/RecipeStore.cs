using Microsoft.EntityFrameworkCore;
using Recipe.Application;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Infrastructure.Persistence;

public sealed class RecipeStore(RecipeDbContext db, ICatalogueStore catalogue) : IRecipeStore
{
    private IQueryable<RecipeEntity> Complete() => db.Recipes.Include(r => r.Tags)
        .Include(r => r.IngredientGroups).ThenInclude(g => g.Ingredients)
        .Include(r => r.CookingSteps).Include(r => r.Images).AsSplitQuery();
    public Task<RecipeEntity?> Find(Guid id, CancellationToken ct) => Complete().SingleOrDefaultAsync(r => r.Id == id, ct);
    public async Task<RecipeDetail?> Read(Guid id, string language, CancellationToken ct)
    {
        var recipe = await Complete().AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (recipe is null) return null;
        var ids = recipe.IngredientGroups.SelectMany(g => g.Ingredients).Select(i => i.IngredientId).Distinct().ToArray();
        var ingredients = await db.Ingredients.AsNoTracking().Include(i => i.Names).Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        var names = ingredients.ToDictionary(i => i.Id, i => CatalogueStore.Display(i.Names.Select(n => (n.Language, n.Name)), language));
        var author = await db.Users.SingleAsync(u => u.Id == recipe.AuthorId, ct);
        return new(recipe.Id, recipe.AuthorId, author.DisplayName, recipe.Name, recipe.Description, recipe.RecipeTypeId,
            Yield(recipe), recipe.TotalTimeMinutes, recipe.PublicationState, recipe.CreatedAt, recipe.UpdatedAt,
            recipe.Tags.Select(t => t.TagId).ToList(),
            recipe.IngredientGroups.OrderBy(g => g.SortOrder).Select(g => new GroupDetail(g.Id, g.Heading, g.SortOrder,
                g.Ingredients.OrderBy(i => i.SortOrder).Select(i => new IngredientDetail(i.Id, i.IngredientId, names[i.IngredientId],
                    new(i.Quantity.Value, i.Quantity.MaximumValue), i.UnitId, i.AmountDescription, i.Preparation, i.Note, i.SortOrder)).ToList())).ToList(),
            recipe.CookingSteps.OrderBy(s => s.SortOrder).Select(s => new StepDetail(s.Id, s.Instruction, s.SortOrder)).ToList(),
            recipe.Images.OrderBy(i => i.SortOrder).Select(i => new ImageDetail(i.Id, ImageUrl(recipe.Id, i.Id), i.IsPrimary, i.Caption, i.SortOrder)).ToList());
    }
    public async Task<Page<RecipeSummary>> Search(RecipeSearch search, string language, CancellationToken ct)
    {
        if (search.Page < 1 || search.PageSize is < 1 or > 100 || (long)(search.Page - 1) * search.PageSize > int.MaxValue)
            throw new InputException("page", "Page must be positive, its offset within supported bounds, and page size between 1 and 100.");
        var query = db.Recipes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search.Name))
        {
            var pattern = "%" + CatalogueStore.EscapeLike(search.Name.Trim()) + "%";
            query = query.Where(r => EF.Functions.ILike(r.Name, pattern, "\\"));
        }
        if (search.RecipeTypeId.HasValue) query = query.Where(r => r.RecipeTypeId == search.RecipeTypeId.Value);
        foreach (var ingredientId in search.IngredientIds.Distinct())
            query = query.Where(r => r.IngredientGroups.Any(g => g.Ingredients.Any(i => i.IngredientId == ingredientId)));
        if (search.TagIds.Length > 0) query = query.Where(r => r.Tags.Any(t => search.TagIds.Contains(t.TagId)));
        var total = await query.CountAsync(ct);
        var matches = await query.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).Include(r => r.Tags).Include(r => r.Images).AsSplitQuery().ToListAsync(ct);
        var types = (await catalogue.Read(language, ct)).RecipeTypes.ToDictionary(t => t.Id, t => t.Name);
        return new(matches.Select(r => new RecipeSummary(r.Id, r.Name, r.Description, r.RecipeTypeId, types[r.RecipeTypeId], Yield(r), r.TotalTimeMinutes,
            r.PublicationState, r.Tags.Select(t => t.TagId).ToList(), r.Images.FirstOrDefault(i => i.IsPrimary) is { } image ? ImageUrl(r.Id, image.Id) : null)).ToList(), total, search.Page, search.PageSize);
    }
    private static YieldInput Yield(RecipeEntity r) => new(new(r.Yield.Quantity.Value, r.Yield.Quantity.MaximumValue), r.Yield.UnitId);
    public static string ImageUrl(Guid recipeId, Guid imageId) => $"/api/recipes/{recipeId}/images/{imageId}/file";
    public void Add(RecipeEntity recipe) => db.Recipes.Add(recipe);
    public async Task Save(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var switches = db.ChangeTracker.Entries<Recipe.Domain.RecipeImage>()
            .Where(e => e.State == EntityState.Modified && e.Property(i => i.IsPrimary).IsModified)
            .Select(e => e.Entity.RecipeId).Distinct().ToArray();
        if (switches.Length == 0) { await db.SaveChangesAsync(ct); return; }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Set<Recipe.Domain.RecipeImage>().Where(i => switches.Contains(i.RecipeId) && i.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsPrimary, false), ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
