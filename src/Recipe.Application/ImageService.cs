using Recipe.Domain;

namespace Recipe.Application;

public sealed class ImageService(IRecipeStore recipes, IImageStorage storage, ICurrentUser user)
{
    public const int MaximumBytes = 10 * 1024 * 1024;
    public async Task<Guid> Upload(Guid recipeId, Stream content, CancellationToken ct)
    {
        var recipe = await Owned(recipeId, ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new InputException("file", "Images must be at most 10 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        var bytes = buffer.ToArray();
        var (extension, type) = Detect(bytes);
        buffer.Position = 0;
        var key = await storage.Save(buffer, extension, ct);
        var image = new RecipeImage { RecipeId = recipe.Id, StorageKey = key, ContentType = type, SortOrder = recipe.Images.Count, IsPrimary = recipe.Images.Count == 0 };
        recipe.Images.Add(image); recipe.UpdatedAt = DateTimeOffset.UtcNow;
        try { await recipes.Save(ct); }
        catch { await storage.Delete(key, CancellationToken.None); throw; }
        return image.Id;
    }
    public async Task Update(Guid recipeId, List<ImageInput> input, CancellationToken ct)
    {
        var recipe = await Owned(recipeId, ct);
        if (input.Count != recipe.Images.Count || input.Select(i => i.Id).Distinct().Count() != input.Count || input.Any(i => !recipe.Images.Any(x => x.Id == i.Id)) || input.Count(i => i.IsPrimary) > 1)
            throw new InputException("images", "Supply each image once, with at most one primary image.");
        // Store applies the primary switch transactionally to satisfy the filtered unique index.
        foreach (var image in recipe.Images)
        {
            var index = input.FindIndex(i => i.Id == image.Id);
            image.Caption = input[index].Caption?.Trim(); image.IsPrimary = input[index].IsPrimary; image.SortOrder = index;
        }
        recipe.UpdatedAt = DateTimeOffset.UtcNow;
        await recipes.Save(ct);
    }
    public async Task Delete(Guid recipeId, Guid imageId, CancellationToken ct)
    {
        var recipe = await Owned(recipeId, ct);
        var image = recipe.Images.SingleOrDefault(i => i.Id == imageId) ?? throw new NotFoundException();
        recipe.Images.Remove(image); recipe.UpdatedAt = DateTimeOffset.UtcNow;
        var remaining = recipe.Images.OrderBy(i => i.SortOrder).ToList();
        for (var index = 0; index < remaining.Count; index++) remaining[index].SortOrder = index;
        await recipes.Save(ct);
        await storage.Delete(image.StorageKey, ct);
    }
    private async Task<Domain.Recipe> Owned(Guid id, CancellationToken ct)
    {
        var recipe = await recipes.Find(id, ct) ?? throw new NotFoundException();
        if (recipe.AuthorId != user.Id) throw new ForbiddenException();
        return recipe;
    }
    private static (string, string) Detect(byte[] b)
    {
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (".png", "image/png");
        if (b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255) return (".jpg", "image/jpeg");
        if (b.Length >= 12 && System.Text.Encoding.ASCII.GetString(b, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(b, 8, 4) == "WEBP") return (".webp", "image/webp");
        throw new InputException("file", "Upload a JPEG, PNG or WebP image.");
    }
}
