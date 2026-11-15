namespace RaceDay.Api.Services;

/// <summary>Fallback used when no Azure connection string is configured (local development only).</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(string root)
    {
        _root = Path.GetFullPath(root);
        Directory.CreateDirectory(_root);
    }

    private string? Resolve(string name)
    {
        var full = Path.GetFullPath(Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null; // stops ../ tricks
    }

    public async Task<string> SaveAsync(Stream content, string folder, string extension, string contentType)
    {
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        var path = Resolve(name)!;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs);
        return name;
    }

    public Task<StoredFile?> OpenAsync(string name)
    {
        var path = Resolve(name);
        if (path is null || !File.Exists(path)) return Task.FromResult<StoredFile?>(null);
        var ct = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
        return Task.FromResult<StoredFile?>(new StoredFile(File.OpenRead(path), ct));
    }

    public Task DeleteAsync(string name)
    {
        var path = Resolve(name);
        if (path is not null && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
