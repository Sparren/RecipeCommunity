using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Recipe.Application;
using Recipe.Domain;
using Recipe.Infrastructure.Images;
using Recipe.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<RecipeDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Recipe")));
builder.Services.AddScoped<IRecipeStore, RecipeStore>();
builder.Services.AddScoped<ICatalogueStore, CatalogueStore>();
builder.Services.AddScoped<RecipeService>();
builder.Services.AddScoped<CatalogueService>();
builder.Services.AddScoped<ImageService>();
builder.Services.AddSingleton<ICurrentUser, LocalCurrentUser>();
builder.Services.AddSingleton<IImageStorage>(new LocalImageStorage(Path.GetFullPath(builder.Configuration["Images:Directory"] ?? "App_Data/images", builder.Environment.ContentRootPath)));
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = ImageService.MaximumBytes + 65536);
var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Recipe 0.1 uses a local development identity. Run in Development only.");
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (Exception e) when (e is InputException or DomainException or NotFoundException or ForbiddenException or BadHttpRequestException)
    {
        var status = e switch { NotFoundException => 404, ForbiddenException => 403, BadHttpRequestException b => b.StatusCode, _ => 400 };
        if (status == 400)
            await Results.ValidationProblem(new Dictionary<string, string[]> { [e is InputException input ? input.Field : "recipe"] = [e.Message] }).ExecuteAsync(context);
        else await Results.Problem(statusCode: status, title: e.Message).ExecuteAsync(context);
    }
});
if (args.Contains("--seed"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DevelopmentSeed.Run(scope.ServiceProvider.GetRequiredService<RecipeDbContext>());
    return;
}
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/me", async (ICurrentUser current, ICatalogueStore store, CancellationToken ct) =>
{
    var user = await store.FindUser(current.Id, ct) ?? throw new NotFoundException();
    return Results.Ok(new { user.Id, user.DisplayName, user.IsAdministrator });
});
app.MapGet("/api/catalogue", async (ICatalogueStore store, CancellationToken ct, string language = "sv") => await store.Read(language, ct));
app.MapGet("/api/tags", async (ICatalogueStore store, CancellationToken ct, string? query = null, bool? isActive = null, string language = "sv") => await store.ListTags(query, isActive, language, ct));
app.MapPost("/api/tags", async (SaveTagInput input, CatalogueService service, CancellationToken ct) =>
{ var id = await service.SaveTag(null, input, ct); return Results.Created("/api/tags", new { id }); });
app.MapPut("/api/tags/{id:guid}", async (Guid id, SaveTagInput input, CatalogueService service, CancellationToken ct) =>
{ await service.SaveTag(id, input, ct); return Results.Ok(new { id }); });
app.MapGet("/api/recipe-types", async (ICatalogueStore store, CancellationToken ct, string? query = null, bool? isActive = null, string language = "sv") => await store.ListRecipeTypes(query, isActive, language, ct));
app.MapPost("/api/recipe-types", async (SaveRecipeTypeInput input, CatalogueService service, CancellationToken ct) =>
{ var id = await service.SaveRecipeType(null, input, ct); return Results.Created("/api/recipe-types", new { id }); });
app.MapPut("/api/recipe-types/{id:guid}", async (Guid id, SaveRecipeTypeInput input, CatalogueService service, CancellationToken ct) =>
{ await service.SaveRecipeType(id, input, ct); return Results.Ok(new { id }); });
app.MapGet("/api/ingredients", async (ICatalogueStore store, CancellationToken ct, string? query = null, string language = "sv") => await store.SearchIngredients(query, language, ct));
app.MapGet("/api/ingredients/catalogue", async (ICatalogueStore store, CancellationToken ct, string? query = null, IngredientStatus? status = null, string language = "sv") => await store.ListIngredients(query, status, language, ct));
app.MapGet("/api/ingredients/{id:guid}", async (Guid id, ICatalogueStore store, ICurrentUser currentUser, CancellationToken ct, string language = "sv") =>
{
    var administrator = (await store.FindUser(currentUser.Id, ct))?.IsAdministrator == true;
    var ingredient = (administrator ? await store.FindIngredientForAdministration(id, ct) : await store.FindIngredient(id, ct)) ?? throw new NotFoundException();
    var name = ingredient.Names.OrderBy(n => n.Language == language ? 0 : n.Language == "sv" ? 1 : n.Language == "en" ? 2 : 3).ThenBy(n => n.Language).First().Name;
    return new IngredientItem(ingredient.Id, name, ingredient.Names.Select(n => new NameInput(n.Language, n.Name)).ToList(), ingredient.Aliases.Select(a => new AliasInput(a.Language, a.Name)).ToList(), ingredient.Status, ingredient.CreatedByUserId, ingredient.CreatedAt);
});
app.MapPost("/api/ingredients", async (SaveIngredientInput input, CatalogueService service, CancellationToken ct) =>
{ var id = await service.Save(null, input, ct); return Results.Created($"/api/ingredients/{id}", new { id }); });
app.MapPut("/api/ingredients/{id:guid}", async (Guid id, SaveIngredientInput input, CatalogueService service, CancellationToken ct) =>
{ await service.Save(id, input, ct); return Results.NoContent(); });
app.MapGet("/api/recipes", async (HttpRequest request, IRecipeStore store, CancellationToken ct,
    string? name = null, Guid? recipeTypeId = null, string language = "sv", int page = 1, int pageSize = 20,
    RecipeListScope scope = RecipeListScope.Published, PublicationState? publicationState = null) =>
{
    Guid[] Ids(string key) => request.Query[key].SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        .Select(v => Guid.TryParse(v, out var id) ? id : throw new InputException(key, "Invalid identifier.")).Distinct().ToArray();
    return await store.Search(new(name, recipeTypeId, Ids("ingredientIds"), Ids("tagIds"), page, pageSize, scope, publicationState), language, ct);
});
app.MapGet("/api/recipes/{id:guid}", async (Guid id, IRecipeStore store, CancellationToken ct, string language = "sv") =>
    await store.Read(id, language, ct) ?? throw new NotFoundException());
app.MapPost("/api/recipes", async (SaveRecipeInput input, RecipeService service, CancellationToken ct) =>
{ var id = await service.Save(null, input, ct); return Results.Created($"/api/recipes/{id}", new { id }); });
app.MapPut("/api/recipes/{id:guid}", async (Guid id, SaveRecipeInput input, RecipeService service, CancellationToken ct) =>
{ await service.Save(id, input, ct); return Results.NoContent(); });
app.MapPost("/api/recipes/{id:guid}/images", async (Guid id, HttpRequest request, ImageService service, CancellationToken ct) =>
{
    if (!request.HasFormContentType) throw new InputException("file", "Submit an image as multipart form data.");
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file") ?? throw new InputException("file", "Choose an image.");
    if (file.Length > ImageService.MaximumBytes) throw new InputException("file", "Images must be at most 10 MB.");
    await using var stream = file.OpenReadStream();
    var imageId = await service.Upload(id, stream, ct);
    return Results.Created(RecipeStore.ImageUrl(id, imageId), new { id = imageId });
});
app.MapPut("/api/recipes/{id:guid}/images", async (Guid id, List<ImageInput> input, ImageService service, CancellationToken ct) =>
{ await service.Update(id, input, ct); return Results.NoContent(); });
app.MapDelete("/api/recipes/{id:guid}/images/{imageId:guid}", async (Guid id, Guid imageId, ImageService service, CancellationToken ct) =>
{ await service.Delete(id, imageId, ct); return Results.NoContent(); });
app.MapGet("/api/recipes/{id:guid}/images/{imageId:guid}/file", async (Guid id, Guid imageId, IRecipeStore store, IImageStorage storage, HttpContext context, CancellationToken ct) =>
{
    var recipe = await store.Find(id, ct) ?? throw new NotFoundException();
    var image = recipe.Images.SingleOrDefault(i => i.Id == imageId) ?? throw new NotFoundException();
    var stream = await storage.Open(image.StorageKey, ct) ?? throw new NotFoundException();
    context.Response.Headers.XContentTypeOptions = "nosniff";
    return Results.File(stream, image.ContentType, enableRangeProcessing: true);
});
app.Run();

public partial class Program;
public sealed class LocalCurrentUser : ICurrentUser { public Guid Id => DevelopmentSeed.UserId; }
