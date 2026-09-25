using Recipe.Domain;

namespace Recipe.Application;

public sealed class CatalogueService(ICatalogueStore store, ICurrentUser currentUser)
{
    public async Task<Guid> Save(Guid? id, SaveIngredientInput input, CancellationToken ct = default)
    {
        var user = await store.FindUser(currentUser.Id, ct);
        if (user?.IsAdministrator != true) throw new ForbiddenException();
        if (input.Names is null || input.Names.Count == 0 || input.Names.Any(n => string.IsNullOrWhiteSpace(n.Name) || string.IsNullOrWhiteSpace(n.Language)))
            throw new InputException("names", "At least one preferred name with a language is required.");
        var names = input.Names.Select(n => new NameInput(n.Language.Trim().ToLowerInvariant(), n.Name.Trim())).ToList();
        if (names.Select(n => n.Language).Distinct().Count() != names.Count)
            throw new InputException("names", "Only one preferred name per language is allowed.");
        foreach (var language in names.Select(n => n.Language).Concat((input.Aliases ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Language)).Select(a => a.Language!.Trim())))
        {
            try { _ = System.Globalization.CultureInfo.GetCultureInfo(language); }
            catch (System.Globalization.CultureNotFoundException) { throw new InputException("language", "Use a language code such as sv or en."); }
        }
        if (names.Any(n => n.Name.Length > 200 || n.Language.Length > 35) || input.Aliases is null || input.Aliases.Any(a => string.IsNullOrWhiteSpace(a.Name) || a.Name.Length > 200 || a.Language?.Length > 35))
            throw new InputException("names", "Names and aliases must contain 1–200 characters; language codes at most 35.");
        var ingredient = id.HasValue ? await store.FindIngredient(id.Value, ct) ?? throw new NotFoundException() : new Ingredient();
        ingredient.Names.RemoveAll(n => !names.Any(x => x.Language == n.Language));
        foreach (var name in names)
        {
            var existing = ingredient.Names.SingleOrDefault(n => n.Language == name.Language);
            if (existing is null) ingredient.Names.Add(new IngredientName { IngredientId = ingredient.Id, Language = name.Language, Name = name.Name });
            else existing.Name = name.Name;
        }
        ingredient.Aliases.Clear();
        ingredient.Aliases.AddRange(input.Aliases.Select(a => new AliasInput(string.IsNullOrWhiteSpace(a.Language) ? null : a.Language.Trim().ToLowerInvariant(), a.Name.Trim())).Distinct()
            .Select(a => new IngredientAlias { IngredientId = ingredient.Id, Name = a.Name, Language = a.Language }));
        if (!id.HasValue) store.Add(ingredient);
        await store.Save(ct);
        return ingredient.Id;
    }
}
