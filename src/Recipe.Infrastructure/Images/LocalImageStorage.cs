using Recipe.Application;

namespace Recipe.Infrastructure.Images;

public sealed class LocalImageStorage(string directory) : IImageStorage
{
    private string PathFor(string key)
    {
        if (Path.GetFileName(key) != key || key.Contains('/') || key.Contains('\\')) throw new ArgumentException("Invalid storage key.");
        return Path.Combine(directory, key);
    }
    public async Task<string> Save(Stream content, string extension, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var key = Guid.NewGuid().ToString("N") + extension;
        try
        {
            await using var file = new FileStream(PathFor(key), FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await content.CopyToAsync(file, ct);
            return key;
        }
        catch { File.Delete(PathFor(key)); throw; }
    }
    public Task<Stream?> Open(string key, CancellationToken ct) => Task.FromResult<Stream?>(File.Exists(PathFor(key)) ? File.OpenRead(PathFor(key)) : null);
    public Task Delete(string key, CancellationToken ct) { File.Delete(PathFor(key)); return Task.CompletedTask; }
}
