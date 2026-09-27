using Recipe.Domain;

namespace Recipe.Application;

public sealed class CatalogueService(ICatalogueStore store, ICurrentUser currentUser)
{
    public async Task<Guid> SaveTag(Guid? id, SaveTagInput input, CancellationToken ct = default)
    {
        await RequireAdministrator(ct);
        var names = ValidateNames(input.Names);
        var aliases = ValidateAliases(input.Aliases);
        var tag = id.HasValue ? await store.FindTag(id.Value, ct) ?? throw new NotFoundException() : new Tag();
        if (!id.HasValue) tag.Code = "tag-" + tag.Id.ToString("N");
        tag.IsActive = input.IsActive;
        tag.Names.RemoveAll(n => !names.Any(x => x.Language == n.Language));
        foreach (var name in names)
        {
            var existing = tag.Names.SingleOrDefault(n => n.Language == name.Language);
            if (existing is null) tag.Names.Add(new TagName { TagId = tag.Id, Language = name.Language, Name = name.Name });
            else existing.Name = name.Name;
        }
        tag.Aliases.RemoveAll(a => !aliases.Any(x => x.Language == a.Language && x.Name == a.Name));
        foreach (var alias in aliases.Where(x => !tag.Aliases.Any(a => a.Language == x.Language && a.Name == x.Name)))
            tag.Aliases.Add(new TagAlias { TagId = tag.Id, Language = alias.Language, Name = alias.Name });
        if (!id.HasValue) store.Add(tag);
        await store.Save(ct);
        return tag.Id;
    }

    public async Task<Guid> SaveRecipeType(Guid? id, SaveRecipeTypeInput input, CancellationToken ct = default)
    {
        await RequireAdministrator(ct);
        var names = ValidateNames(input.Names);
        var aliases = ValidateAliases(input.Aliases);
        if (input.SortOrder < 0) throw new InputException("sortOrder", "Sort order must be zero or greater.");
        var type = id.HasValue ? await store.FindRecipeType(id.Value, ct) ?? throw new NotFoundException() : new RecipeType();
        if (!id.HasValue) type.Code = "type-" + type.Id.ToString("N");
        type.IsActive = input.IsActive;
        type.SortOrder = input.SortOrder;
        type.Names.RemoveAll(n => !names.Any(x => x.Language == n.Language));
        foreach (var name in names)
        {
            var existing = type.Names.SingleOrDefault(n => n.Language == name.Language);
            if (existing is null) type.Names.Add(new RecipeTypeName { RecipeTypeId = type.Id, Language = name.Language, Name = name.Name });
            else existing.Name = name.Name;
        }
        type.Aliases.RemoveAll(a => !aliases.Any(x => x.Language == a.Language && x.Name == a.Name));
        foreach (var alias in aliases.Where(x => !type.Aliases.Any(a => a.Language == x.Language && a.Name == x.Name)))
            type.Aliases.Add(new RecipeTypeAlias { RecipeTypeId = type.Id, Language = alias.Language, Name = alias.Name });
        if (!id.HasValue) store.Add(type);
        await store.Save(ct);
        return type.Id;
    }

    private async Task RequireAdministrator(CancellationToken ct)
    {
        if ((await store.FindUser(currentUser.Id, ct))?.IsAdministrator != true) throw new ForbiddenException();
    }

    private static List<NameInput> ValidateNames(List<NameInput>? input)
    {
        if (input is null || input.Count == 0 || input.Any(n => string.IsNullOrWhiteSpace(n.Name) || string.IsNullOrWhiteSpace(n.Language)))
            throw new InputException("names", "At least one preferred name with a language is required.");
        var names = input.Select(n => new NameInput(n.Language.Trim().ToLowerInvariant(), n.Name.Trim())).ToList();
        if (names.Select(n => n.Language).Distinct().Count() != names.Count)
            throw new InputException("names", "Only one preferred name per language is allowed.");
        if (names.Any(n => n.Name.Length > 200 || n.Language.Length > 35))
            throw new InputException("names", "Names must contain 1–200 characters; language codes at most 35.");
        foreach (var name in names)
        {
            try { _ = System.Globalization.CultureInfo.GetCultureInfo(name.Language); }
            catch (System.Globalization.CultureNotFoundException) { throw new InputException("names", "Use a language code such as sv or en."); }
        }
        return names;
    }

    private static List<NameInput> ValidateAliases(List<NameInput>? input)
    {
        var aliases = new List<NameInput>();
        foreach (var alias in input ?? [])
        {
            // Aliases allow multiple terms per language, but share preferred-name text validation.
            try { aliases.Add(ValidateNames([alias])[0]); }
            catch (InputException e) { throw new InputException("aliases", e.Message); }
        }
        return aliases.Distinct().ToList();
    }

    public async Task<Guid> Save(Guid? id, SaveIngredientInput input, CancellationToken ct = default)
    {
        var user = await store.FindUser(currentUser.Id, ct);
        if (user is null || (id.HasValue && !user.IsAdministrator)) throw new ForbiddenException();
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
        var ingredient = id.HasValue ? await store.FindIngredientForAdministration(id.Value, ct) ?? throw new NotFoundException() : Ingredient.Create(user);
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
