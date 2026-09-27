using Microsoft.EntityFrameworkCore;
using Recipe.Domain;
using RecipeEntity = Recipe.Domain.Recipe;

namespace Recipe.Infrastructure.Persistence;

public static class DevelopmentSeed
{
    public static readonly Guid UserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    // Explicit opt-in, atomic, and intended for an empty development database only.
    public static async Task Run(RecipeDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Users.Add(new User { Id = UserId, DisplayName = "Lokal kock", IsAdministrator = true });
        var types = new[] { Type("main", "Huvudrätt", "Main course"), Type("starter", "Förrätt", "Appetizer"), Type("side", "Tillbehör", "Side dish"), Type("bread", "Bröd", "Bread"), Type("sauce", "Sås", "Sauce"), Type("drink", "Dryck", "Drink"), Type("dessert", "Dessert", "Dessert"), Type("other", "Övrigt", "Other") };
        var tags = new[] { Tag("vegetarian", "Vegetariskt", "Vegetarian"), Tag("quick", "Snabbt", "Quick"), Tag("celebration", "Fest", "Celebration"), Tag("baking", "Bakning", "Baking") };
        var units = new[]
        {
            Unit("g", "gram", "gram", "g", UnitDimension.Mass), Unit("kg", "kilogram", "kilogram", "kg", UnitDimension.Mass),
            Unit("ml", "milliliter", "millilitre", "ml", UnitDimension.Volume), Unit("dl", "deciliter", "decilitre", "dl", UnitDimension.Volume),
            Unit("litre", "liter", "litre", "l", UnitDimension.Volume), Unit("metric-tablespoon", "matsked (15 ml)", "tablespoon (15 ml)", "msk", UnitDimension.Volume),
            Unit("metric-teaspoon", "tesked (5 ml)", "teaspoon (5 ml)", "tsk", UnitDimension.Volume), Unit("us-cup", "US cup", "US cup", "US cup", UnitDimension.Volume),
            Unit("piece", "stycken", "pieces", "st", UnitDimension.Count), Unit("portion", "portioner", "portions", "port", UnitDimension.Count, false),
            Unit("cm", "centimeter", "centimetre", "cm", UnitDimension.Length, true, false), Unit("celsius", "grader Celsius", "degrees Celsius", "°C", UnitDimension.Temperature, false, false),
            Unit("package", "förpackning", "package", "förp", UnitDimension.Package), Unit("pinch", "nypa", "pinch", "nypa", UnitDimension.Other, true, false)
        };
        var ingredients = new[]
        {
            Ingredient("Blomkål", "Cauliflower"), Ingredient("Kaviar", "Caviar"), Ingredient("Grädde", "Cream"),
            Ingredient("Schalottenlök", "Shallot"), Ingredient("Gräslök", "Chives"), Ingredient("Zucchini", "Zucchini", "courgette", "squash"),
            Ingredient("Smör", "Butter"), Ingredient("Vetemjöl", "Wheat flour", "flour"), Ingredient("Ägg", "Egg"),
            Ingredient("Mjölk", "Milk"), Ingredient("Salt", "Salt"), Ingredient("Persilja", "Parsley"), Ingredient("Vatten", "Water"), Ingredient("Socker", "Sugar")
        };
        foreach (var ingredient in ingredients) { ingredient.Status = IngredientStatus.Active; ingredient.CreatedByUserId = UserId; }
        db.RecipeTypes.AddRange(types); db.Tags.AddRange(tags); db.Units.AddRange(units); db.Ingredients.AddRange(ingredients);
        Guid U(string code) => units.Single(u => u.Code == code).Id;
        Guid I(string name) => ingredients.Single(i => i.Names.Any(n => n.Name == name)).Id;
        RecipeIngredient Entry(string name, decimal? amount, string? unit, decimal? max = null, string? preparation = null) => new()
        {
            IngredientId = I(name),
            Quantity = new(amount, max),
            UnitId = unit is null ? null : U(unit),
            Preparation = preparation,
            AmountDescription = amount is null ? "efter smak" : null
        };
        RecipeEntity Make(string name, string description, string type, decimal amount, string unit, decimal? max, string[] tagCodes, IngredientGroup[] groups, params string[] steps)
        {
            var r = new RecipeEntity { AuthorId = UserId, Name = name, Description = description, RecipeTypeId = types.Single(t => t.Code == type).Id, Yield = new(new(amount, max), U(unit)), TotalTimeMinutes = 30, PublicationState = PublicationState.Published };
            r.Tags = tagCodes.Select(code => new RecipeTag { RecipeId = r.Id, TagId = tags.Single(t => t.Code == code).Id }).ToList();
            for (var gi = 0; gi < groups.Length; gi++)
            {
                var g = groups[gi]; g.RecipeId = r.Id; g.SortOrder = gi;
                for (var ii = 0; ii < g.Ingredients.Count; ii++) { g.Ingredients[ii].IngredientGroupId = g.Id; g.Ingredients[ii].SortOrder = ii; }
            }
            r.IngredientGroups = groups.ToList();
            r.CookingSteps = steps.Select((s, index) => new CookingStep { RecipeId = r.Id, Instruction = s, SortOrder = index }).ToList();
            r.Validate(); return r;
        }
        db.Recipes.AddRange(
            Make("Blomkål med kaviar", "Mjuk blomkål med gräddsås och frisk gräslök.", "starter", 4, "portion", null, ["celebration"],
                [new() { Heading = "Blomkål och sås", Ingredients = [Entry("Blomkål", 500, "g"), Entry("Grädde", 2, "dl", 3), Entry("Schalottenlök", 1, "piece")] }, new() { Heading = "Garnering", Ingredients = [Entry("Kaviar", 50, "g"), Entry("Gräslök", null, null)] }],
                "Dela blomkålen och koka den mjuk i lättsaltat vatten.", "Finhacka löken och sjud med grädden. Vänd ner blomkålen.", "Lägg upp och toppa med kaviar och gräslök."),
            Make("Zucchinisoppa", "En enkel grön soppa. Sök även efter courgette eller squash.", "main", 1.5m, "litre", null, ["vegetarian", "quick"],
                [new() { Ingredients = [Entry("Zucchini", 600, "g", preparation: "tärnad"), Entry("Vatten", 1, "litre"), Entry("Grädde", 0.5m, "dl"), Entry("Salt", null, null)] }],
                "Sjud zucchini i vattnet tills den är mjuk.", "Mixa, tillsätt grädde och smaka av med salt."),
            Make("Små pannkakor", "Ett recept med intervall för utbytet.", "dessert", 20, "piece", 24, ["vegetarian", "baking"],
                [new() { Ingredients = [Entry("Vetemjöl", 3, "dl"), Entry("Mjölk", 6, "dl"), Entry("Ägg", 3, "piece"), Entry("Smör", 50, "g")] }],
                "Vispa mjöl och hälften av mjölken slätt.", "Vispa ner resten av mjölken och äggen.", "Stek små pannkakor i smör."),
            Make("Klarat smör", "Ett utbyte uttryckt i vikt.", "sauce", 400, "g", null, ["vegetarian"],
                [new() { Ingredients = [Entry("Smör", 500, "g")] }], "Smält smöret långsamt utan att röra om.", "Skumma ytan och häll försiktigt av det klara fettet.")
        );
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    private static RecipeType Type(string code, string sv, string en) => new() { Code = code, Names = [new() { Language = "sv", Name = sv }, new() { Language = "en", Name = en }] };
    private static Tag Tag(string code, string sv, string en) => new() { Code = code, Names = [new() { Language = "sv", Name = sv }, new() { Language = "en", Name = en }] };
    private static Unit Unit(string code, string sv, string en, string symbol, UnitDimension dimension, bool ingredient = true, bool yield = true) => new()
    { Code = code, Dimension = dimension, ForIngredients = ingredient, ForYield = yield, Names = [new() { Language = "sv", Name = sv, Symbol = symbol }, new() { Language = "en", Name = en, Symbol = symbol }] };
    private static Ingredient Ingredient(string sv, string en, params string[] aliases) => new()
    { Names = [new() { Language = "sv", Name = sv }, new() { Language = "en", Name = en }], Aliases = aliases.Select(a => new IngredientAlias { Name = a }).ToList() };
}
