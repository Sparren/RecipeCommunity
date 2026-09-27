using System.Globalization;
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
public sealed class TagTypeAdministrationTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task<Guid> Created(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }
    private static SaveRecipeInput Draft(Guid? type = null, params Guid[] tags) => new("Catalogue lifecycle", null, type, null, null, PublicationState.Draft, tags.ToList(), [], []);
    private sealed record Current(Guid Id) : ICurrentUser;

    [Theory]
    [InlineData("tags")]
    [InlineData("recipe-types")]
    public async Task Aliases_are_curated_translated_editable_and_resolve_to_one_canonical_identity(string route)
    {
        using var client = fixture.Factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var names = new List<NameInput> { new("sv", "Canonical " + suffix) };
        var aliases = new List<NameInput> { new(" sv ", " Örtig " + suffix + " "), new("fr", "Örtig " + suffix), new("sv", "Extra " + suffix) };
        object Input(List<NameInput> values, bool active = true) => route == "tags" ? new SaveTagInput(names, active, values) : new SaveRecipeTypeInput(names, active, 0, values);
        var id = await Created(await client.PostAsJsonAsync("/api/" + route, Input(aliases)));
        async Task<JsonElement[]> Search(string query) => (await client.GetFromJsonAsync<JsonElement[]>($"/api/{route}?query={Uri.EscapeDataString(query)}"))!;
        var found = Assert.Single(await Search("ÖRTIG " + suffix));
        Assert.Equal(id, found.GetProperty("id").GetGuid());
        Assert.Equal(names[0].Name, found.GetProperty("name").GetString());
        Assert.Equal(3, found.GetProperty("aliases").GetArrayLength());
        var catalogue = (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue"))!;
        var choices = route == "tags" ? catalogue.Tags : catalogue.RecipeTypes;
        Assert.Equal(3, choices.Single(x => x.Id == id).Aliases!.Count);
        // No inferred related terms, and SQL wildcards in input stay literal.
        Assert.Empty(await Search("smårätt " + suffix)); Assert.Empty(await Search("%" + suffix));
        (await client.PutAsJsonAsync($"/api/{route}/{id}", Input([new("en", "Changed " + suffix)], false))).EnsureSuccessStatusCode();
        Assert.Empty(await Search("Örtig " + suffix));
        var updated = Assert.Single(await Search("changed " + suffix));
        Assert.Equal(id, updated.GetProperty("id").GetGuid()); Assert.False(updated.GetProperty("isActive").GetBoolean());
        Assert.Single(updated.GetProperty("aliases").EnumerateArray());
        var activeResults = (await client.GetFromJsonAsync<JsonElement[]>($"/api/{route}?query={Uri.EscapeDataString(suffix)}&isActive=true"))!;
        Assert.Empty(activeResults);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/{route}/{id}", Input([new("", "Invalid")]))).StatusCode);
        (await client.PutAsJsonAsync($"/api/{route}/{id}", Input([]))).EnsureSuccessStatusCode();
        Assert.Empty(await Search("changed " + suffix));
        Assert.Empty(Assert.Single(await Search("Canonical " + suffix)).GetProperty("aliases").EnumerateArray());
    }

    [Fact]
    public async Task Tags_support_translations_search_swedish_order_status_and_stable_identity()
    {
        using var client = fixture.Factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var input = new SaveTagInput([new("sv", "Örtig " + suffix), new("en", "Herbal " + suffix)], true);
        var id = await Created(await client.PostAsJsonAsync("/api/tags", input));
        var second = await Created(await client.PostAsJsonAsync("/api/tags", new SaveTagInput([new("sv", "Ångad " + suffix)], false)));
        var list = (await client.GetFromJsonAsync<List<TagItem>>("/api/tags"))!;
        Assert.Contains(list, t => t.Id == id && t.Names.Count == 2);
        Assert.Contains(list, t => t.Id == second && !t.IsActive);
        Assert.Equal(list.OrderBy(t => t.Name, StringComparer.Create(CultureInfo.GetCultureInfo("sv"), true)).Select(t => t.Id), list.Select(t => t.Id));
        Assert.Equal(id, Assert.Single((await client.GetFromJsonAsync<List<TagItem>>("/api/tags?query=" + Uri.EscapeDataString("HERBAL " + suffix)))!).Id);
        Assert.Equal(id, Assert.Single((await client.GetFromJsonAsync<List<TagItem>>("/api/tags?query=" + Uri.EscapeDataString("ÖRTIG " + suffix)))!).Id);
        Assert.Contains((await client.GetFromJsonAsync<List<TagItem>>("/api/tags?isActive=true"))!, t => t.Id == id);
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<TagItem>>("/api/tags?isActive=true"))!, t => t.Id == second);
        input = new([new("sv", "Uppdaterad " + suffix), new("fr", "Herbes " + suffix)], false);
        (await client.PutAsJsonAsync($"/api/tags/{id}", input)).EnsureSuccessStatusCode();
        var updated = (await client.GetFromJsonAsync<List<TagItem>>("/api/tags?isActive=false"))!.Single(t => t.Id == id);
        Assert.Equal(input.Names, updated.Names.OrderByDescending(n => n.Language).ToList());
        await using var db = fixture.CreateDb();
        Assert.Equal("tag-" + id.ToString("N"), (await db.Tags.FindAsync(id))!.Code);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/tags/{id}")).StatusCode);
    }

    [Fact]
    public async Task Recipe_types_support_translation_editing_order_and_activation()
    {
        using var client = fixture.Factory.CreateClient();
        var input = new SaveRecipeTypeInput([new("sv", "Testförrätt"), new("en", "Test appetizer")], true, 73);
        var id = await Created(await client.PostAsJsonAsync("/api/recipe-types", input));
        var other = await Created(await client.PostAsJsonAsync("/api/recipe-types", input with { SortOrder = 72, IsActive = false }));
        var list = (await client.GetFromJsonAsync<List<RecipeTypeItem>>("/api/recipe-types"))!;
        Assert.True(list.FindIndex(t => t.Id == other) < list.FindIndex(t => t.Id == id));
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<RecipeTypeItem>>("/api/recipe-types?isActive=true"))!, t => t.Id == other);
        (await client.PutAsJsonAsync($"/api/recipe-types/{id}", new SaveRecipeTypeInput([new("sv", "Ny förrätt")], false, 71))).EnsureSuccessStatusCode();
        var updated = (await client.GetFromJsonAsync<List<RecipeTypeItem>>("/api/recipe-types?isActive=false"))!.Single(t => t.Id == id);
        Assert.Equal(71, updated.SortOrder); Assert.Equal("Ny förrätt", Assert.Single(updated.Names).Name);
        (await client.PutAsJsonAsync($"/api/recipe-types/{id}", input with { SortOrder = 70 })).EnsureSuccessStatusCode();
        var catalogue = (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue"))!;
        Assert.True(catalogue.RecipeTypes.Single(t => t.Id == id).IsActive);
        Assert.Equal(catalogue.RecipeTypes.OrderBy(t => t.SortOrder).Select(t => t.Id), catalogue.RecipeTypes.Select(t => t.Id));
        await using var db = fixture.CreateDb();
        Assert.Equal("type-" + id.ToString("N"), (await db.RecipeTypes.FindAsync(id))!.Code);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/recipe-types/{id}")).StatusCode);
    }

    [Fact]
    public async Task Inactive_references_remain_readable_searchable_and_editable_but_cannot_be_newly_assigned()
    {
        using var client = fixture.Factory.CreateClient();
        var tagInput = new SaveTagInput([new("sv", "Retained tag")], true);
        var typeInput = new SaveRecipeTypeInput([new("sv", "Retained type")], true, 0);
        var tag = await Created(await client.PostAsJsonAsync("/api/tags", tagInput));
        var type = await Created(await client.PostAsJsonAsync("/api/recipe-types", typeInput));
        var input = Draft(type, tag);
        var id = await Created(await client.PostAsJsonAsync("/api/recipes", input, Json));
        (await client.PutAsJsonAsync($"/api/tags/{tag}", tagInput with { IsActive = false })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/recipe-types/{type}", typeInput with { IsActive = false })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/recipes/{id}", input with { Description = "Edited with inactive references" }, Json)).EnsureSuccessStatusCode();
        var detail = (await client.GetFromJsonAsync<RecipeDetail>($"/api/recipes/{id}", Json))!;
        Assert.Equal(type, detail.RecipeTypeId); Assert.Contains(tag, detail.TagIds);
        var search = (await client.GetFromJsonAsync<Page<RecipeSummary>>($"/api/recipes?scope=Mine&recipeTypeId={type}&tagIds={tag}", Json))!;
        Assert.Equal(id, Assert.Single(search.Items).Id); Assert.Equal("Retained type", search.Items[0].RecipeTypeName);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", Draft(type), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", Draft(null, tag), Json)).StatusCode);
        // Existing references may also be retained on publication; all completeness rules still apply.
        var catalogue = (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue"))!;
        var ingredient = (await client.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=Salt", Json))!.First();
        var published = input with { PublicationState = PublicationState.Published,
            Yield = new(new(2, null), catalogue.Units.First(u => u.ForYield).Id),
            IngredientGroups = [new(null, null, [new(null, ingredient.Id, new(null, null), null, "efter smak", null, null)])],
            CookingSteps = [new(null, "Servera.")] };
        (await client.PutAsJsonAsync($"/api/recipes/{id}", published, Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/recipes", published, Json)).StatusCode);
        (await client.PutAsJsonAsync($"/api/recipes/{id}", Draft(), Json)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/recipes/{id}", input, Json)).StatusCode);
        (await client.PutAsJsonAsync($"/api/tags/{tag}", tagInput)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/recipe-types/{type}", typeInput)).EnsureSuccessStatusCode();
        await Created(await client.PostAsJsonAsync("/api/recipes", input, Json));
    }

    [Theory]
    [InlineData("tags")]
    [InlineData("recipe-types")]
    public async Task Administration_requires_admin_and_valid_translated_names(string route)
    {
        using var admin = fixture.Factory.CreateClient();
        object Input(List<NameInput> names, int order = 0) => route == "tags" ? new SaveTagInput(names, true) : new SaveRecipeTypeInput(names, true, order);
        foreach (var names in new List<NameInput>[] { [], [new("sv", " ")], [new("sv", "A"), new("SV", "B")], [new("sv", new string('x', 201))] })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/" + route, Input(names))).StatusCode);
        if (route == "recipe-types") Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/" + route, Input([new("sv", "A")], -1))).StatusCode);
        var id = await Created(await admin.PostAsJsonAsync("/api/" + route, Input([new("sv", "Protected")])));
        var user = new User { Id = Guid.NewGuid(), DisplayName = "Ordinary author" };
        await using (var db = fixture.CreateDb()) { db.Users.Add(user); await db.SaveChangesAsync(); }
        await using var factory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(user.Id)); }));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/" + route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/" + route, Input([new("sv", "Forbidden")]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/{route}/{id}", Input([new("sv", "Forbidden")]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/{route}/{Guid.NewGuid()}", Input([new("sv", "Missing")]))).StatusCode);
    }
}
