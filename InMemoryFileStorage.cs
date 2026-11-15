using System.Collections.Concurrent;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

/// <summary>Stand-in for Azure Blob Storage so tests run offline.</summary>
public class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Bytes, string Type)> _files = new();

    public bool Contains(string name) => _files.ContainsKey(name);

    public async Task<string> SaveAsync(Stream content, string folder, string extension, string contentType)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms);
        var name = $"{folder}/{Guid.NewGuid():N}{extension}";
        _files[name] = (ms.ToArray(), contentType);
        return name;
    }

    public Task<StoredFile?> OpenAsync(string name) =>
        Task.FromResult(_files.TryGetValue(name, out var f)
            ? new StoredFile(new MemoryStream(f.Bytes), f.Type)
            : null);

    public Task DeleteAsync(string name)
    {
        _files.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
