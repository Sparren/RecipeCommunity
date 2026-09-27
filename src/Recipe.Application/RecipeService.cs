using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Application;

public sealed class RecipeService(IRecipeStore recipes, ICatalogueStore catalogue, ICurrentUser currentUser)
{
    public async Task<Guid> Save(Guid? id, SaveRecipeInput input, CancellationToken ct = default)
    {
        var recipe = id.HasValue ? await recipes.Find(id.Value, ct) ?? throw new NotFoundException() : new RecipeEntity { AuthorId = currentUser.Id };
        if (recipe.AuthorId != currentUser.Id) throw new ForbiddenException();
        if (await catalogue.FindUser(currentUser.Id, ct) is null) throw new ForbiddenException();
        var choices = await catalogue.Read("sv", ct);
        if (input.RecipeTypeId.HasValue && !choices.RecipeTypes.Any(t => t.Id == input.RecipeTypeId))
            throw new InputException("recipeTypeId", "Select an existing recipe type.");
        if (input.RecipeTypeId.HasValue && input.RecipeTypeId != recipe.RecipeTypeId && !choices.RecipeTypes.Single(t => t.Id == input.RecipeTypeId).IsActive)
            throw new InputException("recipeTypeId", "Inactive recipe types can only be retained on recipes already referencing them.");
        if (input.Yield is not null && !choices.Units.Any(u => u.Id == input.Yield.UnitId && u.ForYield))
            throw new InputException("yield.unitId", "Select a unit suitable for yield.");
        if (input.TagIds is null || input.TagIds.Any(t => !choices.Tags.Any(x => x.Id == t)))
            throw new InputException("tagIds", "Select existing tags.");
        if (input.TagIds.Any(t => !choices.Tags.Single(x => x.Id == t).IsActive && !recipe.Tags.Any(x => x.TagId == t)))
            throw new InputException("tagIds", "Inactive tags can only be retained on recipes already referencing them.");
        if (input.IngredientGroups is null || input.CookingSteps is null)
            throw new InputException("recipe", "Groups and cooking steps must be supplied.");

        var entries = input.IngredientGroups.SelectMany(g => g.Ingredients ?? []).ToList();
        var existingIngredients = await catalogue.ExistingIngredients(entries.Select(i => i.IngredientId), ct);
        var newGroups = new List<IngredientGroup>();
        var usedIds = new HashSet<Guid>();
        for (var gi = 0; gi < input.IngredientGroups.Count; gi++)
        {
            var groupInput = input.IngredientGroups[gi];
            var path = $"ingredientGroups[{gi}]";
            var groupId = ResolveId(groupInput.Id, recipe.IngredientGroups.Select(g => g.Id), usedIds, path);
            var group = new IngredientGroup { Id = groupId, RecipeId = recipe.Id, Heading = Clean(groupInput.Heading), SortOrder = gi };
            if (groupInput.Ingredients is null) throw new InputException(path, "Ingredients must be supplied.");
            for (var ii = 0; ii < groupInput.Ingredients.Count; ii++)
            {
                var item = groupInput.Ingredients[ii];
                var itemPath = $"{path}.ingredients[{ii}]";
                if (!existingIngredients.Contains(item.IngredientId)) throw new InputException(itemPath + ".ingredientId", "Select an existing ingredient; unknown text cannot create one.");
                if (item.UnitId.HasValue && !choices.Units.Any(u => u.Id == item.UnitId && u.ForIngredients))
                    throw new InputException(itemPath + ".unitId", "Select a unit suitable for ingredients.");
                var entry = new RecipeIngredient
                {
                    Id = ResolveId(item.Id, recipe.IngredientGroups.SelectMany(g => g.Ingredients).Select(i => i.Id), usedIds, itemPath),
                    IngredientGroupId = groupId,
                    IngredientId = item.IngredientId,
                    Quantity = MakeQuantity(item.Quantity, itemPath + ".quantity"),
                    UnitId = item.UnitId,
                    AmountDescription = Clean(item.AmountDescription),
                    Preparation = Clean(item.Preparation),
                    Note = Clean(item.Note),
                    SortOrder = ii
                };
                try { entry.Validate(); } catch (DomainException e) { throw new InputException(itemPath, e.Message); }
                group.Ingredients.Add(entry);
            }
            newGroups.Add(group);
        }
        if (newGroups.Count == 0) newGroups.Add(new IngredientGroup { RecipeId = recipe.Id });
        var newSteps = input.CookingSteps.Select((s, index) => new CookingStep
        {
            Id = ResolveId(s.Id, recipe.CookingSteps.Select(x => x.Id), usedIds, $"cookingSteps[{index}]"),
            RecipeId = recipe.Id,
            Instruction = s.Instruction?.Trim() ?? "",
            SortOrder = index
        }).ToList();

        // Apply the rule on every Published write, including edits of already published recipes.
        // Status comes from the catalogue, never from a caller's ingredient display metadata.
        if (input.PublicationState == PublicationState.Published)
        {
            var pending = await catalogue.PendingIngredients(entries.Select(i => i.IngredientId), ct);
            if (pending.Count > 0)
                throw new InputException("publicationState", "Receptet kan inte publiceras. Följande ingredienser väntar på granskning: "
                    + string.Join(", ", pending.Select(i => i.Name)) + ". Spara som utkast eller ersätt dem med godkända ingredienser.");
        }

        recipe.Name = input.Name?.Trim() ?? "";
        recipe.Description = Clean(input.Description);
        recipe.RecipeTypeId = input.RecipeTypeId;
        try { recipe.Yield = input.Yield is null ? null : new Yield(MakeQuantity(input.Yield.Quantity, "yield.quantity"), input.Yield.UnitId); }
        catch (DomainException e) { throw new InputException("yield", e.Message); }
        recipe.TotalTimeMinutes = input.TotalTimeMinutes;
        recipe.PublicationState = input.PublicationState;
        // Apply content to tracked children without replacing their stable identities.
        SynchronizeGroups(recipe, newGroups);
        recipe.CookingSteps.RemoveAll(s => !newSteps.Any(n => n.Id == s.Id));
        foreach (var next in newSteps)
        {
            var existing = recipe.CookingSteps.SingleOrDefault(s => s.Id == next.Id);
            if (existing is null) recipe.CookingSteps.Add(next);
            else { existing.Instruction = next.Instruction; existing.SortOrder = next.SortOrder; }
        }
        recipe.Tags.RemoveAll(t => !input.TagIds.Contains(t.TagId));
        foreach (var tagId in input.TagIds.Distinct().Where(t => !recipe.Tags.Any(x => x.TagId == t)))
            recipe.Tags.Add(new RecipeTag { RecipeId = recipe.Id, TagId = tagId });
        recipe.Validate();
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue) recipes.Add(recipe);
        await recipes.Save(ct);
        return recipe.Id;
    }

    private static void SynchronizeGroups(RecipeEntity recipe, List<IngredientGroup> incoming)
    {
        var existingEntries = recipe.IngredientGroups.SelectMany(g => g.Ingredients).ToDictionary(i => i.Id);
        foreach (var group in recipe.IngredientGroups)
            group.Ingredients.RemoveAll(i => !incoming.Any(g => g.Id == group.Id && g.Ingredients.Any(n => n.Id == i.Id)));
        recipe.IngredientGroups.RemoveAll(g => !incoming.Any(n => n.Id == g.Id));
        foreach (var next in incoming)
        {
            var group = recipe.IngredientGroups.SingleOrDefault(g => g.Id == next.Id);
            if (group is null) { group = new IngredientGroup { Id = next.Id, RecipeId = recipe.Id }; recipe.IngredientGroups.Add(group); }
            group.Heading = next.Heading;
            group.SortOrder = next.SortOrder;
            foreach (var n in next.Ingredients)
            {
                if (!existingEntries.TryGetValue(n.Id, out var item)) item = n;
                else
                {
                    item.IngredientGroupId = group.Id; item.IngredientId = n.IngredientId; item.Quantity = n.Quantity;
                    item.UnitId = n.UnitId; item.AmountDescription = n.AmountDescription; item.Preparation = n.Preparation;
                    item.Note = n.Note; item.SortOrder = n.SortOrder;
                }
                if (!group.Ingredients.Any(i => i.Id == item.Id)) group.Ingredients.Add(item);
            }
        }
    }

    private static Guid ResolveId(Guid? id, IEnumerable<Guid> existing, HashSet<Guid> used, string path)
    {
        if (id.HasValue && (!existing.Contains(id.Value) || !used.Add(id.Value)))
            throw new InputException(path + ".id", "Unknown or duplicate child ID. Omit IDs for new entries.");
        return id ?? Guid.NewGuid();
    }

    private static Quantity MakeQuantity(QuantityInput? input, string field)
    {
        if (input is null) throw new InputException(field, "Supply a quantity object, even for unspecified amounts.");
        try { return new Quantity(input.Value, input.MaximumValue); }
        catch (DomainException e) { throw new InputException(field, e.Message); }
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
