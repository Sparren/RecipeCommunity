using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recipe.Application;
using Recipe.Domain;

namespace Recipe.IntegrationTests;

[Collection("Postgres")]
public sealed class RecipeApiTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private HttpClient Client() => fixture.Factory.CreateClient();
    private async Task<CatalogueData> Catalogue(HttpClient client) => (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue", Json))!;
    private async Task<IngredientItem> Ingredient(HttpClient client, string name) => Assert.Single((await client.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + Uri.EscapeDataString(name), Json))!);
    private async Task<RecipeDetail> Detail(HttpClient client, Guid id) => (await client.GetFromJsonAsync<RecipeDetail>($"/api/recipes/{id}", Json))!;
    private static async Task<Guid> CreatedId(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
    }
    private static SaveRecipeInput Input(CatalogueData c, IngredientItem ingredient) => new("Integration recipe", "Real PostgreSQL", c.RecipeTypes[0].Id,
        new(new(20, 24), c.Units.Single(u => u.Symbol == "st").Id), 20, PublicationState.Published, [c.Tags[0].Id],
        [new(null, null, [new(null, ingredient.Id, new(2, 3), c.Units.Single(u => u.Symbol == "dl").Id, null, "finhackad", null)]), new(null, "Garnering", [])],
        [new(null, "First"), new(null, "Second")]);
    private static SaveRecipeInput Edit(RecipeDetail r) => new(r.Name, r.Description, r.RecipeTypeId, r.Yield, r.TotalTimeMinutes, r.PublicationState, r.TagIds,
        r.IngredientGroups.Select(g => new GroupInput(g.Id, g.Heading, g.Ingredients.Select(i => new IngredientInput(i.Id, i.IngredientId, i.Quantity, i.UnitId, i.AmountDescription, i.Preparation, i.Note)).ToList())).ToList(),
        r.CookingSteps.Select(s => new StepInput(s.Id, s.Instruction)).ToList());

    [Fact]
    public async Task Nonadministrator_cannot_modify_catalogue_through_http()
    {
        var userId = Guid.NewGuid();
        await using (var db = fixture.CreateDb())
        {
            db.Users.Add(new User { Id = userId, DisplayName = "Test author", IsAdministrator = false });
            await db.SaveChangesAsync();
        }
        await using var factory = fixture.Factory.WithWebHostBuilder(host => host.ConfigureServices(services =>
        {
            services.RemoveAll<ICurrentUser>();
            services.AddSingleton<ICurrentUser>(new TestUser(userId));
        }));
        using var client = factory.CreateClient();
        var input = new SaveIngredientInput([new("sv", "Unauthorized")], []);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ingredients", input, Json)).StatusCode);
        var ingredient = await Ingredient(client, "Zucchini");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/ingredients/{ingredient.Id}", input, Json)).StatusCode);
    }

    private sealed record TestUser(Guid Id) : ICurrentUser;

    [Fact]
    public async Task Seed_is_repeatable_without_overwriting_existing_data()
    {
        await using var db = fixture.CreateDb();
        var before = await db.Recipes.CountAsync();
        await Recipe.Infrastructure.Persistence.DevelopmentSeed.Run(db);
        Assert.Equal(before, await db.Recipes.CountAsync());
    }

    [Fact]
    public async Task Yield_rejects_unspecified_ranges_and_ineligible_units()
    {
        using var client = Client(); var c = await Catalogue(client); var ingredient = await Ingredient(client, "Persilja");
        var input = Input(c, ingredient);
        var invalid = input with { Yield = new(new(null, null), input.Yield.UnitId) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", invalid, Json)).StatusCode);
        invalid = input with { Yield = new(new(4, 2), input.Yield.UnitId) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", invalid, Json)).StatusCode);
        invalid = input with { Yield = new(new(4, null), c.Units.First(u => !u.ForYield).Id) };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", invalid, Json)).StatusCode);
    }

    [Fact]
    public async Task Alias_search_resolves_to_the_same_canonical_identity()
    {
        using var client = Client();
        var canonical = await Ingredient(client, "Zucchini");
        Assert.Equal(canonical.Id, (await Ingredient(client, "COURGETTE")).Id);
        Assert.Equal(canonical.Id, (await Ingredient(client, "squash")).Id);
        Assert.Empty((await client.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=%25", Json))!);
    }
    [Fact]
    public async Task Search_combines_ingredient_AND_tag_OR_and_type_AND_name()
    {
        using var client = Client(); var c = await Catalogue(client);
        var cauliflower = await Ingredient(client, "Blomkål"); var caviar = await Ingredient(client, "Kaviar"); var zucchini = await Ingredient(client, "courgette");
        async Task<Page<RecipeSummary>> Search(string query) => (await client.GetFromJsonAsync<Page<RecipeSummary>>("/api/recipes?" + query, Json))!;
        var both = await Search($"ingredientIds={cauliflower.Id}&ingredientIds={caviar.Id}");
        Assert.Equal("Blomkål med kaviar", Assert.Single(both.Items).Name);
        Assert.Empty((await Search($"ingredientIds={cauliflower.Id}&ingredientIds={zucchini.Id}")).Items);
        var party = c.Tags.Single(t => t.Name == "Fest").Id; var baking = c.Tags.Single(t => t.Name == "Bakning").Id;
        var anyTag = await Search($"tagIds={party}&tagIds={baking}");
        Assert.Contains(anyTag.Items, r => r.Name == "Blomkål med kaviar"); Assert.Contains(anyTag.Items, r => r.Name == "Små pannkakor");
        var type = c.RecipeTypes.Single(t => t.Name == "Förrätt").Id;
        Assert.Single((await Search($"name=BLOMKÅL&recipeTypeId={type}&ingredientIds={cauliflower.Id}&ingredientIds={caviar.Id}&tagIds={party}&tagIds={baking}")).Items);
        Assert.Empty((await Search($"recipeTypeId={type}&ingredientIds={cauliflower.Id}&tagIds={baking}")).Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/recipes?page=0")).StatusCode);
    }
    [Fact]
    public async Task Create_edit_reorder_move_and_remove_preserve_ids_and_quantities()
    {
        using var client = Client(); var c = await Catalogue(client); var ingredient = await Ingredient(client, "Persilja");
        var id = await CreatedId(await client.PostAsJsonAsync("/api/recipes", Input(c, ingredient), Json));
        var original = await Detail(client, id);
        Assert.Equal(24, original.Yield.Quantity.MaximumValue);
        var entry = Assert.Single(original.IngredientGroups[0].Ingredients);
        Assert.Equal(3, entry.Quantity.MaximumValue); Assert.Equal(ingredient.Id, entry.IngredientId);
        var update = Edit(original);
        var moved = update.IngredientGroups[0].Ingredients[0] with { Quantity = new(null, null), UnitId = null, AmountDescription = "efter smak" };
        update.IngredientGroups[0].Ingredients.Clear(); update.IngredientGroups[1].Ingredients.Add(moved);
        update.CookingSteps.Reverse();
        var response = await client.PutAsJsonAsync($"/api/recipes/{id}", update, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var edited = await Detail(client, id);
        Assert.Equal(entry.Id, Assert.Single(edited.IngredientGroups[1].Ingredients).Id);
        Assert.Equal(original.CookingSteps[1].Id, edited.CookingSteps[0].Id);
        Assert.Null(edited.IngredientGroups[1].Ingredients[0].Quantity.Value);
        var remove = Edit(edited) with { PublicationState = PublicationState.Draft };
        remove.IngredientGroups.RemoveAt(1); remove.CookingSteps.RemoveAt(0);
        (await client.PutAsJsonAsync($"/api/recipes/{id}", remove, Json)).EnsureSuccessStatusCode();
        var final = await Detail(client, id); Assert.Single(final.IngredientGroups); Assert.Single(final.CookingSteps);
    }
    [Fact]
    public async Task Invalid_catalogue_references_are_rejected_without_creating_data()
    {
        using var client = Client(); var c = await Catalogue(client); var ingredient = await Ingredient(client, "Persilja");
        var input = Input(c, ingredient);
        input.IngredientGroups[0].Ingredients[0] = input.IngredientGroups[0].Ingredients[0] with { IngredientId = Guid.NewGuid() };
        var result = await client.PostAsJsonAsync("/api/recipes", input, Json);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Contains("ingredientGroups[0].ingredients[0].ingredientId", await result.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Catalogue_create_edit_translations_and_aliases_are_searchable()
    {
        using var client = Client(); var suffix = Guid.NewGuid().ToString("N");
        var input = new SaveIngredientInput([new("sv", "Test " + suffix), new("en", "English " + suffix)], [new(null, "Alias " + suffix)]);
        var id = await CreatedId(await client.PostAsJsonAsync("/api/ingredients", input, Json));
        Assert.Equal(id, (await Ingredient(client, "Alias " + suffix)).Id);
        var update = new SaveIngredientInput([new("sv", "Nytt " + suffix)], [new("en", "Changed " + suffix)]);
        (await client.PutAsJsonAsync($"/api/ingredients/{id}", update, Json)).EnsureSuccessStatusCode();
        Assert.Equal(id, (await Ingredient(client, "Changed " + suffix)).Id);
        Assert.Empty((await client.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + Uri.EscapeDataString("Alias " + suffix), Json))!);
    }
    [Fact]
    public async Task Images_upload_switch_primary_reorder_caption_and_delete()
    {
        using var client = Client(); var c = await Catalogue(client); var ingredient = await Ingredient(client, "Persilja");
        var id = await CreatedId(await client.PostAsJsonAsync("/api/recipes", Input(c, ingredient), Json));
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        async Task<Guid> Upload() { using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(png), "file", "test.png"); return await CreatedId(await client.PostAsync($"/api/recipes/{id}/images", form)); }
        var first = await Upload(); var second = await Upload();
        var before = await Detail(client, id); Assert.Equal(first, Assert.Single(before.Images, i => i.IsPrimary).Id);
        var changes = new List<ImageInput> { new(second, "Second image", true), new(first, "First image", false) };
        var response = await client.PutAsJsonAsync($"/api/recipes/{id}/images", changes, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var after = await Detail(client, id); Assert.Equal(second, after.Images[0].Id); Assert.True(after.Images[0].IsPrimary);
        var file = await client.GetAsync(after.Images[0].Url); Assert.Equal("image/png", file.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await file.Content.ReadAsByteArrayAsync());
        (await client.DeleteAsync($"/api/recipes/{id}/images/{second}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(after.Images[0].Url)).StatusCode);
        using var invalid = new MultipartFormDataContent(); invalid.Add(new StringContent("<svg/>"), "file", "bad.svg");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/recipes/{id}/images", invalid)).StatusCode);
    }
    [Fact]
    public async Task Migration_constraints_reject_invalid_quantity_and_referenced_ingredient_deletion()
    {
        await using var db = fixture.CreateDb();
        Assert.False(db.Database.HasPendingModelChanges());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"Recipes\" SET \"YieldMaximumValue\" = \"YieldValue\""));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE \"RecipeIngredients\" SET \"AmountValue\" = NULL, \"AmountMaximumValue\" = 2"));
        var ingredientId = await db.Set<RecipeIngredient>().Select(i => i.IngredientId).FirstAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Ingredients.Where(i => i.Id == ingredientId).ExecuteDeleteAsync());
    }
}
