using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Recipe.Application;
using Recipe.Domain;
using Recipe.Infrastructure.Persistence;

namespace Recipe.IntegrationTests;

[Collection("Postgres")]
public sealed class LifecycleTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed record Current(Guid Id) : ICurrentUser;
    private static SaveRecipeInput Draft() => new(null, null, null, null, null, PublicationState.Draft, [], [], []);
    private static async Task<Guid> Created(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Incomplete_draft_roundtrips_and_can_gain_and_remove_yield_and_type()
    {
        using var client = fixture.Factory.CreateClient();
        var input = Draft();
        var id = await Created(await client.PostAsJsonAsync("/api/recipes", input, Json));
        async Task<RecipeDetail> Read() => (await client.GetFromJsonAsync<RecipeDetail>($"/api/recipes/{id}", Json))!;
        var draft = await Read();
        Assert.Empty(draft.Name); Assert.Null(draft.Yield); Assert.Null(draft.RecipeTypeId);
        Assert.Empty(draft.CookingSteps); Assert.Empty(Assert.Single(draft.IngredientGroups).Ingredients);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/recipes/{id}", input with { PublicationState = PublicationState.Published }, Json)).StatusCode);
        var catalogue = (await client.GetFromJsonAsync<CatalogueData>("/api/catalogue", Json))!;
        input = input with { RecipeTypeId = catalogue.RecipeTypes[0].Id, Yield = new(new(20, 24), catalogue.Units.First(u => u.ForYield).Id) };
        (await client.PutAsJsonAsync($"/api/recipes/{id}", input, Json)).EnsureSuccessStatusCode();
        Assert.Equal(24, (await Read()).Yield!.Quantity.MaximumValue);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/recipes/{id}", input with { Yield = new(new(3, 2), input.Yield.UnitId) }, Json)).StatusCode);
        (await client.PutAsJsonAsync($"/api/recipes/{id}", Draft(), Json)).EnsureSuccessStatusCode();
        Assert.Null((await Read()).Yield);
        // A list response also handles drafts with absent type/yield without projection errors.
        (await client.GetAsync("/api/recipes")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Pending_ownership_is_enforced_in_search_reads_and_recipe_writes_and_admin_catalogue()
    {
        var author = new User { Id = Guid.NewGuid(), DisplayName = "Author" };
        var other = new User { Id = Guid.NewGuid(), DisplayName = "Other" };
        await using (var db = fixture.CreateDb()) { db.Users.AddRange(author, other); await db.SaveChangesAsync(); }
        await using var authorFactory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(author.Id)); }));
        await using var otherFactory = fixture.Factory.WithWebHostBuilder(h => h.ConfigureServices(s => { s.RemoveAll<ICurrentUser>(); s.AddSingleton<ICurrentUser>(new Current(other.Id)); }));
        using var creator = authorFactory.CreateClient(); using var stranger = otherFactory.CreateClient(); using var admin = fixture.Factory.CreateClient();
        var name = "Private " + Guid.NewGuid().ToString("N");
        var ingredientId = await Created(await creator.PostAsJsonAsync("/api/ingredients", new SaveIngredientInput([new("sv", name)], []), Json));
        var pending = (await creator.GetFromJsonAsync<IngredientItem>($"/api/ingredients/{ingredientId}", Json))!;
        Assert.Equal(IngredientStatus.PendingReview, pending.Status); Assert.Equal(author.Id, pending.CreatedByUserId);
        Assert.True(pending.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Single((await creator.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + name, Json))!);
        Assert.Empty((await stranger.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + name, Json))!);
        Assert.Empty((await admin.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + name, Json))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/ingredients/{ingredientId}")).StatusCode);
        var recipe = Draft() with { IngredientGroups = [new(null, null, [new(null, ingredientId, new(null, null), null, "to taste", null, null)])] };
        var recipeId = await Created(await creator.PostAsJsonAsync("/api/recipes", recipe, Json));
        await Created(await creator.PostAsJsonAsync("/api/recipes", recipe, Json)); // reusable beyond its first recipe
        Assert.Equal(HttpStatusCode.BadRequest, (await stranger.PostAsJsonAsync("/api/recipes", recipe, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/recipes/{recipeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync("/api/ingredients/catalogue")).StatusCode);
        var review = (await admin.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients/catalogue?status=PendingReview&query=" + name, Json))!;
        Assert.Equal(ingredientId, Assert.Single(review).Id);
        var activeName = "Shared " + Guid.NewGuid().ToString("N");
        var activeId = await Created(await admin.PostAsJsonAsync("/api/ingredients", new SaveIngredientInput([new("sv", activeName)], []), Json));
        var active = (await stranger.GetFromJsonAsync<IngredientItem>($"/api/ingredients/{activeId}", Json))!;
        Assert.Equal(IngredientStatus.Active, active.Status); Assert.Equal(DevelopmentSeed.UserId, active.CreatedByUserId);
        Assert.Single((await stranger.GetFromJsonAsync<List<IngredientItem>>("/api/ingredients?query=" + activeName, Json))!);
    }

    [Fact]
    public async Task Autocomplete_ranks_names_and_aliases_without_duplicate_identities()
    {
        await using var db = fixture.CreateDb();
        var token = "Rank" + Guid.NewGuid().ToString("N");
        var admin = await db.Users.SingleAsync(u => u.Id == DevelopmentSeed.UserId);
        Ingredient Make(string name, string? alias = null)
        {
            var i = Ingredient.Create(admin); i.Names.Add(new() { Language = "sv", Name = name });
            if (alias is not null) i.Aliases.Add(new() { Name = alias }); return i;
        }
        var exact = Make(token, token + " alias"); exact.Names.Add(new() { Language = "en", Name = token });
        var prefix = Make(token + " suffix"); var aliasPrefix = Make("Canonical prefix", token + " alternative");
        var contains = Make("Contains " + token); var aliasContains = Make("Canonical contains", "alias " + token);
        db.Ingredients.AddRange(exact, prefix, aliasPrefix, contains, aliasContains); await db.SaveChangesAsync();
        var store = new CatalogueStore(db, new Current(admin.Id));
        var matches = await store.SearchIngredients(token.ToUpperInvariant(), "sv", default);
        Assert.Equal(new[] { exact.Id, prefix.Id, aliasPrefix.Id, contains.Id, aliasContains.Id }, matches.Select(i => i.Id));
        var courgette = await store.SearchIngredients("cour", "sv", default);
        Assert.Contains(courgette, i => i.Name == "Zucchini");
        Assert.Contains(await store.SearchIngredients("GRÄD", "sv", default), i => i.Name == "Grädde");
    }

    [Fact]
    public async Task Short_autocomplete_queries_do_not_execute_database_commands()
    {
        var counter = new CommandCounter();
        await using var db = new RecipeDbContext(new DbContextOptionsBuilder<RecipeDbContext>().UseNpgsql(fixture.ConnectionString).AddInterceptors(counter).Options);
        var store = new CatalogueStore(db, new Current(DevelopmentSeed.UserId));
        foreach (var query in new string?[] { null, "", " ", "R", " R ", "🍅" }) Assert.Empty(await store.SearchIngredients(query, "sv", default));
        Assert.Equal(0, counter.Count);
        Assert.NotEmpty(await store.SearchIngredients("gr", "sv", default)); Assert.True(counter.Count > 0);
    }

    [Fact]
    public async Task Admin_catalogue_is_untruncated_sorted_by_display_language_and_filters_canonical_entries()
    {
        await using var db = fixture.CreateDb();
        var admin = await db.Users.SingleAsync(u => u.Id == DevelopmentSeed.UserId);
        var marker = Guid.NewGuid().ToString("N");
        for (var index = 0; index < 55; index++)
        {
            var i = Ingredient.Create(admin);
            i.Names = [new() { Language = "sv", Name = $"Katalog {marker} {54 - index:D2}" }, new() { Language = "en", Name = $"English {marker} {index:D2}" }];
            i.Aliases = [new() { Name = $"alias {marker} {index:D2}" }];
            if (index == 0) i.Status = IngredientStatus.PendingReview;
            db.Ingredients.Add(i);
        }
        await db.SaveChangesAsync();
        var store = new CatalogueStore(db, new Current(admin.Id));
        var all = await store.ListIngredients(null, null, "sv", default);
        Assert.Equal(await db.Ingredients.CountAsync(), all.Count);
        Assert.Equal(all.Select(i => i.Name).OrderBy(n => n, StringComparer.Create(CultureInfo.GetCultureInfo("sv"), true)), all.Select(i => i.Name));
        Assert.Equal(54, (await store.ListIngredients(marker, IngredientStatus.Active, "sv", default)).Count);
        Assert.Single(await store.ListIngredients(marker, IngredientStatus.PendingReview, "sv", default));
        var alias = Assert.Single(await store.ListIngredients($"alias {marker} 00", null, "sv", default));
        Assert.Equal($"Katalog {marker} 54", alias.Name);
        var preferred = Assert.Single(await store.ListIngredients($"English {marker} 00", null, "sv", default));
        Assert.Equal(alias.Id, preferred.Id);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }
}
