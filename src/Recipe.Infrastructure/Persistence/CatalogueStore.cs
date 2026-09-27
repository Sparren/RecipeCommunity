using Microsoft.EntityFrameworkCore;
using Recipe.Application;
using Recipe.Domain;
using System.Globalization;
using System.Text;

namespace Recipe.Infrastructure.Persistence;

public sealed class CatalogueStore(RecipeDbContext db, ICurrentUser currentUser) : ICatalogueStore
{
    internal static string Display(IEnumerable<(string Language, string Name)> names, string language) =>
        names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3)
            .ThenBy(n => n.Language).Select(n => n.Name).FirstOrDefault() ?? "?";
    internal static IngredientItem Map(Ingredient i, string language) => new(i.Id,
        Display(i.Names.Select(n => (n.Language, n.Name)), language),
        i.Names.OrderBy(n => n.Language).Select(n => new NameInput(n.Language, n.Name)).ToList(),
        i.Aliases.OrderBy(a => a.Name).Select(a => new AliasInput(a.Language, a.Name)).ToList(), i.Status, i.CreatedByUserId, i.CreatedAt);
    public Task<User?> FindUser(Guid id, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
    public async Task<CatalogueData> Read(string language, CancellationToken ct)
    {
        var types = await db.RecipeTypes.AsNoTracking().Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().ToListAsync(ct);
        var tags = await db.Tags.AsNoTracking().Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().ToListAsync(ct);
        var units = await db.Units.AsNoTracking().Include(t => t.Names).ToListAsync(ct);
        return new(types.Select(t => new LookupItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language), t.IsActive, t.SortOrder, t.Names.Select(n => new NameInput(n.Language, n.Name)).ToList(), t.Aliases.Select(a => new NameInput(a.Language, a.Name)).ToList())).OrderBy(x => x.SortOrder).ThenBy(x => x.Name, NameComparer(language)).ThenBy(x => x.Id).ToList(),
            tags.Select(t => new LookupItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language), t.IsActive, 0, t.Names.Select(n => new NameInput(n.Language, n.Name)).ToList(), t.Aliases.Select(a => new NameInput(a.Language, a.Name)).ToList())).OrderBy(x => x.Name, NameComparer(language)).ThenBy(x => x.Id).ToList(),
            units.Select(u =>
            {
                var name = u.Names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3).ThenBy(n => n.Language).First();
                return new UnitItem(u.Id, name.Name, name.Symbol, u.Dimension.ToString(), u.ForIngredients, u.ForYield);
            }).OrderBy(x => x.Name).ToList());
    }
    private static StringComparer NameComparer(string language)
    {
        try { return StringComparer.Create(CultureInfo.GetCultureInfo(language), true); }
        catch (CultureNotFoundException) { throw new InputException("language", "Unknown display language."); }
    }
    public async Task<List<TagItem>> ListTags(string? query, bool? isActive, string language, CancellationToken ct)
    {
        await RequireAdministrator(ct);
        var tags = db.Tags.AsNoTracking();
        if (isActive.HasValue) tags = tags.Where(t => t.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = "%" + EscapeLike(query.Trim()) + "%";
            tags = tags.Where(t => t.Names.Any(n => EF.Functions.ILike(n.Name, pattern, "\\")) || t.Aliases.Any(a => EF.Functions.ILike(a.Name, pattern, "\\")));
        }
        var matches = await tags.Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().ToListAsync(ct);
        return matches.Select(t => new TagItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language), t.IsActive,
            t.Names.OrderBy(n => n.Language).Select(n => new NameInput(n.Language, n.Name)).ToList(), t.Aliases.OrderBy(a => a.Language).ThenBy(a => a.Name).Select(a => new NameInput(a.Language, a.Name)).ToList()))
            .OrderBy(t => t.Name, NameComparer(language)).ThenBy(t => t.Id).ToList();
    }
    public async Task<List<RecipeTypeItem>> ListRecipeTypes(string? query, bool? isActive, string language, CancellationToken ct)
    {
        await RequireAdministrator(ct);
        var types = db.RecipeTypes.AsNoTracking();
        if (isActive.HasValue) types = types.Where(t => t.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = "%" + EscapeLike(query.Trim()) + "%";
            types = types.Where(t => t.Names.Any(n => EF.Functions.ILike(n.Name, pattern, "\\")) || t.Aliases.Any(a => EF.Functions.ILike(a.Name, pattern, "\\")));
        }
        var matches = await types.Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().ToListAsync(ct);
        return matches.Select(t => new RecipeTypeItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language), t.IsActive, t.SortOrder,
            t.Names.OrderBy(n => n.Language).Select(n => new NameInput(n.Language, n.Name)).ToList(), t.Aliases.OrderBy(a => a.Language).ThenBy(a => a.Name).Select(a => new NameInput(a.Language, a.Name)).ToList()))
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name, NameComparer(language)).ThenBy(t => t.Id).ToList();
    }
    public Task<Tag?> FindTag(Guid id, CancellationToken ct) => db.Tags.Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().SingleOrDefaultAsync(t => t.Id == id, ct);
    public Task<RecipeType?> FindRecipeType(Guid id, CancellationToken ct) => db.RecipeTypes.Include(t => t.Names).Include(t => t.Aliases).AsSplitQuery().SingleOrDefaultAsync(t => t.Id == id, ct);
    public void Add(Tag tag) => db.Tags.Add(tag);
    public void Add(RecipeType recipeType) => db.RecipeTypes.Add(recipeType);

    public async Task<List<IngredientItem>> SearchIngredients(string? query, string language, CancellationToken ct)
    {
        query = query?.Trim();
        // Return before constructing/executing a database query for short autocomplete input.
        if (query is null || query.EnumerateRunes().Count() < 2) return [];
        var exact = EscapeLike(query);
        var prefix = exact + "%";
        var contains = "%" + exact + "%";
        var matches = await Matching(Visible(), query).AsNoTracking()
            .OrderBy(i => i.Names.Any(n => EF.Functions.ILike(n.Name, exact, "\\")) ? 0
                : i.Names.Any(n => EF.Functions.ILike(n.Name, prefix, "\\")) ? 1
                : i.Aliases.Any(a => EF.Functions.ILike(a.Name, prefix, "\\")) ? 2
                : i.Names.Any(n => EF.Functions.ILike(n.Name, contains, "\\")) ? 3 : 4)
            .ThenBy(i => i.Names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3)
                .ThenBy(n => n.Language).Select(n => n.Name).First())
            .ThenBy(i => i.Id).Take(50).Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery().ToListAsync(ct);
        return matches.Select(i => Map(i, language)).ToList();
    }

    public async Task<List<IngredientItem>> ListIngredients(string? query, IngredientStatus? status, string language, CancellationToken ct)
    {
        await RequireAdministrator(ct);
        if (status.HasValue && !Enum.IsDefined(status.Value)) throw new InputException("status", "Unknown ingredient status.");
        var ingredients = Matching(db.Ingredients.AsNoTracking(), query);
        if (status.HasValue) ingredients = ingredients.Where(i => i.Status == status.Value);
        var matches = await ingredients.Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery().ToListAsync(ct);
        CultureInfo culture;
        try { culture = CultureInfo.GetCultureInfo(language); }
        catch (CultureNotFoundException) { throw new InputException("language", "Unknown display language."); }
        // The small administrator catalogue is untruncated and sorted by its actual display names.
        return matches.Select(i => Map(i, language)).OrderBy(i => i.Name, StringComparer.Create(culture, true)).ThenBy(i => i.Id).ToList();
    }
    private IQueryable<Ingredient> Visible() => db.Ingredients.Where(i => i.Status == IngredientStatus.Active || i.CreatedByUserId == currentUser.Id);
    private static IQueryable<Ingredient> Matching(IQueryable<Ingredient> ingredients, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return ingredients;
        var pattern = "%" + EscapeLike(query.Trim()) + "%";
        return ingredients.Where(i => i.Names.Any(n => EF.Functions.ILike(n.Name, pattern, "\\")) || i.Aliases.Any(a => EF.Functions.ILike(a.Name, pattern, "\\")));
    }
    private async Task RequireAdministrator(CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == currentUser.Id && u.IsAdministrator, ct)) throw new ForbiddenException();
    }
    internal static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    public Task<Ingredient?> FindIngredient(Guid id, CancellationToken ct) => Visible().Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery().SingleOrDefaultAsync(i => i.Id == id, ct);
    public async Task<Ingredient?> FindIngredientForAdministration(Guid id, CancellationToken ct)
    {
        await RequireAdministrator(ct);
        return await db.Ingredients.Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery().SingleOrDefaultAsync(i => i.Id == id, ct);
    }
    public async Task<HashSet<Guid>> ExistingIngredients(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var requested = ids.Distinct().ToArray();
        return (await Visible().Where(i => requested.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct)).ToHashSet();
    }
    public async Task<List<LookupItem>> PendingIngredients(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var requested = ids.Distinct().ToArray();
        var pending = await Visible().AsNoTracking().Where(i => requested.Contains(i.Id) && i.Status == IngredientStatus.PendingReview)
            .Include(i => i.Names).ToListAsync(ct);
        return pending.Select(i => new LookupItem(i.Id, Display(i.Names.Select(n => (n.Language, n.Name)), "sv")))
            .OrderBy(i => i.Name, NameComparer("sv")).ToList();
    }
    public void Add(Ingredient ingredient) => db.Ingredients.Add(ingredient);
    public Task Save(CancellationToken ct) => db.SaveChangesAsync(ct);
}
