using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recipe.Application;
using Recipe.Domain;

namespace Recipe.IntegrationTests;

[Collection("Postgres")]
public sealed class ActivityModeTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed record Current(Guid Id) : ICurrentUser;
    private async Task<User> Author(string name)
    {
        var user = new User { Id = Guid.NewGuid(), DisplayName = name };
        await using var db = fixture.CreateDb(); db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static async Task<Guid> Created(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
    }
    private static SaveRecipeInput Draft(string name) => new(name, null, null, null, null, PublicationState.Draft, [], [], []);
    private static async Task<SaveRecipeInput> Complete(HttpClient client, string name, params Guid[] ingredients)
    {
        var c = (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue", Json))!;
        return new(name, "Original description", c.RecipeTypes.First(t => t.IsActive).Id, new(new(4, null), c.Units.First(u => u.ForYield).Id), null,
            PublicationState.Published, [], [new(null, null, ingredients.Select(id => new IngredientInput(null, id, new(null, null), null, "efter smak", null, null)).ToList())], [new(null, "Servera.")]);
    }
    private static async Task<RecipeDetail> Read(HttpClient client, Guid id) => (await client.GetFromJsonAsync<RecipeDetail>($"/api/recipes/{id}", Json))!;

    [Fact]
    public async Task Discovery_is_published_only_and_creator_scope_is_owned_with_private_draft_reads()
    {
        var first = await Author("First author"); var second = await Author("Second author");
        await using var firstFactory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(first.Id)); }));
        await using var secondFactory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(second.Id)); }));
        using var a = firstFactory.CreateClient(); using var b = secondFactory.CreateClient(); using var admin = fixture.Factory.CreateClient();
        var prefix = "Scope " + Guid.NewGuid();
        var salt = (await a.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=Salt", Json))!.First().Id;
        var draftA = await Created(await a.PostAsJsonAsync("/api/recipes", Draft(prefix + " draft A"), Json));
        var draftB = await Created(await b.PostAsJsonAsync("/api/recipes", Draft(prefix + " draft B"), Json));
        var publicA = await Created(await a.PostAsJsonAsync("/api/recipes", await Complete(a, prefix + " public A", salt), Json));
        var publicB = await Created(await b.PostAsJsonAsync("/api/recipes", await Complete(b, prefix + " public B", salt), Json));
        async Task<Page<RecipeSummary>> Search(HttpClient client, string extra = "") => (await client.GetFromJsonAsync<Page<RecipeSummary>>("/api/recipes?name=" + Uri.EscapeDataString(prefix) + extra, Json))!;
        var discover = await Search(a);
        Assert.Equal(2, discover.Total); Assert.All(discover.Items, r => Assert.Equal(PublicationState.Published, r.PublicationState));
        Assert.Contains(discover.Items, r => r.Id == publicB);
        var mine = await Search(a, "&scope=Mine&authorId=" + second.Id);
        Assert.Equal(new[] { draftA, publicA }.Order(), mine.Items.Select(r => r.Id).Order());
        Assert.Equal(draftA, Assert.Single((await Search(a, "&scope=Mine&publicationState=Draft")).Items).Id);
        Assert.Equal(publicA, Assert.Single((await Search(a, "&scope=Mine&publicationState=Published")).Items).Id);
        Assert.Empty((await Search(a, "&scope=Published&publicationState=Draft")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/recipes/{draftB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/recipes/{draftA}")).StatusCode);
        Assert.Equal(publicB, (await Read(a, publicB)).Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PutAsJsonAsync($"/api/recipes/{publicB}", Draft("Forbidden"), Json)).StatusCode);
        foreach (var endpoint in new[] { "ingredients/catalogue", "tags", "recipe-types" })
            Assert.Equal(HttpStatusCode.Forbidden, (await a.GetAsync("/api/" + endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/recipes?scope=99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.GetAsync("/api/recipes?scope=Mine&publicationState=99")).StatusCode);
    }

    [Fact]
    public async Task Pending_ingredients_allow_drafts_but_block_every_published_write_without_partial_changes()
    {
        var author = await Author("Pending author");
        await using var factory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(author.Id)); }));
        using var client = factory.CreateClient();
        var pending = await Created(await client.PostAsJsonAsync("/api/ingredients", new SaveIngredientInput([new("sv", "Testört väntar")], []), Json));
        var pending2 = await Created(await client.PostAsJsonAsync("/api/ingredients", new SaveIngredientInput([new("sv", "Testkrydda väntar")], []), Json));
        // Standalone creation uses no Recipe ID and can later be reused in drafts.
        var ingredient = (await client.GetFromJsonAsync<IngredientItem>($"/api/ingredients/{pending}", Json))!;
        Assert.Equal(IngredientStatus.PendingReview, ingredient.Status); Assert.Equal(author.Id, ingredient.CreatedByUserId);
        var input = await Complete(client, "Publication guard " + Guid.NewGuid(), pending, pending2, pending);
        var rejected = await client.PostAsJsonAsync("/api/recipes", input, Json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var message = await rejected.Content.ReadAsStringAsync();
        Assert.Contains("Testört väntar", message); Assert.Contains("Testkrydda väntar", message); Assert.Contains("utkast", message);
        var draft = await Created(await client.PostAsJsonAsync("/api/recipes", input with { PublicationState = PublicationState.Draft }, Json));
        Assert.All((await Read(client, draft)).IngredientGroups[0].Ingredients, i => Assert.Equal(IngredientStatus.PendingReview, i.IngredientStatus));
        rejected = await client.PutAsJsonAsync($"/api/recipes/{draft}", input, Json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(PublicationState.Draft, (await Read(client, draft)).PublicationState);
        var salt = (await client.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=Salt", Json))!.First().Id;
        var approved = await Complete(client, input.Name!, salt);
        (await client.PutAsJsonAsync($"/api/recipes/{draft}", approved, Json)).EnsureSuccessStatusCode();
        Assert.Equal(PublicationState.Published, (await Read(client, draft)).PublicationState);
        // Adding a pending ingredient to an already Published recipe cannot bypass the rule.
        rejected = await client.PutAsJsonAsync($"/api/recipes/{draft}", input with { Description = "Must not be saved" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var unchanged = await Read(client, draft);
        Assert.Equal(PublicationState.Published, unchanged.PublicationState); Assert.Equal("Original description", unchanged.Description);
        Assert.Equal(salt, Assert.Single(unchanged.IngredientGroups[0].Ingredients).IngredientId);
        (await client.PutAsJsonAsync($"/api/recipes/{draft}", input with { PublicationState = PublicationState.Draft }, Json)).EnsureSuccessStatusCode();
        Assert.Equal(PublicationState.Draft, (await Read(client, draft)).PublicationState);
        // A later approval in catalogue data is honored; no moderation API is introduced.
        await using (var db = fixture.CreateDb())
        {
            (await db.Ingredients.FindAsync(pending))!.Status = IngredientStatus.Active;
            (await db.Ingredients.FindAsync(pending2))!.Status = IngredientStatus.Active;
            await db.SaveChangesAsync();
        }
        (await client.PutAsJsonAsync($"/api/recipes/{draft}", input, Json)).EnsureSuccessStatusCode();
        Assert.Equal(PublicationState.Published, (await Read(client, draft)).PublicationState);
    }
}
