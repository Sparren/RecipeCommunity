using Microsoft.EntityFrameworkCore;
using Recipe.Application;
using Recipe.Domain;

namespace Recipe.Infrastructure.Persistence;

public sealed class CatalogueStore(RecipeDbContext db) : ICatalogueStore
{
    internal static string Display(IEnumerable<(string Language, string Name)> names, string language) =>
        names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3)
            .ThenBy(n => n.Language).Select(n => n.Name).FirstOrDefault() ?? "?";
    internal static IngredientItem Map(Ingredient i, string language) => new(i.Id,
        Display(i.Names.Select(n => (n.Language, n.Name)), language),
        i.Names.OrderBy(n => n.Language).Select(n => new NameInput(n.Language, n.Name)).ToList(),
        i.Aliases.OrderBy(a => a.Name).Select(a => new AliasInput(a.Language, a.Name)).ToList());
    public Task<User?> FindUser(Guid id, CancellationToken ct) => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
    public async Task<CatalogueData> Read(string language, CancellationToken ct)
    {
        var types = await db.RecipeTypes.AsNoTracking().Include(t => t.Names).ToListAsync(ct);
        var tags = await db.Tags.AsNoTracking().Include(t => t.Names).ToListAsync(ct);
        var units = await db.Units.AsNoTracking().Include(t => t.Names).ToListAsync(ct);
        return new(types.Select(t => new LookupItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language))).OrderBy(x => x.Name).ToList(),
            tags.Select(t => new LookupItem(t.Id, Display(t.Names.Select(n => (n.Language, n.Name)), language))).OrderBy(x => x.Name).ToList(),
            units.Select(u =>
            {
                var name = u.Names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3).ThenBy(n => n.Language).First();
                return new UnitItem(u.Id, name.Name, name.Symbol, u.Dimension.ToString(), u.ForIngredients, u.ForYield);
            }).OrderBy(x => x.Name).ToList());
    }
    public async Task<List<IngredientItem>> SearchIngredients(string? query, string language, CancellationToken ct)
    {
        var ingredients = db.Ingredients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = "%" + EscapeLike(query.Trim()) + "%";
            ingredients = ingredients.Where(i => i.Names.Any(n => EF.Functions.ILike(n.Name, pattern, "\\")) || i.Aliases.Any(a => EF.Functions.ILike(a.Name, pattern, "\\")));
        }
        var matches = await ingredients.Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery()
            .OrderBy(i => i.Names.OrderBy(n => n.Language).Select(n => n.Name).First()).ThenBy(i => i.Id).Take(50).ToListAsync(ct);
        return matches.Select(i => Map(i, language)).OrderBy(i => i.Name).ToList();
    }
    internal static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    public Task<Ingredient?> FindIngredient(Guid id, CancellationToken ct) => db.Ingredients.Include(i => i.Names).Include(i => i.Aliases).AsSplitQuery().SingleOrDefaultAsync(i => i.Id == id, ct);
    public async Task<HashSet<Guid>> ExistingIngredients(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var requested = ids.Distinct().ToArray();
        return (await db.Ingredients.Where(i => requested.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct)).ToHashSet();
    }
    public void Add(Ingredient ingredient) => db.Ingredients.Add(ingredient);
    public Task Save(CancellationToken ct) => db.SaveChangesAsync(ct);
}
